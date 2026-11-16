import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import ConfirmDialog from '../../components/ConfirmDialog';
import { useAuth } from '../../context/AuthContext';
import TorneoFinanzas from './torneos/TorneoFinanzas';
import EncounterModal, { RefereeWaiverModal } from './torneos/EncounterModal';
import toast from 'react-hot-toast';
import {
  Trophy, Plus, Pencil, Trash2, ArrowLeft, Users, CalendarPlus, CalendarDays, Clock, MapPin,
  Shirt, AlertTriangle, Phone, ChevronRight, Send, UserCheck, BadgePercent,
} from 'lucide-react';
import {
  TournamentFormModal, TeamFormModal, RosterModal, GenerateMatchesModal, MatchFormModal, MatchCallsModal, ParticipationModal,
} from './torneos/TorneoModals';
import {
  FORMATS, MODALITIES, LEVELS, STATUSES, MATCH_STATUSES, labelOf, scheduleText, fmtDate, timeOf, dateOf, errMsg, todayLima,
} from './torneos/torneoUtils';

const STATUS_FILTERS = [{ value: 0, label: 'Todos' }, ...STATUSES];

export default function Torneos() {
  const [params, setParams] = useSearchParams();
  const selectedId = params.get('t');
  const [tournaments, setTournaments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState(0);
  const [editing, setEditing] = useState(null); // null | 'new' | tournament

  const load = useCallback(async () => {
    try {
      setLoading(true);
      const { data } = await api.get('/tournaments');
      setTournaments(data);
    } catch { toast.error('Error al cargar torneos'); }
    finally { setLoading(false); }
  }, []);
  useEffect(() => { load(); }, [load]);

  const open = id => setParams(id ? { t: id } : {});
  const shown = tournaments.filter(t => !filter || t.status === filter);

  if (selectedId) return <AppLayout title="Torneos"><TournamentDetail id={selectedId} onBack={() => { open(null); load(); }} /></AppLayout>;

  return (
    <AppLayout title="Torneos">
      <div className="page-header">
        <div>
          <h2 className="page-title"><Trophy size={26} /> Torneos</h2>
          <p className="page-subtitle">Competencias en las que participa la academia: equipos, planteles, fechas y convocatorias.</p>
        </div>
        <button className="btn btn-primary" onClick={() => setEditing('new')}><Plus size={16} /> Nuevo torneo</button>
      </div>

      <div className="tabs" style={{ maxWidth: 640, overflowX: 'auto' }}>
        {STATUS_FILTERS.map(s => (
          <button key={s.value} className={`tab-pill ${filter === s.value ? 'active' : ''}`} style={{ flex: '1 0 auto', whiteSpace: 'nowrap' }} onClick={() => setFilter(s.value)}>
            {s.label}
            <span style={{ opacity: 0.7, marginLeft: 4 }}>{tournaments.filter(t => !s.value || t.status === s.value).length}</span>
          </button>
        ))}
      </div>

      {loading ? <p>Cargando…</p> : shown.length === 0 ? (
        <div className="card empty-state" style={{ padding: 32, textAlign: 'center' }}>
          <Trophy size={36} style={{ opacity: 0.4 }} />
          <p className="text-muted">{tournaments.length ? 'No hay torneos con ese estado.' : 'Aún no registras torneos. Crea el primero para inscribir a tus equipos.'}</p>
        </div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(min(300px, 100%), 1fr))', gap: 16 }}>
          {shown.map(t => {
            const st = labelOf(STATUSES, t.status);
            return (
              <button key={t.id} className="card" onClick={() => open(t.id)}
                style={{ textAlign: 'left', cursor: 'pointer', padding: 18, color: 'inherit', fontFamily: 'inherit', display: 'flex', flexDirection: 'column', gap: 10 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'flex-start' }}>
                  <div>
                    <strong style={{ fontSize: 16 }}>{t.name}</strong>
                    <div className="text-muted" style={{ fontSize: 12 }}>{t.organizer} · {labelOf(MODALITIES, t.modality).label}{t.level ? ` · ${labelOf(LEVELS, t.level).label}` : ''}</div>
                  </div>
                  <span className={`badge ${st.badge}`}>{st.label}</span>
                </div>
                <div style={{ fontSize: 13, display: 'flex', flexDirection: 'column', gap: 4 }}>
                  <span><CalendarDays size={13} /> {scheduleText(t)}</span>
                  <span><MapPin size={13} /> {t.mainLocation}</span>
                </div>
                <div style={{ display: 'flex', gap: 14, fontSize: 12, borderTop: '1px solid var(--border)', paddingTop: 10, alignItems: 'center' }}>
                  <span><Users size={13} /> {t.teamsCount} grupo(s)</span>
                  <span><Trophy size={13} /> {t.matchesPlayed}/{t.matchesCount} jugados</span>
                  {t.nextMatchAt && <span style={{ marginLeft: 'auto' }}>Próximo: {fmtDate(t.nextMatchAt)} {t.nextMatchTimePending ? '· hora por definir' : timeOf(t.nextMatchAt)}</span>}
                  {!t.nextMatchAt && <ChevronRight size={16} style={{ marginLeft: 'auto', opacity: 0.6 }} />}
                </div>
              </button>
            );
          })}
        </div>
      )}

      {editing && (
        <TournamentFormModal tournament={editing === 'new' ? null : editing} onClose={() => setEditing(null)}
          onSaved={t => { setEditing(null); load(); if (editing === 'new') open(t.id); }} />
      )}
    </AppLayout>
  );
}

