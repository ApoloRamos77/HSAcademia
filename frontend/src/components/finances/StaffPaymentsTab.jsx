import React, { useEffect, useMemo, useState } from 'react';
import api from '../../api/axios';
import { Users, Plus, CheckCircle, Clock, Edit2, Trash2, Eye, RotateCcw, FileText, Wallet, Layers } from 'lucide-react';
import toast from 'react-hot-toast';

const MONTHS = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];
const SCHEMES = [
  { value: 0, label: 'Sueldo fijo', rateLabel: 'Sueldo mensual (S/)' },
  { value: 2, label: 'Por hora', rateLabel: 'Tarifa por hora sin tarifario (S/)' },
  { value: 1, label: 'Por sesión', rateLabel: 'Tarifa por sesión sin tarifario (S/)' },
  { value: 3, label: 'Sueldo fijo + horas', rateLabel: 'Sueldo mensual (S/)' },
];
const FREQUENCIES = [
  { value: 0, label: 'Mensual' }, { value: 1, label: 'Quincenal' }, { value: 2, label: 'Semanal' }, { value: 3, label: 'Diario' },
];
const METHODS = [
  { value: 'Cash', label: 'Efectivo' }, { value: 'BankTransfer', label: 'Transferencia' }, { value: 'Yape', label: 'Yape' }, { value: 'Plin', label: 'Plin' }, { value: 'Other', label: 'Otro' },
];
const STATUS = {
  Draft: { label: 'Borrador', cls: 'badge-info', icon: FileText },
  Approved: { label: 'Por pagar', cls: 'badge-warning', icon: Clock },
  Paid: { label: 'Pagado', cls: 'badge-success', icon: CheckCircle },
};
const money = v => `S/ ${Number(v || 0).toFixed(2)}`;
const day = iso => (iso || '').slice(0, 10);
const dm = iso => { const [, m, d] = day(iso).split('-'); return d ? `${d}/${m}` : ''; };
const errMsg = (err, fallback) => err?.response?.data?.message || fallback;

