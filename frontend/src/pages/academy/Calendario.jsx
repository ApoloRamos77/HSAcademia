import { useState, useEffect, useCallback } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import {
  CalendarDays, ChevronLeft, ChevronRight, Plus, X, Filter,
  Cake, Trophy, Swords, Dumbbell, Clock, MapPin, Users
} from 'lucide-react';
import toast from 'react-hot-toast';

// ─────────────────────────── helpers ────────────────────────────
const EVENT_TYPES = [
  { value: 1, label: 'Entrenamiento', color: '#6366f1', bg: 'rgba(99,102,241,0.18)', icon: Dumbbell },
  { value: 2, label: 'Amistoso',      color: '#f59e0b', bg: 'rgba(245,158,11,0.18)', icon: Swords   },
  { value: 3, label: 'Torneo',        color: '#ef4444', bg: 'rgba(239,68,68,0.18)',  icon: Trophy   },
  { value: 4, label: 'Cumpleaños',    color: '#ec4899', bg: 'rgba(236,72,153,0.18)', icon: Cake     },
];
const typeConfig = (t) => EVENT_TYPES.find(e => e.value === t) ?? EVENT_TYPES[0];

const MONTH_NAMES = [
  'Enero','Febrero','Marzo','Abril','Mayo','Junio',
  'Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'
];
const DAY_NAMES = ['Dom','Lun','Mar','Mié','Jue','Vie','Sáb'];

function buildCalendarDays(year, month) {
  const firstDay = new Date(year, month - 1, 1).getDay();
  const daysInMonth = new Date(year, month, 0).getDate();
  const days = [];
  for (let i = 0; i < firstDay; i++) days.push(null);
  for (let d = 1; d <= daysInMonth; d++) days.push(d);
  return days;
}

