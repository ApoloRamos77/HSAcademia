import { useState, useEffect, useCallback } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { BarChart2, AlertTriangle, CheckCircle, Users, Filter, TrendingUp, TrendingDown } from 'lucide-react';
import toast from 'react-hot-toast';

const MONTH_NAMES = [
  'Enero','Febrero','Marzo','Abril','Mayo','Junio',
  'Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'
];

export default function AsistenciaMetricas() {
  const today = new Date();
  const [categories, setCategories]   = useState([]);
  const [categoryId, setCategoryId]   = useState('');
  const [year,  setYear]  = useState(today.getFullYear());
  const [month, setMonth] = useState(today.getMonth() + 1);
  const [summary, setSummary]         = useState(null);
  const [loading, setLoading]         = useState(false);

  // Load categories on mount
  useEffect(() => {
    api.get('/academy-config/categories').then(r => {
      setCategories(r.data);
      if (r.data.length > 0) setCategoryId(r.data[0].id);
    }).catch(() => {});
  }, []);

  const fetchMetrics = useCallback(async () => {
    if (!categoryId) return;
    setLoading(true);
    try {
      const { data } = await api.get(
        `/attendance/metrics/category/${categoryId}?year=${year}&month=${month}`
      );
      setSummary(data);
    } catch {
      toast.error('Error al cargar las métricas');
    } finally {
      setLoading(false);
    }
  }, [categoryId, year, month]);

  useEffect(() => { fetchMetrics(); }, [fetchMetrics]);

  const alertStudents  = summary?.students?.filter(s => s.isAlert) ?? [];
  const okStudents     = summary?.students?.filter(s => !s.isAlert) ?? [];
  const avgPercent     = summary?.students?.length
    ? Math.round(summary.students.reduce((a, s) => a + s.attendancePercent, 0) / summary.students.length)
    : 0;

  const getColor = (pct) => {
    if (pct >= 80) return '#10b981';
    if (pct >= 70) return '#f59e0b';
    return '#ef4444';
  };

  return (
    <AppLayout>
      <div style={{ display:'flex', flexDirection:'column', gap:20 }}>

        {/* Header */}
        <div style={{ display:'flex', justifyContent:'space-between', alignItems:'flex-start' }}>
          <div>
            <h2 className="page-title" style={{ display:'flex', alignItems:'center', gap:10 }}>
              <BarChart2 size={24}/> Métricas de Asistencia
            </h2>
            <p style={{ color:'var(--text-muted)', fontSize:14, marginTop:4 }}>
              Porcentaje mensual por alumno · Alerta automática al caer por debajo del 70%
            </p>
          </div>
        </div>

        {/* Filters */}
        <div className="card" style={{ display:'flex', gap:12, flexWrap:'wrap', padding:'14px 20px', alignItems:'center' }}>
          <Filter size={15} style={{ color:'var(--text-muted)' }}/>
          <select className="cal-select" value={categoryId} onChange={e => setCategoryId(e.target.value)}>
            <option value="">Seleccionar categoría</option>
            {categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
          <select className="cal-select" value={month} onChange={e => setMonth(Number(e.target.value))}>
            {MONTH_NAMES.map((m, i) => <option key={i} value={i+1}>{m}</option>)}
          </select>
          <select className="cal-select" value={year} onChange={e => setYear(Number(e.target.value))}>
            {[2024,2025,2026,2027].map(y => <option key={y} value={y}>{y}</option>)}
          </select>
        </div>

        {loading ? (
          <div style={{ textAlign:'center', padding:60, color:'var(--text-muted)' }}>
            <div className="spinner" style={{ margin:'0 auto 12px' }}/>
            Calculando métricas...
          </div>
        ) : summary ? (
          <>
            {/* Summary cards */}
            <div className="stats-grid">
              <div className="stat-card">
                <div className="stat-value" style={{ color: getColor(avgPercent) }}>{avgPercent}%</div>
                <div className="stat-label">Promedio del equipo</div>
                <div className="stat-icon"><TrendingUp size={40}/></div>
              </div>
              <div className="stat-card">
                <div className="stat-value">{summary.students?.length ?? 0}</div>
                <div className="stat-label">Alumnos activos</div>
                <div className="stat-icon"><Users size={40}/></div>
              </div>
              <div className="stat-card" style={{ borderTop: '2px solid #10b981' }}>
                <div className="stat-value" style={{ color:'#10b981' }}>{okStudents.length}</div>
                <div className="stat-label">Con asistencia ≥ 70%</div>
                <div className="stat-icon"><CheckCircle size={40}/></div>
              </div>
              <div className="stat-card" style={{ borderTop: '2px solid #ef4444' }}>
                <div className="stat-value" style={{ color:'#ef4444' }}>{alertStudents.length}</div>
                <div className="stat-label">Con alerta (&lt; 70%)</div>
                <div className="stat-icon"><AlertTriangle size={40}/></div>
              </div>
            </div>

            {/* Alert section */}
            {alertStudents.length > 0 && (
              <div className="card" style={{ borderLeft:'4px solid #ef4444' }}>
                <div style={{ display:'flex', alignItems:'center', gap:8, marginBottom:16 }}>
                  <AlertTriangle size={18} style={{ color:'#ef4444' }}/>
                  <h3 style={{ fontSize:15, fontWeight:700, color:'#ef4444' }}>
                    Alumnos con asistencia crítica — {MONTH_NAMES[month-1]} {year}
                  </h3>
                </div>
                <div style={{ display:'grid', gap:10 }}>
                  {alertStudents.map(s => (
                    <StudentMetricRow key={s.studentId} student={s} getColor={getColor}/>
                  ))}
                </div>
              </div>
            )}

            {/* Full table */}
            <div className="card">
              <div className="card-header">
                <h3 className="card-title">
                  {summary.categoryName} — {MONTH_NAMES[month-1]} {year}
                </h3>
                <span style={{ fontSize:13, color:'var(--text-muted)' }}>
                  {summary.students?.length ?? 0} alumnos · {summary.students?.[0]?.totalSessions ?? 0} sesiones registradas
                </span>
              </div>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Alumno</th>
                      <th style={{ textAlign:'center' }}>Presentes</th>
                      <th style={{ textAlign:'center' }}>Total Sesiones</th>
                      <th>Asistencia</th>
                      <th style={{ textAlign:'center' }}>Estado</th>
                    </tr>
                  </thead>
                  <tbody>
                    {summary.students?.sort((a,b) => a.attendancePercent - b.attendancePercent).map(s => (
                      <tr key={s.studentId}>
                        <td style={{ fontWeight:600 }}>{s.fullName}</td>
                        <td style={{ textAlign:'center' }}>{s.presentCount}</td>
                        <td style={{ textAlign:'center' }}>{s.totalSessions}</td>
                        <td style={{ minWidth:180 }}>
                          <ProgressBar value={s.attendancePercent} getColor={getColor}/>
                        </td>
                        <td style={{ textAlign:'center' }}>
                          {s.isAlert
                            ? <span className="badge badge-danger"><AlertTriangle size={11}/> Alerta</span>
                            : <span className="badge badge-success"><CheckCircle size={11}/> OK</span>}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </>
        ) : (
          <div className="empty-state card">
            <BarChart2 size={48} style={{ margin:'0 auto 12px', display:'block' }}/>
            <p>Selecciona una categoría para ver las métricas.</p>
          </div>
        )}
      </div>
    </AppLayout>
  );
}

// ── Sub-components ──
function StudentMetricRow({ student, getColor }) {
  const color = getColor(student.attendancePercent);
  return (
    <div style={{
      display:'flex', alignItems:'center', gap:14,
      background:'var(--bg-surface)', borderRadius:8, padding:'10px 14px'
    }}>
      <div style={{
        width:40, height:40, borderRadius:'50%',
        background:`conic-gradient(${color} ${student.attendancePercent * 3.6}deg, var(--border) 0)`,
        display:'flex', alignItems:'center', justifyContent:'center', flexShrink:0
      }}>
        <div style={{
          width:30, height:30, borderRadius:'50%',
          background:'var(--bg-surface)',
          display:'flex', alignItems:'center', justifyContent:'center',
          fontSize:10, fontWeight:700, color
        }}>
          {student.attendancePercent}%
        </div>
      </div>
      <div style={{ flex:1 }}>
        <div style={{ fontWeight:600, fontSize:14 }}>{student.fullName}</div>
        <div style={{ fontSize:12, color:'var(--text-muted)' }}>
          {student.presentCount} de {student.totalSessions} sesiones
        </div>
      </div>
      <TrendingDown size={18} style={{ color:'#ef4444' }}/>
    </div>
  );
}

function ProgressBar({ value, getColor }) {
  const color = getColor(value);
  return (
    <div style={{ display:'flex', alignItems:'center', gap:8 }}>
      <div style={{
        flex:1, height:8, background:'var(--bg-surface)',
        borderRadius:999, overflow:'hidden'
      }}>
        <div style={{
          height:'100%', width:`${Math.min(value,100)}%`,
          background: color,
          borderRadius:999, transition:'width 0.5s ease'
        }}/>
      </div>
      <span style={{ fontSize:12, fontWeight:700, color, width:42, textAlign:'right' }}>
        {value}%
      </span>
    </div>
  );
}
