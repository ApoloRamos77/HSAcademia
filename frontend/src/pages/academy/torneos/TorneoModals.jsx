import { useEffect, useMemo, useState } from 'react';
import { X, Search, Trash2, Plus, AlertTriangle } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../../api/axios';
import SameDayDiscount from './SameDayDiscount';
import {
  FORMATS, SCHEDULE_MODES, LEGACY_SCHEDULE, MODALITIES, LEVELS, STATUSES, MATCH_STATUSES, DAYS, errMsg,
  dateOf, timeOf, wall, addMinutesWall, fmtDate,
} from './torneoUtils';

export function Modal({ title, onClose, children, footer, maxWidth = 560 }) {
  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal-box" style={{ maxWidth }}>
        <div className="modal-header">
          <h3>{title}</h3>
          <button className="btn-icon" onClick={onClose} aria-label="Cerrar"><X size={18} /></button>
        </div>
        <div className="modal-body">{children}</div>
        {footer && <div className="modal-footer">{footer}</div>}
      </div>
    </div>
  );
}

function DayPicker({ value, onChange }) {
  const toggle = d => onChange(value.includes(d) ? value.filter(x => x !== d) : [...value, d].sort());
  return (
    <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
      {DAYS.map(d => (
        <button key={d.value} type="button" onClick={() => toggle(d.value)}
          className={`btn btn-sm ${value.includes(d.value) ? 'btn-primary' : 'btn-secondary'}`} style={{ minWidth: 44 }}>
          {d.short}
        </button>
      ))}
    </div>
  );
}

