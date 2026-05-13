import React, { useState, useEffect, useCallback, Fragment } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import {
  DollarSign, Settings, Play, AlertTriangle, CheckCircle, Search,
  Calendar, FileText, RefreshCw, Ban, CreditCard, ChevronDown, ChevronUp, X, CalendarPlus, Download,
  TrendingDown, BarChart3, Archive, Users
} from 'lucide-react';
import toast from 'react-hot-toast';
import { generateReceiptPDF } from '../../utils/pdfGenerator';
import ExpensesTab from '../../components/finances/ExpensesTab';
import PettyCashTab from '../../components/finances/PettyCashTab';
import StaffPaymentsTab from '../../components/finances/StaffPaymentsTab';
import FinanceDashboardTab from '../../components/finances/FinanceDashboardTab';

const PAYMENT_METHODS = [
  { value: 'Cash', label: '💵 Efectivo' },
  { value: 'BankTransfer', label: '🏦 Transferencia' },
  { value: 'Yape', label: '📱 Yape' },
  { value: 'Plin', label: '📱 Plin' },
  { value: 'Card', label: '💳 Tarjeta' },
  { value: 'Other', label: '🔹 Otro' },
];

const EXCLUSION_TYPES = [
  { value: 'FreeClass', label: '🎁 Clase Gratuita' },
  { value: 'Recovery', label: '🔄 Recuperación' },
  { value: 'Suspension', label: '⏸️ Suspensión' },
  { value: 'OtherExclusion', label: '📋 Otro' },
];

const statusBadge = (d) => {
  if (d.isPaid) return <span className="badge badge-success">Pagado</span>;
  if (d.status === 'Vencido') return <span className="badge badge-danger flex items-center gap-1 w-max"><AlertTriangle size={11}/> Vencido {d.daysOverdue}d</span>;
  return <span className="badge badge-warning">En Curso</span>;
};

