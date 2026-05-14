import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { useAuth } from '../../context/AuthContext';
import {
  CalendarCheck, Save, UserCheck, UserX,
  Clock, FileMinus, CalendarDays, AlertTriangle, BarChart2,
  Filter, ChevronDown, ChevronUp, Search
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
  const { user } = useAuth();
  const isAdmin = user?.role === 'AcademyAdmin';

  const [mode, setMode] = useState('date'); // 'date' | 'event'

  // Filters/meta
  const [categorias,       setCategorias]       = useState([]);
  const [sedes,            setSedes]            = useState([]);
  const [selectedCategory, setSelectedCategory] = useState('');
  const [selectedSede,     setSelectedSede]     = useState('');
  const [date,             setDate]             = useState(new Date().toISOString().split('T')[0]);
  const [eventSearch,      setEventSearch]      = useState('');

  // Event-mode
  const [events,        setEvents]        = useState([]);
  const [selectedEvent, setSelectedEvent] = useState('');
  const [windowBlocked, setWindowBlocked] = useState(null);

  // Shared
  const [students,        setStudents]        = useState([]);
  const [loading,         setLoading]         = useState(true);
  const [loadingStudents, setLoadingStudents] = useState(false);
  const [saving,          setSaving]          = useState(false);

  const today = new Date();
  const [filterYear, setFilterYear] = useState(today.getFullYear());
  const [filterMonth, setFilterMonth] = useState(today.getMonth() + 1);

  // ── Load metadata ──────────────────────────────────────────────
  useEffect(() => {
    Promise.all([
      api.get('/academy-config/categories'),
      api.get('/academy-config/headquarters'),
    ]).then(([catRes, sedeRes]) => {
      setCategorias(catRes.data);
      setSedes(sedeRes.data);
    }).catch(() => toast.error('Error al cargar datos'))
      .finally(() => setLoading(false));
  }, []);

  const loadUpcomingEvents = async () => {
    try {
      const params = new URLSearchParams({ year: filterYear, month: filterMonth });
      const { data } = await api.get(`/calendar/events?${params}`);
      const attendableTypes = [1, 2, 3];
      const filtered = data.filter(e => attendableTypes.includes(e.type) && !e.isVirtual);
      setEvents(filtered);
    } catch { /* silent */ }
  };

  useEffect(() => {
    loadUpcomingEvents();
  }, [filterYear, filterMonth]);

  // ── Date-mode fetch ───────────────────────────────────────────
  useEffect(() => {
    if (mode !== 'date') return;
    fetchByDate();
  }, [selectedCategory, date, mode]);

  const fetchByDate = async () => {
    if (!selectedCategory || !date) { setStudents([]); return; }
    setLoadingStudents(true); setWindowBlocked(null);
    try {
      const { data } = await api.get(`/attendance/category/${selectedCategory}?date=${date}`);
      setStudents(data);
    } catch { toast.error('Error al cargar asistencia'); }
    finally { setLoadingStudents(false); }
  };

  // ── Event-mode fetch ──────────────────────────────────────────
  useEffect(() => {
    if (mode !== 'event' || !selectedEvent) return;
    fetchByEvent();
  }, [selectedEvent, mode]);

  const fetchByEvent = async () => {
    if (!selectedEvent) { setStudents([]); setWindowBlocked(null); return; }
    setLoadingStudents(true); setWindowBlocked(null);
    try {
      const { data } = await api.get(`/attendance/event/${selectedEvent}`);
      setStudents(data);
    } catch (err) {
      if (err.response?.status === 409) {
        setWindowBlocked(err.response.data?.message ?? 'Lista aún no disponible.');
        setStudents([]);
      } else { toast.error('Error al cargar asistencia del evento'); }
    } finally { setLoadingStudents(false); }
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
      if (err.response?.status === 409)
        toast.error(err.response.data?.message ?? 'La ventana de asistencia no está abierta aún.');
      else
        toast.error(err.response?.data?.message || 'Error al guardar la asistencia');
    } finally { setSaving(false); }
  };

  // ── Filter events ─────────────────────────────────────────────
  const filteredEvents = events.filter(e => {
    const matchSede   = !selectedSede     || e.headquarterId?.toString() === selectedSede.toString();
    const matchCat    = !selectedCategory || e.categoryId?.toString() === selectedCategory.toString()
                          || (e.categoryIds && e.categoryIds.some(id => id.toString() === selectedCategory.toString()));
    const matchSearch = !eventSearch || e.title.toLowerCase().includes(eventSearch.toLowerCase());
    return matchSede && matchCat && matchSearch;
  });

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
          <button className={`tab-pill ${mode === 'date' ? 'active' : ''}`}
            onClick={() => { setMode('date'); setStudents([]); setWindowBlocked(null); }}>
            📅 Por Fecha
          </button>
          <button className={`tab-pill ${mode === 'event' ? 'active' : ''}`}
            onClick={() => { setMode('event'); setStudents([]); setWindowBlocked(null); }}>
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

              {/* Admin: filters for events */}
              {isAdmin && (
                <div style={{ display:'grid', gridTemplateColumns:'repeat(auto-fit, minmax(130px, 1fr))', gap:12, padding:12, background:'var(--bg-dark)', borderRadius:8, border:'1px solid var(--border)' }}>
                  <div className="form-group" style={{ marginBottom:0 }}>
                    <label style={{ fontSize:12 }}><Filter size={12}/> Mes</label>
                    <select className="form-control text-sm" value={filterMonth} onChange={e => setFilterMonth(parseInt(e.target.value))}>
                      {['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'].map((m, i) => <option key={i+1} value={i+1}>{m}</option>)}
                    </select>
                  </div>
                  <div className="form-group" style={{ marginBottom:0 }}>
                    <label style={{ fontSize:12 }}><Filter size={12}/> Año</label>
                    <select className="form-control text-sm" value={filterYear} onChange={e => setFilterYear(parseInt(e.target.value))}>
                      {[2024, 2025, 2026, 2027].map(y => <option key={y} value={y}>{y}</option>)}
                    </select>
                  </div>
                  <div className="form-group" style={{ marginBottom:0 }}>
                    <label style={{ fontSize:12 }}><Filter size={12}/> Sede</label>
                    <select className="form-control text-sm" value={selectedSede}
                      onChange={e => setSelectedSede(e.target.value)}>
                      <option value="">Todas las Sedes</option>
                      {sedes.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
                    </select>
                  </div>
                  <div className="form-group" style={{ marginBottom:0 }}>
                    <label style={{ fontSize:12 }}><Filter size={12}/> Categoría</label>
                    <select className="form-control text-sm" value={selectedCategory}
                      onChange={e => setSelectedCategory(e.target.value)}>
                      <option value="">Todas las Categorías</option>
                      {categorias.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                    </select>
                  </div>
                  <div className="form-group" style={{ marginBottom:0 }}>
                    <label style={{ fontSize:12 }}><Search size={12}/> Buscar Evento</label>
                    <input className="form-control text-sm" placeholder="Nombre..."
                      value={eventSearch} onChange={e => setEventSearch(e.target.value)}/>
                  </div>
                </div>
              )}

              {/* Admin: expandable event list */}
              {isAdmin ? (
                <div style={{ display:'flex', flexDirection:'column', gap:8, maxHeight:400, overflowY:'auto' }}>
                  {filteredEvents.length === 0 ? (
                    <p style={{ fontSize:13, color:'var(--text-muted)', textAlign:'center', padding:16 }}>
                      No hay eventos que coincidan con los filtros.
                    </p>
                  ) : filteredEvents.map(ev => {
                    const d = new Date(ev.startTime);
                    const fmt = d.toLocaleDateString('es-PE', { weekday:'short', day:'2-digit', month:'short', timeZone:'UTC' });
                    const hr  = d.toLocaleTimeString('es-PE', { hour:'2-digit', minute:'2-digit', timeZone:'UTC' });
                    const isSelected = selectedEvent === ev.id;
                    return (
                      <div key={ev.id} style={{
                        border:`1px solid ${isSelected ? 'var(--primary)' : 'var(--border)'}`,
                        borderRadius:10, overflow:'hidden',
                        background: isSelected ? 'rgba(var(--primary-rgb),0.05)' : 'var(--bg-surface)',
                        transition:'all 0.15s'
                      }}>
                        <div
                          style={{ padding:'10px 14px', display:'flex', justifyContent:'space-between', alignItems:'center', cursor:'pointer' }}
                          onClick={() => {
                            const next = isSelected ? '' : ev.id;
                            setSelectedEvent(next);
                            setStudents([]);
                            setWindowBlocked(null);
                          }}>
                          <div style={{ display:'flex', alignItems:'center', gap:10 }}>
                            <div style={{ width:6, height:6, borderRadius:3, background: isSelected ? 'var(--primary)' : 'var(--border)' }}/>
                            <div>
                              <span style={{ fontWeight:600, fontSize:14, color:'var(--text-main)' }}>{ev.title}</span>
                              <span style={{ fontSize:12, color:'var(--text-muted)', marginLeft:8 }}>{fmt} {hr}</span>
                              {ev.headquarterName && (
                                <span style={{ fontSize:11, background:'var(--bg-dark)', color:'var(--text-muted)', padding:'1px 6px', borderRadius:4, marginLeft:6 }}>
                                  {ev.headquarterName}
                                </span>
                              )}
                              {ev.categoryName && (
                                <span style={{ fontSize:11, background:'rgba(99,102,241,0.15)', color:'var(--primary)', padding:'1px 6px', borderRadius:4, marginLeft:4 }}>
                                  {ev.categoryName}
                                </span>
                              )}
                            </div>
                          </div>
                          {isSelected
                            ? <ChevronUp size={16} style={{ color:'var(--primary)', flexShrink:0 }}/>
                            : <ChevronDown size={16} style={{ color:'var(--text-muted)', flexShrink:0 }}/>}
                        </div>

                        {/* Expanded: load button */}
                        {isSelected && (
                          <div style={{ borderTop:'1px solid var(--border)', padding:'8px 14px 12px' }}>
                            <button className="btn btn-primary btn-sm" onClick={fetchByEvent}>
                              📋 Cargar Lista de Asistencia ({ev.title})
                            </button>
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>
              ) : (
                /* Staff: simple dropdown */
                <div>
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
                    <p style={{ fontSize:13, color:'var(--text-muted)', marginTop:8 }}>
                      No hay eventos este mes. Créalos desde el{' '}
                      <span onClick={() => navigate('/academy/calendario')}
                        style={{ color:'var(--primary)', cursor:'pointer', textDecoration:'underline' }}>
                        Calendario
                      </span>.
                    </p>
                  )}
                </div>
              )}
            </div>
          )}
        </div>

        {/* 15-min window blocked */}
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
                                <button key={btn.value} title={btn.label}
                                  onClick={() => handleStatusChange(s.studentId, btn.value)}
                                  style={{
                                    width:36, height:36, borderRadius:8, border:'1px solid',
                                    cursor:'pointer', display:'flex', alignItems:'center', justifyContent:'center',
                                    transition:'all 0.15s',
                                    background: active ? btn.color : 'var(--bg-surface)',
                                    borderColor: active ? btn.color : 'var(--border)',
                                    color: active ? '#fff' : btn.color,
                                  }}>
                                  <Icon size={14}/>
                                </button>
                              );
                            })}
                          </div>
                        </td>
                        <td>
                          <input type="text" className="form-control"
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

        {/* Empty state */}
        {!loadingStudents && students.length === 0 && !windowBlocked && (
          <div className="empty-state card">
            <CalendarDays size={48} style={{ margin:'0 auto 12px', display:'block', opacity:0.3 }}/>
            <p style={{ color:'var(--text-muted)' }}>
              {mode === 'date'
                ? 'Selecciona una categoría y fecha para cargar la lista.'
                : isAdmin
                  ? 'Selecciona un evento de la lista y presiona "Cargar Lista de Asistencia".'
                  : 'Selecciona un evento para abrir la lista de asistencia.'}
            </p>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