// ───────────────────────── Torneo ─────────────────────────
export function TournamentFormModal({ tournament, onClose, onSaved }) {
  const [form, setForm] = useState(() => ({
    name: tournament?.name || '',
    organizer: tournament?.organizer || '',
    mainLocation: tournament?.mainLocation || '',
    format: tournament?.format ?? 1,
    modality: tournament?.modality ?? 7,
    level: tournament?.level ?? 1,
    inscriptionFee: tournament?.inscriptionFee ?? '',
    refereeFee: tournament?.refereeFee ?? '',
    refereeSameDayDiscountPercent: tournament?.refereeSameDayDiscountPercent ?? 0,
    defaultCoachFee: tournament?.defaultCoachFee ?? '',
    scheduleMode: tournament?.scheduleMode ?? 2,
    status: tournament?.status ?? 1,
    startDate: dateOf(tournament?.startDate) || '',
    endDate: dateOf(tournament?.endDate) || '',
    playDays: tournament?.playDays || [6],
    contactName: tournament?.contactName || '',
    contactPhone: tournament?.contactPhone || '',
    rules: tournament?.rules || '',
    yellowCardsForSuspension: tournament?.yellowCardsForSuspension ?? '',
    redCardSuspensionMatches: tournament?.redCardSuspensionMatches ?? '',
    requiresAuthorization: tournament?.requiresAuthorization ?? false,
    authorizationText: tournament?.authorizationText || '',
    requiredDocuments: tournament?.requiredDocuments || [],
  }));
  const [docDraft, setDocDraft] = useState('');
  const addDoc = name => {
    const d = (name ?? docDraft).trim();
    if (d && !form.requiredDocuments.some(x => x.toLowerCase() === d.toLowerCase())) set({ requiredDocuments: [...form.requiredDocuments, d] });
    setDocDraft('');
  };
  const [saving, setSaving] = useState(false);
  const set = patch => setForm(f => ({ ...f, ...patch }));

  const save = async () => {
    if (!form.name.trim() || !form.organizer.trim() || !form.mainLocation.trim()) return toast.error('Nombre, organizador y lugar son obligatorios');
    if (!form.startDate) return toast.error(form.scheduleMode === 1 ? 'Indica el día del torneo' : 'Indica la fecha de inicio');
    if ([2, 4].includes(form.scheduleMode) && !form.endDate) return toast.error('Indica la fecha de fin');
    if ([2, 3].includes(form.scheduleMode) && form.playDays.length === 0) return toast.error('Marca los días de juego');
    const money = v => (v === '' || v == null ? 0 : Number(v));
    const body = {
      ...form,
      yellowCardsForSuspension: form.yellowCardsForSuspension === '' ? 0 : Number(form.yellowCardsForSuspension),
      redCardSuspensionMatches: form.redCardSuspensionMatches === '' ? 0 : Number(form.redCardSuspensionMatches),
      startDate: form.startDate,
      endDate: form.scheduleMode === 1 ? form.startDate : (form.endDate || null),
      playDays: [1, 4].includes(form.scheduleMode) ? [] : form.playDays,
      inscriptionFee: money(form.inscriptionFee),
      refereeFee: money(form.refereeFee),
      refereeSameDayDiscountPercent: Number(form.refereeSameDayDiscountPercent) || 0,
      defaultCoachFee: money(form.defaultCoachFee),
    };
    setSaving(true);
    try {
      const { data } = tournament
        ? await api.put(`/tournaments/${tournament.id}`, body)
        : await api.post('/tournaments', body);
      toast.success(tournament ? 'Torneo actualizado' : 'Torneo creado');
      onSaved(data);
    } catch (e) { toast.error(errMsg(e, 'No se pudo guardar el torneo')); }
    finally { setSaving(false); }
  };

  return (
    <Modal title={tournament ? 'Editar torneo' : 'Nuevo torneo'} onClose={onClose} maxWidth={640}
      footer={<>
        <button className="btn btn-secondary" onClick={onClose}>Cancelar</button>
        <button className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Guardando…' : 'Guardar'}</button>
      </>}>
      <div className="form-row-2">
        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <label>Nombre del torneo *</label>
          <input className="form-control" value={form.name} onChange={e => set({ name: e.target.value })} placeholder="Ej: Copa Federación 2026" />
        </div>
        <div className="form-group">
          <label>Organizador *</label>
          <input className="form-control" value={form.organizer} onChange={e => set({ organizer: e.target.value })} placeholder="Ej: Liga distrital" />
        </div>
        <div className="form-group">
          <label>Lugar principal *</label>
          <input className="form-control" value={form.mainLocation} onChange={e => set({ mainLocation: e.target.value })} placeholder="Ej: Estadio municipal" />
        </div>
        <div className="form-group">
          <label>Modalidad</label>
          <select className="form-control" value={form.modality} onChange={e => set({ modality: Number(e.target.value) })}>
            {MODALITIES.map(f => <option key={f.value} value={f.value}>{f.label}</option>)}
          </select>
        </div>
        <div className="form-group">
          <label>Nivel</label>
          <select className="form-control" value={form.level} onChange={e => set({ level: Number(e.target.value) })}>
            {LEVELS.map(f => <option key={f.value} value={f.value}>{f.label}</option>)}
          </select>
        </div>
        <div className="form-group">
          <label>Formato</label>
          <select className="form-control" value={form.format} onChange={e => set({ format: Number(e.target.value) })}>
            {FORMATS.map(f => <option key={f.value} value={f.value}>{f.label}</option>)}
          </select>
        </div>
        <div className="form-group">
          <label>Estado</label>
          <select className="form-control" value={form.status} onChange={e => set({ status: Number(e.target.value) })}>
            {STATUSES.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
          </select>
        </div>

        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <label>¿Cómo se juega?</label>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 8 }}>
            {(form.scheduleMode === 3 ? [...SCHEDULE_MODES, LEGACY_SCHEDULE] : SCHEDULE_MODES).map(m => (
              <button key={m.value} type="button" onClick={() => set({ scheduleMode: m.value })}
                className={`btn ${form.scheduleMode === m.value ? 'btn-primary' : 'btn-secondary'}`}
                style={{ flexDirection: 'column', alignItems: 'flex-start', textAlign: 'left', height: 'auto', padding: '10px 12px' }}>
                <strong style={{ fontSize: 13 }}>{m.label}</strong>
                <span style={{ fontSize: 11, opacity: 0.85, fontWeight: 400 }}>{m.hint}</span>
              </button>
            ))}
          </div>
        </div>

        <div className="form-group">
          <label>{form.scheduleMode === 1 ? 'Día *' : form.scheduleMode === 3 ? 'Desde *' : 'Inicio *'}</label>
          <input type="date" className="form-control" value={form.startDate} onChange={e => set({ startDate: e.target.value })} />
        </div>
        {form.scheduleMode !== 1 && (
          <div className="form-group">
            <label>{form.scheduleMode === 3 ? 'Hasta (opcional)' : 'Fin *'}</label>
            <input type="date" className="form-control" value={form.endDate} min={form.startDate || undefined} onChange={e => set({ endDate: e.target.value })} />
          </div>
        )}
        {[2, 3].includes(form.scheduleMode) && (
          <div className="form-group" style={{ gridColumn: '1/-1' }}>
            <label>Días de juego *</label>
            <DayPicker value={form.playDays} onChange={playDays => set({ playDays })} />
            <small className="text-muted">Al asociar categorías desde el Calendario se crean estos días con hora y rival por definir.</small>
          </div>
        )}

        <div className="form-group" style={{ gridColumn: '1/-1', marginBottom: 0 }}><strong style={{ fontSize: 13 }}>Costos y honorario</strong></div>
        <div className="form-group">
          <label>Inscripción por alumno (S/)</label>
          <input type="number" min={0} step="0.50" className="form-control" value={form.inscriptionFee} onChange={e => set({ inscriptionFee: e.target.value })} placeholder="Vacío = no se cobra" />
          <small className="text-muted">Se cobra una vez a cada alumno de las categorías participantes.</small>
        </div>
        <div className="form-group">
          <label>Arbitraje por partido (S/)</label>
          <input type="number" min={0} step="0.50" className="form-control" value={form.refereeFee} onChange={e => set({ refereeFee: e.target.value })} placeholder="Vacío = no se cobra" />
          <small className="text-muted">Por alumno, en cada fecha a la que asiste.</small>
        </div>
        {Number(form.refereeFee) > 0 && (
          <SameDayDiscount value={form.refereeSameDayDiscountPercent} onChange={v => set({ refereeSameDayDiscountPercent: v })} />
        )}
        <div className="form-group">
          <label>Honorario del entrenador por fecha (S/)</label>
          <input type="number" min={0} step="0.50" className="form-control" value={form.defaultCoachFee} onChange={e => set({ defaultCoachFee: e.target.value })} placeholder="Sugerido; se cambia por día" />
        </div>
        <div className="form-group" />

        <div className="form-group">
          <label>Contacto del organizador</label>
          <input className="form-control" value={form.contactName} onChange={e => set({ contactName: e.target.value })} placeholder="Nombre del delegado" />
        </div>
        <div className="form-group">
          <label>Teléfono</label>
          <input className="form-control" value={form.contactPhone} onChange={e => set({ contactPhone: e.target.value })} placeholder="999 999 999" />
        </div>
        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <label>Reglamento y documentos exigidos</label>
          <textarea className="form-control" rows={3} value={form.rules} onChange={e => set({ rules: e.target.value })}
            placeholder="Ej: DNI original, ficha médica vigente, 18 jugadores por planilla, tiempos de 25 min…" />
        </div>

        <div className="form-group" style={{ gridColumn: '1/-1', marginBottom: 0 }}><strong style={{ fontSize: 13 }}>Reglas para los jugadores</strong></div>
        <div className="form-group">
          <label>Amarillas que suspenden 1 partido</label>
          <input type="number" min={0} className="form-control" value={form.yellowCardsForSuspension} onChange={e => set({ yellowCardsForSuspension: e.target.value })} placeholder="Ej: 3 (vacío = no se controla)" />
        </div>
        <div className="form-group">
          <label>Partidos de suspensión por roja</label>
          <input type="number" min={0} className="form-control" value={form.redCardSuspensionMatches} onChange={e => set({ redCardSuspensionMatches: e.target.value })} placeholder="Ej: 1 (vacío = no se controla)" />
        </div>
        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <label>Documentos que debe entregar cada jugador</label>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginBottom: 6 }}>
            {form.requiredDocuments.map(d => (
              <span key={d} className="badge badge-info" style={{ display: 'inline-flex', gap: 4, alignItems: 'center' }}>
                {d}<button type="button" className="btn-icon" style={{ padding: 0 }} aria-label={`Quitar ${d}`}
                  onClick={() => set({ requiredDocuments: form.requiredDocuments.filter(x => x !== d) })}><X size={12} /></button>
              </span>
            ))}
            {['DNI', 'Certificado médico', 'Foto', 'Carné de la liga'].filter(d => !form.requiredDocuments.includes(d)).map(d => (
              <button key={d} type="button" className="btn btn-ghost btn-sm" style={{ padding: '2px 8px' }} onClick={() => addDoc(d)}>+ {d}</button>
            ))}
          </div>
          <div style={{ display: 'flex', gap: 6 }}>
            <input className="form-control" value={docDraft} onChange={e => setDocDraft(e.target.value)} placeholder="Otro documento"
              onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); addDoc(); } }} />
            <button type="button" className="btn btn-secondary btn-sm" onClick={() => addDoc()} disabled={!docDraft.trim()}>Agregar</button>
          </div>
        </div>
        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <label style={{ display: 'flex', gap: 8, alignItems: 'center', cursor: 'pointer' }}>
            <input type="checkbox" checked={form.requiresAuthorization} onChange={e => set({ requiresAuthorization: e.target.checked })} />
            Pedir autorización del apoderado (viajes, salidas con menores)
          </label>
          {form.requiresAuthorization && (
            <textarea className="form-control" rows={2} style={{ marginTop: 6 }} value={form.authorizationText} onChange={e => set({ authorizationText: e.target.value })}
              placeholder="Autorizo a mi hijo(a) a participar en este torneo, incluidos los traslados con la delegación de la academia." />
          )}
          {form.requiresAuthorization && <small className="text-muted">La familia la acepta en la app. Si la firmó en papel, regístrala desde el plantel. No bloquea la convocatoria.</small>}
        </div>
      </div>
    </Modal>
  );
}

