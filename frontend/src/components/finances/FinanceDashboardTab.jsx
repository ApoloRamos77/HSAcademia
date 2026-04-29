import React, { useState, useEffect } from 'react';
import api from '../../api/axios';
import {
  BarChart3, TrendingUp, TrendingDown, Users, DollarSign,
  ArrowUpRight, ArrowDownRight, RefreshCw, X
} from 'lucide-react';
import toast from 'react-hot-toast';
import {
  ResponsiveContainer,
  LineChart, Line,
  BarChart, Bar,
  PieChart, Pie, Cell,
  XAxis, YAxis, CartesianGrid,
  Tooltip, Legend,
  ReferenceLine
} from 'recharts';

const MONTHS = [
  'Enero','Febrero','Marzo','Abril','Mayo','Junio',
  'Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'
];

const EXPENSE_LABELS = {
  'Operativo': '⚙️ Operativo',
  'Marketing': '📣 Marketing',
  'Equipamiento': '🏋️ Equipamiento',
  'Alquiler': '🏠 Alquiler',
  'Otro': '📋 Otro',
};

const PIE_COLORS = ['#6366f1', '#f59e0b', '#10b981', '#ef4444', '#8b5cf6', '#06b6d4'];

const CustomTooltip = ({ active, payload, label }) => {
  if (active && payload && payload.length) {
    return (
      <div style={{
        background: 'rgba(15,15,35,0.95)',
        border: '1px solid rgba(255,255,255,0.1)',
        borderRadius: 12,
        padding: '12px 16px',
        fontSize: 13
      }}>
        <p style={{ color: '#a0aec0', marginBottom: 8, fontWeight: 600 }}>{label}</p>
        {payload.map((entry, i) => (
          <p key={i} style={{ color: entry.color, margin: '2px 0' }}>
            {entry.name}: <strong>S/ {Number(entry.value).toFixed(2)}</strong>
          </p>
        ))}
      </div>
    );
  }
  return null;
};

