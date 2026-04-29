import React, { useState, useEffect } from 'react';
import api from '../../api/axios';
import { BarChart3, TrendingUp, TrendingDown, Users, DollarSign, ArrowUpRight, ArrowDownRight } from 'lucide-react';
import toast from 'react-hot-toast';

const MONTHS = [
  'Enero','Febrero','Marzo','Abril','Mayo','Junio',
  'Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'
];

const EXPENSE_TYPE_LABELS = {
  'Operativo': '⚙️ Operativo',
  'Marketing': '📣 Marketing',
  'Equipamiento': '🏋️ Equipamiento',
  'Alquiler': '🏠 Alquiler',
  'Otro': '📋 Otro',
};

export default function FinanceDashboardTab() {
  const now = new Date();
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [year, setYear] = useState(now.getFullYear());
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => { fetchSummary(); }, [month, year]);

  const fetchSummary = async () => {
    try {
      setLoading(true);
      const { data } = await api.get(`/finances-premium/summary?month=${month}&year=${year}`);
      setSummary(data);
    } catch { toast.error('Error al cargar el resumen financiero'); }
    finally { setLoading(false); }
  };

  const isProfit = summary ? summary.netBalance >= 0 : true;

  return (
    <div className="fade-in space-y-6">
      {/* Period selector */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-card/40 backdrop-blur-md border border-border/50 p-4 rounded-xl">
        <h3 className="text-xl font-bold text-white flex items-center gap-2">
          <BarChart3 className="text-primary" size={20}/> Resumen Financiero
        </h3>
        <div className="flex items-center gap-3">
          <select className="form-control text-sm py-2 w-auto" value={month} onChange={e => setMonth(+e.target.value)}>
            {MONTHS.map((m, i) => <option key={i+1} value={i+1}>{m}</option>)}
          </select>
          <select className="form-control text-sm py-2 w-auto" value={year} onChange={e => setYear(+e.target.value)}>
            {[2024,2025,2026,2027].map(y => <option key={y} value={y}>{y}</option>)}
          </select>
        </div>
      </div>

      {loading ? (
        <div className="text-center py-16 text-text-muted">Calculando resumen...</div>
      ) : !summary ? null : (
        <>
          {/* KPI Cards */}
          <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
            {/* Ingresos */}
            <div className="card p-5 border-l-4 border-l-success">
              <div className="flex justify-between items-start">
                <div>
                  <p className="text-xs text-text-muted uppercase tracking-wide font-medium">Ingresos Cobrados</p>
                  <p className="text-2xl font-bold text-success mt-1">S/ {summary.totalIncome.toFixed(2)}</p>
                </div>
                <div className="p-2 bg-success/20 rounded-lg text-success"><TrendingUp size={20}/></div>
              </div>
              <p className="text-xs text-text-muted mt-3">Mensualidades cobradas en {MONTHS[month-1]}</p>
            </div>

            {/* Egresos */}
            <div className="card p-5 border-l-4 border-l-danger">
              <div className="flex justify-between items-start">
                <div>
                  <p className="text-xs text-text-muted uppercase tracking-wide font-medium">Egresos Generales</p>
                  <p className="text-2xl font-bold text-danger mt-1">S/ {summary.totalExpenses.toFixed(2)}</p>
                </div>
                <div className="p-2 bg-danger/20 rounded-lg text-danger"><TrendingDown size={20}/></div>
              </div>
              <p className="text-xs text-text-muted mt-3">Gastos operativos del período</p>
            </div>

            {/* Nómina */}
            <div className="card p-5 border-l-4 border-l-warning">
              <div className="flex justify-between items-start">
                <div>
                  <p className="text-xs text-text-muted uppercase tracking-wide font-medium">Nómina Pagada</p>
                  <p className="text-2xl font-bold text-warning mt-1">S/ {summary.totalStaffPayments.toFixed(2)}</p>
                </div>
                <div className="p-2 bg-warning/20 rounded-lg text-warning"><Users size={20}/></div>
              </div>
              <p className="text-xs text-text-muted mt-3">Pagos al personal confirmados</p>
            </div>

            {/* Balance Neto */}
            <div className={`card p-5 border-l-4 ${isProfit ? 'border-l-primary' : 'border-l-danger'}`}>
              <div className="flex justify-between items-start">
                <div>
                  <p className="text-xs text-text-muted uppercase tracking-wide font-medium">Balance Neto</p>
                  <p className={`text-2xl font-bold mt-1 ${isProfit ? 'text-primary-400' : 'text-danger'}`}>
                    S/ {summary.netBalance.toFixed(2)}
                  </p>
                </div>
                <div className={`p-2 rounded-lg ${isProfit ? 'bg-primary/20 text-primary-400' : 'bg-danger/20 text-danger'}`}>
                  {isProfit ? <ArrowUpRight size={20}/> : <ArrowDownRight size={20}/>}
                </div>
              </div>
              <p className={`text-xs mt-3 font-medium ${isProfit ? 'text-success' : 'text-danger'}`}>
                {isProfit ? '✅ Mes con superávit' : '⚠️ Mes con déficit'}
              </p>
            </div>
          </div>

          {/* Visual P&L Bar */}
          <div className="card p-6">
            <h4 className="font-bold text-white mb-4 flex items-center gap-2">
              <BarChart3 size={18} className="text-primary"/> Ganancias y Pérdidas del Período
            </h4>
            <div className="space-y-3">
              {/* Income bar */}
              <div>
                <div className="flex justify-between text-sm mb-1">
                  <span className="text-text-secondary">Ingresos</span>
                  <span className="text-success font-bold">S/ {summary.totalIncome.toFixed(2)}</span>
                </div>
                <div className="w-full bg-bg-dark rounded-full h-4 overflow-hidden">
                  <div className="h-4 rounded-full bg-success transition-all" style={{ width: '100%' }}/>
                </div>
              </div>

              {/* Expenses bar relative to income */}
              {[
                { label: 'Egresos Generales', amount: summary.totalExpenses, color: 'bg-danger' },
                { label: 'Nómina', amount: summary.totalStaffPayments, color: 'bg-warning' },
              ].map(item => {
                const pct = summary.totalIncome > 0
                  ? Math.min(100, Math.round((item.amount / summary.totalIncome) * 100))
                  : 0;
                return (
                  <div key={item.label}>
                    <div className="flex justify-between text-sm mb-1">
                      <span className="text-text-secondary">{item.label}</span>
                      <span className="font-bold text-white">{pct}% — S/ {item.amount.toFixed(2)}</span>
                    </div>
                    <div className="w-full bg-bg-dark rounded-full h-4 overflow-hidden">
                      <div className={`h-4 rounded-full transition-all ${item.color}`} style={{ width: `${pct}%` }}/>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Expenses by category */}
          {summary.expensesByCategory?.length > 0 && (
            <div className="card p-6">
              <h4 className="font-bold text-white mb-4 flex items-center gap-2">
                <DollarSign size={18} className="text-primary"/> Egresos por Categoría
              </h4>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
                {summary.expensesByCategory.map(cat => (
                  <div key={cat.category} className="bg-bg-dark border border-border/50 rounded-xl p-4 flex justify-between items-center">
                    <span className="text-sm text-text-secondary">
                      {EXPENSE_TYPE_LABELS[cat.category] || cat.category}
                    </span>
                    <span className="text-white font-bold text-sm">S/ {cat.total.toFixed(2)}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