// ───────────────────────── Equipo ─────────────────────────
export function TeamFormModal({ tournamentId, team, categories, staff, onClose, onSaved }) {
  const [form, setForm] = useState(() => ({
    name: team?.name || '',
    categoryIds: team?.categoryIds || [],
    headCoachId: team?.headCoachId || '',
    assistantId: team?.assistantId || '',
    birthYearFrom: team?.birthYearFrom ?? '',
    birthYearTo: team?.birthYearTo ?? '',
    maxPlayers: team?.maxPlayers ?? '',
    notes: team?.notes || '',
    matchFee: team?.matchFee ?? '',
    applyCoachToUpcoming: true,
  }));
  const [saving, setSaving] = useState(false);
  const set = patch => setForm(f => ({ ...f, ...patch }));
  const coachChanged = team && (team.headCoachId || '') !== form.headCoachId;

  const toggleCat = id => {
    const next = form.categoryIds.includes(id) ? form.categoryIds.filter(x => x !== id) : [...form.categoryIds, id];
    const patch = { categoryIds: next };
    if (!form.name.trim() && next.length === 1) patch.name = categories.find(c => c.id === id)?.name || '';
    set(patch);
  };

  const save = async () => {
    if (!form.name.trim()) return toast.error('Ponle un nombre al equipo');
    if (form.categoryIds.length === 0) return toast.error('Elige al menos una categoría');
    const num = v => (v === '' || v === null ? null : Number(v));
    const body = {
      ...form,
      headCoachId: form.headCoachId || null,
      assistantId: form.assistantId || null,
      birthYearFrom: num(form.birthYearFrom), birthYearTo: num(form.birthYearTo), maxPlayers: num(form.maxPlayers),
      matchFee: form.matchFee === '' ? null : Number(form.matchFee),
    };
    setSaving(true);
    try {
      const { data } = team
        ? await api.put(`/tournaments/teams/${team.id}`, body)
        : await api.post(`/tournaments/${tournamentId}/teams`, body);
      toast.success(team ? 'Equipo actualizado' : 'Equipo inscrito');
      onSaved(data);
    } catch (e) { toast.error(errMsg(e, 'No se pudo guardar el equipo')); }
    finally { setSaving(false); }
  };

  return (
    <Modal title={team ? `Editar ${team.name}` : 'Inscribir equipo'} onClose={onClose} maxWidth={620}
      footer={<>
        <button className="btn btn-secondary" onClick={onClose}>Cancelar</button>
        <button className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Guardando…' : 'Guardar'}</button>
      </>}>
      <div className="form-row-2">
        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <label>Categorías de origen *</label>
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            {categories.map(c => (
              <button key={c.id} type="button" onClick={() => toggleCat(c.id)}
                className={`btn btn-sm ${form.categoryIds.includes(c.id) ? 'btn-primary' : 'btn-secondary'}`}>{c.name}{c.headquarterName ? ` · ${c.headquarterName}` : ''}</button>
            ))}
          </div>
          <small className="text-muted">Sirven para convocar en bloque. Luego puedes sumar refuerzos de otras categorías al plantel.</small>
        </div>
        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <label>Nombre del equipo *</label>
          <input className="form-control" value={form.name} onChange={e => set({ name: e.target.value })} placeholder="Ej: Sub-12 A" />
          <small className="text-muted">Si la categoría juega con más de un equipo, distínguelos: Sub-12 A, Sub-12 B.</small>
        </div>
        <div className="form-group">
          <label>Entrenador responsable</label>
          <select className="form-control" value={form.headCoachId} onChange={e => set({ headCoachId: e.target.value })}>
            <option value="">Sin asignar</option>
            {staff.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
          <small className="text-muted">Puede ser distinto al de los entrenamientos.</small>
        </div>
        <div className="form-group">
          <label>Asistente o delegado</label>
          <select className="form-control" value={form.assistantId} onChange={e => set({ assistantId: e.target.value })}>
            <option value="">Ninguno</option>
            {staff.filter(s => s.id !== form.headCoachId).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
        </div>
        {coachChanged && (
          <div className="form-group" style={{ gridColumn: '1/-1' }}>
            <label style={{ display: 'flex', gap: 8, alignItems: 'center', cursor: 'pointer' }}>
              <input type="checkbox" checked={form.applyCoachToUpcoming} onChange={e => set({ applyCoachToUpcoming: e.target.checked })} />
              <span>Pasar al nuevo responsable los partidos por jugar (los reemplazos puntuales se respetan)</span>
            </label>
          </div>
        )}
        <div className="form-group">
          <label>Nacidos desde (año)</label>
          <input type="number" className="form-control" value={form.birthYearFrom} onChange={e => set({ birthYearFrom: e.target.value })} placeholder="Ej: 2014" />
        </div>
        <div className="form-group">
          <label>Nacidos hasta (año)</label>
          <input type="number" className="form-control" value={form.birthYearTo} onChange={e => set({ birthYearTo: e.target.value })} placeholder="Ej: 2015" />
        </div>
        <div className="form-group">
          <label>Tope de jugadores en planilla</label>
          <input type="number" className="form-control" value={form.maxPlayers} onChange={e => set({ maxPlayers: e.target.value })} placeholder="Ej: 18" />
        </div>
        <div className="form-group">
          <label>Notas</label>
          <input className="form-control" value={form.notes} onChange={e => set({ notes: e.target.value })} placeholder="Grupo, uniforme, etc." />
        </div>
        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <label>Honorario por partido del responsable (S/)</label>
          <input type="number" min={0} step="0.50" className="form-control" value={form.matchFee} onChange={e => set({ matchFee: e.target.value })} placeholder="Vacío = sin honorario" />
          <small className="text-muted">Lo cobra quien dirige el partido (titular o reemplazo) y figura presente en la asistencia de entrenadores. Con honorario, el partido no cuenta como sesión u hora en su planilla.</small>
        </div>
      </div>
    </Modal>
  );
}

// ───────────────────────── Plantel ─────────────────────────
export function RosterModal({ team, tournament, onClose, onSaved }) {
  const [candidates, setCandidates] = useState([]);
  const [players, setPlayers] = useState(() => team.players.map(p => ({ ...p })));
  const [query, setQuery] = useState('');
  const [onlyTeamCats, setOnlyTeamCats] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    api.get(`/tournaments/teams/${team.id}/candidates`).then(r => setCandidates(r.data))
      .catch(e => toast.error(errMsg(e, 'No se pudieron cargar los alumnos')));
  }, [team.id]);

  const inRoster = useMemo(() => new Set(players.map(p => p.studentId)), [players]);
  const byId = useMemo(() => Object.fromEntries(candidates.map(c => [c.studentId, c])), [candidates]);
  const visible = candidates.filter(c => !inRoster.has(c.studentId) && (!onlyTeamCats || c.inTeamCategories) &&
    c.fullName.toLowerCase().includes(query.trim().toLowerCase()));

  const required = tournament?.requiredDocuments || [];
  const add = c => setPlayers(ps => [...ps, { studentId: c.studentId, fullName: c.fullName, categoryName: c.categoryName,
    outsideBirthYears: c.outsideBirthYears, isActive: true, shirtNumber: null, position: '', deliveredDocuments: [], isNew: true }]);
  const toggleDoc = (id, doc) => setPlayers(ps => ps.map(p => {
    if (p.studentId !== id) return p;
    const docs = p.deliveredDocuments || [];
    return { ...p, deliveredDocuments: docs.includes(doc) ? docs.filter(d => d !== doc) : [...docs, doc] };
  }));
  const paper = async p => {
    try {
      await api.post(`/tournaments/${tournament.id}/authorizations`, { studentId: p.studentId, note: 'Firmada en papel' });
      update(p.studentId, { authorized: true, authorizationMethod: 'papel' });
      toast.success('Autorización registrada');
    } catch (e) { toast.error(errMsg(e, 'No se pudo registrar')); }
  };
  const addAllVisible = () => visible.forEach(add);
  const update = (id, patch) => setPlayers(ps => ps.map(p => (p.studentId === id ? { ...p, ...patch } : p)));
  const remove = id => setPlayers(ps => ps.filter(p => p.studentId !== id));

  const shirtCount = players.reduce((acc, p) => (p.shirtNumber != null && p.shirtNumber !== '' ? { ...acc, [p.shirtNumber]: (acc[p.shirtNumber] || 0) + 1 } : acc), {});
  const over = team.maxPlayers && players.length > team.maxPlayers;

  const save = async () => {
    setSaving(true);
    try {
      const body = { players: players.map(p => ({ studentId: p.studentId, shirtNumber: p.shirtNumber === '' || p.shirtNumber == null ? null : Number(p.shirtNumber),
        position: p.position || null, deliveredDocuments: p.deliveredDocuments || [] })) };
      const { data } = await api.put(`/tournaments/teams/${team.id}/players`, body);
      const extra = data.matchesUpdated ? ` · ${data.matchesUpdated} partido(s) por jugar actualizados` : '';
      toast.success(`Plantel guardado${extra}`);
      onSaved(data.team);
    } catch (e) { toast.error(errMsg(e, 'No se pudo guardar el plantel')); }
    finally { setSaving(false); }
  };

  return (
    <Modal title={`Plantel · ${team.name}`} onClose={onClose} maxWidth={980}
      footer={<>
        <span className="text-muted" style={{ marginRight: 'auto', fontSize: 13 }}>
          {players.length}{team.maxPlayers ? ` / ${team.maxPlayers}` : ''} jugadores
          {over && <span style={{ color: 'var(--warning, #f59e0b)', marginLeft: 8 }}>supera el tope</span>}
        </span>
        <button className="btn btn-secondary" onClick={onClose}>Cancelar</button>
        <button className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Guardando…' : 'Guardar plantel'}</button>
      </>}>
      {team.autoRoster && (
        <p className="text-muted" style={{ marginTop: 0, fontSize: 13 }}>
          Son los alumnos activos de {team.name}: se actualiza solo cuando alguien entra o sale de la categoría.
          Aquí registras camiseta, posición, documentos y autorizaciones.
        </p>
      )}
      <div style={{ display: 'grid', gridTemplateColumns: team.autoRoster ? '1fr' : 'repeat(auto-fit, minmax(300px, 1fr))', gap: 16 }}>
        {!team.autoRoster && <div>
          <div style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
            <div style={{ position: 'relative', flex: 1 }}>
              <Search size={14} style={{ position: 'absolute', left: 10, top: 11, opacity: 0.6 }} />
              <input className="form-control" style={{ paddingLeft: 30 }} value={query} onChange={e => setQuery(e.target.value)} placeholder="Buscar alumno" />
            </div>
            <button className="btn btn-secondary btn-sm" onClick={addAllVisible} disabled={visible.length === 0} title="Agregar a todos los de la lista">
              <Plus size={14} /> Todos
            </button>
          </div>
          <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, marginBottom: 8, cursor: 'pointer' }}>
            <input type="checkbox" checked={onlyTeamCats} onChange={e => setOnlyTeamCats(e.target.checked)} />
            Solo de las categorías del equipo (desmarca para buscar refuerzos)
          </label>
          <div style={{ maxHeight: 380, overflowY: 'auto', border: '1px solid var(--border)', borderRadius: 8 }}>
            {visible.length === 0 && <p className="text-muted" style={{ padding: 12, margin: 0 }}>Sin alumnos para agregar.</p>}
            {visible.map(c => (
              <button key={c.studentId} type="button" onClick={() => add(c)}
                style={{ display: 'flex', width: '100%', justifyContent: 'space-between', gap: 8, padding: '8px 12px', background: 'none', border: 'none',
                  borderBottom: '1px solid var(--border)', color: 'inherit', cursor: 'pointer', textAlign: 'left', fontFamily: 'inherit' }}>
                <span>
                  <strong style={{ fontSize: 13 }}>{c.fullName}</strong>
                  <span className="text-muted" style={{ fontSize: 12, display: 'block' }}>
                    {c.categoryName}{c.birthDate ? ` · ${c.birthDate.slice(0, 4)}` : ''}
                    {c.otherTeams.length > 0 && ` · ya en ${c.otherTeams.join(', ')}`}
                  </span>
                </span>
                <span style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                  {!c.inTeamCategories && <span className="badge badge-info">Refuerzo</span>}
                  {c.outsideBirthYears && <span className="badge badge-warning">Fuera de años</span>}
                  <Plus size={16} />
                </span>
              </button>
            ))}
          </div>
        </div>}

        <div>
          <div className="table-wrap" style={{ maxHeight: 440, overflowY: 'auto' }}>
            <table className="table" style={{ width: '100%', fontSize: 13 }}>
              <thead>
                <tr><th style={{ width: 70 }}>N°</th><th>Jugador</th><th style={{ width: 110 }}>Posición</th><th style={{ width: 36 }} /></tr>
              </thead>
              <tbody>
                {players.length === 0 && <tr><td colSpan={4} className="text-muted">Agrega jugadores desde la lista.</td></tr>}
                {players.map(p => {
                  const info = byId[p.studentId];
                  const dup = p.shirtNumber != null && p.shirtNumber !== '' && shirtCount[p.shirtNumber] > 1;
                  return (
                    <tr key={p.studentId}>
                      <td>
                        <input className="form-control" type="number" min={0} max={999} value={p.shirtNumber ?? ''} style={{ padding: '6px 4px', textAlign: 'center', minWidth: 52, ...(dup ? { borderColor: '#f59e0b' } : {}) }}
                          onChange={e => update(p.studentId, { shirtNumber: e.target.value })} title={dup ? 'Número repetido' : undefined} />
                      </td>
                      <td>
                        <strong>{p.fullName}</strong>
                        <span className="text-muted" style={{ display: 'block', fontSize: 12 }}>
                          {p.categoryName}
                          {info && !info.inTeamCategories && ' · refuerzo'}
                          {p.outsideBirthYears && ' · fuera de años'}
                          {!p.isActive && ' · retirado'}
                        </span>
                        {!p.isNew && p.stats && p.stats.matches > 0 && (
                          <span className="text-muted" style={{ display: 'block', fontSize: 11 }}>
                            {p.stats.matches} PJ · {p.stats.minutes} min · {p.stats.goals} gol(es){p.stats.yellowCards ? ` · ${p.stats.yellowCards} TA` : ''}{p.stats.redCards ? ` · ${p.stats.redCards} TR` : ''}
                          </span>
                        )}
                        <span style={{ display: 'flex', gap: 4, flexWrap: 'wrap', marginTop: 3 }}>
                          {!p.isNew && p.fit && <span className="badge badge-success" style={{ fontSize: 10 }}>Apto</span>}
                          {p.suspendedFor && <span className="badge badge-danger" style={{ fontSize: 10 }} title={p.suspendedFor}>Suspendido · {p.suspendedFor.split(':')[0]}</span>}
                          {tournament?.requiresAuthorization && (p.authorized
                            ? <span className="badge badge-muted" style={{ fontSize: 10 }}>Autorizado{p.authorizationMethod === 'papel' ? ' (papel)' : ''}</span>
                            : p.isNew ? null : <button type="button" className="badge badge-warning" style={{ fontSize: 10, border: 'none', cursor: 'pointer' }}
                                onClick={() => paper(p)} title="Registrar autorización firmada en papel">Sin autorización · registrar papel</button>)}
                        </span>
                        {required.length > 0 && (
                          <span style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 4 }}>
                            {required.map(d => (
                              <label key={d} style={{ fontSize: 11, display: 'inline-flex', gap: 3, alignItems: 'center', cursor: 'pointer' }}>
                                <input type="checkbox" checked={(p.deliveredDocuments || []).includes(d)} onChange={() => toggleDoc(p.studentId, d)} />{d}
                              </label>
                            ))}
                          </span>
                        )}
                      </td>
                      <td><input className="form-control" value={p.position || ''} onChange={e => update(p.studentId, { position: e.target.value })} placeholder="—" /></td>
                      <td>{!team.autoRoster && <button className="btn-icon text-danger" onClick={() => remove(p.studentId)} aria-label="Quitar"><Trash2 size={15} /></button>}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          {!team.autoRoster && <small className="text-muted">Al guardar, los partidos por jugar convocan a los nuevos y retiran a quienes salieron. La familia recibe un aviso.</small>}
        </div>
      </div>
    </Modal>
  );
}