export default function Finanzas() {
  const [activeTab, setActiveTab] = useState('debts');
  const [showAll, setShowAll] = useState(false);
  const [config, setConfig] = useState({ defaultPaymentDay: 5 });
  const [debts, setDebts] = useState([]);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);

  // Filtro mes/año para cobranzas
  const today = new Date();
  const [filterYear,  setFilterYear]  = useState(today.getFullYear());
  const [filterMonth, setFilterMonth] = useState(today.getMonth() + 1); // 1-based

  // Mes/año objetivo para el motor de generación
  const [motorYear,  setMotorYear]  = useState(today.getFullYear());
  const [motorMonth, setMotorMonth] = useState(today.getMonth() + 1);

  // Modal state
  const [modal, setModal] = useState(null); // null | 'pay' | 'recalculate' | 'exclude'
  const [selected, setSelected] = useState(null);
  const [expandedId, setExpandedId] = useState(null);

  // Pay form
  const [payForm, setPayForm] = useState({ amountPaid: '', method: 'Cash', operationNumber: '', voucherUrl: '', notes: '' });
  // Recalc form
  const [recalcForm, setRecalcForm] = useState({ newAmount: '', proratedStartDate: '', notes: '' });
  // Exclude form
  const [excludeForm, setExcludeForm] = useState({ exclusionType: 'FreeClass', exclusionNote: '' });

  const fetchData = useCallback(async () => {
    try {
      const endpoint = showAll
        ? `/finances/debts/all?year=${filterYear}&month=${filterMonth}`
        : `/finances/debts/pending?year=${filterYear}&month=${filterMonth}`;
      const [cRes, dRes] = await Promise.all([api.get('/finances/config'), api.get(endpoint)]);
      setConfig(cRes.data);
      setDebts(dRes.data);
    } catch { toast.error('Error al cargar datos financieros.'); }
    finally { setLoading(false); }
  }, [showAll, filterYear, filterMonth]);

  useEffect(() => { fetchData(); }, [fetchData]);

  const handleConfigSubmit = async (e) => {
    e.preventDefault();
    try { await api.put('/finances/config', config); toast.success('Configuración guardada.'); }
    catch { toast.error('Error al guardar configuración.'); }
  };

  const handleGenerateDebts = async () => {
    setGenerating(true);
    try {
      const r = await api.post('/finances/generate-debts', { year: motorYear, month: motorMonth });
      toast.success(r.data.message);
      // Sync filter to the motor period so the user sees the result
      setFilterYear(motorYear);
      setFilterMonth(motorMonth);
      fetchData();
    }
    catch { toast.error('Error al ejecutar el motor.'); }
    finally { setGenerating(false); }
  };

  // ── Open modals ──────────────────────────────────────────────
  const openPay = (d) => {
    setSelected(d);
    setPayForm({ amountPaid: (d.amount - d.amountPaid).toFixed(2), method: 'Cash', operationNumber: '', voucherUrl: '', notes: '' });
    setModal('pay');
  };
  const openRecalc = (d) => {
    setSelected(d);
    setRecalcForm({ newAmount: d.amount.toFixed(2), proratedStartDate: '', notes: '' });
    setModal('recalculate');
  };
  const openExclude = (d) => {
    setSelected(d);
    setExcludeForm({ exclusionType: 'FreeClass', exclusionNote: '' });
    setModal('exclude');
  };

  // ── Submit handlers ──────────────────────────────────────────
  const handlePay = async (e) => {
    e.preventDefault();
    try {
      const res = await api.post('/finances/debts/pay', {
        paymentRecordId: selected.id,
        amountPaid: parseFloat(payForm.amountPaid),
        method: payForm.method,
        operationNumber: payForm.operationNumber || null,
        voucherUrl: payForm.voucherUrl || null,
        notes: payForm.notes || null,
      });
      toast.success('Pago registrado.');
      setModal(null);

      // Receipt number comes from the payment response (stored in DB)
      const lastInstallment = res.data?.installments?.slice(-1)[0];
      const receiptNumber = lastInstallment?.receiptNumber;
      generateReceiptPDF({
        receiptNumber,
        customerName: selected.studentName,
        description: selected.description,
        quantity: 1,
        total: parseFloat(payForm.amountPaid),
        notes: payForm.notes || 'Ninguna'
      });

      fetchData();
    } catch (err) { toast.error(err.response?.data?.message || 'Error al registrar pago.'); }
  };

  const handleRecalc = async (e) => {
    e.preventDefault();
    try {
      await api.post('/finances/debts/recalculate', {
        paymentRecordId: selected.id,
        newAmount: recalcForm.newAmount ? parseFloat(recalcForm.newAmount) : null,
        proratedStartDate: recalcForm.proratedStartDate || null,
        notes: recalcForm.notes || null,
      });
      toast.success('Monto recalculado.');
      setModal(null);
      fetchData();
    } catch (err) { toast.error(err.response?.data?.message || 'Error al recalcular.'); }
  };

  const handleExclude = async (e) => {
    e.preventDefault();
    try {
      await api.post('/finances/debts/exclude', {
        paymentRecordId: selected.id,
        exclusionType: excludeForm.exclusionType,
        exclusionNote: excludeForm.exclusionNote || null,
      });
      toast.success('Cobro exonerado.');
      setModal(null);
      fetchData();
    } catch (err) { toast.error(err.response?.data?.message || 'Error al exonerar.'); }
  };

  const handleGenerateNextMonth = async (d) => {
    try {
      await api.post(`/finances/debts/${d.studentId}/generate-next-month`);
      toast.success(`Mensualidad del mes siguiente generada para ${d.studentName}.`);
      fetchData();
    } catch (err) { toast.error(err.response?.data?.message || 'Error al generar mes siguiente.'); }
  };

  const regenerateDebtReceipt = (d) => {
    // Use the stored receipt number from the last installment (no new counter increment)
    const lastInstallment = d.installments?.slice(-1)[0];
    const receiptNumber = lastInstallment?.receiptNumber;
    generateReceiptPDF({
      receiptNumber,
      customerName: d.studentName,
      description: d.description,
      quantity: 1,
      total: parseFloat(d.amountPaid || d.amount),
      notes: 'Copia de Recibo - Mensualidad/Cobro'
    });
  };

  const filtered = debts.filter(d =>
    d.studentName?.toLowerCase().includes(search.toLowerCase()) ||
    d.categoryName?.toLowerCase().includes(search.toLowerCase())
  );

  const overdueDebts   = debts.filter(d => !d.isPaid && d.status === 'Vencido');
  const inCourseDebts  = debts.filter(d => !d.isPaid && d.status === 'En Curso');
  const totalPending   = debts.reduce((s, d) => s + (d.amount - d.amountPaid), 0);

  if (loading) return <AppLayout title="Finanzas"><div className="text-center p-8"><span className="spinner" style={{borderColor:'var(--primary)',borderTopColor:'transparent'}}></span></div></AppLayout>;

  return (
    <AppLayout title="Gestión Financiera y Cobranzas">
      <div className="fade-in">

        {/* Tabs */}
        <div className="flex gap-4 mb-6 border-b border-border/50 pb-2 overflow-x-auto whitespace-nowrap">
          <button className={`flex items-center gap-2 pb-2 px-1 border-b-2 transition-colors shrink-0 ${activeTab==='dashboard'?'border-primary text-primary font-bold':'border-transparent text-text-muted hover:text-text-main'}`} onClick={()=>setActiveTab('dashboard')}>
            <BarChart3 size={18}/> Resumen P&amp;L
          </button>
          <button className={`flex items-center gap-2 pb-2 px-1 border-b-2 transition-colors shrink-0 ${activeTab==='debts'?'border-primary text-primary font-bold':'border-transparent text-text-muted hover:text-text-main'}`} onClick={()=>setActiveTab('debts')}>
            <DollarSign size={18}/> Cobranzas
          </button>
          <button className={`flex items-center gap-2 pb-2 px-1 border-b-2 transition-colors shrink-0 ${activeTab==='expenses'?'border-primary text-primary font-bold':'border-transparent text-text-muted hover:text-text-main'}`} onClick={()=>setActiveTab('expenses')}>
            <TrendingDown size={18}/> Egresos
          </button>
          <button className={`flex items-center gap-2 pb-2 px-1 border-b-2 transition-colors shrink-0 ${activeTab==='petty'?'border-primary text-primary font-bold':'border-transparent text-text-muted hover:text-text-main'}`} onClick={()=>setActiveTab('petty')}>
            <Archive size={18}/> Caja Chica
          </button>
          <button className={`flex items-center gap-2 pb-2 px-1 border-b-2 transition-colors shrink-0 ${activeTab==='staff'?'border-primary text-primary font-bold':'border-transparent text-text-muted hover:text-text-main'}`} onClick={()=>setActiveTab('staff')}>
            <Users size={18}/> Nómina
          </button>
          <button className={`flex items-center gap-2 pb-2 px-1 border-b-2 transition-colors shrink-0 ${activeTab==='config'?'border-primary text-primary font-bold':'border-transparent text-text-muted hover:text-text-main'}`} onClick={()=>setActiveTab('config')}>
            <Settings size={18}/> Configuración
          </button>
        </div>

        {/* DEBTS TAB */}
        {activeTab === 'debts' && (
          <div className="fade-in">
            {/* Stats */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
              <div className="card p-4 flex items-center gap-4 bg-danger/10 border-danger/30">
                <div className="p-3 bg-danger/20 rounded-full text-danger"><AlertTriangle size={22}/></div>
                <div><h4 className="text-danger font-bold">En Mora</h4><p className="text-sm text-text-secondary">{overdueDebts.length} registros vencidos</p></div>
              </div>
              <div className="card p-4 flex items-center gap-4 bg-warning/10 border-warning/30">
                <div className="p-3 bg-warning/20 rounded-full text-warning"><Calendar size={22}/></div>
                <div><h4 className="text-warning font-bold">En Curso</h4><p className="text-sm text-text-secondary">{inCourseDebts.length} pendientes por vencer</p></div>
              </div>
              <div className="card p-4 flex items-center gap-4 bg-primary/10 border-primary/30">
                <div className="p-3 bg-primary/20 rounded-full text-primary"><DollarSign size={22}/></div>
                <div><h4 className="text-primary font-bold">Total por Cobrar</h4><p className="text-sm text-text-secondary">S/. {totalPending.toFixed(2)}</p></div>
              </div>
            </div>

            <div className="card">
              <div className="card-header border-b border-border/50 pb-4 mb-4 flex-wrap gap-3">
                <div className="flex items-center gap-3">
                  <h3 className="card-title">Cuentas por Cobrar</h3>
                  <button onClick={()=>setShowAll(p=>!p)} className={`btn btn-sm ${showAll?'btn-primary':'btn-ghost'}`}>
                    {showAll ? 'Ver Pendientes' : 'Ver Todos'}
                  </button>
                </div>
                <div className="flex items-center gap-3 flex-wrap">
                  <select
                    className="form-control text-sm py-2 w-auto"
                    value={filterMonth}
                    onChange={e => setFilterMonth(parseInt(e.target.value))}
                  >
                    {['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre']
                      .map((m, i) => <option key={i+1} value={i+1}>{m}</option>)}
                  </select>
                  <select
                    className="form-control text-sm py-2 w-auto"
                    value={filterYear}
                    onChange={e => setFilterYear(parseInt(e.target.value))}
                  >
                    {[2024, 2025, 2026, 2027].map(y => <option key={y} value={y}>{y}</option>)}
                  </select>
                  <div className="relative flex items-center">
                    <Search size={15} className="absolute left-3 text-text-muted"/>
                    <input type="text" placeholder="Buscar alumno..." className="form-control pl-9 py-2 text-sm w-48"
                      value={search} onChange={e=>setSearch(e.target.value)}/>
                  </div>
                </div>
              </div>

              <div className="table-container">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Estado</th>
                      <th>Alumno</th>
                      <th>Categoría</th>
                      <th>Concepto</th>
                      <th>Venc.</th>
                      <th>Total</th>
                      <th>Pagado</th>
                      <th>Pendiente</th>
                      <th className="text-right">Acciones</th>
                    </tr>
                  </thead>
                  <tbody>
                    {Object.values(filtered.reduce((acc, d) => {
                      const key = d.studentId || d.studentName;
                      if (!acc[key]) acc[key] = { studentName: d.studentName, totalPending: 0, items: [] };
                      acc[key].items.push(d);
                      acc[key].totalPending += d.amountPending;
                      return acc;
                    }, {})).sort((a,b) => b.totalPending - a.totalPending).map((group, gIdx) => (
                      <Fragment key={gIdx}>
                        {/* Fila de resumen del alumno */}
                        <tr className="bg-primary/10 border-t-2 border-primary/20">
                          <td colSpan={7} className="font-bold text-primary-100 p-3">
                            <Users size={15} className="inline mr-2 mb-1"/>
                            Resumen de Deuda Total: {group.studentName}
                          </td>
                          <td className="font-bold text-danger p-3 text-lg">
                            S/. {group.totalPending.toFixed(2)}
                          </td>
                          <td></td>
                        </tr>
                        {/* Detalles de la deuda */}
                        {group.items.map(d => (
                          <React.Fragment key={d.id}>
                            <tr className={`${d.status==='Vencido'&&!d.isPaid?'bg-danger/5':''} cursor-pointer`}>
                              <td>{statusBadge(d)}</td>
                              <td className="font-medium text-white pl-6">
                                ↳ {d.categoryName ? d.categoryName : 'Concepto individual'}
                                {d.isProrated && <span className="ml-1 text-xs text-warning bg-warning/10 px-1 rounded">Prorr.</span>}
                                {d.exclusionType !== 'None' && <span className="ml-1 text-xs text-success bg-success/10 px-1 rounded">Exon.</span>}
                              </td>
                              <td className="text-sm">{d.categoryName}</td>
                              <td>
                                <div className="flex items-center gap-1">
                                  <FileText size={13} className="text-primary-400"/>
                                  <span className="text-sm">{d.description}</span>
                                </div>
                                {d.isProrated && (
                                  <div className="text-xs text-warning/80 mt-0.5">
                                    Desde día {d.proratedStartDate ? new Date(d.proratedStartDate).toLocaleDateString('es-PE', { timeZone: 'UTC', day: 'numeric' }) : '?'} — {d.proratedDaysCharged}/{d.proratedTotalDays} sesiones
                                  </div>
                                )}
                              </td>
                              <td className="text-sm">{new Date(d.dueDate).toLocaleDateString('es-PE', { timeZone: 'UTC' })}</td>
                              <td className="font-bold">S/. {d.amount.toFixed(2)}</td>
                              <td className="text-success font-medium">S/. {d.amountPaid.toFixed(2)}</td>
                              <td className={`font-bold ${d.amountPending > 0 ? 'text-danger' : 'text-success'}`}>
                                S/. {d.amountPending.toFixed(2)}
                              </td>
                              <td className="text-right">
                                <div className="flex items-center justify-end gap-1">
                                  {!d.isPaid && (
                                    <>
                                      <button onClick={()=>openPay(d)} className="btn btn-sm btn-success flex items-center gap-1" title="Registrar Pago">
                                        <CreditCard size={13}/> Cobrar
                                      </button>
                                      <button onClick={()=>openRecalc(d)} className="btn btn-sm btn-ghost text-warning flex items-center gap-1" title="Recalcular">
                                        <RefreshCw size={13}/>
                                      </button>
                                      <button onClick={()=>openExclude(d)} className="btn btn-sm btn-ghost text-text-muted flex items-center gap-1" title="Exonerar">
                                        <Ban size={13}/>
                                      </button>
                                    </>
                                  )}
                                  {d.isPaid && (
                                    <>
                                      <button onClick={()=>regenerateDebtReceipt(d)}
                                        className="btn btn-sm btn-ghost text-success flex items-center gap-1"
                                        title="Volver a descargar recibo">
                                        <Download size={13}/>
                                      </button>
                                      <button onClick={()=>handleGenerateNextMonth(d)}
                                        className="btn btn-sm btn-ghost text-primary flex items-center gap-1"
                                        title="Generar mensualidad del mes siguiente">
                                        <CalendarPlus size={13}/>
                                      </button>
                                    </>
                                  )}
                                  {d.installments?.length > 0 && (
                                    <button onClick={()=>setExpandedId(expandedId===d.id?null:d.id)} className="btn btn-sm btn-ghost text-primary-400" title="Ver pagos">
                                      {expandedId===d.id ? <ChevronUp size={13}/> : <ChevronDown size={13}/>}
                                    </button>
                                  )}
                                </div>
                              </td>
                            </tr>
                            {expandedId === d.id && d.installments?.length > 0 && (
                              <tr key={`${d.id}-detail`} className="bg-bg-dark/50">
                                <td colSpan={9} className="p-4">
                                  <p className="text-xs text-text-muted font-bold mb-2 uppercase tracking-wide">Historial de pagos parciales</p>
                                  <div className="flex flex-col gap-1">
                                    {d.installments.map(i => (
                                      <div key={i.id} className="flex items-center gap-3 text-sm bg-bg-surface rounded p-2">
                                        <span className="text-success font-bold">S/. {i.amountPaid.toFixed(2)}</span>
                                        <span className="text-text-muted">{new Date(i.paidAt).toLocaleDateString('es-PE')} {new Date(i.paidAt).toLocaleTimeString('es-PE',{hour:'2-digit',minute:'2-digit'})}</span>
                                        <span className="badge badge-primary text-xs">{PAYMENT_METHODS.find(m=>m.value===i.method)?.label || i.method}</span>
                                        {i.operationNumber && <span className="text-text-muted text-xs">Op: {i.operationNumber}</span>}
                                        {i.notes && <span className="text-text-muted text-xs italic">{i.notes}</span>}
                                        {i.voucherUrl && <a href={i.voucherUrl} target="_blank" rel="noreferrer" className="text-primary-400 text-xs underline">Ver voucher</a>}
                                      </div>
                                    ))}
                                  </div>
                                </td>
                              </tr>
                            )}
                          </React.Fragment>
                        ))}
                      </Fragment>
                    ))}
                  </tbody>
                </table>
              </div>
              {filtered.length === 0 && <div className="empty-state"><CheckCircle size={40} className="text-success"/><p>No hay registros.</p></div>}
            </div>
          </div>
        )}

        {/* CONFIG TAB */}
        {activeTab === 'config' && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 fade-in">
            <div className="card">
              <div className="card-header border-b border-border/50 pb-4 mb-4"><h3 className="card-title">Configuración del Ciclo</h3></div>
              <form onSubmit={handleConfigSubmit}>
                <div className="form-group">
                  <label className="form-label">Día de Cobro Mensual Estándar</label>
                  <p className="text-sm text-text-muted mb-2">Las mensualidades vencerán automáticamente este día de cada mes.</p>
                  <select className="form-control" value={config.defaultPaymentDay} onChange={e=>setConfig({...config,defaultPaymentDay:parseInt(e.target.value)})}>
                    {[...Array(31).keys()].map(i=><option key={i+1} value={i+1}>Día {i+1}</option>)}
                  </select>
                </div>
                <button type="submit" className="btn btn-primary mt-2">Guardar Configuración</button>
              </form>
            </div>
            <div className="card border-primary/30">
              <div className="card-header border-b border-primary/20 pb-4 mb-4"><h3 className="card-title text-primary-400">Motor de Generación</h3></div>
              <p className="text-sm text-text-secondary mb-3">Genera deudas mensuales para todos los alumnos activos. Calcula prorrateo automático si el alumno inició a mitad de mes.</p>

              {/* Selector mes/año para el motor */}
              <div className="mb-4">
                <label className="form-label flex items-center gap-2 mb-2">
                  <Calendar size={14} className="text-primary-400"/> Período objetivo
                </label>
                <div className="flex flex-wrap items-center gap-3">
                  <select
                    className="form-control text-sm py-2 w-auto"
                    value={motorMonth}
                    onChange={e => setMotorMonth(parseInt(e.target.value))}
                  >
                    {['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre']
                      .map((m, i) => <option key={i+1} value={i+1}>{m}</option>)}
                  </select>
                  <select
                    className="form-control text-sm py-2 w-auto"
                    value={motorYear}
                    onChange={e => setMotorYear(parseInt(e.target.value))}
                  >
                    {[2024, 2025, 2026, 2027].map(y => <option key={y} value={y}>{y}</option>)}
                  </select>
                </div>
              </div>

              <div className="bg-bg-dark p-4 rounded border border-border mb-4">
                <ul className="text-sm text-text-muted list-disc pl-4 space-y-1">
                  <li>Respeta exoneraciones (Becados, Invitados).</li>
                  <li><b className="text-warning">Reemplaza</b> cargos no pagados si ya existen para ese mes.</li>
                  <li><b className="text-success">Nunca sobreescribe</b> un cobro ya pagado.</li>
                  <li>Aplica montos preferenciales si existen.</li>
                  <li>Calcula <b>prorrateo automático</b> si el alumno inició después del día 1.</li>
                  <li>Estado <b>En Curso</b> = antes de vencer · <b>Vencido</b> = superó la fecha.</li>
                </ul>
              </div>
              <button onClick={handleGenerateDebts} disabled={generating} className="btn btn-primary w-full flex justify-center items-center gap-2">
                {generating ? <span className="spinner w-4 h-4"></span> : <Play size={16}/>}
                {generating ? 'Generando...' : `Ejecutar Motor — ${['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'][motorMonth-1]} ${motorYear}`}
              </button>
            </div>
          </div>
        )}

        {/* EXPENSES TAB */}
        {activeTab === 'expenses' && (
          <ExpensesTab />
        )}

        {/* PETTY CASH TAB */}
        {activeTab === 'petty' && (
          <PettyCashTab />
        )}

        {/* STAFF PAYMENTS TAB */}
        {activeTab === 'staff' && (
          <StaffPaymentsTab />
        )}

        {/* DASHBOARD / P&L TAB */}
        {activeTab === 'dashboard' && (
          <FinanceDashboardTab />
        )}
      </div>

      {/* ── MODAL: REGISTRAR PAGO ── */}
      {modal === 'pay' && selected && (
        <div className="modal-overlay" onClick={()=>setModal(null)}>
          <div className="modal" onClick={e=>e.stopPropagation()} style={{maxWidth:480}}>
            <div className="flex justify-between items-center mb-5 border-b border-border pb-3">
              <h3 className="modal-title m-0 flex items-center gap-2"><CreditCard size={18}/> Registrar Pago</h3>
              <button onClick={()=>setModal(null)} className="btn btn-ghost btn-sm"><X size={16}/></button>
            </div>
            <div className="bg-bg-dark p-3 rounded border border-border mb-4 text-sm">
              <p className="font-bold text-white">{selected.studentName}</p>
              <p className="text-text-muted">{selected.description}</p>
              <div className="flex gap-4 mt-2">
                <span>Total: <b className="text-white">S/. {selected.amount.toFixed(2)}</b></span>
                <span>Pagado: <b className="text-success">S/. {selected.amountPaid.toFixed(2)}</b></span>
                <span>Pendiente: <b className="text-danger">S/. {selected.amountPending.toFixed(2)}</b></span>
              </div>
            </div>
            <form onSubmit={handlePay}>
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label flex justify-between items-center">
                    <span>Monto a Pagar *</span>
                    <span className={`badge ${parseFloat(payForm.amountPaid || 0) < selected.amountPending ? 'badge-warning' : 'badge-success'} text-xs`}>
                      {parseFloat(payForm.amountPaid || 0) < selected.amountPending ? 'Pago Parcial' : 'Pago Total'}
                    </span>
                  </label>
                  <input required type="number" step="0.01" min="0.01" max={selected.amountPending} className="form-control text-lg font-bold"
                    value={payForm.amountPaid} onChange={e=>setPayForm({...payForm,amountPaid:e.target.value})}/>
                  <span className="text-xs text-text-muted mt-1 inline-block">Máx. pendiente: S/. {selected.amountPending.toFixed(2)}</span>
                </div>
                <div className="form-group">
                  <label className="form-label">Medio de Pago *</label>
                  <select required className="form-control" value={payForm.method} onChange={e=>setPayForm({...payForm,method:e.target.value})}>
                    {PAYMENT_METHODS.map(m=><option key={m.value} value={m.value}>{m.label}</option>)}
                  </select>
                </div>
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">N° Operación (Opcional)</label>
                  <input type="text" className="form-control" placeholder="Ej. 123456789"
                    value={payForm.operationNumber} onChange={e=>setPayForm({...payForm,operationNumber:e.target.value})}/>
                </div>
                <div className="form-group">
                  <label className="form-label">URL Voucher (Opcional)</label>
                  <input type="url" className="form-control" placeholder="https://..."
                    value={payForm.voucherUrl} onChange={e=>setPayForm({...payForm,voucherUrl:e.target.value})}/>
                </div>
              </div>
              <div className="form-group">
                <label className="form-label">Nota (Opcional)</label>
                <input type="text" className="form-control" placeholder="Observación..."
                  value={payForm.notes} onChange={e=>setPayForm({...payForm,notes:e.target.value})}/>
              </div>
              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={()=>setModal(null)} className="btn btn-ghost text-danger">Cancelar</button>
                <button type="submit" className="btn btn-success flex items-center gap-2"><CheckCircle size={15}/> Confirmar Pago</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── MODAL: RECALCULAR ── */}
      {modal === 'recalculate' && selected && (
        <div className="modal-overlay" onClick={()=>setModal(null)}>
          <div className="modal" onClick={e=>e.stopPropagation()} style={{maxWidth:440}}>
            <div className="flex justify-between items-center mb-5 border-b border-border pb-3">
              <h3 className="modal-title m-0 flex items-center gap-2"><RefreshCw size={18}/> Recalcular Monto</h3>
              <button onClick={()=>setModal(null)} className="btn btn-ghost btn-sm"><X size={16}/></button>
            </div>
            <div className="bg-bg-dark p-3 rounded border border-border mb-4 text-sm">
              <p className="font-bold text-white">{selected.studentName} — {selected.description}</p>
              <p className="text-text-muted">Monto actual: <b className="text-white">S/. {selected.amount.toFixed(2)}</b></p>
            </div>
            <form onSubmit={handleRecalc}>
              <div className="form-group">
                <label className="form-label">Nuevo Monto Fijo (Opcional)</label>
                <input type="number" step="0.01" min="0" className="form-control"
                  placeholder="Deje vacío para recalc. desde categoría"
                  value={recalcForm.newAmount} onChange={e=>setRecalcForm({...recalcForm,newAmount:e.target.value})}/>
              </div>
              <div className="form-group">
                <label className="form-label">Prorrateo — Fecha de Inicio (Opcional)</label>
                <input type="date" className="form-control"
                  value={recalcForm.proratedStartDate} onChange={e=>setRecalcForm({...recalcForm,proratedStartDate:e.target.value})}/>
                <span className="text-xs text-text-muted mt-1 inline-block">Si el alumno inició a mitad de mes, indique la fecha. El sistema calculará los días proporcionales.</span>
              </div>
              <div className="form-group">
                <label className="form-label">Nota</label>
                <input type="text" className="form-control" value={recalcForm.notes} onChange={e=>setRecalcForm({...recalcForm,notes:e.target.value})}/>
              </div>
              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={()=>setModal(null)} className="btn btn-ghost text-danger">Cancelar</button>
                <button type="submit" className="btn btn-warning flex items-center gap-2"><RefreshCw size={15}/> Recalcular</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── MODAL: EXONERAR ── */}
      {modal === 'exclude' && selected && (
        <div className="modal-overlay" onClick={()=>setModal(null)}>
          <div className="modal" onClick={e=>e.stopPropagation()} style={{maxWidth:420}}>
            <div className="flex justify-between items-center mb-5 border-b border-border pb-3">
              <h3 className="modal-title m-0 flex items-center gap-2"><Ban size={18}/> Exonerar Cobro</h3>
              <button onClick={()=>setModal(null)} className="btn btn-ghost btn-sm"><X size={16}/></button>
            </div>
            <p className="text-sm text-text-secondary mb-4">Marcar este cobro como exonerado reduce el monto a S/. 0.00 y lo marca como pagado. Esta acción indica que el período no se debe cobrar.</p>
            <form onSubmit={handleExclude}>
              <div className="form-group">
                <label className="form-label">Motivo de Exoneración *</label>
                <select required className="form-control" value={excludeForm.exclusionType} onChange={e=>setExcludeForm({...excludeForm,exclusionType:e.target.value})}>
                  {EXCLUSION_TYPES.map(t=><option key={t.value} value={t.value}>{t.label}</option>)}
                </select>
              </div>
              <div className="form-group">
                <label className="form-label">Observación (Opcional)</label>
                <input type="text" className="form-control" placeholder="Detalle del motivo..."
                  value={excludeForm.exclusionNote} onChange={e=>setExcludeForm({...excludeForm,exclusionNote:e.target.value})}/>
              </div>
              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={()=>setModal(null)} className="btn btn-ghost text-danger">Cancelar</button>
                <button type="submit" className="btn btn-primary flex items-center gap-2"><Ban size={15}/> Exonerar</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