// ─────────────────────────── main component ────────────────────────────
export default function Calendario() {
  const today = new Date();
  const [year,  setYear]  = useState(today.getFullYear());
  const [month, setMonth] = useState(today.getMonth() + 1);

  const [events,       setEvents]       = useState([]);
  const [categories,   setCategories]   = useState([]);
  const [headquarters, setHeadquarters] = useState([]);
  const [tournaments,  setTournaments]  = useState([]);
  const [loading,      setLoading]      = useState(false);

  // Filters
  const [filterType,  setFilterType]  = useState('');
  const [filterHq,    setFilterHq]    = useState('');
  const [filterCat,   setFilterCat]   = useState('');

  // Selected day popup
  const [selectedDay,     setSelectedDay]     = useState(null);
  const [dayEvents,       setDayEvents]       = useState([]);

  const [showModal, setShowModal] = useState(false);
  const [form, setForm] = useState({
    title: '', description: '', type: 1,
    startTime: '', endTime: '',
    headquarterId: '', categoryId: '', teacherId: '',
    tournamentId: '', opponentTeam: ''
  });
  const [recurringEnabled, setRecurringEnabled] = useState(false);
  const [recurringDays, setRecurringDays]       = useState([]); // 0=Dom..6=Sáb
  const [recurringStart, setRecurringStart]     = useState(''); // HH:mm
  const [recurringEnd, setRecurringEnd]         = useState(''); // HH:mm
  const [recurringMonth, setRecurringMonth]     = useState(today.getMonth() + 1);
  const [recurringYear,  setRecurringYear]       = useState(today.getFullYear());
  const [saving, setSaving] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [editingId, setEditingId] = useState(null);

  const MONTH_NAMES_FULL = ['Enero','Febrero','Marzo','Abril','Mayo','Junio',
                            'Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'];
  const DAY_LABELS = ['Dom','Lun','Mar','Mié','Jue','Vie','Sáb'];
  const toggleDay = (d) => setRecurringDays(prev =>
    prev.includes(d) ? prev.filter(x => x !== d) : [...prev, d]
  );

  // Build year options: current year and next year
  const yearOptions = [today.getFullYear(), today.getFullYear() + 1];

  // ── Fetch helpers ──
  const fetchEvents = useCallback(async () => {
    setLoading(true);
    try {
      const params = new URLSearchParams({ year, month });
      if (filterType)  params.append('eventType',     filterType);
      if (filterHq)    params.append('headquarterId', filterHq);
      if (filterCat)   params.append('categoryId',    filterCat);
      const { data } = await api.get(`/calendar/events?${params}`);
      setEvents(data);
    } catch {
      toast.error('Error al cargar el calendario');
    } finally {
      setLoading(false);
    }
  }, [year, month, filterType, filterHq, filterCat]);

  const fetchSupport = async () => {
    try {
      const [catRes, hqRes, tRes] = await Promise.all([
        api.get('/academy-config/categories'),
        api.get('/academy-config/headquarters'),
        api.get('/calendar/tournaments'),
      ]);
      setCategories(catRes.data);
      setHeadquarters(hqRes.data);
      setTournaments(tRes.data);
    } catch (err) {
      console.error('Error cargando sedes/categorias:', err);
    }
  };

  useEffect(() => { fetchEvents(); }, [fetchEvents]);
  useEffect(() => { fetchSupport(); }, []);

  // ── Navigation ──
  const prevMonth = () => {
    if (month === 1) { setMonth(12); setYear(y => y - 1); }
    else setMonth(m => m - 1);
  };
  const nextMonth = () => {
    if (month === 12) { setMonth(1); setYear(y => y + 1); }
    else setMonth(m => m + 1);
  };

  // ── Day click ──
  const handleDayClick = (day) => {
    if (!day) return;
    const dayStr = `${year}-${String(month).padStart(2,'0')}-${String(day).padStart(2,'0')}`;
    const de = events.filter(e => e.startTime.startsWith(dayStr));
    setSelectedDay(day);
    setDayEvents(de);
  };

  // ── Events per day map ──
  const eventsPerDay = {};
  events.forEach(e => {
    const d = new Date(e.startTime);
    const key = d.getUTCDate(); // Strict UTC grouping
    if (!eventsPerDay[key]) eventsPerDay[key] = [];
    eventsPerDay[key].push(e);
  });

  // ── Save event (single or recurring) ──
  const handleSave = async () => {
    setSaving(true);
    try {
      // ── RECURRING mode (Training only) ──
      if (form.type === 1 && recurringEnabled) {
        if (!form.title || !recurringStart || !recurringEnd || recurringDays.length === 0) {
          toast.error('Completa título, horario y selecciona al menos un día'); setSaving(false); return;
        }
        // Build all matching dates in the selected month
        const daysInMonth = new Date(recurringYear, recurringMonth, 0).getDate();
        let created = 0;
        for (let day = 1; day <= daysInMonth; day++) {
          const d = new Date(Date.UTC(recurringYear, recurringMonth - 1, day));
          if (!recurringDays.includes(d.getUTCDay())) continue;
          const dateStr = `${recurringYear}-${String(recurringMonth).padStart(2,'0')}-${String(day).padStart(2,'0')}`;
          const body = {
            title: form.title,
            description: form.description || null,
            type: 1,
            startTime: `${dateStr}T${recurringStart}:00.000Z`,
            endTime:   `${dateStr}T${recurringEnd}:00.000Z`,
            headquarterId: form.headquarterId || null,
            categoryId:    form.categoryId    || null,
          };
          try { await api.post('/calendar/events', body); created++; }
          catch { /* skip conflicts */ }
        }
        toast.success(`${created} entrenamientos creados para ${MONTH_NAMES_FULL[recurringMonth-1]} ${recurringYear}`);
      } else {
        // ── SINGLE event ──
        if (!form.title || !form.startTime || !form.endTime) {
          toast.error('Completa los campos obligatorios'); setSaving(false); return;
        }
        if (new Date(form.endTime) <= new Date(form.startTime)) {
          toast.error('La hora de fin debe ser posterior al inicio'); setSaving(false); return;
        }
        const body = {
          title: form.title,
          description: form.description || null,
          type: Number(form.type),
          startTime: form.startTime + ':00.000Z',
          endTime:   form.endTime + ':00.000Z',
          headquarterId:  form.headquarterId  || null,
          categoryId:     form.categoryId     || null,
          teacherId:      form.teacherId      || null,
          tournamentId:   form.tournamentId   || null,
          opponentTeam:   form.opponentTeam   || null,
        };

        if (isEditing) {
          await api.put(`/calendar/events/${editingId}`, body);
          toast.success('Evento actualizado exitosamente');
        } else {
          await api.post('/calendar/events', body);
          toast.success('Evento creado exitosamente');
        }
      }
      setShowModal(false);
      setIsEditing(false);
      setEditingId(null);
      setForm({ title:'', description:'', type:1, startTime:'', endTime:'',
                headquarterId:'', categoryId:'', teacherId:'', tournamentId:'', opponentTeam:'' });
      setRecurringEnabled(false); setRecurringDays([]); setRecurringStart(''); setRecurringEnd('');
      setRecurringMonth(today.getMonth() + 1); setRecurringYear(today.getFullYear());
      fetchEvents();
    } catch (err) {
      toast.error(err.response?.data?.message ?? 'Error al crear el evento');
    } finally {
      setSaving(false);
    }
  };

  // ── Edit event ──
  const openEdit = (e) => {
    setForm({
      title: e.title,
      description: e.description || '',
      type: e.type,
      // Slice directly the UTC time (YYYY-MM-DDThh:mm) so no browser shift is applied
      startTime: e.startTime.slice(0, 16),
      endTime: e.endTime.slice(0, 16),
      headquarterId: e.headquarterId || '',
      categoryId: e.categoryId || '',
      teacherId: e.teacherId || '',
      tournamentId: e.tournamentId || '',
      opponentTeam: e.opponentTeam || ''
    });
    setIsEditing(true);
    setEditingId(e.id);
    setRecurringEnabled(false);
    setShowModal(true);
  };

  // ── Delete event ──
  const handleDelete = async (id) => {
    if (!window.confirm('¿Eliminar este evento?')) return;
    try {
      await api.delete(`/calendar/events/${id}`);
      toast.success('Evento eliminado');
      setSelectedDay(null);
      fetchEvents();
    } catch { toast.error('Error al eliminar'); }
  };

  const handleBulkShift = async (days) => {
    if (!window.confirm(`¿Estás seguro de desplazar todos los eventos ${days > 0 ? '+' : ''}${days} días?`)) return;
    try {
      await api.post(`/calendar/events/bulk-shift?days=${days}`);
      toast.success('Eventos actualizados masivamente');
      fetchEvents();
    } catch { toast.error('Error al actualizar masivamente'); }
  };

  const calendarDays = buildCalendarDays(year, month);

  // ─────────── RENDER ───────────
  return (
    <AppLayout>
      <div className="calendar-page">

        {/* ── Header ── */}
        <div className="cal-header">
          <div>
            <h2 className="page-title" style={{ display:'flex', alignItems:'center', gap:10 }}>
              <CalendarDays size={26} /> Calendario Maestro
            </h2>
            <p style={{ color:'var(--text-muted)', marginTop:4, fontSize:14 }}>
              Visualiza y gestiona todos los eventos de tu academia
            </p>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <button className="btn btn-secondary" onClick={() => handleBulkShift(-1)} title="Restar 1 día a todos los eventos">
              -1 Día
            </button>
            <button className="btn btn-secondary" onClick={() => handleBulkShift(1)} title="Sumar 1 día a todos los eventos">
              +1 Día
            </button>
            <button className="btn btn-primary" onClick={() => {
              setIsEditing(false); setEditingId(null);
              setForm({ title:'', description:'', type:1, startTime:'', endTime:'',
                headquarterId:'', categoryId:'', teacherId:'', tournamentId:'', opponentTeam:'' });
              setShowModal(true);
            }}>
              <Plus size={16} /> Nuevo Evento
            </button>
          </div>
        </div>

        {/* ── Filters ── */}
        <div className="cal-filters card">
          <Filter size={15} style={{ color:'var(--text-muted)' }} />
          <select value={filterType} onChange={e => setFilterType(e.target.value)} className="cal-select">
            <option value="">Todos los tipos</option>
            {EVENT_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
          </select>
          <select value={filterHq} onChange={e => setFilterHq(e.target.value)} className="cal-select">
            <option value="">Todas las sedes</option>
            {headquarters.map(h => <option key={h.id} value={h.id}>{h.name}</option>)}
          </select>
          <select value={filterCat} onChange={e => setFilterCat(e.target.value)} className="cal-select">
            <option value="">Todas las categorías</option>
            {categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
        </div>

        {/* ── Calendar ── */}
        <div className="cal-container card">
          {/* month nav */}
          <div className="cal-nav">
            <button className="cal-nav-btn" onClick={prevMonth}><ChevronLeft size={20} /></button>
            <h3 className="cal-month-label">{MONTH_NAMES[month-1]} {year}</h3>
            <button className="cal-nav-btn" onClick={nextMonth}><ChevronRight size={20} /></button>
          </div>

          {/* day-of-week headers */}
          <div className="cal-grid cal-dow-row">
            {DAY_NAMES.map(d => <div key={d} className="cal-dow">{d}</div>)}
          </div>

          {/* days */}
          {loading ? (
            <div className="cal-loading">Cargando eventos...</div>
          ) : (
            <div className="cal-grid">
              {calendarDays.map((day, i) => {
                if (!day) return <div key={`empty-${i}`} className="cal-cell cal-empty" />;
                const isToday = day === today.getDate() && month === today.getMonth()+1 && year === today.getFullYear();
                const dayEvts = eventsPerDay[day] || [];
                return (
                  <div
                    key={day}
                    className={`cal-cell ${isToday ? 'cal-today' : ''} ${selectedDay === day ? 'cal-selected' : ''}`}
                    onClick={() => handleDayClick(day)}
                  >
                    <span className="cal-day-num">{day}</span>
                    <div className="cal-evt-chips">
                      {dayEvts.slice(0,3).map(e => {
                        const cfg = typeConfig(e.type);
                        return (
                          <div key={e.id} className="cal-chip" style={{ background: cfg.bg, borderLeft:`3px solid ${cfg.color}` }}>
                            <span style={{ color: cfg.color, fontSize:11 }}>{e.title}</span>
                          </div>
                        );
                      })}
                      {dayEvts.length > 3 && (
                        <div className="cal-chip cal-chip-more">+{dayEvts.length - 3} más</div>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* ── Day popup ── */}
        {selectedDay && (
          <div className="cal-day-popup card">
            <div style={{ display:'flex', justifyContent:'space-between', alignItems:'center', marginBottom:14 }}>
              <h4 style={{ margin:0 }}>
                {String(selectedDay).padStart(2,'0')} de {MONTH_NAMES[month-1]}
              </h4>
              <button className="btn-icon" onClick={() => setSelectedDay(null)}><X size={16}/></button>
            </div>
            {dayEvents.length === 0 ? (
              <p style={{ color:'var(--text-muted)', fontSize:13 }}>Sin eventos este día.</p>
            ) : dayEvents.map(e => {
              const cfg = typeConfig(e.type);
              const Icon = cfg.icon;
              const st = new Date(e.startTime);
              const et = new Date(e.endTime);
              const fmt = (d) => d.toLocaleTimeString('es', { hour:'2-digit', minute:'2-digit', timeZone:'UTC' });
              return (
                <div key={e.id} className="day-event-card" style={{ borderLeft:`4px solid ${cfg.color}` }}>
                  <div style={{ display:'flex', justifyContent:'space-between' }}>
                    <div style={{ display:'flex', gap:8, alignItems:'center' }}>
                      <Icon size={16} style={{ color:cfg.color }} />
                      <strong style={{ fontSize:14 }}>{e.title}</strong>
                    </div>
                    {!e.isVirtual && (
                      <div style={{ display:'flex', gap: 4 }}>
                        <button
                          className="btn-icon"
                          style={{ color:'var(--primary)' }}
                          onClick={() => { setSelectedDay(null); openEdit(e); }}
                          title="Editar"
                        ><RefreshCw size={14}/></button>
                        <button
                          className="btn-icon"
                          style={{ color:'#ef4444' }}
                          onClick={() => handleDelete(e.id)}
                          title="Eliminar"
                        ><X size={14}/></button>
                      </div>
                    )}
                  </div>
                  <div style={{ display:'flex', flexDirection:'column', gap:4, marginTop:6 }}>
                    <span style={{ fontSize:12, color:'var(--text-muted)', display:'flex', gap:6, alignItems:'center' }}>
                      <Clock size={12}/> {fmt(st)} – {fmt(et)}
                    </span>
                    {e.headquarterName && (
                      <span style={{ fontSize:12, color:'var(--text-muted)', display:'flex', gap:6, alignItems:'center' }}>
                        <MapPin size={12}/> {e.headquarterName}
                      </span>
                    )}
                    {e.categoryName && (
                      <span style={{ fontSize:12, color:'var(--text-muted)', display:'flex', gap:6, alignItems:'center' }}>
                        <Users size={12}/> {e.categoryName}
                      </span>
                    )}
                    {e.opponentTeam && (
                      <span style={{ fontSize:12, color: cfg.color }}>vs {e.opponentTeam}</span>
                    )}
                    {e.description && (
                      <span style={{ fontSize:12, color:'var(--text-muted)', marginTop:2 }}>{e.description}</span>
                    )}
                    {e.isVirtual && (
                      <span style={{ fontSize:11, color:'#ec4899', marginTop:4 }}>🎂 Evento automático</span>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}

        {/* ── Legend ── */}
        <div className="cal-legend">
          {EVENT_TYPES.map(t => {
            const Icon = t.icon;
            return (
              <span key={t.value} className="legend-item" style={{ color: t.color }}>
                <Icon size={13}/> {t.label}
              </span>
            );
          })}
        </div>
      </div>

      {/* ════════ Create Event Modal ════════ */}
      {showModal && (
        <div className="modal-overlay" onClick={e => e.target === e.currentTarget && setShowModal(false)}>
          <div className="modal-box" style={{ maxWidth:600 }}>
            <div className="modal-header">
              <h3>{isEditing ? <RefreshCw size={18}/> : <Plus size={18}/>} {isEditing ? 'Editar Evento' : 'Nuevo Evento'}</h3>
              <button className="btn-icon" onClick={() => setShowModal(false)}><X size={18}/></button>
            </div>
            <div className="modal-body">
              <div className="form-row-2">
                <div className="form-group" style={{ gridColumn:'1/-1' }}>
                  <label>Título *</label>
                  <input className="form-control" placeholder="Ej: Entrenamiento Sub-12"
                    value={form.title} onChange={e => setForm({...form, title: e.target.value})} />
                </div>

                <div className="form-group">
                  <label>Tipo de Evento *</label>
                  <select className="form-control" value={form.type}
                    onChange={e => { setForm({...form, type: Number(e.target.value)}); setRecurringEnabled(false); }}>
                    {EVENT_TYPES.filter(t => t.value !== 4).map(t =>
                      <option key={t.value} value={t.value}>{t.label}</option>
                    )}
                  </select>
                </div>

                {form.type === 2 && (
                  <div className="form-group">
                    <label>Equipo Rival</label>
                    <input className="form-control" placeholder="Nombre del equipo rival"
                      value={form.opponentTeam} onChange={e => setForm({...form, opponentTeam: e.target.value})} />
                  </div>
                )}
                {form.type === 3 && (
                  <div className="form-group">
                    <label>Torneo</label>
                    <select className="form-control" value={form.tournamentId}
                      onChange={e => setForm({...form, tournamentId: e.target.value})}>
                      <option value="">Sin torneo vinculado</option>
                      {tournaments.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}
                    </select>
                  </div>
                )}

                {/* ── Recurring option (Training only) - Hidden when editing ── */}
                {form.type === 1 && !isEditing && (
                  <div className="form-group" style={{ gridColumn:'1/-1' }}>
                    <label style={{ display:'flex', alignItems:'center', gap:8, cursor:'pointer' }}>
                      <input type="checkbox" checked={recurringEnabled}
                        onChange={e => setRecurringEnabled(e.target.checked)} />
                      <span>📅 Entrenamiento recurrente por mes</span>
                    </label>
                    {recurringEnabled && (
                      <div style={{ marginTop:10, padding:12, background:'var(--bg-dark)', borderRadius:8, border:'1px solid var(--border)' }}>
                        {/* Month / Year selector */}
                        <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr', gap:8, marginBottom:12 }}>
                          <div className="form-group" style={{ margin:0 }}>
                            <label style={{ fontSize:12 }}>Mes *</label>
                            <select className="form-control" value={recurringMonth}
                              onChange={e => setRecurringMonth(Number(e.target.value))}>
                              {MONTH_NAMES_FULL.map((name, i) => (
                                <option key={i+1} value={i+1}>{name}</option>
                              ))}
                            </select>
                          </div>
                          <div className="form-group" style={{ margin:0 }}>
                            <label style={{ fontSize:12 }}>Año *</label>
                            <select className="form-control" value={recurringYear}
                              onChange={e => setRecurringYear(Number(e.target.value))}>
                              {yearOptions.map(y => <option key={y} value={y}>{y}</option>)}
                            </select>
                          </div>
                        </div>
                        <p style={{ fontSize:12, color:'var(--text-muted)', marginBottom:8 }}>Selecciona los días de entrenamiento:</p>
                        <div style={{ display:'flex', gap:6, flexWrap:'wrap', marginBottom:12 }}>
                          {DAY_LABELS.map((label, idx) => (
                            <button key={idx} type="button"
                              onClick={() => toggleDay(idx)}
                              style={{
                                padding:'4px 10px', borderRadius:6, fontSize:12, cursor:'pointer',
                                border: recurringDays.includes(idx) ? '2px solid var(--primary)' : '1px solid var(--border)',
                                background: recurringDays.includes(idx) ? 'var(--primary)' : 'var(--bg-surface)',
                                color: recurringDays.includes(idx) ? '#fff' : 'var(--text-muted)',
                              }}>{label}</button>
                          ))}
                        </div>
                        <div style={{ display:'grid', gridTemplateColumns:'1fr 1fr', gap:8 }}>
                          <div className="form-group" style={{ margin:0 }}>
                            <label style={{ fontSize:12 }}>Hora inicio *</label>
                            <input type="time" className="form-control"
                              value={recurringStart} onChange={e => setRecurringStart(e.target.value)} />
                          </div>
                          <div className="form-group" style={{ margin:0 }}>
                            <label style={{ fontSize:12 }}>Hora fin *</label>
                            <input type="time" className="form-control"
                              value={recurringEnd} onChange={e => setRecurringEnd(e.target.value)} />
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {/* Single event date/time (hidden when creating recurring) */}
                {(!recurringEnabled || isEditing) && (
                  <>
                    <div className="form-group">
                      <label>Inicio *</label>
                      <input type="datetime-local" className="form-control"
                        value={form.startTime} onChange={e => setForm({...form, startTime: e.target.value})} />
                    </div>
                    <div className="form-group">
                      <label>Fin *</label>
                      <input type="datetime-local" className="form-control"
                        value={form.endTime} onChange={e => setForm({...form, endTime: e.target.value})} />
                    </div>
                  </>
                )}

                <div className="form-group">
                  <label>Sede</label>
                  <select className="form-control" value={form.headquarterId}
                    onChange={e => setForm({...form, headquarterId: e.target.value})}>
                    <option value="">Sin sede específica</option>
                    {headquarters.map(h => <option key={h.id} value={h.id}>{h.name}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label>Categoría</label>
                  <select className="form-control" value={form.categoryId}
                    onChange={e => setForm({...form, categoryId: e.target.value})}>
                    <option value="">Sin categoría</option>
                    {categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                  </select>
                </div>

                <div className="form-group" style={{ gridColumn:'1/-1' }}>
                  <label>Descripción</label>
                  <textarea className="form-control" rows={2} placeholder="Notas, instrucciones..."
                    value={form.description} onChange={e => setForm({...form, description: e.target.value})} />
                </div>
              </div>
            </div>
            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
              <button className="btn btn-primary" onClick={handleSave} disabled={saving}>
                {saving ? 'Guardando...' : (isEditing ? 'Actualizar Evento' : (form.type === 1 && recurringEnabled ? '📅 Crear Entrenamientos' : 'Crear Evento'))}
              </button>
            </div>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