// ───────────────────────── Asistente de fechas ─────────────────────────
export function GenerateMatchesModal({ team, tournament, headquarters, onClose, onDone }) {
  const today = new Date().toLocaleDateString('en-CA', { timeZone: 'America/Lima' });
  const start = dateOf(tournament.startDate);
  const [form, setForm] = useState({
    fromDate: start && start > today ? start : today,
    toDate: dateOf(tournament.endDate) || '',
    daysOfWeek: tournament.playDays?.length ? tournament.playDays : [6],
    startTime: '09:00', endTime: '10:30', callMinutesBefore: 45,
    location: tournament.mainLocation || '', headquarterId: '', roundPrefix: 'Fecha',
  });
  const [result, setResult] = useState(null);
  const [saving, setSaving] = useState(false);
  const set = patch => setForm(f => ({ ...f, ...patch }));

  const run = async () => {
    if (!form.fromDate || !form.toDate) return toast.error('Indica desde y hasta cuándo');
    if (form.daysOfWeek.length === 0) return toast.error('Elige al menos un día');
    setSaving(true);
    try {
      const { data } = await api.post(`/tournaments/teams/${team.id}/matches/generate`, {
        ...form, headquarterId: form.headquarterId || null, callMinutesBefore: Number(form.callMinutesBefore) || 0,
      });
      setResult(data);
      if (data.created > 0) { toast.success(`${data.created} partido(s) creados`); onDone(); }
      else toast('No se creó ningún partido');
    } catch (e) { toast.error(errMsg(e, 'No se pudieron generar las fechas')); }
    finally { setSaving(false); }
  };

  return (
    <Modal title={`Generar fechas · ${team.name}`} onClose={onClose} maxWidth={620}
      footer={<>
        <button className="btn btn-secondary" onClick={onClose}>{result ? 'Cerrar' : 'Cancelar'}</button>
        <button className="btn btn-primary" onClick={run} disabled={saving}>{saving ? 'Generando…' : 'Generar partidos'}</button>
      </>}>
      <p className="text-muted" style={{ marginTop: 0, fontSize: 13 }}>
        Crea un partido por cada día de juego del rango. El rival, la hora o la cancha se ajustan luego en cada partido, cuando el organizador publique el fixture.
      </p>
      <div className="form-row-2">
        <div className="form-group"><label>Desde *</label><input type="date" className="form-control" value={form.fromDate} onChange={e => set({ fromDate: e.target.value })} /></div>
        <div className="form-group"><label>Hasta *</label><input type="date" className="form-control" value={form.toDate} min={form.fromDate} onChange={e => set({ toDate: e.target.value })} /></div>
        <div className="form-group" style={{ gridColumn: '1/-1' }}><label>Días de juego *</label><DayPicker value={form.daysOfWeek} onChange={daysOfWeek => set({ daysOfWeek })} /></div>
        <div className="form-group"><label>Inicio</label><input type="time" className="form-control" value={form.startTime} onChange={e => set({ startTime: e.target.value })} /></div>
        <div className="form-group"><label>Fin</label><input type="time" className="form-control" value={form.endTime} onChange={e => set({ endTime: e.target.value })} /></div>
        <div className="form-group">
          <label>Citación (minutos antes)</label>
          <input type="number" min={0} className="form-control" value={form.callMinutesBefore} onChange={e => set({ callMinutesBefore: e.target.value })} />
        </div>
        <div className="form-group">
          <label>Nombre de la fecha</label>
          <input className="form-control" value={form.roundPrefix} onChange={e => set({ roundPrefix: e.target.value })} placeholder="Fecha → Fecha 1, Fecha 2…" />
        </div>
        <div className="form-group">
          <label>Cancha / lugar</label>
          <input className="form-control" value={form.location} onChange={e => set({ location: e.target.value })} />
        </div>
        <div className="form-group">
          <label>Sede de la academia (si es local)</label>
          <select className="form-control" value={form.headquarterId} onChange={e => set({ headquarterId: e.target.value })}>
            <option value="">Ninguna</option>
            {headquarters.map(h => <option key={h.id} value={h.id}>{h.name}</option>)}
          </select>
        </div>
      </div>
      {result && (
        <div className={`alert ${result.created ? 'alert-success' : 'alert-warning'}`} style={{ marginTop: 8 }}>
          <strong>{result.created} partido(s) creados.</strong>
          {result.skipped.length > 0 && (
            <ul style={{ margin: '6px 0 0', paddingLeft: 18, fontSize: 13 }}>
              {result.skipped.map(s => <li key={s.date}>{fmtDate(s.date)}: {s.reason}</li>)}
            </ul>
          )}
        </div>
      )}
    </Modal>
  );
}