export default function FinanceDashboardTab() {
  const now = new Date();
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [year, setYear] = useState(now.getFullYear());
  const [trendMonths, setTrendMonths] = useState(6);
  const [summary, setSummary] = useState(null);
  const [goal, setGoal] = useState(null);
  const [closing, setClosing] = useState(null);
  const [trendData, setTrendData] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadingTrend, setLoadingTrend] = useState(true);
  
  // Goal Modal
  const [showGoalModal, setShowGoalModal] = useState(false);
  const [goalForm, setGoalForm] = useState({ targetIncome: '', targetProfit: '' });

  // Closing Modal
  const [showClosingModal, setShowClosingModal] = useState(false);
  const [closingNotes, setClosingNotes] = useState('');

  useEffect(() => { fetchSummaryAndGoal(); }, [month, year]);
  useEffect(() => { fetchTrend(); }, [trendMonths]);

  const fetchSummaryAndGoal = async () => {
    try {
      setLoading(true);
      const [sumRes, goalRes, closingRes] = await Promise.all([
        api.get(`/finances-premium/summary?month=${month}&year=${year}`),
        api.get(`/finances-premium/goals?month=${month}&year=${year}`),
        api.get(`/finances-premium/closings?month=${month}&year=${year}`)
      ]);
      setSummary(sumRes.data);
      setGoal(goalRes.data);
      setClosing(closingRes.data);
      if (goalRes.data) {
        setGoalForm({
          targetIncome: goalRes.data.targetIncome,
          targetProfit: goalRes.data.targetProfit
        });
      } else {
        setGoalForm({ targetIncome: '', targetProfit: '' });
      }
    } catch { toast.error('Error al cargar datos'); }
    finally { setLoading(false); }
  };

  const fetchTrend = async () => {
    try {
      setLoadingTrend(true);
      const { data } = await api.get(`/finances-premium/trends?months=${trendMonths}`);
      setTrendData(data);
    } catch { toast.error('Error al cargar tendencias'); }
    finally { setLoadingTrend(false); }
  };

  const handleSaveGoal = async (e) => {
    e.preventDefault();
    try {
      await api.post('/finances-premium/goals', {
        month,
        year,
        targetIncome: parseFloat(goalForm.targetIncome),
        targetProfit: parseFloat(goalForm.targetProfit)
      });
      toast.success('Meta actualizada');
      setShowGoalModal(false);
      fetchSummaryAndGoal();
    } catch (err) { toast.error('Error al guardar meta'); }
  };

  const handleCloseMonth = async (e) => {
    e.preventDefault();
    try {
      await api.post('/finances-premium/closings', { month, year, notes: closingNotes });
      toast.success('Mes cerrado con éxito');
      setShowClosingModal(false);
      fetchSummaryAndGoal();
    } catch (err) { toast.error(err.response?.data?.message || 'Error al cerrar el mes'); }
  };

  const handleExportPL = () => {
    toast.success('Generando reporte P&L en PDF...');
    // A placeholder for the actual PDF generation logic.
    setTimeout(() => toast.success('Reporte descargado.'), 1500);
  };

  const isProfit = summary ? summary.netBalance >= 0 : true;
  const profitMargin = summary && summary.totalIncome > 0
    ? ((summary.netBalance / summary.totalIncome) * 100).toFixed(1)
    : 0;

  // Pie chart data from expense categories
  const pieData = summary?.expensesByCategory?.map(cat => ({
    name: EXPENSE_LABELS[cat.category] || cat.category,
    value: cat.total
  })) || [];

  return (
    <div className="fade-in space-y-6">

      {/* ── Period Selector ── */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-card/40 backdrop-blur-md border border-border/50 p-4 rounded-xl">
        <div className="flex items-center gap-3">
          <h3 className="text-xl font-bold text-white flex items-center gap-2">
            <BarChart3 className="text-primary" size={20}/> Resumen Financiero
          </h3>
          {closing && closing.status === 'Closed' && (
            <span className="badge badge-success flex items-center gap-1">🔒 Mes Cerrado</span>
          )}
        </div>
        <div className="flex items-center gap-3">
          <select className="form-control text-sm py-2 w-auto" value={month} onChange={e => setMonth(+e.target.value)}>
            {MONTHS.map((m, i) => <option key={i+1} value={i+1}>{m}</option>)}
          </select>
          <select className="form-control text-sm py-2 w-auto" value={year} onChange={e => setYear(+e.target.value)}>
            {[2024,2025,2026,2027].map(y => <option key={y} value={y}>{y}</option>)}
          </select>
          
          {closing && closing.status === 'Closed' ? (
             <button onClick={handleExportPL} className="btn btn-primary btn-sm flex items-center gap-2">
               <DollarSign size={16}/> Reporte P&L
             </button>
          ) : (
             <button onClick={() => setShowClosingModal(true)} className="btn btn-success btn-sm flex items-center gap-2">
               <ArrowUpRight size={16}/> Cerrar Mes
             </button>
          )}

          <button onClick={fetchSummaryAndGoal} className="btn btn-ghost btn-sm p-2" title="Actualizar">
            <RefreshCw size={16}/>
          </button>
        </div>
      </div>

      {loading ? (
        <div className="text-center py-16 text-text-muted">
          <span className="spinner mx-auto block mb-3" style={{borderColor:'var(--primary)',borderTopColor:'transparent'}}></span>
          Calculando resumen...
        </div>
      ) : !summary ? null : (
        <>
          {/* ── KPI Cards ── */}
          <div className="grid grid-cols-2 xl:grid-cols-4 gap-4">
            <div className="card p-5 border-l-4 border-l-success">
              <div className="flex justify-between items-start">
                <div>
                  <p className="text-xs text-text-muted uppercase tracking-wide">Ingresos</p>
                  <p className="text-2xl font-bold text-success mt-1">S/ {summary.totalIncome.toFixed(2)}</p>
                </div>
                <div className="p-2 bg-success/20 rounded-lg text-success"><TrendingUp size={20}/></div>
              </div>
              <p className="text-xs text-text-muted mt-3">Mensualidades cobradas en {MONTHS[month-1]}</p>
            </div>

            <div className="card p-5 border-l-4 border-l-danger">
              <div className="flex justify-between items-start">
                <div>
                  <p className="text-xs text-text-muted uppercase tracking-wide">Egresos</p>
                  <p className="text-2xl font-bold text-danger mt-1">S/ {summary.totalExpenses.toFixed(2)}</p>
                </div>
                <div className="p-2 bg-danger/20 rounded-lg text-danger"><TrendingDown size={20}/></div>
              </div>
              <p className="text-xs text-text-muted mt-3">Gastos operativos del período</p>
            </div>

            <div className="card p-5 border-l-4 border-l-warning">
              <div className="flex justify-between items-start">
                <div>
                  <p className="text-xs text-text-muted uppercase tracking-wide">Nómina</p>
                  <p className="text-2xl font-bold text-warning mt-1">S/ {summary.totalStaffPayments.toFixed(2)}</p>
                </div>
                <div className="p-2 bg-warning/20 rounded-lg text-warning"><Users size={20}/></div>
              </div>
              <p className="text-xs text-text-muted mt-3">Pagos al personal confirmados</p>
            </div>

            <div className={`card p-5 border-l-4 ${isProfit ? 'border-l-primary' : 'border-l-danger'}`}>
              <div className="flex justify-between items-start">
                <div>
                  <p className="text-xs text-text-muted uppercase tracking-wide">Balance Neto</p>
                  <p className={`text-2xl font-bold mt-1 ${isProfit ? 'text-primary-400' : 'text-danger'}`}>
                    S/ {summary.netBalance.toFixed(2)}
                  </p>
                </div>
                <div className={`p-2 rounded-lg ${isProfit ? 'bg-primary/20 text-primary-400' : 'bg-danger/20 text-danger'}`}>
                  {isProfit ? <ArrowUpRight size={20}/> : <ArrowDownRight size={20}/>}
                </div>
              </div>
              <p className={`text-xs mt-3 font-semibold ${isProfit ? 'text-success' : 'text-danger'}`}>
                {isProfit ? `✅ Margen: ${profitMargin}%` : `⚠️ Déficit este mes`}
              </p>
            </div>
          </div>

          {/* ── Metas Financieras ── */}
          <div className="card p-6">
            <div className="flex justify-between items-center mb-5">
              <h4 className="font-bold text-white flex items-center gap-2">
                <span className="text-xl">🎯</span> Progreso de Metas
              </h4>
              <button onClick={() => setShowGoalModal(true)} className="btn btn-ghost btn-sm text-primary">
                {goal ? 'Editar Metas' : 'Establecer Metas'}
              </button>
            </div>

            {!goal ? (
              <div className="text-center py-6 text-text-muted bg-bg-dark rounded-xl border border-border/50">
                <p>No hay metas establecidas para este mes.</p>
                <button onClick={() => setShowGoalModal(true)} className="btn btn-primary btn-sm mt-3">Establecer Ahora</button>
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
                {/* Meta Ingresos */}
                <div>
                  <div className="flex justify-between text-sm mb-2">
                    <span className="text-text-secondary">Meta de Ingresos</span>
                    <span className="font-bold text-success">S/ {goal.currentIncome.toFixed(2)} / S/ {goal.targetIncome.toFixed(2)}</span>
                  </div>
                  <div className="w-full bg-bg-dark rounded-full h-3 overflow-hidden">
                    <div className="h-3 rounded-full bg-success transition-all" style={{ width: `${goal.incomeProgress}%` }}/>
                  </div>
                  <p className="text-xs text-text-muted mt-2 text-right">{goal.incomeProgress}% alcanzado</p>
                </div>
                {/* Meta Rentabilidad */}
                <div>
                  <div className="flex justify-between text-sm mb-2">
                    <span className="text-text-secondary">Meta de Rentabilidad (Neta)</span>
                    <span className="font-bold text-primary-400">S/ {goal.currentProfit.toFixed(2)} / S/ {goal.targetProfit.toFixed(2)}</span>
                  </div>
                  <div className="w-full bg-bg-dark rounded-full h-3 overflow-hidden">
                    <div className="h-3 rounded-full bg-primary-500 transition-all" style={{ width: `${goal.profitProgress}%` }}/>
                  </div>
                  <p className="text-xs text-text-muted mt-2 text-right">{goal.profitProgress}% alcanzado</p>
                </div>
              </div>
            )}
          </div>

          {/* ── Row: Trend Line + Pie Chart ── */}
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">

            {/* Trend Line Chart — 2/3 width */}
            <div className="card p-6 lg:col-span-2">
              <div className="flex justify-between items-center mb-5">
                <h4 className="font-bold text-white flex items-center gap-2">
                  <TrendingUp size={18} className="text-primary"/> Tendencia Financiera
                </h4>
                <select
                  className="form-control text-sm py-1 w-auto"
                  value={trendMonths}
                  onChange={e => setTrendMonths(+e.target.value)}
                >
                  <option value={3}>Últimos 3 meses</option>
                  <option value={6}>Últimos 6 meses</option>
                  <option value={12}>Último año</option>
                </select>
              </div>

              {loadingTrend ? (
                <div className="flex items-center justify-center h-56 text-text-muted">Cargando gráfico...</div>
              ) : (
                <ResponsiveContainer width="100%" height={240}>
                  <LineChart data={trendData} margin={{ top: 5, right: 10, left: 0, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)"/>
                    <XAxis dataKey="label" tick={{ fill: '#718096', fontSize: 12 }} axisLine={false} tickLine={false}/>
                    <YAxis tick={{ fill: '#718096', fontSize: 12 }} axisLine={false} tickLine={false}
                      tickFormatter={v => `S/${v}`}/>
                    <Tooltip content={<CustomTooltip />}/>
                    <Legend wrapperStyle={{ fontSize: 12, paddingTop: 12 }}/>
                    <ReferenceLine y={0} stroke="rgba(255,255,255,0.1)"/>
                    <Line type="monotone" dataKey="income" name="Ingresos" stroke="#10b981"
                      strokeWidth={2.5} dot={{ fill: '#10b981', r: 4 }} activeDot={{ r: 6 }}/>
                    <Line type="monotone" dataKey="expenses" name="Egresos" stroke="#ef4444"
                      strokeWidth={2.5} dot={{ fill: '#ef4444', r: 4 }} activeDot={{ r: 6 }}/>
                    <Line type="monotone" dataKey="staffPayments" name="Nómina" stroke="#f59e0b"
                      strokeWidth={2} strokeDasharray="5 3" dot={{ fill: '#f59e0b', r: 3 }} activeDot={{ r: 5 }}/>
                    <Line type="monotone" dataKey="netBalance" name="Balance" stroke="#6366f1"
                      strokeWidth={2.5} dot={{ fill: '#6366f1', r: 4 }} activeDot={{ r: 6 }}/>
                  </LineChart>
                </ResponsiveContainer>
              )}
            </div>

            {/* Pie Chart — 1/3 width */}
            <div className="card p-6">
              <h4 className="font-bold text-white mb-5 flex items-center gap-2">
                <DollarSign size={18} className="text-primary"/> Distribución Egresos
              </h4>
              {pieData.length === 0 ? (
                <div className="flex items-center justify-center h-56 text-text-muted text-sm text-center">
                  <div>
                    <p className="text-4xl mb-2">📊</p>
                    <p>Sin egresos registrados<br/>en este período</p>
                  </div>
                </div>
              ) : (
                <>
                  <ResponsiveContainer width="100%" height={200}>
                    <PieChart>
                      <Pie
                        data={pieData}
                        cx="50%" cy="50%"
                        innerRadius={55} outerRadius={85}
                        paddingAngle={3}
                        dataKey="value"
                      >
                        {pieData.map((_, i) => (
                          <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]}/>
                        ))}
                      </Pie>
                      <Tooltip formatter={(val) => `S/ ${val.toFixed(2)}`}
                        contentStyle={{ background: 'rgba(15,15,35,0.95)', border: '1px solid rgba(255,255,255,0.1)', borderRadius: 10 }}
                        labelStyle={{ color: '#a0aec0' }} itemStyle={{ color: '#fff' }}/>
                    </PieChart>
                  </ResponsiveContainer>
                  <div className="space-y-2 mt-2">
                    {pieData.map((item, i) => (
                      <div key={i} className="flex items-center justify-between text-xs">
                        <div className="flex items-center gap-2">
                          <span className="w-2.5 h-2.5 rounded-full shrink-0"
                            style={{ background: PIE_COLORS[i % PIE_COLORS.length] }}/>
                          <span className="text-text-secondary">{item.name}</span>
                        </div>
                        <span className="text-white font-semibold">S/ {item.value.toFixed(2)}</span>
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>
          </div>

          {/* ── Bar Chart: Monthly Comparison ── */}
          {trendData.length > 0 && (
            <div className="card p-6">
              <h4 className="font-bold text-white mb-5 flex items-center gap-2">
                <BarChart3 size={18} className="text-primary"/> Comparativa Mensual (Ingresos vs Egresos totales)
              </h4>
              <ResponsiveContainer width="100%" height={220}>
                <BarChart
                  data={trendData.map(d => ({
                    ...d,
                    totalEgresos: d.expenses + d.staffPayments
                  }))}
                  margin={{ top: 5, right: 10, left: 0, bottom: 5 }}
                  barGap={4}
                  barCategoryGap="30%"
                >
                  <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.06)" vertical={false}/>
                  <XAxis dataKey="label" tick={{ fill: '#718096', fontSize: 12 }} axisLine={false} tickLine={false}/>
                  <YAxis tick={{ fill: '#718096', fontSize: 12 }} axisLine={false} tickLine={false}
                    tickFormatter={v => `S/${v}`}/>
                  <Tooltip content={<CustomTooltip />}/>
                  <Legend wrapperStyle={{ fontSize: 12, paddingTop: 12 }}/>
                  <Bar dataKey="income" name="Ingresos" fill="#10b981" radius={[6,6,0,0]}/>
                  <Bar dataKey="totalEgresos" name="Total Egresos" fill="#ef4444" radius={[6,6,0,0]}/>
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}
        </>
      )}

      {/* ── Modal: Metas Financieras ── */}
      {showGoalModal && (
        <div className="modal-overlay" onClick={() => setShowGoalModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 440 }}>
            <div className="flex justify-between items-center mb-5 border-b border-border pb-3">
              <h3 className="modal-title m-0 flex items-center gap-2"><span className="text-xl">🎯</span> {goal ? 'Editar Metas' : 'Nuevas Metas'}</h3>
              <button onClick={() => setShowGoalModal(false)} className="btn btn-ghost btn-sm"><X size={16}/></button>
            </div>
            <p className="text-sm text-text-secondary mb-4">
              Establece tus objetivos financieros para <strong>{MONTHS[month-1]} {year}</strong>.
            </p>
            <form onSubmit={handleSaveGoal}>
              <div className="form-group">
                <label className="form-label">Meta de Ingresos (S/)</label>
                <input required type="number" step="0.01" min="0" className="form-control"
                  placeholder="Ej. 15000"
                  value={goalForm.targetIncome} onChange={e => setGoalForm({...goalForm, targetIncome: e.target.value})}/>
              </div>
              <div className="form-group mt-3">
                <label className="form-label">Meta de Rentabilidad Neta (S/)</label>
                <input required type="number" step="0.01" min="0" className="form-control"
                  placeholder="Ej. 5000"
                  value={goalForm.targetProfit} onChange={e => setGoalForm({...goalForm, targetProfit: e.target.value})}/>
                <p className="text-xs text-text-muted mt-1">Es lo que esperas ganar libres después de pagar egresos y nómina.</p>
              </div>
              <div className="modal-footer mt-5 flex justify-end gap-2">
                <button type="button" onClick={() => setShowGoalModal(false)} className="btn btn-ghost text-danger">Cancelar</button>
                <button type="submit" className="btn btn-primary">Guardar Metas</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── Modal: Cerrar Mes ── */}
      {showClosingModal && (
        <div className="modal-overlay" onClick={() => setShowClosingModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 460 }}>
            <div className="flex justify-between items-center mb-5 border-b border-border pb-3">
              <h3 className="modal-title m-0 flex items-center gap-2"><span className="text-xl">🔒</span> Confirmar Cierre Mensual</h3>
              <button onClick={() => setShowClosingModal(false)} className="btn btn-ghost btn-sm"><X size={16}/></button>
            </div>
            
            <div className="bg-warning/10 border border-warning/30 p-4 rounded-lg mb-4">
              <p className="text-warning text-sm font-semibold mb-1">¡Atención! Acción irreversible</p>
              <p className="text-xs text-warning/80">
                Al cerrar el mes, los ingresos, egresos, y pagos de nómina de <strong>{MONTHS[month-1]} {year}</strong> quedarán congelados para auditoría. No se podrán modificar o eliminar transacciones de este periodo.
              </p>
            </div>

            {summary && (
              <div className="bg-bg-dark rounded-xl p-4 mb-4 text-sm border border-border/50">
                <div className="flex justify-between mb-2"><span className="text-text-secondary">Ingresos:</span> <span className="text-success font-bold">S/ {summary.totalIncome.toFixed(2)}</span></div>
                <div className="flex justify-between mb-2"><span className="text-text-secondary">Egresos:</span> <span className="text-danger font-bold">S/ {summary.totalExpenses.toFixed(2)}</span></div>
                <div className="flex justify-between mb-2"><span className="text-text-secondary">Nómina:</span> <span className="text-warning font-bold">S/ {summary.totalStaffPayments.toFixed(2)}</span></div>
                <div className="flex justify-between pt-2 border-t border-border/50 mt-2">
                  <span className="text-text-main font-semibold">Rentabilidad Neta:</span> 
                  <span className={`font-bold ${summary.netBalance >= 0 ? 'text-primary-400' : 'text-danger'}`}>S/ {summary.netBalance.toFixed(2)}</span>
                </div>
              </div>
            )}

            <form onSubmit={handleCloseMonth}>
              <div className="form-group">
                <label className="form-label">Notas de Cierre (Opcional)</label>
                <textarea className="form-control" rows={3} placeholder="Añade algún comentario para la auditoría..."
                  value={closingNotes} onChange={e => setClosingNotes(e.target.value)}></textarea>
              </div>

              <div className="modal-footer mt-5 flex justify-end gap-2">
                <button type="button" onClick={() => setShowClosingModal(false)} className="btn btn-ghost">Cancelar</button>
                <button type="submit" className="btn btn-danger flex items-center gap-2"><ArrowUpRight size={15}/> Cerrar Mes Definitivamente</button>
              </div>
            </form>
          </div>
        </div>
      )}

    </div>
  );
}