// ═══════════════════════════════ Detalle ═══════════════════════════════
function TournamentDetail({ id, onBack }) {
  const [t, setT] = useState(null);
  const [tab, setTab] = useState('matches');
  const [support, setSupport] = useState({ categories: [], headquarters: [], staff: [] });
  const [modal, setModal] = useState(null); // { kind, ...data }
  const [confirm, setConfirm] = useState(null);
  const { can } = useAuth();
  const canFinance = can('collections.view');

  const backRef = useRef(onBack);
  backRef.current = onBack;
  const load = useCallback(async () => {
    try { setT((await api.get(`/tournaments/${id}`)).data); }
    catch (e) { toast.error(errMsg(e, 'No se pudo cargar el torneo')); backRef.current(); }
  }, [id]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => {
    Promise.all([api.get('/academy-config/categories'), api.get('/academy-config/headquarters'), api.get('/academy-config/users')])
      .then(([c, h, u]) => setSupport({
        categories: c.data,
        headquarters: h.data.filter(x => x.isActive !== false),
        staff: u.data.filter(x => ['Staff', 'AcademyAdmin'].includes(x.systemRole) && x.status === 'Active')
          .map(x => ({ id: x.id, name: `${x.firstName} ${x.lastName}`.trim() }))
          .sort((a, b) => a.name.localeCompare(b.name)),
      }))
      .catch(() => toast.error('No se pudieron cargar categorías y personal'));
  }, []);

  const close = () => setModal(null);
  const saved = () => { close(); load(); };

  const doConfirm = async () => {
    const c = confirm; setConfirm(null);
    try {
      if (c.kind === 'tournament') { await api.delete(`/tournaments/${id}`); toast.success('Torneo eliminado'); onBack(); return; }
      if (c.kind === 'team') { await api.delete(`/tournaments/teams/${c.team.id}`); toast.success('Equipo retirado'); }
      if (c.kind === 'match') { await api.delete(`/calendar/events/${c.match.id}`); toast.success('Partido eliminado'); }
      load();
    } catch (e) { toast.error(errMsg(e, 'No se pudo eliminar')); }
  };

  if (!t) return <p>Cargando…</p>;
  const st = labelOf(STATUSES, t.status);
  const warnings = t.teams.reduce((n, x) => n + x.warnings.length, 0);

  return (
    <>
      <button className="btn btn-ghost btn-sm" onClick={onBack} style={{ marginBottom: 8 }}><ArrowLeft size={15} /> Torneos</button>
      <div className="page-header">
        <div>
          <h2 className="page-title"><Trophy size={26} /> {t.name} <span className={`badge ${st.badge}`} style={{ fontSize: 12 }}>{st.label}</span></h2>
          <p className="page-subtitle" style={{ display: 'flex', gap: 14, flexWrap: 'wrap' }}>
            <span>{t.organizer} · {labelOf(MODALITIES, t.modality).label}{t.level ? ` · ${labelOf(LEVELS, t.level).label}` : ''} · {labelOf(FORMATS, t.format).label}</span>
            {(t.inscriptionFee || t.refereeFee) && (
              <span>{t.inscriptionFee ? `Inscripción S/ ${Number(t.inscriptionFee).toFixed(2)}` : ''}{t.inscriptionFee && t.refereeFee ? ' · ' : ''}{t.refereeFee ? `Arbitraje S/ ${Number(t.refereeFee).toFixed(2)} por partido` : ''}</span>
            )}
            <span><CalendarDays size={13} /> {scheduleText(t)}</span>
            <span><MapPin size={13} /> {t.mainLocation}</span>
            {t.contactName && <span><Phone size={13} /> {t.contactName}{t.contactPhone ? ` · ${t.contactPhone}` : ''}</span>}
          </p>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn-secondary" onClick={() => setModal({ kind: 'tournament' })}><Pencil size={15} /> Editar</button>
          <button className="btn btn-danger" onClick={() => setConfirm({ kind: 'tournament' })}><Trash2 size={15} /></button>
        </div>
      </div>

      <div className="stats-grid" style={{ marginBottom: 16 }}>
        <Stat label="Por completar (hora / rival)" value={t.matches.filter(m => m.needsDetails).length} />
        <Stat label="Alumnos" value={t.teams.reduce((n, x) => n + x.players.length, 0)} />
        <Stat label="Partidos jugados" value={`${t.matchesPlayed}/${t.matchesCount}`} />
        <Stat label="Próximo partido" value={t.nextMatchAt ? `${fmtDate(t.nextMatchAt)} ${t.nextMatchTimePending ? '· hora por definir' : timeOf(t.nextMatchAt)}` : '—'} />
      </div>

      <div className="tabs" style={{ maxWidth: 680, overflowX: 'auto' }}>
        <button className={`tab-pill ${tab === 'matches' ? 'active' : ''}`} onClick={() => setTab('matches')}>Fechas ({t.matchesCount})</button>
        <button className={`tab-pill ${tab === 'teams' ? 'active' : ''}`} onClick={() => setTab('teams')}>
          Categorías y alumnos {warnings > 0 && <AlertTriangle size={12} style={{ marginLeft: 4 }} />}
        </button>
        {canFinance && <button className={`tab-pill ${tab === 'finance' ? 'active' : ''}`} onClick={() => setTab('finance')}>Cobros y gastos</button>}
        <button className={`tab-pill ${tab === 'rules' ? 'active' : ''}`} onClick={() => setTab('rules')}>Reglamento</button>
      </div>

      {tab === 'teams' && (
        <TeamsTab t={t} onNew={() => setModal({ kind: 'participation' })} onEdit={team => setModal({ kind: 'team', team })}
          onRoster={team => setModal({ kind: 'roster', team })} onGenerate={team => setModal({ kind: 'generate', team })}
          onMatch={team => setModal({ kind: 'match', team })} onDelete={team => setConfirm({ kind: 'team', team })} />
      )}
      {tab === 'matches' && (
        <MatchesTab t={t} canWaive={can('collections.adjust') && !!t.refereeFee}
          onNew={(team, defaults) => setModal({ kind: 'encounter', team, defaults })}
          onEdit={match => setModal({ kind: t.teams.find(x => x.id === match.teamId)?.autoRoster ? 'encounter' : 'match', match })}
          onWaive={match => setModal({ kind: 'waiver', match })}
          onCalls={match => setModal({ kind: 'calls', match })} onDelete={match => setConfirm({ kind: 'match', match })} />
      )}
      {tab === 'finance' && canFinance && <TorneoFinanzas t={t} can={can} />}
      {tab === 'rules' && (
        <div className="card" style={{ padding: 18, fontSize: 14, display: 'flex', flexDirection: 'column', gap: 10 }}>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <span className="badge badge-info">{t.yellowCardsForSuspension ? `${t.yellowCardsForSuspension} amarillas = 1 partido` : 'Amarillas sin control'}</span>
            <span className="badge badge-info">{t.redCardSuspensionMatches ? `Roja = ${t.redCardSuspensionMatches} partido(s)` : 'Rojas sin control'}</span>
            <span className="badge badge-info">{t.requiresAuthorization ? 'Pide autorización del apoderado' : 'Sin autorización'}</span>
            {t.requiredDocuments?.length > 0 && <span className="badge badge-info">Documentos: {t.requiredDocuments.join(', ')}</span>}
          </div>
          {t.requiresAuthorization && t.authorizationText && <div className="text-muted" style={{ fontSize: 13 }}>Texto de la autorización: “{t.authorizationText}”</div>}
          <div style={{ whiteSpace: 'pre-wrap' }}>
            {t.rules || <span className="text-muted">Sin reglamento registrado. Anota tiempos, cambios y tope de planilla en Editar.</span>}
          </div>
        </div>
      )}

      {modal?.kind === 'tournament' && <TournamentFormModal tournament={t} onClose={close} onSaved={saved} />}
      {modal?.kind === 'team' && <TeamFormModal tournamentId={t.id} team={modal.team} categories={support.categories} staff={support.staff} onClose={close} onSaved={saved} />}
      {modal?.kind === 'roster' && <RosterModal team={modal.team} tournament={t} onClose={close} onSaved={saved} />}
      {modal?.kind === 'generate' && <GenerateMatchesModal team={modal.team} tournament={t} headquarters={support.headquarters} onClose={close} onDone={load} />}
      {modal?.kind === 'match' && (
        <MatchFormModal match={modal.match} team={modal.team} teams={t.teams} headquarters={support.headquarters} staff={support.staff} onClose={close} onSaved={saved} />
      )}
      {modal?.kind === 'calls' && <MatchCallsModal match={modal.match} onClose={close} onChanged={load} />}
      {modal?.kind === 'encounter' && (
        <EncounterModal tournament={t} match={modal.match} team={modal.team} defaults={modal.defaults} categories={support.categories}
          headquarters={support.headquarters} staff={support.staff} onClose={close} onSaved={saved} />
      )}
      {modal?.kind === 'waiver' && <RefereeWaiverModal match={modal.match} onClose={close} onSaved={saved} />}
      {modal?.kind === 'participation' && (
        <ParticipationModal tournament={t} categories={support.categories} headquarters={support.headquarters} staff={support.staff} onClose={close} onSaved={() => { close(); load(); }} />
      )}

      <ConfirmDialog
        isOpen={!!confirm}
        title={confirm?.kind === 'tournament' ? 'Eliminar torneo' : confirm?.kind === 'team' ? 'Quitar del torneo' : 'Eliminar encuentro'}
        message={confirm?.kind === 'tournament' ? `¿Eliminar "${t.name}"?` : confirm?.kind === 'team' ? `¿Quitar a ${confirm.team.name} del torneo?` : '¿Eliminar este encuentro?'}
        detail={confirm?.kind === 'match' ? 'Se quita del calendario y de la agenda de las familias.'
          : 'Se quitan los partidos por jugar que aún no tienen asistencia. Lo ya jugado queda en el historial.'}
        confirmLabel="Sí, eliminar"
        onConfirm={doConfirm}
        onCancel={() => setConfirm(null)}
      />
    </>
  );
}

