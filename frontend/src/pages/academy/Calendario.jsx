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

  // Create event modal
  const [showModal, setShowModal] = useState(false);
  const [form, setForm] = useState({
    title: '', description: '', type: 1,
    startTime: '', endTime: '',
    headquarterId: '', categoryId: '', teacherId: '',
    tournamentId: '', opponentTeam: ''
  });
  const [saving, setSaving] = useState(false);

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
        api.get('/categories'),
        api.get('/headquarters'),
        api.get('/calendar/tournaments'),
      ]);
      setCategories(catRes.data);
      setHeadquarters(hqRes.data);
      setTournaments(tRes.data);
    } catch { /* silent */ }
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
    const key = d.getUTCDate();
    if (!eventsPerDay[key]) eventsPerDay[key] = [];
    eventsPerDay[key].push(e);
  });

  // ── Save event ──
  const handleSave = async () => {
    if (!form.title || !form.startTime || !form.endTime) {
      toast.error('Completa los campos obligatorios'); return;
    }
    if (new Date(form.endTime) <= new Date(form.startTime)) {
      toast.error('La hora de fin debe ser posterior al inicio'); return;
    }
    setSaving(true);
    try {
      const body = {
        title: form.title,
        description: form.description || null,
        type: Number(form.type),
        startTime: new Date(form.startTime).toISOString(),
        endTime:   new Date(form.endTime).toISOString(),
        headquarterId:  form.headquarterId  || null,
        categoryId:     form.categoryId     || null,
        teacherId:      form.teacherId      || null,
        tournamentId:   form.tournamentId   || null,
        opponentTeam:   form.opponentTeam   || null,
      };
      await api.post('/calendar/events', body);
      toast.success('Evento creado exitosamente');
      setShowModal(false);
      setForm({ title:'', description:'', type:1, startTime:'', endTime:'',
                headquarterId:'', categoryId:'', teacherId:'', tournamentId:'', opponentTeam:'' });
      fetchEvents();
    } catch (err) {
      const msg = err.response?.data?.message ?? 'Error al crear el evento';
      toast.error(msg);
    } finally {
      setSaving(false);
    }
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
          <button className="btn btn-primary" onClick={() => setShowModal(true)}>
            <Plus size={16} /> Nuevo Evento
          </button>
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
                      <button
                        className="btn-icon"
                        style={{ color:'#ef4444' }}
                        onClick={() => handleDelete(e.id)}
                        title="Eliminar"
                      ><X size={14}/></button>
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
          <div className="modal-box" style={{ maxWidth:580 }}>
            <div className="modal-header">
              <h3><Plus size={18}/> Nuevo Evento</h3>
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
                    onChange={e => setForm({...form, type: Number(e.target.value)})}>
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
                  <textarea className="form-control" rows={3} placeholder="Notas, instrucciones..."
                    value={form.description} onChange={e => setForm({...form, description: e.target.value})} />
                </div>
              </div>
            </div>
            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
              <button className="btn btn-primary" onClick={handleSave} disabled={saving}>
                {saving ? 'Guardando...' : 'Crear Evento'}
              </button>
            </div>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
