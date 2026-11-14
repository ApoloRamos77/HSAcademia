import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import ConfirmDialog from '../../components/ConfirmDialog';
import toast from 'react-hot-toast';
import {
  Trophy, Plus, Pencil, Trash2, ArrowLeft, Users, CalendarPlus, CalendarDays, Clock, MapPin,
  Shirt, AlertTriangle, Phone, ChevronRight, Send, UserCheck,
} from 'lucide-react';
import {
  TournamentFormModal, TeamFormModal, RosterModal, GenerateMatchesModal, MatchFormModal, MatchCallsModal,
} from './torneos/TorneoModals';
import {
  FORMATS, STATUSES, MATCH_STATUSES, labelOf, scheduleText, fmtDate, timeOf, dateOf, errMsg, todayLima,
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

  if (selectedId) return <AppLayout><TournamentDetail id={selectedId} onBack={() => { open(null); load(); }} /></AppLayout>;

  return (
    <AppLayout>
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
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 16 }}>
          {shown.map(t => {
            const st = labelOf(STATUSES, t.status);
            return (
              <button key={t.id} className="card" onClick={() => open(t.id)}
                style={{ textAlign: 'left', cursor: 'pointer', padding: 18, color: 'inherit', fontFamily: 'inherit', display: 'flex', flexDirection: 'column', gap: 10 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'flex-start' }}>
                  <div>
                    <strong style={{ fontSize: 16 }}>{t.name}</strong>
                    <div className="text-muted" style={{ fontSize: 12 }}>{t.organizer} · {labelOf(FORMATS, t.format).label}</div>
                  </div>
                  <span className={`badge ${st.badge}`}>{st.label}</span>
                </div>
                <div style={{ fontSize: 13, display: 'flex', flexDirection: 'column', gap: 4 }}>
                  <span><CalendarDays size={13} /> {scheduleText(t)}</span>
                  <span><MapPin size={13} /> {t.mainLocation}</span>
                </div>
                <div style={{ display: 'flex', gap: 14, fontSize: 12, borderTop: '1px solid var(--border)', paddingTop: 10, alignItems: 'center' }}>
                  <span><Users size={13} /> {t.teamsCount} equipo(s)</span>
                  <span><Trophy size={13} /> {t.matchesPlayed}/{t.matchesCount} jugados</span>
                  {t.nextMatchAt && <span style={{ marginLeft: 'auto' }}>Próximo: {fmtDate(t.nextMatchAt)} {timeOf(t.nextMatchAt)}</span>}
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
  const [tab, setTab] = useState('teams');
  const [support, setSupport] = useState({ categories: [], headquarters: [], staff: [] });
  const [modal, setModal] = useState(null); // { kind, ...data }
  const [confirm, setConfirm] = useState(null);

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
            <span>{t.organizer} · {labelOf(FORMATS, t.format).label}</span>
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
        <Stat label="Equipos" value={t.teamsCount} />
        <Stat label="Jugadores" value={t.teams.reduce((n, x) => n + x.players.length, 0)} />
        <Stat label="Partidos jugados" value={`${t.matchesPlayed}/${t.matchesCount}`} />
        <Stat label="Próximo partido" value={t.nextMatchAt ? `${fmtDate(t.nextMatchAt)} ${timeOf(t.nextMatchAt)}` : '—'} />
      </div>

      <div className="tabs" style={{ maxWidth: 520 }}>
        <button className={`tab-pill ${tab === 'teams' ? 'active' : ''}`} onClick={() => setTab('teams')}>
          Equipos y planteles {warnings > 0 && <AlertTriangle size={12} style={{ marginLeft: 4 }} />}
        </button>
        <button className={`tab-pill ${tab === 'matches' ? 'active' : ''}`} onClick={() => setTab('matches')}>Partidos ({t.matchesCount})</button>
        <button className={`tab-pill ${tab === 'rules' ? 'active' : ''}`} onClick={() => setTab('rules')}>Reglamento</button>
      </div>

      {tab === 'teams' && (
        <TeamsTab t={t} onNew={() => setModal({ kind: 'team' })} onEdit={team => setModal({ kind: 'team', team })}
          onRoster={team => setModal({ kind: 'roster', team })} onGenerate={team => setModal({ kind: 'generate', team })}
          onMatch={team => setModal({ kind: 'match', team })} onDelete={team => setConfirm({ kind: 'team', team })} />
      )}
      {tab === 'matches' && (
        <MatchesTab t={t} onNew={() => setModal({ kind: 'match' })} onEdit={match => setModal({ kind: 'match', match })}
          onCalls={match => setModal({ kind: 'calls', match })} onDelete={match => setConfirm({ kind: 'match', match })} />
      )}
      {tab === 'rules' && (
        <div className="card" style={{ padding: 18, whiteSpace: 'pre-wrap', fontSize: 14 }}>
          {t.rules || <span className="text-muted">Sin reglamento registrado. Anota documentos exigidos, tiempos, cambios y tope de planilla en Editar.</span>}
        </div>
      )}

      {modal?.kind === 'tournament' && <TournamentFormModal tournament={t} onClose={close} onSaved={saved} />}
      {modal?.kind === 'team' && <TeamFormModal tournamentId={t.id} team={modal.team} categories={support.categories} staff={support.staff} onClose={close} onSaved={saved} />}
      {modal?.kind === 'roster' && <RosterModal team={modal.team} onClose={close} onSaved={saved} />}
      {modal?.kind === 'generate' && <GenerateMatchesModal team={modal.team} tournament={t} headquarters={support.headquarters} onClose={close} onDone={load} />}
      {modal?.kind === 'match' && (
        <MatchFormModal match={modal.match} team={modal.team} teams={t.teams} headquarters={support.headquarters} staff={support.staff} onClose={close} onSaved={saved} />
      )}
      {modal?.kind === 'calls' && <MatchCallsModal match={modal.match} onClose={close} onChanged={load} />}

      <ConfirmDialog
        isOpen={!!confirm}
        title={confirm?.kind === 'tournament' ? 'Eliminar torneo' : confirm?.kind === 'team' ? 'Retirar equipo' : 'Eliminar partido'}
        message={confirm?.kind === 'tournament' ? `¿Eliminar "${t.name}"?` : confirm?.kind === 'team' ? `¿Retirar a ${confirm.team.name} del torneo?` : '¿Eliminar este partido?'}
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
        <button className="btn btn-primary" onClick={onNew}><Plus size={15} /> Inscribir equipo</button>
      </div>
      {t.teams.length === 0 ? (
        <div className="card" style={{ padding: 24, textAlign: 'center' }}>
          <Users size={32} style={{ opacity: 0.4 }} />
          <p className="text-muted">Inscribe un equipo por cada categoría que participa. Una categoría puede llevar más de un equipo (Sub-12 A, Sub-12 B).</p>
        </div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(340px, 1fr))', gap: 16 }}>
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
                  <span><Shirt size={13} /> Plantel: {team.players.length}{team.maxPlayers ? ` / ${team.maxPlayers}` : ''} jugadores</span>
                  <span><CalendarDays size={13} /> {upcoming.length} partido(s) por jugar
                    {upcoming[0] && ` · próximo ${fmtDate(upcoming[0].startTime)} ${timeOf(upcoming[0].startTime)}`}</span>
                </div>
                {team.warnings.length > 0 && (
                  <div className="alert alert-warning" style={{ fontSize: 12, padding: '8px 10px', margin: 0 }}>
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
                  <button className="btn btn-secondary btn-sm" onClick={() => onRoster(team)}><Shirt size={14} /> Plantel</button>
                  <button className="btn btn-secondary btn-sm" onClick={() => onGenerate(team)}><CalendarPlus size={14} /> Generar fechas</button>
                  <button className="btn btn-secondary btn-sm" onClick={() => onMatch(team)}><Plus size={14} /> Partido</button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </>
  );
}

function MatchesTab({ t, onNew, onEdit, onCalls, onDelete }) {
  const [teamFilter, setTeamFilter] = useState('');
  const [when, setWhen] = useState('upcoming');
  const today = todayLima();
  const list = useMemo(() => {
    let l = t.matches.filter(m => !teamFilter || m.teamId === teamFilter);
    if (when === 'upcoming') l = l.filter(m => dateOf(m.startTime) >= today);
    if (when === 'past') l = l.filter(m => dateOf(m.startTime) < today).reverse();
    return l;
  }, [t.matches, teamFilter, when, today]);

  return (
    <>
      <div style={{ display: 'flex', gap: 8, marginBottom: 12, flexWrap: 'wrap', alignItems: 'center' }}>
        <select className="form-control" style={{ maxWidth: 220 }} value={teamFilter} onChange={e => setTeamFilter(e.target.value)}>
          <option value="">Todos los equipos</option>
          {t.teams.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
        </select>
        <select className="form-control" style={{ maxWidth: 180 }} value={when} onChange={e => setWhen(e.target.value)}>
          <option value="upcoming">Por jugar</option>
          <option value="past">Anteriores</option>
          <option value="all">Todos</option>
        </select>
        <button className="btn btn-primary" style={{ marginLeft: 'auto' }} onClick={onNew} disabled={t.teams.length === 0}
          title={t.teams.length === 0 ? 'Primero inscribe un equipo' : undefined}><Plus size={15} /> Nuevo partido</button>
      </div>
      {list.length === 0 ? (
        <div className="card" style={{ padding: 24 }}>
          <p className="text-muted" style={{ margin: 0 }}>
            {t.teams.length === 0 ? 'Inscribe un equipo y usa "Generar fechas" para crear sus partidos.' : 'No hay partidos en esta vista.'}
          </p>
        </div>
      ) : (
        <div className="card table-wrap" style={{ padding: 0 }}>
          <table className="table" style={{ width: '100%', fontSize: 13 }}>
            <thead>
              <tr>
                <th>Fecha</th><th>Equipo</th><th>Rival · fase</th><th>Lugar</th><th>Responsable</th><th>Estado</th><th>Convocados</th><th style={{ width: 110 }} />
              </tr>
            </thead>
            <tbody>
              {list.map(m => {
                const ms = labelOf(MATCH_STATUSES, m.matchStatus);
                const score = m.goalsFor != null && m.goalsAgainst != null ? `${m.goalsFor} – ${m.goalsAgainst}` : null;
                return (
                  <tr key={m.id}>
                    <td style={{ whiteSpace: 'nowrap' }}>
                      <strong>{fmtDate(m.startTime)}</strong> {timeOf(m.startTime)}
                      {m.callTime && <div className="text-muted" style={{ fontSize: 11 }}><Clock size={11} /> citación {timeOf(m.callTime)}</div>}
                    </td>
                    <td>{m.teamName || <span className="text-muted">Sin equipo</span>}</td>
                    <td>{m.opponentTeam || <span className="text-muted">Por definir</span>}{m.round && <div className="text-muted" style={{ fontSize: 11 }}>{m.round}</div>}</td>
                    <td>{m.location || m.headquarterName || <span className="text-muted">—</span>}</td>
                    <td>
                      {m.teacherName || <span className="text-muted">Sin asignar</span>}
                      {m.isReplacement && <div><span className="badge badge-warning" style={{ fontSize: 10 }}>Reemplazo</span></div>}
                    </td>
                    <td><span className={`badge ${ms.badge}`}>{ms.label}</span>{score && <div style={{ fontWeight: 700, marginTop: 2 }}>{score}</div>}</td>
                    <td style={{ whiteSpace: 'nowrap' }}>
                      {m.calledCount}
                      <span className="text-muted" style={{ fontSize: 11 }}> · ✓{m.confirmedCount} ✗{m.declinedCount}</span>
                    </td>
                    <td>
                      <div style={{ display: 'flex', gap: 2, justifyContent: 'flex-end' }}>
                        <button className="btn-icon" onClick={() => onCalls(m)} aria-label="Convocatoria" title="Convocatoria"><Send size={15} /></button>
                        <button className="btn-icon" onClick={() => onEdit(m)} aria-label="Editar partido" title="Editar, resultado o reemplazo"><Pencil size={15} /></button>
                        <button className="btn-icon text-danger" onClick={() => onDelete(m)} aria-label="Eliminar partido"><Trash2 size={15} /></button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
