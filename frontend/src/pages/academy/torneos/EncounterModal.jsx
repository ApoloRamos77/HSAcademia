import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, Search } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../../api/axios';
import { Modal } from './TorneoModals';
import { MATCH_STATUSES, errMsg, dateOf, timeOf, wall, addMinutesWall, fmtDate } from './torneoUtils';

/**
 * Encuentro de una fecha del torneo: categoría de referencia, hora, rival y convocados.
 * Se listan los alumnos de la categoría y de las menores; los de categorías mayores se pueden agregar
 * y quedan resaltados. Un alumno puede jugar varios encuentros el mismo día.
 */
export default function EncounterModal({ tournament, match, team, defaults = {}, categories, headquarters, staff, onClose, onSaved }) {
  const teams = tournament.teams;
  const [teamId, setTeamId] = useState(match?.teamId || team?.id || teams[0]?.id || '');
  const currentTeam = teams.find(t => t.id === teamId);
  const teamSede = useMemo(() => {
    const ids = currentTeam?.categoryIds || [];
    const sedes = [...new Set(categories.filter(c => ids.includes(c.id)).map(c => c.headquarterId))];
    return sedes.length === 1 ? sedes[0] : '';
  }, [currentTeam, categories]);

  const [form, setForm] = useState(() => ({
    referenceCategoryId: match?.referenceCategoryId || '',
    date: dateOf(match?.startTime) || defaults.date || '',
    start: match && !match.timePending ? timeOf(match.startTime) : '',
    end: match && !match.timePending ? timeOf(match.endTime) : '',
    call: timeOf(match?.callTime) || '',
    opponentTeam: match?.opponentTeam || '',
    round: match?.round || defaults.round || '',
    location: match?.location ?? defaults.location ?? tournament.mainLocation ?? '',
    headquarterId: match?.headquarterId || defaults.headquarterId || '',
    teacherId: match?.isReplacement ? match.teacherId : '',
    coachFee: match ? (match.coachFee ?? '') : (defaults.coachFee ?? tournament.defaultCoachFee ?? ''),
    matchStatus: match?.matchStatus || 1,
    goalsFor: match?.goalsFor ?? '',
    goalsAgainst: match?.goalsAgainst ?? '',
  }));
  const set = patch => setForm(f => ({ ...f, ...patch }));
  useEffect(() => { if (!form.headquarterId && teamSede) set({ headquarterId: teamSede }); }, [teamSede]); // eslint-disable-line react-hooks/exhaustive-deps

  // ── Convocatoria ──
  const [candidates, setCandidates] = useState(null);
  const [selected, setSelected] = useState(() => new Set());
  const [callNames, setCallNames] = useState({}); // convocados que ya no figuran (retirados)
  const [touched, setTouched] = useState(!!match);
  const [showSuperior, setShowSuperior] = useState(false);
  const [allSedes, setAllSedes] = useState(false);
  const [q, setQ] = useState('');

  useEffect(() => {
    if (!match) return;
    api.get(`/calendar/events/${match.id}/calls`).then(r => {
      setSelected(new Set(r.data.map(c => c.studentId)));
      setCallNames(Object.fromEntries(r.data.map(c => [c.studentId, `${c.studentName} · ${c.studentCategory || ''}`])));
    }).catch(() => toast.error('No se pudo cargar la convocatoria'));
  }, [match]);

  useEffect(() => {
    const params = { referenceCategoryId: form.referenceCategoryId || undefined, date: form.date || undefined, eventId: match?.id };
    api.get(`/tournaments/${tournament.id}/encounter-candidates`, { params })
      .then(r => {
        setCandidates(r.data);
        if (!touched && form.referenceCategoryId)
          setSelected(new Set(r.data.filter(c => c.sameCategory).map(c => c.studentId)));
      })
      .catch(() => setCandidates([]));
  }, [tournament.id, form.referenceCategoryId, form.date, match?.id]); // eslint-disable-line react-hooks/exhaustive-deps

  const byId = useMemo(() => Object.fromEntries((candidates || []).map(c => [c.studentId, c])), [candidates]);
  const toggle = id => { setTouched(true); setSelected(s => { const n = new Set(s); n.has(id) ? n.delete(id) : n.add(id); return n; }); };
  const setMany = (ids, on) => { setTouched(true); setSelected(s => { const n = new Set(s); ids.forEach(id => (on ? n.add(id) : n.delete(id))); return n; }); };

  const refCats = categories.filter(c => c.isActive !== false && (!teamSede || c.headquarterId === teamSede || c.id === form.referenceCategoryId));
  const refName = categories.find(c => c.id === form.referenceCategoryId)?.name;

  const visible = useMemo(() => {
    const text = q.trim().toLowerCase();
    return (candidates || []).filter(c =>
      (allSedes || !teamSede || c.headquarterId === teamSede) &&
      (!text || c.fullName.toLowerCase().includes(text) || (c.categoryName || '').toLowerCase().includes(text)));
  }, [candidates, q, allSedes, teamSede]);
  const groups = useMemo(() => {
    const g = {};
    for (const c of visible.filter(c => !c.superior)) (g[c.categoryName || 'Sin categoría'] ||= []).push(c);
    return Object.entries(g).sort(([a, la], [b, lb]) =>
      (lb[0].sameCategory ? 1 : 0) - (la[0].sameCategory ? 1 : 0) || a.localeCompare(b));
  }, [visible]);
  const superior = visible.filter(c => c.superior);
  const selectedList = [...selected];
  const superiorSelected = selectedList.filter(id => byId[id]?.superior).length;

  const [saving, setSaving] = useState(false);
  const save = async () => {
    if (!teamId) return toast.error('Elige las categorías del torneo');
    if (!form.referenceCategoryId) return toast.error('Elige la categoría de referencia del encuentro');
    if (!form.date) return toast.error('Indica la fecha');
    if (!form.start || !form.end) return toast.error('Indica la hora de inicio y de fin');
    if (form.end <= form.start) return toast.error('La hora de fin debe ser posterior al inicio');
    if (form.call && form.call > form.start) return toast.error('La citación debe ser antes del encuentro');
    if (selected.size === 0) return toast.error('Convoca al menos a un alumno');
    const num = v => (v === '' || v === null ? null : Number(v));
    const body = {
      startTime: wall(form.date, form.start), endTime: wall(form.date, form.end),
      callTime: form.call ? wall(form.date, form.call) : null,
      opponentTeam: form.opponentTeam || null, round: form.round || null, location: form.location || null,
      headquarterId: form.headquarterId || null, teacherId: form.teacherId || null,
      matchStatus: Number(form.matchStatus), goalsFor: num(form.goalsFor), goalsAgainst: num(form.goalsAgainst),
      coachFee: num(form.coachFee), referenceCategoryId: form.referenceCategoryId, studentIds: selectedList,
    };
    setSaving(true);
    try {
      const { data } = match
        ? await api.put(`/tournaments/matches/${match.id}`, body)
        : await api.post(`/tournaments/teams/${teamId}/matches`, body);
      toast.success(match ? 'Encuentro actualizado' : `Encuentro creado · ${data.calledCount} convocado(s)`);
      onSaved(data);
    } catch (e) { toast.error(errMsg(e, 'No se pudo guardar el encuentro')); }
    finally { setSaving(false); }
  };

  const played = [2, 5, 6].includes(Number(form.matchStatus));
  const Row = ({ c }) => (
    <label style={{ display: 'flex', gap: 8, alignItems: 'center', padding: '3px 0', cursor: 'pointer', fontSize: 13 }}>
      <input type="checkbox" checked={selected.has(c.studentId)} onChange={() => toggle(c.studentId)} />
      <span>{c.fullName}</span>
      {c.superior && <span className="badge badge-warning" style={{ fontSize: 10 }}><AlertTriangle size={10} /> Categoría superior</span>}
      {c.otherMatches.length > 0 && <span className="badge badge-info" style={{ fontSize: 10 }}>también juega {c.otherMatches.join(', ')}</span>}
    </label>
  );

  return (
    <Modal title={match ? `Editar encuentro · ${fmtDate(match.startTime)}` : 'Nuevo encuentro'} onClose={onClose} maxWidth={760}
      footer={<>
        <span className="text-muted" style={{ marginRight: 'auto', fontSize: 13 }}>
          {selected.size} convocado(s){superiorSelected > 0 && <strong style={{ color: '#d97706' }}> · ⚠ {superiorSelected} de categoría superior</strong>}
        </span>
        <button className="btn btn-secondary" onClick={onClose}>Cancelar</button>
        <button className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Guardando…' : 'Guardar'}</button>
      </>}>
      <div className="form-row-2">
        {!match && teams.length > 1 && (
          <div className="form-group" style={{ gridColumn: '1/-1' }}>
            <label>Categorías del torneo *</label>
            <select className="form-control" value={teamId} onChange={e => { setTeamId(e.target.value); set({ referenceCategoryId: '', headquarterId: '' }); setTouched(false); }}>
              {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
        )}
        <div className="form-group">
          <label>Categoría de referencia *</label>
          <select className="form-control" value={form.referenceCategoryId} onChange={e => set({ referenceCategoryId: e.target.value })}>
            <option value="">Elige la categoría</option>
            {refCats.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
          <small className="text-muted">Juegan ella y las menores. Las mayores se pueden agregar y quedan resaltadas.</small>
        </div>
        <div className="form-group"><label>Fecha *</label><input type="date" className="form-control" value={form.date} onChange={e => set({ date: e.target.value })} /></div>
        <div className="form-group"><label>Inicio *</label><input type="time" className="form-control" value={form.start} onChange={e => set({ start: e.target.value, end: form.end || (e.target.value ? timeOf(addMinutesWall(wall('2000-01-01', e.target.value), 60)) : '') })} /></div>
        <div className="form-group"><label>Fin *</label><input type="time" className="form-control" value={form.end} onChange={e => set({ end: e.target.value })} /></div>
        <div className="form-group">
          <label>Citación</label>
          <input type="time" className="form-control" value={form.call} onChange={e => set({ call: e.target.value })} />
          {!form.call && form.start && (
            <button type="button" className="btn btn-ghost btn-sm" style={{ padding: 0, marginTop: 4 }}
              onClick={() => set({ call: timeOf(addMinutesWall(wall('2000-01-01', form.start), -45)) })}>Usar 45 min antes</button>
          )}
        </div>
        <div className="form-group"><label>Rival</label><input className="form-control" value={form.opponentTeam} onChange={e => set({ opponentTeam: e.target.value })} placeholder="Por definir" /></div>
        <div className="form-group"><label>Fase / fecha</label><input className="form-control" value={form.round} onChange={e => set({ round: e.target.value })} placeholder="Ej: Fecha 3, Semifinal" /></div>
        <div className="form-group"><label>Cancha / lugar</label><input className="form-control" value={form.location} onChange={e => set({ location: e.target.value })} /></div>
        <div className="form-group">
          <label>Sede de la academia</label>
          <select className="form-control" value={form.headquarterId} onChange={e => set({ headquarterId: e.target.value })}>
            <option value="">Ninguna</option>
            {headquarters.map(h => <option key={h.id} value={h.id}>{h.name}</option>)}
          </select>
        </div>
        <div className="form-group">
          <label>Responsable</label>
          <select className="form-control" value={form.teacherId} onChange={e => set({ teacherId: e.target.value })}>
            <option value="">{currentTeam?.headCoachName ? `El de las categorías (${currentTeam.headCoachName})` : 'El de las categorías'}</option>
            {staff.filter(s => s.id !== currentTeam?.headCoachId).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
        </div>
        <div className="form-group">
          <label>Honorario de este encuentro (S/)</label>
          <input type="number" min={0} step="0.50" className="form-control" value={form.coachFee} onChange={e => set({ coachFee: e.target.value })} placeholder="Vacío = sin honorario" />
        </div>
        {match && (
          <>
            <div className="form-group">
              <label>Estado</label>
              <select className="form-control" value={form.matchStatus} onChange={e => set({ matchStatus: Number(e.target.value) })}>
                {MATCH_STATUSES.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label>Marcador (nosotros – rival)</label>
              <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
                <input type="number" min={0} className="form-control" value={form.goalsFor} onChange={e => set({ goalsFor: e.target.value })} />
                <span>–</span>
                <input type="number" min={0} className="form-control" value={form.goalsAgainst} onChange={e => set({ goalsAgainst: e.target.value })} />
              </div>
              {!played && form.goalsFor !== '' && form.goalsAgainst !== '' && <small className="text-muted">Con marcador se marca como Jugado.</small>}
            </div>
          </>
        )}
      </div>

      <div style={{ borderTop: '1px solid var(--border)', marginTop: 8, paddingTop: 12 }}>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', marginBottom: 8 }}>
          <strong>Convocados</strong>
          <div style={{ position: 'relative', flex: '1 1 180px' }}>
            <Search size={14} style={{ position: 'absolute', left: 8, top: 10, opacity: 0.5 }} />
            <input className="form-control" style={{ paddingLeft: 26 }} placeholder="Buscar alumno o categoría" value={q} onChange={e => setQ(e.target.value)} />
          </div>
          {teamSede && (
            <label style={{ fontSize: 12, display: 'flex', gap: 4, alignItems: 'center', cursor: 'pointer' }}>
              <input type="checkbox" checked={allSedes} onChange={e => setAllSedes(e.target.checked)} /> Otras sedes
            </label>
          )}
        </div>
        {!form.referenceCategoryId ? (
          <p className="text-muted" style={{ fontSize: 13 }}>Elige la categoría de referencia para ver a los alumnos.</p>
        ) : candidates === null ? <p>Cargando…</p> : (
          <div style={{ maxHeight: 300, overflowY: 'auto', border: '1px solid var(--border)', borderRadius: 8, padding: '6px 10px' }}>
            {groups.length === 0 && <p className="text-muted" style={{ fontSize: 13 }}>No hay alumnos de {refName} ni de categorías menores{teamSede && !allSedes ? ' en esta sede' : ''}.</p>}
            {groups.map(([name, list]) => {
              const ids = list.map(c => c.studentId);
              const all = ids.every(id => selected.has(id));
              return (
                <div key={name} style={{ marginBottom: 6 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 8, fontSize: 12, fontWeight: 700, marginTop: 4 }}>
                    {name}{list[0].sameCategory ? ' (referencia)' : ''} · {list.length}
                    <button type="button" className="btn btn-ghost btn-sm" style={{ padding: '0 4px' }} onClick={() => setMany(ids, !all)}>{all ? 'Quitar todos' : 'Marcar todos'}</button>
                  </div>
                  {list.map(c => <Row key={c.studentId} c={c} />)}
                </div>
              );
            })}
            <div style={{ borderTop: '1px dashed var(--border)', marginTop: 6, paddingTop: 6 }}>
              <button type="button" className="btn btn-ghost btn-sm" style={{ padding: 0 }} onClick={() => setShowSuperior(v => !v)}>
                <AlertTriangle size={13} /> {showSuperior ? 'Ocultar' : 'Agregar de'} categorías superiores ({superior.length})
              </button>
              {showSuperior && superior.map(c => <Row key={c.studentId} c={c} />)}
            </div>
          </div>
        )}
        {selectedList.some(id => !byId[id] && callNames[id]) && (
          <small className="text-muted">También convocados (retirados): {selectedList.filter(id => !byId[id]).map(id => callNames[id]).join(', ')}</small>
        )}
        {superiorSelected > 0 && (
          <div className="alert alert-warning" style={{ fontSize: 12, marginTop: 8, display: 'block' }}>
            <AlertTriangle size={12} /> Convocaste a {superiorSelected} alumno(s) de una categoría superior a {refName}. Verifica que el reglamento lo permita.
          </div>
        )}
      </div>
    </Modal>
  );
}

/** Exonerar (o volver a cobrar) el arbitraje de todo un encuentro. */
export function RefereeWaiverModal({ match, onClose, onSaved }) {
  const [note, setNote] = useState(match.refereeWaivedNote || '');
  const [saving, setSaving] = useState(false);
  const submit = async waived => {
    if (waived && !note.trim()) return toast.error('Indica el motivo');
    setSaving(true);
    try {
      await api.put(`/tournaments/matches/${match.id}/referee-waiver`, { waived, note: waived ? note.trim() : null });
      toast.success(waived ? 'Arbitraje exonerado en este encuentro' : 'Se vuelve a cobrar el arbitraje');
      onSaved();
    } catch (e) { toast.error(errMsg(e, 'No se pudo guardar')); }
    finally { setSaving(false); }
  };
  return (
    <Modal title="Arbitraje del encuentro" onClose={onClose} maxWidth={460}
      footer={<>
        <button className="btn btn-secondary" onClick={onClose}>Cancelar</button>
        {match.refereeWaived && <button className="btn btn-secondary" disabled={saving} onClick={() => submit(false)}>Volver a cobrar</button>}
        <button className="btn btn-primary" disabled={saving} onClick={() => submit(true)}>{match.refereeWaived ? 'Guardar motivo' : 'Exonerar'}</button>
      </>}>
      <p style={{ marginTop: 0, fontSize: 13 }}>
        {fmtDate(match.startTime)} {timeOf(match.startTime)} · {match.referenceCategoryName || match.teamName}{match.opponentTeam ? ` vs ${match.opponentTeam}` : ''}
      </p>
      <p className="text-muted" style={{ fontSize: 13 }}>
        Nadie paga el arbitraje de este encuentro. Si alguien ya lo pagó, se avisa en Cobros para que revises la devolución.
        Para exonerar solo a un alumno, hazlo en Cobros y gastos → Arbitraje.
      </p>
      <div className="form-group">
        <label>Motivo *</label>
        <input className="form-control" value={note} maxLength={250} onChange={e => setNote(e.target.value)} placeholder="Ej: lo asumió la organización" />
      </div>
    </Modal>
  );
}
