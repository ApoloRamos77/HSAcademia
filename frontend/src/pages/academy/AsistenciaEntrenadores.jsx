import { useCallback, useEffect, useState } from 'react';
import { UserCheck, UserX, Clock, FileMinus, CalendarCheck } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { useAuth } from '../../context/AuthContext';
import { todayISO } from '../../utils/dates';

const STATUS = [
  { value: 1, label: 'Presente', icon: UserCheck, color: '#10b981' },
  { value: 3, label: 'Tarde', icon: Clock, color: '#f59e0b' },
  { value: 2, label: 'Falta', icon: UserX, color: '#ef4444' },
  { value: 4, label: 'Justificada', icon: FileMinus, color: '#6366f1' },
];
const MONTHS = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];
const isReplaceable = (status) => status === 2 || status === 4;

/** Asistencia de entrenadores por sesión y resumen mensual de sesiones dictadas. */
export default function AsistenciaEntrenadores() {
  const { user } = useAuth();
  const now = new Date();
  const [tab, setTab] = useState('mark');
  const [date, setDate] = useState(todayISO());
  const [sessions, setSessions] = useState([]);
  const [trainers, setTrainers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [savingId, setSavingId] = useState(null);
  const [year, setYear] = useState(now.getFullYear());
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [report, setReport] = useState([]);

  const isAdmin = user?.role === 'AcademyAdmin';
  const selectable = trainers.filter(t => isAdmin || t.id !== user?.userId);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [s, t] = await Promise.all([
        api.get('/staff-attendance/sessions', { params: { date } }),
        api.get('/staff-attendance/trainers'),
      ]);
      setSessions(s.data);
      setTrainers(t.data);
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudieron cargar las sesiones.');
    } finally { setLoading(false); }
  }, [date]);

  useEffect(() => { if (tab === 'mark') load(); }, [tab, load]);

  useEffect(() => {
    if (tab !== 'report') return;
    api.get('/staff-attendance/report', { params: { year, month } })
      .then(r => setReport(r.data))
      .catch(err => toast.error(err.response?.data?.message || 'No se pudo cargar el reporte.'));
  }, [tab, year, month]);

  const save = async (session, patch) => {
    const next = { status: session.status, staffId: session.staffId, substituteStaffId: session.substituteStaffId, ...patch };
    if (!next.status) return;
    setSavingId(session.eventId);
    try {
      await api.post('/staff-attendance/sessions', {
        records: [{
          eventId: session.eventId,
          status: next.status,
          staffId: next.staffId || null,
          substituteStaffId: isReplaceable(next.status) ? next.substituteStaffId || null : null,
          notes: session.notes || null,
        }],
      });
      await load();
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo guardar.');
    } finally { setSavingId(null); }
  };

  return (
    <AppLayout>
      <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
        <div>
          <h2 className="page-title" style={{ display: 'flex', alignItems: 'center', gap: 10 }}><CalendarCheck size={24} /> Asistencia de Entrenadores</h2>
          <p style={{ color: 'var(--text-muted)', fontSize: 14, marginTop: 4 }}>Registra quién dictó cada sesión. Cada marca se guarda al tocarla.</p>
        </div>

        <div className="tabs" style={{ maxWidth: 400 }}>
          <button className={`tab-pill ${tab === 'mark' ? 'active' : ''}`} onClick={() => setTab('mark')}>Marcar</button>
          <button className={`tab-pill ${tab === 'report' ? 'active' : ''}`} onClick={() => setTab('report')}>Resumen mensual</button>
        </div>

        {tab === 'mark' ? (
          <>
            <div className="card">
              <div className="form-group" style={{ marginBottom: 0, maxWidth: 240 }}>
                <label>Fecha</label>
                <input type="date" className="form-control" value={date} onChange={e => setDate(e.target.value)} />
              </div>
            </div>

            {loading ? (
              <div style={{ textAlign: 'center', padding: 40 }}><div className="spinner" style={{ margin: '0 auto' }} /></div>
            ) : sessions.length === 0 ? (
              <div className="empty-state card"><p style={{ color: 'var(--text-muted)' }}>No hay sesiones en tus categorías para esta fecha.</p></div>
            ) : (
              <div className="card">
                <div className="table-wrap">
                  <table>
                    <thead><tr><th>Sesión</th><th>Entrenador</th><th style={{ textAlign: 'center' }}>Asistencia</th><th>Reemplazo</th></tr></thead>
                    <tbody>
                      {sessions.map(s => {
                        const disabled = !!s.blockedReason || savingId === s.eventId;
                        return (
                          <tr key={s.eventId}>
                            <td>
                              <div style={{ fontWeight: 600 }}>{s.startTime} · {s.title}</div>
                              <div style={{ fontSize: 12, color: 'var(--text-muted)' }}>{s.categoryName} · {s.headquarterName}</div>
                              {s.blockedReason && <div style={{ fontSize: 12, color: '#f59e0b' }}>{s.blockedReason}</div>}
                            </td>
                            <td>
                              {s.staffName || (
                                <select className="form-control" style={{ fontSize: 13 }} disabled={disabled} value="" aria-label="Quién dictó la sesión"
                                  onChange={e => e.target.value && save(s, { staffId: e.target.value, status: s.status || 1 })}>
                                  <option value="">¿Quién dictó?</option>
                                  {selectable.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
                                </select>
                              )}
                              {s.markedByName && <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>Marcó {s.markedByName}</div>}
                            </td>
                            <td>
                              <div style={{ display: 'flex', gap: 6, justifyContent: 'center' }}>
                                {STATUS.map(b => {
                                  const Icon = b.icon;
                                  const active = s.status === b.value;
                                  return (
                                    <button key={b.value} title={b.label} aria-label={b.label} disabled={disabled || !s.staffId}
                                      onClick={() => save(s, { status: b.value })}
                                      style={{ width: 36, height: 36, borderRadius: 8, border: '1px solid', cursor: disabled || !s.staffId ? 'not-allowed' : 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center',
                                        background: active ? b.color : 'var(--bg-surface)', borderColor: active ? b.color : 'var(--border)', color: active ? '#fff' : b.color, opacity: disabled ? 0.5 : 1 }}>
                                      <Icon size={14} />
                                    </button>
                                  );
                                })}
                              </div>
                            </td>
                            <td>
                              {isReplaceable(s.status) ? (
                                <select className="form-control" style={{ fontSize: 13 }} disabled={disabled} value={s.substituteStaffId || ''} aria-label="Reemplazo"
                                  onChange={e => save(s, { substituteStaffId: e.target.value })}>
                                  <option value="">Sin reemplazo</option>
                                  {selectable.filter(t => t.id !== s.staffId).map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
                                </select>
                              ) : <span style={{ color: 'var(--text-muted)' }}>—</span>}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </>
        ) : (
          <>
            <div className="card">
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(130px, 200px))', gap: 12 }}>
                <div className="form-group" style={{ marginBottom: 0 }}>
                  <label>Mes</label>
                  <select className="form-control" value={month} onChange={e => setMonth(parseInt(e.target.value, 10))}>
                    {MONTHS.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}
                  </select>
                </div>
                <div className="form-group" style={{ marginBottom: 0 }}>
                  <label>Año</label>
                  <input type="number" className="form-control" value={year} onChange={e => setYear(parseInt(e.target.value, 10) || now.getFullYear())} />
                </div>
              </div>
            </div>
            <div className="card">
              {report.length === 0 ? (
                <p style={{ color: 'var(--text-muted)' }}>Sin sesiones en este mes.</p>
              ) : (
                <div className="table-wrap">
                  <table>
                    <thead><tr><th>Entrenador</th><th>Programadas</th><th>Dictadas</th><th>Tardanzas</th><th>Faltas</th><th>Justificadas</th><th>Reemplazos hechos</th><th>Sin marcar</th></tr></thead>
                    <tbody>
                      {report.map(r => (
                        <tr key={r.staffId}>
                          <td style={{ fontWeight: 600 }}>{r.name}</td>
                          <td>{r.programmed}</td><td><strong>{r.dictated}</strong></td><td>{r.late}</td><td>{r.absent}</td><td>{r.excused}</td><td>{r.substitutions}</td><td>{r.unmarked}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              <p style={{ fontSize: 12, color: 'var(--text-muted)', marginTop: 8 }}>Dictadas incluye los reemplazos. Solo cuenta sesiones ya iniciadas.</p>
            </div>
          </>
        )}
      </div>
    </AppLayout>
  );
}