/** Nómina: liquidaciones por periodo (borrador → aprobada → pagada), fichas laborales, tarifarios y adelantos. */
export default function StaffPaymentsTab() {
  const now = new Date();
  const [section, setSection] = useState('payments');
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [year, setYear] = useState(now.getFullYear());
  const [payments, setPayments] = useState([]);
  const [staffList, setStaffList] = useState([]);
  const [scales, setScales] = useState([]);
  const [advances, setAdvances] = useState([]);
  const [loading, setLoading] = useState(true);
  const [editor, setEditor] = useState(null);   // { id?, staffId, start, end, adjustments, notes }
  const [viewing, setViewing] = useState(null); // liquidación con detalle
  const [paying, setPaying] = useState(null);   // { id, method, paidOn }
  const [terms, setTerms] = useState(null);     // ficha laboral en edición
  const [scaleForm, setScaleForm] = useState(null);
  const [advanceForm, setAdvanceForm] = useState(null);

  const range = useMemo(() => {
    const from = `${year}-${String(month).padStart(2, '0')}-01`;
    const to = `${year}-${String(month).padStart(2, '0')}-${new Date(year, month, 0).getDate()}`;
    return { from, to };
  }, [month, year]);

  const loadPayments = () => api.get(`/payroll/payments?from=${range.from}&to=${range.to}`).then(r => setPayments(r.data));
  const loadStaff = () => api.get('/academy-config/users').then(r => setStaffList(r.data.filter(u => u.systemRole === 'Staff' || u.systemRole === 'AcademyAdmin')));
  const loadScales = () => api.get('/payroll/scales').then(r => setScales(r.data));
  const loadAdvances = () => api.get('/payroll/advances').then(r => setAdvances(r.data));

  useEffect(() => {
    setLoading(true);
    Promise.all([loadPayments(), loadStaff(), loadScales(), loadAdvances()])
      .catch(() => toast.error('Error al cargar la nómina'))
      .finally(() => setLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [range]);

  const refresh = () => Promise.all([loadPayments(), loadAdvances()]).catch(() => {});

  const act = async (fn, ok) => {
    try { await fn(); if (ok) toast.success(ok); await refresh(); return true; }
    catch (err) { toast.error(errMsg(err, 'No se pudo completar la acción.')); return false; }
  };

  const openView = async id => {
    try { setViewing((await api.get(`/payroll/payments/${id}`)).data); }
    catch (err) { toast.error(errMsg(err, 'No se pudo abrir la liquidación.')); }
  };

  const openEditDraft = async id => {
    try {
      const { data } = await api.get(`/payroll/payments/${id}`);
      setEditor({
        id, staffId: data.staffId, start: day(data.periodStart), end: day(data.periodEnd), notes: data.notes || '',
        adjustments: data.lines.filter(l => l.kind !== 'Advance').map(l => ({ kind: l.kind === 'Bonus' ? 0 : 1, concept: l.concept, amount: String(l.amount) })),
      });
    } catch (err) { toast.error(errMsg(err, 'No se pudo abrir el borrador.')); }
  };

  const totals = {
    draft: payments.filter(p => p.status === 'Draft').reduce((s, p) => s + p.total, 0),
    toPay: payments.filter(p => p.status === 'Approved').reduce((s, p) => s + p.total, 0),
    paid: payments.filter(p => p.status === 'Paid').reduce((s, p) => s + p.total, 0),
  };

  const tabBtn = (key, label, Icon) => (
    <button key={key} onClick={() => setSection(key)}
      className={`flex items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium ${section === key ? 'bg-primary text-white' : 'text-text-muted hover:text-text-primary'}`}>
      <Icon size={15} /> {label}
    </button>
  );

  return (
    <div className="fade-in space-y-6">
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-card/40 border border-border/50 p-4 rounded-xl">
        <h3 className="text-xl font-bold text-text-primary flex items-center gap-2"><Users className="text-primary" size={20} /> Nómina del personal</h3>
        <div className="flex flex-wrap gap-2">
          {tabBtn('payments', 'Liquidaciones', FileText)}
          {tabBtn('terms', 'Fichas laborales', Users)}
          {tabBtn('scales', 'Tarifarios', Layers)}
          {tabBtn('advances', 'Adelantos', Wallet)}
        </div>
      </div>

      {section === 'payments' && (
        <>
          <div className="flex flex-wrap items-center gap-3">
            <select className="form-control text-sm py-2 w-auto" value={month} onChange={e => setMonth(+e.target.value)}>
              {MONTHS.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}
            </select>
            <select className="form-control text-sm py-2 w-auto" value={year} onChange={e => setYear(+e.target.value)}>
              {[year - 1, year, year + 1].map(y => <option key={y} value={y}>{y}</option>)}
            </select>
            <button onClick={() => setEditor({ staffId: '', start: '', end: '', adjustments: [], notes: '' })} className="btn btn-primary flex items-center gap-2">
              <Plus size={16} /> Nueva liquidación
            </button>
          </div>

          <div className="stats-grid">
            <div className="stat-card"><div className="stat-value">{money(totals.draft)}</div><div className="stat-label">En borrador</div></div>
            <div className="stat-card" style={{ borderTop: '2px solid var(--warning)' }}><div className="stat-value" style={{ color: 'var(--warning)' }}>{money(totals.toPay)}</div><div className="stat-label">Aprobado por pagar</div></div>
            <div className="stat-card" style={{ borderTop: '2px solid var(--success)' }}><div className="stat-value" style={{ color: 'var(--success)' }}>{money(totals.paid)}</div><div className="stat-label">Pagado</div></div>
          </div>

          <div className="card overflow-hidden">
            <div className="table-container">
              <table className="table">
                <thead><tr><th>Personal</th><th>Periodo</th><th>Horas / ses.</th><th>Fijo</th><th>Por horas/ses.</th><th>Bonos</th><th>Desc. y adel.</th><th>Total</th><th>Estado</th><th className="text-center">Acciones</th></tr></thead>
                <tbody>
                  {loading ? <tr><td colSpan="10" className="text-center p-8 text-text-muted">Cargando...</td></tr>
                    : payments.length === 0 ? <tr><td colSpan="10" className="text-center p-8 text-text-muted">No hay liquidaciones en este mes.</td></tr>
                      : payments.map(p => {
                        const st = STATUS[p.status] || STATUS.Approved;
                        const Icon = st.icon;
                        return (
                          <tr key={p.id}>
                            <td><p className="font-bold text-text-primary">{p.staffName}</p><p className="text-xs text-text-muted">{p.frequency}</p></td>
                            <td className="text-sm whitespace-nowrap">{dm(p.periodStart)} – {dm(p.periodEnd)}</td>
                            <td className="text-sm">{p.hours != null ? `${p.hours} h · ${p.sessions ?? 0} ses.` : '—'}</td>
                            <td className="whitespace-nowrap">{money(p.baseAmount)}</td>
                            <td className="whitespace-nowrap">{money(p.unitsAmount)}</td>
                            <td className="text-success whitespace-nowrap">+ {money(p.bonuses)}</td>
                            <td className="text-danger whitespace-nowrap">− {money(p.deductions)}</td>
                            <td className="font-bold whitespace-nowrap">{money(p.total)}</td>
                            <td>
                              <span className={`badge ${st.cls} flex items-center gap-1 w-max`}><Icon size={11} /> {st.label}</span>
                              {p.paidAt && <p className="text-xs text-text-muted mt-1">{new Date(p.paidAt).toLocaleDateString('es-PE')} · {METHODS.find(m => m.value === p.paymentMethod)?.label ?? ''}</p>}
                            </td>
                            <td>
                              <div className="flex flex-wrap gap-1 justify-center">
                                <button className="btn-icon" title="Ver detalle" onClick={() => openView(p.id)}><Eye size={15} /></button>
                                {p.status === 'Draft' && <>
                                  <button className="btn-icon" title="Editar borrador" onClick={() => openEditDraft(p.id)}><Edit2 size={15} /></button>
                                  <button className="btn btn-sm btn-primary" onClick={() => act(() => api.post(`/payroll/payments/${p.id}/approve`), 'Liquidación aprobada')}>Aprobar</button>
                                </>}
                                {p.status === 'Approved' && <>
                                  <button className="btn btn-sm btn-success" onClick={() => setPaying({ id: p.id, name: p.staffName, total: p.total, method: 'Cash', paidOn: new Date().toISOString().slice(0, 10) })}>Pagar</button>
                                  <button className="btn-icon" title="Volver a borrador" onClick={() => act(() => api.post(`/payroll/payments/${p.id}/reopen`), 'Volvió a borrador')}><RotateCcw size={15} /></button>
                                </>}
                                {p.status !== 'Paid' && (
                                  <button className="btn-icon text-danger" title="Eliminar" onClick={() => window.confirm(`¿Eliminar la liquidación de ${p.staffName}?`) && act(() => api.delete(`/payroll/payments/${p.id}`), 'Liquidación eliminada')}><Trash2 size={15} /></button>
                                )}
                              </div>
                            </td>
                          </tr>
                        );
                      })}
                </tbody>
              </table>
            </div>
          </div>
        </>
      )}

      {section === 'terms' && (
        <div className="card overflow-hidden">
          <div className="card-header"><div><h3 className="card-title">Fichas laborales</h3><p className="text-muted mt-1">Cómo y cada cuánto se paga a cada persona. Las horas salen de la asistencia de entrenadores.</p></div></div>
          <div className="table-container">
            <table className="table">
              <thead><tr><th>Personal</th><th>Esquema</th><th>Frecuencia</th><th>Sueldo / tarifa</th><th>Tarifario</th><th className="text-right">Acción</th></tr></thead>
              <tbody>
                {staffList.map(s => {
                  const scheme = SCHEMES.find(x => x.value === (s.paymentType ?? 0));
                  const scale = scales.find(x => x.id === s.payScaleId);
                  const usesScale = s.paymentType !== 0;
                  return (
                    <tr key={s.id}>
                      <td>{s.firstName} {s.lastName}</td>
                      <td>{scheme?.label}</td>
                      <td>{FREQUENCIES.find(f => f.value === (s.payFrequency ?? 0))?.label}</td>
                      <td>{Number(s.paymentRate) > 0 ? money(s.paymentRate) : '—'}</td>
                      <td className="text-sm">{!usesScale ? '—' : scale ? <b>{scale.name}</b> : 'Predeterminado'}</td>
                      <td className="text-right">
                        <button className="btn-icon inline-flex" title="Editar ficha laboral" onClick={() => setTerms({ id: s.id, name: `${s.firstName} ${s.lastName}`, paymentType: s.paymentType ?? 0, paymentRate: String(s.paymentRate ?? 0), payFrequency: s.payFrequency ?? 0, payScaleId: s.payScaleId ?? '' })}><Edit2 size={15} /></button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {section === 'scales' && (
        <div className="space-y-4">
          <div className="flex justify-between items-center">
            <p className="text-sm text-text-muted">Tarifa por hora o por sesión, con descuento cuando dictan varias en el mismo día. El predeterminado se aplica a todos; asigna otro en la ficha como excepción.</p>
            <button className="btn btn-primary flex items-center gap-2" onClick={() => setScaleForm({ name: '', unit: 0, isDefault: scales.length === 0, tiers: [{ from: 1, rate: '' }] })}><Plus size={16} /> Nuevo tarifario</button>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {scales.length === 0 && <div className="card text-text-muted">Aún no hay tarifarios. Crea uno por hora para los entrenadores.</div>}
            {scales.map(s => (
              <div key={s.id} className="card">
                <div className="flex justify-between items-start mb-2">
                  <div><h4 className="font-bold">{s.name}</h4><p className="text-xs text-text-muted">Por {s.unit === 0 ? 'hora' : 'sesión'}{s.isDefault ? ' · Predeterminado' : ''}</p></div>
                  <div className="flex gap-1">
                    <button className="btn-icon" title="Editar" onClick={() => setScaleForm({ ...s, tiers: s.tiers.map(t => ({ from: t.from, rate: String(t.rate) })) })}><Edit2 size={15} /></button>
                    <button className="btn-icon text-danger" title="Eliminar" onClick={() => window.confirm(`¿Eliminar "${s.name}"?`) && act(async () => { await api.delete(`/payroll/scales/${s.id}`); await loadScales(); }, 'Tarifario eliminado')}><Trash2 size={15} /></button>
                  </div>
                </div>
                <ul className="text-sm">
                  {s.tiers.map(t => <li key={t.from}>{t.from === 1 ? `1.ª ${s.unit === 0 ? 'hora' : 'sesión'} del día` : `Desde la ${t.from}.ª`}: <b>{money(t.rate)}</b></li>)}
                </ul>
              </div>
            ))}
          </div>
        </div>
      )}

      {section === 'advances' && (
        <div className="card overflow-hidden">
          <div className="card-header flex justify-between items-center">
            <div><h3 className="card-title">Adelantos</h3><p className="text-muted mt-1">Se descuentan solos en la siguiente liquidación.</p></div>
            <button className="btn btn-primary flex items-center gap-2" onClick={() => setAdvanceForm({ staffId: '', date: new Date().toISOString().slice(0, 10), amount: '', concept: '', method: 'Cash' })}><Plus size={16} /> Registrar adelanto</button>
          </div>
          <div className="table-container">
            <table className="table">
              <thead><tr><th>Fecha</th><th>Personal</th><th>Concepto</th><th>Monto</th><th>Estado</th><th></th></tr></thead>
              <tbody>
                {advances.length === 0 ? <tr><td colSpan="6" className="text-center p-6 text-text-muted">Sin adelantos registrados.</td></tr>
                  : advances.map(a => (
                    <tr key={a.id}>
                      <td>{dm(a.date)}</td><td>{a.staffName}</td><td>{a.concept}</td><td>{money(a.amount)}</td>
                      <td>{a.discounted ? <span className="badge badge-success">Descontado</span> : <span className="badge badge-warning">Pendiente</span>}</td>
                      <td className="text-right">{!a.discounted && <button className="btn-icon text-danger" title="Eliminar" onClick={() => window.confirm('¿Eliminar este adelanto?') && act(() => api.delete(`/payroll/advances/${a.id}`), 'Adelanto eliminado')}><Trash2 size={15} /></button>}</td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {editor && <PaymentEditor editor={editor} setEditor={setEditor} staffList={staffList} onSaved={() => { setEditor(null); refresh(); }} />}
      {viewing && <PaymentView payment={viewing} onClose={() => setViewing(null)} />}

      {paying && (
        <div className="modal-overlay" onClick={() => setPaying(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 420 }}>
            <h3 className="modal-title">Pagar a {paying.name}</h3>
            <p className="text-2xl font-bold mb-4">{money(paying.total)}</p>
            <div className="form-group"><label className="form-label">Medio de pago</label>
              <select className="form-control" value={paying.method} onChange={e => setPaying({ ...paying, method: e.target.value })}>{METHODS.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}</select></div>
            <div className="form-group"><label className="form-label">Fecha de pago</label>
              <input type="date" className="form-control" value={paying.paidOn} onChange={e => setPaying({ ...paying, paidOn: e.target.value })} /></div>
            <div className="modal-footer">
              <button className="btn btn-ghost" onClick={() => setPaying(null)}>Cancelar</button>
              <button className="btn btn-success" onClick={async () => { if (await act(() => api.post(`/payroll/payments/${paying.id}/pay`, { method: paying.method, paidOn: paying.paidOn }), 'Pago registrado')) setPaying(null); }}>Registrar pago</button>
            </div>
          </div>
        </div>
      )}

      {terms && (
        <div className="modal-overlay" onClick={() => setTerms(null)}>
          <form className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 460 }} onSubmit={async e => {
            e.preventDefault();
            const ok = await act(async () => {
              await api.put(`/academy-config/users/${terms.id}/employment`, {
                paymentType: +terms.paymentType, paymentRate: parseFloat(terms.paymentRate) || 0,
                payFrequency: +terms.payFrequency, payScaleId: terms.paymentType !== 0 && terms.payScaleId ? terms.payScaleId : null,
              });
              await loadStaff();
            }, 'Ficha laboral actualizada');
            if (ok) setTerms(null);
          }}>
            <h3 className="modal-title">Ficha laboral · {terms.name}</h3>
            <div className="form-group"><label className="form-label">Esquema de pago</label>
              <select className="form-control" value={terms.paymentType} onChange={e => setTerms({ ...terms, paymentType: +e.target.value, payScaleId: '' })}>{SCHEMES.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}</select></div>
            <div className="form-group"><label className="form-label">{SCHEMES.find(s => s.value === terms.paymentType)?.rateLabel}</label>
              <input type="number" min="0" step="0.01" className="form-control" value={terms.paymentRate} onChange={e => setTerms({ ...terms, paymentRate: e.target.value })} />
              {(terms.paymentType === 1 || terms.paymentType === 2) && <small className="text-xs text-text-muted">Solo se usa si no hay tarifario (propio ni predeterminado).</small>}</div>
            <div className="form-group"><label className="form-label">Frecuencia de pago</label>
              <select className="form-control" value={terms.payFrequency} onChange={e => setTerms({ ...terms, payFrequency: +e.target.value })}>{FREQUENCIES.map(f => <option key={f.value} value={f.value}>{f.label}</option>)}</select>
              <small className="text-xs text-text-muted">Semanal: lunes a domingo. Quincenal: del 1 al 15 y del 16 a fin de mes.</small></div>
            {terms.paymentType !== 0 && (
              <div className="form-group"><label className="form-label">Tarifario</label>
                <select className="form-control" value={terms.payScaleId} onChange={e => setTerms({ ...terms, payScaleId: e.target.value })}>
                  <option value="">Predeterminado de la academia</option>
                  {scales.filter(s => s.unit === (terms.paymentType === 1 ? 1 : 0)).map(s => <option key={s.id} value={s.id}>{s.name}{s.isDefault ? ' (predeterminado)' : ''}</option>)}
                </select></div>
            )}
            <div className="modal-footer"><button type="button" className="btn btn-ghost" onClick={() => setTerms(null)}>Cancelar</button><button type="submit" className="btn btn-primary">Guardar</button></div>
          </form>
        </div>
      )}

      {scaleForm && (
        <div className="modal-overlay" onClick={() => setScaleForm(null)}>
          <form className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 480 }} onSubmit={async e => {
            e.preventDefault();
            const body = { name: scaleForm.name, unit: +scaleForm.unit, isDefault: !!scaleForm.isDefault, tiers: scaleForm.tiers.map(t => ({ from: +t.from, rate: parseFloat(t.rate) || 0 })) };
            const ok = await act(async () => {
              if (scaleForm.id) await api.put(`/payroll/scales/${scaleForm.id}`, body); else await api.post('/payroll/scales', body);
              await loadScales();
            }, 'Tarifario guardado');
            if (ok) setScaleForm(null);
          }}>
            <h3 className="modal-title">{scaleForm.id ? 'Editar tarifario' : 'Nuevo tarifario'}</h3>
            <div className="form-group"><label className="form-label">Nombre</label><input required maxLength={100} className="form-control" placeholder="Ej. Entrenadores" value={scaleForm.name} onChange={e => setScaleForm({ ...scaleForm, name: e.target.value })} /></div>
            <div className="form-row">
              <div className="form-group"><label className="form-label">Se paga por</label>
                <select className="form-control" value={scaleForm.unit} onChange={e => setScaleForm({ ...scaleForm, unit: +e.target.value })}><option value={0}>Hora</option><option value={1}>Sesión</option></select></div>
              <label className="form-group flex items-center gap-2" style={{ marginTop: 28 }}><input type="checkbox" checked={!!scaleForm.isDefault} onChange={e => setScaleForm({ ...scaleForm, isDefault: e.target.checked })} /> Predeterminado</label>
            </div>
            <label className="form-label">Tarifas del mismo día</label>
            {scaleForm.tiers.map((t, i) => (
              <div key={i} className="flex items-center gap-2 mb-2">
                <span className="text-sm" style={{ minWidth: 110 }}>{i === 0 ? `1.ª ${scaleForm.unit === 0 ? 'hora' : 'sesión'}` : 'Desde la'}</span>
                {i > 0 && <input type="number" min="2" max="24" className="form-control" style={{ width: 70 }} value={t.from} onChange={e => setScaleForm({ ...scaleForm, tiers: scaleForm.tiers.map((x, j) => j === i ? { ...x, from: e.target.value } : x) })} />}
                {i > 0 && <span className="text-sm">.ª</span>}
                <input required type="number" min="0" step="0.01" className="form-control" placeholder="S/" value={t.rate} onChange={e => setScaleForm({ ...scaleForm, tiers: scaleForm.tiers.map((x, j) => j === i ? { ...x, rate: e.target.value } : x) })} />
                {i > 0 && <button type="button" className="btn-icon text-danger" onClick={() => setScaleForm({ ...scaleForm, tiers: scaleForm.tiers.filter((_, j) => j !== i) })}><Trash2 size={14} /></button>}
              </div>
            ))}
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => setScaleForm({ ...scaleForm, tiers: [...scaleForm.tiers, { from: (Math.max(...scaleForm.tiers.map(t => +t.from || 1)) + 1), rate: '' }] })}><Plus size={14} /> Agregar tramo</button>
            <p className="text-xs text-text-muted mt-2">Ejemplo: 1.ª hora S/ 30 y desde la 2.ª S/ 25. Las horas se cuentan por día, en el orden de las sesiones.</p>
            <div className="modal-footer"><button type="button" className="btn btn-ghost" onClick={() => setScaleForm(null)}>Cancelar</button><button type="submit" className="btn btn-primary">Guardar</button></div>
          </form>
        </div>
      )}

      {advanceForm && (
        <div className="modal-overlay" onClick={() => setAdvanceForm(null)}>
          <form className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 440 }} onSubmit={async e => {
            e.preventDefault();
            const ok = await act(() => api.post('/payroll/advances', { ...advanceForm, amount: parseFloat(advanceForm.amount) }), 'Adelanto registrado');
            if (ok) setAdvanceForm(null);
          }}>
            <h3 className="modal-title">Registrar adelanto</h3>
            <div className="form-group"><label className="form-label">Personal</label>
              <select required className="form-control" value={advanceForm.staffId} onChange={e => setAdvanceForm({ ...advanceForm, staffId: e.target.value })}><option value="">Seleccione...</option>{staffList.map(s => <option key={s.id} value={s.id}>{s.firstName} {s.lastName}</option>)}</select></div>
            <div className="form-row">
              <div className="form-group"><label className="form-label">Fecha</label><input required type="date" className="form-control" value={advanceForm.date} onChange={e => setAdvanceForm({ ...advanceForm, date: e.target.value })} /></div>
              <div className="form-group"><label className="form-label">Monto (S/)</label><input required type="number" min="0.01" step="0.01" className="form-control" value={advanceForm.amount} onChange={e => setAdvanceForm({ ...advanceForm, amount: e.target.value })} /></div>
            </div>
            <div className="form-group"><label className="form-label">Concepto</label><input required maxLength={150} className="form-control" placeholder="Ej. Pasajes" value={advanceForm.concept} onChange={e => setAdvanceForm({ ...advanceForm, concept: e.target.value })} /></div>
            <div className="form-group"><label className="form-label">Medio</label><select className="form-control" value={advanceForm.method} onChange={e => setAdvanceForm({ ...advanceForm, method: e.target.value })}>{METHODS.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}</select></div>
            <div className="modal-footer"><button type="button" className="btn btn-ghost" onClick={() => setAdvanceForm(null)}>Cancelar</button><button type="submit" className="btn btn-primary">Registrar</button></div>
          </form>
        </div>
      )}
    </div>
  );
}

/** Nueva liquidación o edición de un borrador: el cálculo lo hace el servidor; aquí se ven el detalle y los ajustes. */
function PaymentEditor({ editor, setEditor, staffList, onSaved }) {
  const [preview, setPreview] = useState(null);
  const [busy, setBusy] = useState(false);
  const set = patch => setEditor({ ...editor, ...patch });

  useEffect(() => {
    if (!editor.staffId || editor.id) return;
    api.get(`/payroll/suggest-period?staffId=${editor.staffId}`)
      .then(r => setEditor(e => ({ ...e, start: day(r.data.start), end: day(r.data.end) }))).catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editor.staffId]);

  useEffect(() => {
    setPreview(null);
    if (!editor.staffId || !editor.start || !editor.end) return;
    api.get(`/payroll/preview?staffId=${editor.staffId}&start=${editor.start}&end=${editor.end}`)
      .then(r => setPreview(r.data)).catch(err => toast.error(errMsg(err, 'No se pudo calcular.')));
  }, [editor.staffId, editor.start, editor.end]);

  const bonus = editor.adjustments.filter(a => a.kind === 0).reduce((s, a) => s + (parseFloat(a.amount) || 0), 0);
  const deduct = editor.adjustments.filter(a => a.kind === 1).reduce((s, a) => s + (parseFloat(a.amount) || 0), 0);
  const adv = preview ? preview.pendingAdvances.reduce((s, a) => s + a.amount, 0) : 0;
  const total = preview ? preview.baseAmount + preview.unitsAmount + bonus - deduct - adv : 0;

  const save = async () => {
    setBusy(true);
    try {
      const body = { staffId: editor.staffId, periodStart: editor.start, periodEnd: editor.end, notes: editor.notes || null,
        adjustments: editor.adjustments.map(a => ({ kind: a.kind, concept: a.concept, amount: parseFloat(a.amount) || 0 })) };
      if (editor.id) await api.put(`/payroll/payments/${editor.id}`, body); else await api.post('/payroll/payments', body);
      toast.success('Borrador guardado. Revísalo y apruébalo para pagarlo.');
      onSaved();
    } catch (err) { toast.error(errMsg(err, 'No se pudo guardar.')); }
    finally { setBusy(false); }
  };

  return (
    <div className="modal-overlay" onClick={() => setEditor(null)}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 760, maxHeight: '90vh', overflowY: 'auto' }}>
        <h3 className="modal-title">{editor.id ? 'Editar borrador' : 'Nueva liquidación'}</h3>
        <div className="form-row">
          <div className="form-group"><label className="form-label">Personal</label>
            <select className="form-control" disabled={!!editor.id} value={editor.staffId} onChange={e => set({ staffId: e.target.value })}><option value="">Seleccione...</option>{staffList.map(s => <option key={s.id} value={s.id}>{s.firstName} {s.lastName}</option>)}</select></div>
          <div className="form-group"><label className="form-label">Desde</label><input type="date" className="form-control" disabled={!!editor.id} value={editor.start} onChange={e => set({ start: e.target.value })} /></div>
          <div className="form-group"><label className="form-label">Hasta</label><input type="date" className="form-control" disabled={!!editor.id} value={editor.end} onChange={e => set({ end: e.target.value })} /></div>
        </div>

        {preview && (
          <>
            <p className="text-sm text-text-muted mb-2">{preview.scheme} · {preview.frequency} · {preview.rateLabel}</p>
            {preview.warnings.map(w => <p key={w} className="text-xs text-warning mb-1">⚠ {w}</p>)}
            {preview.days.length > 0 && (
              <div className="table-container mb-3">
                <table className="table text-sm">
                  <thead><tr><th>Día</th><th>Sesión</th><th>Horas</th><th>Cálculo</th><th className="text-right">Monto</th></tr></thead>
                  <tbody>
                    {preview.days.flatMap(d => d.items.map((it, i) => (
                      <tr key={it.eventId}>
                        <td>{i === 0 ? new Date(d.date).toLocaleDateString('es-PE', { weekday: 'short', day: '2-digit', month: '2-digit', timeZone: 'UTC' }) : ''}</td>
                        <td>{it.title} <span className="text-text-muted">{it.time}</span>{it.role === 'Reemplazo' && <span className="badge badge-info ml-1">Reemplazo</span>}{it.late && <span className="badge badge-warning ml-1">Tarde</span>}</td>
                        <td>{it.hours}</td><td className="text-xs">{it.rate}</td><td className="text-right">{money(it.amount)}</td>
                      </tr>
                    )))}
                  </tbody>
                </table>
              </div>
            )}

            <label className="form-label">Bonos y descuentos</label>
            {editor.adjustments.map((a, i) => (
              <div key={i} className="flex gap-2 mb-2">
                <select className="form-control" style={{ width: 130 }} value={a.kind} onChange={e => set({ adjustments: editor.adjustments.map((x, j) => j === i ? { ...x, kind: +e.target.value } : x) })}><option value={0}>Bono</option><option value={1}>Descuento</option></select>
                <input className="form-control" maxLength={150} placeholder="Concepto" value={a.concept} onChange={e => set({ adjustments: editor.adjustments.map((x, j) => j === i ? { ...x, concept: e.target.value } : x) })} />
                <input className="form-control" style={{ width: 120 }} type="number" min="0" step="0.01" placeholder="S/" value={a.amount} onChange={e => set({ adjustments: editor.adjustments.map((x, j) => j === i ? { ...x, amount: e.target.value } : x) })} />
                <button className="btn-icon text-danger" onClick={() => set({ adjustments: editor.adjustments.filter((_, j) => j !== i) })}><Trash2 size={14} /></button>
              </div>
            ))}
            <button className="btn btn-ghost btn-sm mb-3" onClick={() => set({ adjustments: [...editor.adjustments, { kind: 0, concept: '', amount: '' }] })}><Plus size={14} /> Agregar bono o descuento</button>

            <div className="card" style={{ background: 'var(--bg-dark, transparent)' }}>
              <Row label="Sueldo fijo" value={money(preview.baseAmount)} />
              <Row label={`Horas / sesiones (${preview.hours} h · ${preview.sessions} ses.)`} value={money(preview.unitsAmount)} />
              {bonus > 0 && <Row label="Bonos" value={`+ ${money(bonus)}`} />}
              {deduct > 0 && <Row label="Descuentos" value={`− ${money(deduct)}`} />}
              {preview.pendingAdvances.map(a => <Row key={a.id} label={`Adelanto ${dm(a.date)} · ${a.concept}`} value={`− ${money(a.amount)}`} />)}
              <Row label={<b>Total a pagar</b>} value={<b className={total < 0 ? 'text-danger' : ''}>{money(total)}</b>} />
            </div>
            <div className="form-group mt-3"><label className="form-label">Notas</label><input className="form-control" maxLength={500} value={editor.notes} onChange={e => set({ notes: e.target.value })} /></div>
          </>
        )}

        <div className="modal-footer">
          <button className="btn btn-ghost" onClick={() => setEditor(null)}>Cancelar</button>
          <button className="btn btn-primary" disabled={!preview || busy || total < 0} onClick={save}>{busy ? 'Guardando...' : 'Guardar borrador'}</button>
        </div>
      </div>
    </div>
  );
}

function Row({ label, value }) {
  return <div className="flex justify-between text-sm py-1"><span>{label}</span><span>{value}</span></div>;
}

function PaymentView({ payment: p, onClose }) {
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 720, maxHeight: '90vh', overflowY: 'auto' }}>
        <h3 className="modal-title">{p.staffName} · {dm(p.periodStart)} – {dm(p.periodEnd)}</h3>
        <p className="text-sm text-text-muted mb-3">{p.frequency} · {STATUS[p.status]?.label}{p.approvedByName ? ` · aprobó ${p.approvedByName}` : ''}{p.paidAt ? ` · pagado ${new Date(p.paidAt).toLocaleDateString('es-PE')}` : ''}</p>
        {p.days?.length > 0 && (
          <div className="table-container mb-3">
            <table className="table text-sm">
              <thead><tr><th>Día</th><th>Sesión</th><th>Horas</th><th>Cálculo</th><th className="text-right">Monto</th></tr></thead>
              <tbody>
                {p.days.flatMap(d => d.items.map((it, i) => (
                  <tr key={it.eventId}>
                    <td>{i === 0 ? new Date(d.date).toLocaleDateString('es-PE', { weekday: 'short', day: '2-digit', month: '2-digit', timeZone: 'UTC' }) : ''}</td>
                    <td>{it.title} <span className="text-text-muted">{it.time}</span>{it.role === 'Reemplazo' && <span className="badge badge-info ml-1">Reemplazo</span>}</td>
                    <td>{it.hours}</td><td className="text-xs">{it.rate}</td><td className="text-right">{money(it.amount)}</td>
                  </tr>
                )))}
              </tbody>
            </table>
          </div>
        )}
        <Row label="Sueldo fijo" value={money(p.baseAmount)} />
        <Row label={`Horas / sesiones${p.hours != null ? ` (${p.hours} h · ${p.sessions ?? 0} ses.)` : ''}`} value={money(p.unitsAmount)} />
        {p.lines.map(l => <Row key={l.id} label={l.kind === 'Advance' ? l.concept : `${l.kind === 'Bonus' ? 'Bono' : 'Descuento'} · ${l.concept}`} value={`${l.kind === 'Bonus' ? '+' : '−'} ${money(l.amount)}`} />)}
        <Row label={<b>Total</b>} value={<b>{money(p.total)}</b>} />
        {p.notes && <p className="text-sm text-text-muted mt-2">Notas: {p.notes}</p>}
        <div className="modal-footer"><button className="btn btn-primary" onClick={onClose}>Cerrar</button></div>
      </div>
    </div>
  );
}
