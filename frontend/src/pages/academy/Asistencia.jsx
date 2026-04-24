import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import {
  CalendarCheck, Users, Save, UserCheck, UserX,
  Clock, FileMinus, CalendarDays, AlertTriangle, BarChart2
} from 'lucide-react';
import toast from 'react-hot-toast';
import { useNavigate } from 'react-router-dom';

const STATUS_BUTTONS = [
  { value: 1, label: 'Presente',    icon: UserCheck,  color: '#10b981' },
  { value: 2, label: 'Ausente',     icon: UserX,      color: '#ef4444' },
  { value: 3, label: 'Tarde',       icon: Clock,      color: '#f59e0b' },
  { value: 4, label: 'Justificado', icon: FileMinus,  color: '#6366f1' },
];

export default function Asistencia() {
  const navigate = useNavigate();
  const [mode, setMode] = useState('date'); // 'date' | 'event'

  // Date-mode
  const [categorias,       setCategorias]       = useState([]);
  const [selectedCategory, setSelectedCategory] = useState('');
  const [date,             setDate]             = useState(new Date().toISOString().split('T')[0]);

  // Event-mode
  const [events,         setEvents]         = useState([]);
  const [selectedEvent,  setSelectedEvent]  = useState('');
  const [windowBlocked,  setWindowBlocked]  = useState(null); // null | string message

  // Shared
  const [students,        setStudents]        = useState([]);
  const [loading,         setLoading]         = useState(true);
  const [loadingStudents, setLoadingStudents] = useState(false);
  const [saving,          setSaving]          = useState(false);

  // Load categories and upcoming events on mount
  useEffect(() => {
    Promise.all([
      api.get('/academy-config/categories'),
      loadUpcomingEvents()
    ]).then(([catRes]) => {
      setCategorias(catRes.data);
    }).catch(() => {
      toast.error('Error al cargar datos');
    }).finally(() => setLoading(false));
  }, []);

  const loadUpcomingEvents = async () => {
    try {
      const today = new Date();
      const params = new URLSearchParams({
        year: today.getFullYear(),
        month: today.getMonth() + 1,
      });
      // Only Training and FriendlyMatch events (have attendance)
      const { data } = await api.get(`/calendar/events?${params}`);
      const attendableTypes = [1, 2, 3]; // Training, FriendlyMatch, TournamentMatch
      const filtered = data.filter(e => attendableTypes.includes(e.type) && !e.isVirtual);
      setEvents(filtered);
    } catch { /* silent */ }
  };

  // Load by date
  useEffect(() => {
    if (mode !== 'date') return;
    fetchByDate();
  }, [selectedCategory, date, mode]);

  const fetchByDate = async () => {
    if (!selectedCategory || !date) { setStudents([]); return; }
    setLoadingStudents(true);
    setWindowBlocked(null);
    try {
      const { data } = await api.get(`/attendance/category/${selectedCategory}?date=${date}`);
      setStudents(data);
    } catch {
      toast.error('Error al cargar asistencia');
    } finally {
      setLoadingStudents(false);
    }
  };

  // Load by event
  useEffect(() => {
    if (mode !== 'event') return;
    fetchByEvent();
  }, [selectedEvent, mode]);

  const fetchByEvent = async () => {
    if (!selectedEvent) { setStudents([]); setWindowBlocked(null); return; }
    setLoadingStudents(true);
    setWindowBlocked(null);
    try {
      const { data } = await api.get(`/attendance/event/${selectedEvent}`);
      setStudents(data);
    } catch (err) {
      if (err.response?.status === 409) {
        setWindowBlocked(err.response.data?.message ?? 'Lista aún no disponible.');
        setStudents([]);
      } else {
        toast.error('Error al cargar asistencia del evento');
      }
    } finally {
      setLoadingStudents(false);
    }
  };

  const handleStatusChange = (studentId, status) =>
    setStudents(prev => prev.map(s => s.studentId === studentId ? { ...s, status } : s));

  const handleNotesChange = (studentId, notes) =>
    setStudents(prev => prev.map(s => s.studentId === studentId ? { ...s, notes } : s));

  const markAll = (status) => setStudents(prev => prev.map(s => ({ ...s, status })));

  const handleSave = async () => {
    const records = students.filter(s => s.status).map(s => ({
      studentId: s.studentId, status: s.status, notes: s.notes || ''
    }));
    if (records.length === 0) { toast.error('Marca la asistencia de al menos un alumno.'); return; }
    setSaving(true);
    try {
      if (mode === 'event' && selectedEvent) {
        await api.post('/attendance/event', { eventId: selectedEvent, records });
      } else {
        await api.post(`/attendance/category/${selectedCategory}`, { date, records });
      }
      toast.success('Asistencia guardada correctamente');
      mode === 'event' ? fetchByEvent() : fetchByDate();
    } catch (err) {
      if (err.response?.status === 409) {
        toast.error(err.response.data?.message ?? 'La ventana de asistencia no está abierta aún.');
      } else {
        toast.error(err.response?.data?.message || 'Error al guardar la asistencia');
      }
    } finally {
      setSaving(false);
    }
  };

  if (loading) return (
    <AppLayout>
      <div style={{ textAlign:'center', padding:60 }}>
        <div className="spinner" style={{ margin:'0 auto' }}/>
      </div>
    </AppLayout>
  );

  return (
    <AppLayout>
      <div style={{ display:'flex', flexDirection:'column', gap:20 }}>

        {/* Header */}
        <div style={{ display:'flex', justifyContent:'space-between', alignItems:'flex-start' }}>
          <div>
            <h2 className="page-title" style={{ display:'flex', alignItems:'center', gap:10 }}>
              <CalendarCheck size={24}/> Control de Asistencia
            </h2>
            <p style={{ color:'var(--text-muted)', fontSize:14, marginTop:4 }}>
              Registra la asistencia por fecha libre o vinculada a un evento del calendario
            </p>
          </div>
          <button
            className="btn btn-ghost btn-sm"
            onClick={() => navigate('/academy/asistencia-metricas')}
            style={{ display:'flex', alignItems:'center', gap:6 }}
          >
            <BarChart2 size={15}/> Ver Métricas
          </button>
        </div>

        {/* Mode tabs */}
        <div className="tabs" style={{ maxWidth:400 }}>
          <button
            className={`tab-pill ${mode === 'date' ? 'active' : ''}`}
            onClick={() => { setMode('date'); setStudents([]); setWindowBlocked(null); }}
          >
            📅 Por Fecha
          </button>
          <button
            className={`tab-pill ${mode === 'event' ? 'active' : ''}`}
            onClick={() => { setMode('event'); setStudents([]); setWindowBlocked(null); }}
          >
            🗓️ Por Evento
          </button>
        </div>

        {/* Filters card */}
        <div className="card">
          {mode === 'date' ? (
            <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr', gap:16 }}>
              <div className="form-group" style={{ marginBottom:0 }}>
                <label>Categoría / Equipo</label>
                <select className="form-control" value={selectedCategory}
                  onChange={e => setSelectedCategory(e.target.value)}>
                  <option value="">-- Seleccione Categoría --</option>
                  {categorias.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>
              <div className="form-group" style={{ marginBottom:0 }}>
                <label>Fecha</label>
                <input type="date" className="form-control" value={date}
                  onChange={e => setDate(e.target.value)}/>
              </div>
            </div>
          ) : (
            <div style={{ display:'flex', flexDirection:'column', gap:16 }}>
              <div className="form-group" style={{ marginBottom:0 }}>
                <label>Evento del Mes</label>
                <select className="form-control" value={selectedEvent}
                  onChange={e => setSelectedEvent(e.target.value)}>
                  <option value="">-- Seleccione un evento --</option>
                  {events.map(e => {
                    const d = new Date(e.startTime);
                    const fmt = d.toLocaleDateString('es', { weekday:'short', day:'2-digit', month:'short', timeZone:'UTC' });
                    const hr  = d.toLocaleTimeString('es', { hour:'2-digit', minute:'2-digit', timeZone:'UTC' });
                    return (
                      <option key={e.id} value={e.id}>
                        {fmt} {hr} — {e.title} {e.categoryName ? `(${e.categoryName})` : ''}
                      </option>
                    );
                  })}
                </select>
              </div>
              {events.length === 0 && (
                <p style={{ fontSize:13, color:'var(--text-muted)' }}>
                  No hay eventos este mes. Créalos desde el{' '}
                  <span
                    onClick={() => navigate('/academy/calendario')}
                    style={{ color:'var(--primary)', cursor:'pointer', textDecoration:'underline' }}
                  >
                    Calendario
                  </span>.
                </p>
              )}
            </div>
          )}
        </div>

        {/* 15-min window blocked message */}
        {windowBlocked && (
          <div className="alert alert-warning" style={{ alignItems:'flex-start' }}>
            <AlertTriangle size={18} style={{ flexShrink:0, marginTop:1 }}/>
            <div>
              <strong>Lista no disponible aún</strong>
              <p style={{ marginTop:4, fontSize:13 }}>{windowBlocked}</p>
            </div>
          </div>
        )}

        {/* Attendance table */}
        {students.length > 0 && (
          <div className="card">
            <div className="card-header">
              <div>
                <h3 className="card-title">Listado de Alumnos</h3>
                <p style={{ fontSize:13, color:'var(--text-muted)', marginTop:4 }}>
                  {students.length} alumnos · Marca su estado de asistencia
                </p>
              </div>
              <div style={{ display:'flex', gap:8, flexWrap:'wrap' }}>
                <button onClick={() => markAll(1)} className="btn btn-sm btn-ghost"
                  style={{ color:'#10b981', borderColor:'rgba(16,185,129,0.3)' }}>
                  <UserCheck size={13}/> Todos Presentes
                </button>
                <button onClick={() => markAll(2)} className="btn btn-sm btn-ghost"
                  style={{ color:'#ef4444', borderColor:'rgba(239,68,68,0.3)' }}>
                  <UserX size={13}/> Todos Ausentes
                </button>
                <button onClick={handleSave} className="btn btn-primary" disabled={saving || loadingStudents}>
                  <Save size={15}/> {saving ? 'Guardando...' : 'Guardar Asistencia'}
                </button>
              </div>
            </div>

            {loadingStudents ? (
              <div style={{ textAlign:'center', padding:40 }}>
                <div className="spinner" style={{ margin:'0 auto' }}/>
              </div>
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Alumno</th>
                      <th style={{ textAlign:'center' }}>Estado de Asistencia</th>
                      <th>Notas / Observaciones</th>
                    </tr>
                  </thead>
                  <tbody>
                    {students.map(s => (
                      <tr key={s.studentId}>
                        <td>
                          <div style={{ fontWeight:600 }}>{s.lastName}, {s.firstName}</div>
                          {s.status && (
                            <span style={{ fontSize:11, color: STATUS_BUTTONS.find(b => b.value === s.status)?.color }}>
                              {STATUS_BUTTONS.find(b => b.value === s.status)?.label}
                            </span>
                          )}
                        </td>
                        <td>
                          <div style={{ display:'flex', gap:6, justifyContent:'center', flexWrap:'wrap' }}>
                            {STATUS_BUTTONS.map(btn => {
                              const Icon = btn.icon;
                              const active = s.status === btn.value;
                              return (
                                <button
                                  key={btn.value}
                                  title={btn.label}
                                  onClick={() => handleStatusChange(s.studentId, btn.value)}
                                  style={{
                                    width:36, height:36, borderRadius:8, border:'1px solid',
                                    cursor:'pointer', display:'flex', alignItems:'center', justifyContent:'center',
                                    transition:'all 0.15s',
                                    background: active ? btn.color : 'var(--bg-surface)',
                                    borderColor: active ? btn.color : 'var(--border)',
                                    color: active ? '#fff' : btn.color,
                                  }}
                                >
                                  <Icon size={14}/>
                                </button>
                              );
                            })}
                          </div>
                        </td>
                        <td>
                          <input
                            type="text"
                            className="form-control"
                            style={{ fontSize:13, padding:'6px 10px' }}
                            placeholder="Observación opcional..."
                            value={s.notes || ''}
                            onChange={e => handleNotesChange(s.studentId, e.target.value)}
                          />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {/* Empty state when no category/event selected */}
        {!loadingStudents && students.length === 0 && !windowBlocked && (
          <div className="empty-state card">
            <CalendarDays size={48} style={{ margin:'0 auto 12px', display:'block', opacity:0.3 }}/>
            <p style={{ color:'var(--text-muted)' }}>
              {mode === 'date'
                ? 'Selecciona una categoría y fecha para cargar la lista.'
                : 'Selecciona un evento para abrir la lista de asistencia.'}
            </p>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