// ───────────────────────── Partido ─────────────────────────
export function MatchFormModal({ match, team, teams, headquarters, staff, onClose, onSaved }) {
  const teamOf = id => teams.find(t => t.id === id);
  const [teamId, setTeamId] = useState(match?.teamId || team?.id || teams[0]?.id || '');
  const currentTeam = teamOf(teamId);
  const [form, setForm] = useState(() => ({
    date: dateOf(match?.startTime) || '',
    start: match?.timePending ? '' : (timeOf(match?.startTime) || '09:00'),
    end: match?.timePending ? '' : (timeOf(match?.endTime) || '10:30'),
    coachFee: match?.coachFee ?? '',
    call: timeOf(match?.callTime) || '',
    opponentTeam: match?.opponentTeam || '',
    round: match?.round || '',
    location: match?.location || '',
    headquarterId: match?.headquarterId || '',
    teacherId: match?.isReplacement ? match.teacherId : '',
    matchStatus: match?.matchStatus || 1,
    goalsFor: match?.goalsFor ?? '',
    goalsAgainst: match?.goalsAgainst ?? '',
    description: '',
  }));
  const [saving, setSaving] = useState(false);
  const set = patch => setForm(f => ({ ...f, ...patch }));

  const save = async () => {
    if (!form.date) return toast.error('Indica la fecha del partido');
    if (!form.start || !form.end) return toast.error('Indica la hora de inicio y de fin');
    if (form.end <= form.start) return toast.error('La hora de fin debe ser posterior al inicio');
    if (form.call && form.call > form.start) return toast.error('La citación debe ser antes del partido');
    const num = v => (v === '' || v === null ? null : Number(v));
    const body = {
      startTime: wall(form.date, form.start), endTime: wall(form.date, form.end),
      callTime: form.call ? wall(form.date, form.call) : null,
      opponentTeam: form.opponentTeam || null, round: form.round || null, location: form.location || null,
      headquarterId: form.headquarterId || null, teacherId: form.teacherId || null,
      matchStatus: Number(form.matchStatus), goalsFor: num(form.goalsFor), goalsAgainst: num(form.goalsAgainst),
      coachFee: num(form.coachFee),
    };
    setSaving(true);
    try {
      const { data } = match
        ? await api.put(`/tournaments/matches/${match.id}`, body)
        : await api.post(`/tournaments/teams/${teamId}/matches`, body);
      toast.success(match ? 'Partido actualizado' : `Partido creado · ${data.calledCount} convocado(s)`);
      onSaved(data);
    } catch (e) { toast.error(errMsg(e, 'No se pudo guardar el partido')); }
    finally { setSaving(false); }
  };

  const played = [2, 5, 6].includes(Number(form.matchStatus));
  return (
    <Modal title={match ? 'Editar partido' : 'Nuevo partido'} onClose={onClose} maxWidth={640}
      footer={<>
        <button className="btn btn-secondary" onClick={onClose}>Cancelar</button>
        <button className="btn btn-primary" onClick={save} disabled={saving || (!match && !teamId)}>{saving ? 'Guardando…' : 'Guardar'}</button>
      </>}>
      <div className="form-row-2">
        {!match && (
          <div className="form-group" style={{ gridColumn: '1/-1' }}>
            <label>Equipo *</label>
            <select className="form-control" value={teamId} onChange={e => setTeamId(e.target.value)}>
              {teams.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </div>
        )}
        <div className="form-group"><label>Fecha *</label><input type="date" className="form-control" value={form.date} onChange={e => set({ date: e.target.value })} /></div>
        <div className="form-group">
          <label>Citación</label>
          <input type="time" className="form-control" value={form.call} onChange={e => set({ call: e.target.value })} />
          {!form.call && form.start && (
            <button type="button" className="btn btn-ghost btn-sm" style={{ padding: 0, marginTop: 4 }}
              onClick={() => set({ call: timeOf(addMinutesWall(wall('2000-01-01', form.start), -45)) })}>Usar 45 min antes</button>
          )}
        </div>
        <div className="form-group"><label>Inicio *</label><input type="time" className="form-control" value={form.start} onChange={e => set({ start: e.target.value })} /></div>
        <div className="form-group"><label>Fin *</label><input type="time" className="form-control" value={form.end} onChange={e => set({ end: e.target.value })} /></div>
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
        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <label>Responsable del partido</label>
          <select className="form-control" value={form.teacherId} onChange={e => set({ teacherId: e.target.value })}>
            <option value="">El del equipo{currentTeam?.headCoachName ? ` (${currentTeam.headCoachName})` : ''}</option>
            {staff.filter(s => s.id !== currentTeam?.headCoachId).map(s => <option key={s.id} value={s.id}>Reemplazo: {s.name}</option>)}
          </select>
          <small className="text-muted">El reemplazo solo aplica a este partido y es quien figura en la planilla.</small>
        </div>
        <div className="form-group">
          <label>Honorario de este día (S/)</label>
          <input type="number" min={0} step="0.50" className="form-control" value={form.coachFee} onChange={e => set({ coachFee: e.target.value })} placeholder="Vacío = sin honorario" />
        </div>
        {match?.needsDetails && (
          <div className="alert alert-warning" style={{ gridColumn: '1/-1', fontSize: 12, display: 'block', margin: 0 }}>
            Completa la hora y el rival: sin ellos no se puede tomar asistencia. Al guardarlos se convoca a los alumnos.
          </div>
        )}
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
              {!played && (form.goalsFor !== '' && form.goalsAgainst !== '') && <small className="text-muted">Con marcador se marca como Jugado.</small>}
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}

// ───────────────────────── Convocatoria de un partido ─────────────────────────
export function MatchCallsModal({ match, onClose, onChanged }) {
  const [calls, setCalls] = useState(null);
  const [candidates, setCandidates] = useState([]);
  const [adding, setAdding] = useState('');
  const [suspended, setSuspended] = useState([]);
  const load = () => api.get(`/calendar/events/${match.id}/calls`).then(r => setCalls(r.data)).catch(() => setCalls([]));

  useEffect(() => {
    load();
    if (match.teamId) api.get(`/tournaments/teams/${match.teamId}/candidates`).then(r => setCandidates(r.data)).catch(() => {});
    api.get(`/tournaments/matches/${match.id}/suspensions`).then(r => setSuspended(r.data)).catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [match.id]);

  const called = new Set((calls || []).map(c => c.studentId));
  const add = async () => {
    if (!adding) return;
    try { await api.post(`/tournaments/matches/${match.id}/calls/${adding}`); setAdding(''); await load(); onChanged(); }
    catch (e) { toast.error(errMsg(e, 'No se pudo convocar')); }
  };
  const remove = async c => {
    try { await api.delete(`/tournaments/matches/${match.id}/calls/${c.studentId}`); await load(); onChanged(); }
    catch (e) { toast.error(errMsg(e, 'No se pudo quitar')); }
  };
  const counts = (calls || []).reduce((a, c) => ({ ...a, [String(c.isConfirmed)]: (a[String(c.isConfirmed)] || 0) + 1 }), {});

  return (
    <Modal title={`Convocatoria · ${match.round || fmtDate(match.startTime)}`} onClose={onClose} maxWidth={620}>
      <p className="text-muted" style={{ marginTop: 0, fontSize: 13 }}>
        {match.teamName} · {fmtDate(match.startTime)} {timeOf(match.startTime)}{match.opponentTeam ? ` vs ${match.opponentTeam}` : ''}.
        {' '}Confirmados {counts.true || 0} · No van {counts.false || 0} · Pendientes {counts.null || 0}
      </p>
      {match.teamId && (
        <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
          <select className="form-control" value={adding} onChange={e => setAdding(e.target.value)}>
            <option value="">Convocar a otro alumno solo para este partido…</option>
            {candidates.filter(c => !called.has(c.studentId)).map(c => (
              <option key={c.studentId} value={c.studentId}>{c.fullName} · {c.categoryName}</option>
            ))}
          </select>
          <button className="btn btn-primary btn-sm" onClick={add} disabled={!adding}><Plus size={14} /> Convocar</button>
        </div>
      )}
      {suspended.filter(s => !s.overridden).length > 0 && (
        <div className="alert alert-warning" style={{ fontSize: 12, marginBottom: 12, display: 'block' }}>
          <strong>No convocados por suspensión:</strong>
          {suspended.filter(s => !s.overridden).map(s => <div key={s.studentId}>{s.studentName}: {s.reason}</div>)}
          <div style={{ marginTop: 4 }}>Si el reglamento lo permite, convócalo arriba: la suspensión queda levantada.</div>
        </div>
      )}
      {calls === null ? <p>Cargando…</p> : calls.length === 0 ? (
        <p className="text-muted">Nadie convocado. Revisa el plantel del equipo.</p>
      ) : (
        <div className="table-wrap" style={{ maxHeight: 360, overflowY: 'auto' }}>
          <table className="table" style={{ width: '100%', fontSize: 13 }}>
            <thead><tr><th>Alumno</th><th>Categoría</th><th>Respuesta</th><th style={{ width: 36 }} /></tr></thead>
            <tbody>
              {calls.map(c => (
                <tr key={c.id}>
                  <td>{c.studentName}</td>
                  <td>{c.studentCategory}</td>
                  <td>
                    {c.isConfirmed === true && <span className="badge badge-success">Confirmado</span>}
                    {c.isConfirmed === false && <span className="badge badge-danger">No va</span>}
                    {c.isConfirmed === null && <span className="badge badge-warning">Pendiente</span>}
                  </td>
                  <td><button className="btn-icon text-danger" onClick={() => remove(c)} aria-label="Quitar de este partido"><Trash2 size={15} /></button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {!match.teamId && (
        <div className="alert alert-warning" style={{ marginTop: 12, fontSize: 13 }}>
          <AlertTriangle size={14} /> Partido sin equipo (registrado antes): se convoca desde el Calendario.
        </div>
      )}
    </Modal>
  );
}

// ───────────────────────── Agregar categorías (mismo flujo que el Calendario) ─────────────────────────
export function ParticipationModal({ tournament, categories, headquarters = [], staff, onClose, onSaved }) {
  const [headquarterId, setHeadquarterId] = useState(headquarters.length === 1 ? headquarters[0].id : '');
  const [categoryIds, setCategoryIds] = useState([]);
  const visibleCategories = categories.filter(c => c.headquarterId === headquarterId && c.isActive !== false);
  const [headCoachId, setHeadCoachId] = useState('');
  const [coachFee, setCoachFee] = useState(tournament.defaultCoachFee ?? '');
  const [saving, setSaving] = useState(false);
  const toggle = id => setCategoryIds(c => (c.includes(id) ? c.filter(x => x !== id) : [...c, id]));
  const save = async () => {
    if (!headquarterId) return toast.error('Elige la sede');
    if (categoryIds.length === 0) return toast.error('Elige al menos una categoría');
    setSaving(true);
    try {
      const { data } = await api.post(`/tournaments/${tournament.id}/participations`, {
        categoryIds, headCoachId: headCoachId || null, coachFee: coachFee === '' ? null : Number(coachFee),
      });
      toast.success(`${data.daysCreated} día(s) creados para ${data.teamName}`);
      data.warnings?.forEach(w => toast(w, { icon: 'ℹ️' }));
      onSaved(data);
    } catch (e) { toast.error(errMsg(e, 'No se pudo agregar')); }
    finally { setSaving(false); }
  };
  return (
    <Modal title="Agregar categorías al torneo" onClose={onClose}
      footer={<>
        <button className="btn btn-secondary" onClick={onClose}>Cancelar</button>
        <button className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Creando…' : 'Crear días del torneo'}</button>
      </>}>
      <p className="text-muted" style={{ marginTop: 0, fontSize: 13 }}>
        Se crean los días del torneo para estas categorías con hora y rival por definir. Para otra categoría con otro entrenador, agrégala aparte.
      </p>
      <div className="form-group">
        <label>Sede *</label>
        <select className="form-control" value={headquarterId} onChange={e => { setHeadquarterId(e.target.value); setCategoryIds([]); }}>
          <option value="">Elige la sede</option>
          {headquarters.map(h => <option key={h.id} value={h.id}>{h.name}</option>)}
        </select>
      </div>
      <div className="form-group">
        <label>Categorías *</label>
        {!headquarterId ? <small className="text-muted">Primero elige la sede.</small> : visibleCategories.length === 0 ? (
          <small className="text-muted">Esta sede no tiene categorías activas.</small>
        ) : (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
            {visibleCategories.map(c => (
              <button key={c.id} type="button" onClick={() => toggle(c.id)}
                className={`btn btn-sm ${categoryIds.includes(c.id) ? 'btn-primary' : 'btn-secondary'}`}>{c.name}</button>
            ))}
          </div>
        )}
      </div>
      <div className="form-row-2">
        <div className="form-group">
          <label>Entrenador responsable</label>
          <select className="form-control" value={headCoachId} onChange={e => setHeadCoachId(e.target.value)}>
            <option value="">Sin asignar</option>
            {staff.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
        </div>
        <div className="form-group">
          <label>Honorario por fecha (S/)</label>
          <input type="number" min={0} step="0.50" className="form-control" value={coachFee} onChange={e => setCoachFee(e.target.value)} placeholder="Vacío = sin honorario" />
        </div>
      </div>
    </Modal>
  );
}