function Stat({ label, value }) {
  return (
    <div className="stat-card">
      <div>
        <div className="stat-value" style={{ fontSize: 20 }}>{value}</div>
        <div className="stat-label">{label}</div>
      </div>
    </div>
  );
}

function TeamsTab({ t, onNew, onEdit, onRoster, onGenerate, onMatch, onDelete }) {
  return (
    <>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
        <button className="btn btn-primary" onClick={onNew}><Plus size={15} /> Agregar categorías</button>
      </div>
      {t.teams.length === 0 ? (
        <div className="card" style={{ padding: 24, textAlign: 'center' }}>
          <Users size={32} style={{ opacity: 0.4 }} />
          <p className="text-muted">Agrega las categorías que juegan el torneo (o hazlo desde el Calendario con un evento de tipo Torneo). Se crean sus días con hora y rival por definir.</p>
        </div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(min(340px, 100%), 1fr))', gap: 16 }}>
          {t.teams.map(team => {
            const upcoming = t.matches.filter(m => m.teamId === team.id && m.matchStatus === 1 && dateOf(m.startTime) >= todayLima());
            const years = team.birthYearFrom || team.birthYearTo ? `${team.birthYearFrom || '…'}–${team.birthYearTo || '…'}` : null;
            return (
              <div key={team.id} className="card" style={{ padding: 18, display: 'flex', flexDirection: 'column', gap: 10 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                  <div>
                    <strong style={{ fontSize: 16 }}>{team.name}</strong>
                    <div className="text-muted" style={{ fontSize: 12 }}>{team.categoryNames.join(', ')}{years ? ` · nacidos ${years}` : ''}</div>
                  </div>
                  <div style={{ display: 'flex', gap: 2 }}>
                    <button className="btn-icon" onClick={() => onEdit(team)} aria-label="Editar equipo"><Pencil size={15} /></button>
                    <button className="btn-icon text-danger" onClick={() => onDelete(team)} aria-label="Retirar equipo"><Trash2 size={15} /></button>
                  </div>
                </div>
                <div style={{ fontSize: 13, display: 'flex', flexDirection: 'column', gap: 3 }}>
                  <span><UserCheck size={13} /> Responsable: <strong>{team.headCoachName || 'Sin asignar'}</strong></span>
                  {team.assistantName && <span><Users size={13} /> Asistente: {team.assistantName}</span>}
                  <span><Shirt size={13} /> {team.autoRoster ? 'Alumnos' : 'Plantel'}: {team.players.length}{team.maxPlayers ? ` / ${team.maxPlayers}` : ''}</span>
                  {team.record?.played > 0 && (
                    <span><Trophy size={13} /> {team.record.played} PJ · {team.record.won}G {team.record.drawn}E {team.record.lost}P · {team.record.goalsFor}-{team.record.goalsAgainst} · <strong>{team.record.points} pts</strong></span>
                  )}
                  {team.matchFee > 0 && <span className="text-muted" style={{ fontSize: 12 }}>Honorario por partido: S/ {Number(team.matchFee).toFixed(2)}</span>}
                  <span><CalendarDays size={13} /> {upcoming.length} partido(s) por jugar
                    {upcoming[0] && ` · próximo ${fmtDate(upcoming[0].startTime)} ${timeOf(upcoming[0].startTime)}`}</span>
                </div>
                {team.warnings.length > 0 && (
                  <div className="alert alert-warning" style={{ fontSize: 12, padding: '8px 10px', margin: 0, display: 'block' }}>
                    {team.warnings.map(w => <div key={w}><AlertTriangle size={12} /> {w}</div>)}
                  </div>
                )}
                {team.players.length > 0 && (
                  <div style={{ display: 'flex', flexWrap: 'wrap', gap: 4 }}>
                    {team.players.slice(0, 14).map(p => (
                      <span key={p.studentId} className={`badge ${p.outsideBirthYears || !p.isActive ? 'badge-warning' : 'badge-muted'}`} title={p.categoryName}>
                        {p.shirtNumber != null ? `${p.shirtNumber} · ` : ''}{p.fullName.split(' ')[0]}
                      </span>
                    ))}
                    {team.players.length > 14 && <span className="badge badge-muted">+{team.players.length - 14}</span>}
                  </div>
                )}
                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 'auto' }}>
                  <button className="btn btn-secondary btn-sm" onClick={() => onRoster(team)}><Shirt size={14} /> {team.autoRoster ? 'Alumnos' : 'Plantel'}</button>
                  {!team.autoRoster && <button className="btn btn-secondary btn-sm" onClick={() => onGenerate(team)}><CalendarPlus size={14} /> Generar fechas</button>}
                  <button className="btn btn-secondary btn-sm" onClick={() => onMatch(team)}><Plus size={14} /> Fecha extra</button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </>
  );
}

/** Día del torneo aún sin encuentros (creado automáticamente con hora por definir). */
const isEmptyDay = m => m.timePending && m.calledCount === 0 && !m.referenceCategoryId;

function MatchesTab({ t, canWaive, onNew, onEdit, onWaive, onCalls, onDelete }) {
  const [teamFilter, setTeamFilter] = useState('');
  const [when, setWhen] = useState('upcoming');
  const today = todayLima();
  const list = useMemo(() => {
    let l = t.matches.filter(m => !teamFilter || m.teamId === teamFilter);
    if (when === 'upcoming') l = l.filter(m => dateOf(m.startTime) >= today);
    if (when === 'past') l = l.filter(m => dateOf(m.startTime) < today).reverse();
    if (when === 'pending') l = l.filter(m => m.needsDetails);
    return l;
  }, [t.matches, teamFilter, when, today]);
  // Una fecha = un día de unas categorías del torneo, con todos sus encuentros.
  const days = useMemo(() => {
    const map = new Map();
    for (const m of list) {
      const key = `${dateOf(m.startTime)}|${m.teamId || ''}`;
      if (!map.has(key)) map.set(key, { key, date: dateOf(m.startTime), teamId: m.teamId, teamName: m.teamName, items: [] });
      map.get(key).items.push(m);
    }
    for (const d of map.values()) {
      d.items.sort((a, b) => (a.startTime < b.startTime ? -1 : 1));
      d.round = d.items.find(m => m.round)?.round;
      d.encounters = d.items.filter(m => !isEmptyDay(m));
    }
    return [...map.values()];
  }, [list]);
  const teamOf = id => t.teams.find(x => x.id === id);
  const newIn = d => {
    const ref = d.items[0];
    onNew(teamOf(d.teamId), { date: d.date, round: d.round, location: ref.location, headquarterId: ref.headquarterId, coachFee: ref.coachFee });
  };

  return (
    <>
      <div style={{ display: 'flex', gap: 8, marginBottom: 12, flexWrap: 'wrap', alignItems: 'center' }}>
        <select className="form-control" style={{ maxWidth: 220 }} value={teamFilter} onChange={e => setTeamFilter(e.target.value)}>
          <option value="">Todas las categorías</option>
          {t.teams.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
        </select>
        <select className="form-control" style={{ maxWidth: 180 }} value={when} onChange={e => setWhen(e.target.value)}>
          <option value="upcoming">Por jugar</option>
          <option value="pending">Por completar</option>
          <option value="past">Anteriores</option>
          <option value="all">Todos</option>
        </select>
        <button className="btn btn-primary" style={{ marginLeft: 'auto' }} onClick={() => onNew(teamFilter ? teamOf(teamFilter) : null, {})} disabled={t.teams.length === 0}
          title={t.teams.length === 0 ? 'Primero agrega categorías' : 'Encuentro en cualquier día (fecha extra)'}><Plus size={15} /> Encuentro</button>
      </div>
      {list.length === 0 ? (
        <div className="card" style={{ padding: 24 }}>
          <p className="text-muted" style={{ margin: 0 }}>
            {t.teams.length === 0 ? 'Aún no hay fechas: agrega categorías (aquí en "Categorías y alumnos" o desde el Calendario con un evento de tipo Torneo).' : 'No hay fechas en esta vista.'}
          </p>
        </div>
      ) : (
        <div className="card table-wrap" style={{ padding: 0 }}>
          <table className="table" style={{ width: '100%', fontSize: 13 }}>
            <thead>
              <tr>
                <th>Hora</th><th>Categoría</th><th>Rival · fase</th><th>Lugar</th><th>Responsable</th><th>Estado</th><th>Convocados</th><th style={{ width: 130 }} />
              </tr>
            </thead>
            {days.map(d => (
            <tbody key={d.key}>
              <tr style={{ background: 'var(--bg-subtle, rgba(14,165,233,0.06))' }}>
                <td colSpan={8}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap' }}>
                    <strong>{fmtDate(d.date)}</strong>
                    {d.round && <span>{d.round}</span>}
                    <span className="text-muted" style={{ fontSize: 12 }}>{d.teamName}</span>
                    <span className="badge badge-muted">{d.encounters.length} encuentro(s)</span>
                    {teamOf(d.teamId)?.autoRoster && (
                      <button className="btn btn-secondary btn-sm" style={{ marginLeft: 'auto' }} onClick={() => newIn(d)}><Plus size={13} /> Encuentro</button>
                    )}
                  </div>
                </td>
              </tr>
              {d.encounters.length === 0 && (
                <tr>
                  <td colSpan={7} className="text-muted" style={{ fontSize: 12 }}>Sin encuentros programados: agrégalos con “+ Encuentro”.</td>
                  <td>
                    <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                      <button className="btn-icon text-danger" onClick={() => onDelete(d.items[0])} aria-label="Eliminar fecha" title="Eliminar fecha"><Trash2 size={15} /></button>
                    </div>
                  </td>
                </tr>
              )}
              {d.encounters.map(m => {
                const ms = labelOf(MATCH_STATUSES, m.matchStatus);
                const score = m.goalsFor != null && m.goalsAgainst != null ? `${m.goalsFor} – ${m.goalsAgainst}` : null;
                return (
                  <tr key={m.id}>
                    <td style={{ whiteSpace: 'nowrap' }}>
                      {m.timePending ? <span style={{ color: '#d97706' }}>hora por definir</span> : <strong>{timeOf(m.startTime)}</strong>}
                      {m.callTime && <div className="text-muted" style={{ fontSize: 11 }}><Clock size={11} /> citación {timeOf(m.callTime)}</div>}
                    </td>
                    <td>
                      {m.referenceCategoryName || m.teamName || <span className="text-muted">Sin equipo</span>}
                      {m.superiorCount > 0 && <div><span className="badge badge-warning" style={{ fontSize: 10 }} title="Convocados de una categoría mayor"><AlertTriangle size={10} /> {m.superiorCount} de cat. superior</span></div>}
                      {m.refereeWaived && <div><span className="badge badge-muted" style={{ fontSize: 10 }} title={m.refereeWaivedNote || ''}>Arbitraje exonerado</span></div>}
                    </td>
                    <td>{m.opponentTeam || <span style={{ color: '#d97706' }}>Por definir</span>}{m.round && <div className="text-muted" style={{ fontSize: 11 }}>{m.round}</div>}</td>
                    <td>{m.location || m.headquarterName || <span className="text-muted">—</span>}</td>
                    <td>
                      {m.teacherName || <span className="text-muted">Sin asignar</span>}
                      {m.isReplacement && <div><span className="badge badge-warning" style={{ fontSize: 10 }}>Reemplazo</span></div>}
                      {m.coachFee ? <div className="text-muted" style={{ fontSize: 11 }}>Honorario S/ {Number(m.coachFee).toFixed(2)}</div> : null}
                    </td>
                    <td>
                      {m.needsDetails ? <span className="badge badge-warning">Completar</span> : <span className={`badge ${ms.badge}`}>{ms.label}</span>}
                      {score && <div style={{ fontWeight: 700, marginTop: 2 }}>{score}</div>}
                    </td>
                    <td style={{ whiteSpace: 'nowrap' }}>
                      {m.calledCount}
                      <span className="text-muted" style={{ fontSize: 11 }}> · ✓{m.confirmedCount} ✗{m.declinedCount}</span>
                    </td>
                    <td>
                      <div style={{ display: 'flex', gap: 2, justifyContent: 'flex-end' }}>
                        <button className="btn-icon" onClick={() => onCalls(m)} aria-label="Convocatoria" title="Convocatoria"><Send size={15} /></button>
                        <button className="btn-icon" onClick={() => onEdit(m)} aria-label="Editar encuentro" title="Editar, convocados, resultado o reemplazo"><Pencil size={15} /></button>
                        {canWaive && <button className="btn-icon" onClick={() => onWaive(m)} aria-label="Arbitraje" title={m.refereeWaived ? 'Arbitraje exonerado' : 'Exonerar arbitraje del encuentro'}><BadgePercent size={15} /></button>}
                        <button className="btn-icon text-danger" onClick={() => onDelete(m)} aria-label="Eliminar encuentro"><Trash2 size={15} /></button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
            ))}
          </table>
        </div>
      )}
    </>
  );
}
