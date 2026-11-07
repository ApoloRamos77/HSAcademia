import { useEffect, useMemo, useRef, useState } from 'react';
import { X, Receipt, Search, Users, ShoppingBag, Plus, Trash2, CheckCircle, Upload, Tag, AlertTriangle } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../api/axios';
import { useAuth } from '../../context/AuthContext';
import { printReceipt } from '../../utils/pdfGenerator';

const METHODS = [
  { value: 'Cash', label: '💵 Efectivo' },
  { value: 'Yape', label: '📱 Yape' },
  { value: 'Plin', label: '📱 Plin' },
  { value: 'BankTransfer', label: '🏦 Transferencia' },
  { value: 'Card', label: '💳 Tarjeta' },
  { value: 'Other', label: '🔹 Otro' },
];
const DIGITAL = ['Yape', 'Plin', 'BankTransfer'];
const money = (n) => `S/. ${(Number(n) || 0).toFixed(2)}`;
const num = (v) => { const n = parseFloat(String(v).replace(',', '.')); return Number.isFinite(n) ? n : 0; };
const newKey = () => (crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random().toString(36).slice(2)}`);
let lineKey = 0;

/**
 * Cobro con varios ítems en UN comprobante: mensualidades de todos los hermanos (mismo apoderado),
 * productos de tienda y abonos parciales, con un solo medio de pago.
 * Los descuentos (pronto pago, promoción) los calcula el servidor en la vista previa: lo que se ve es lo que se cobra.
 */
export default function CheckoutModal({ open, onClose, onDone, initialStudentId, initialDebtIds = [] }) {
  const { user } = useAuth();
  const isAdmin = user?.role === 'AcademyAdmin';
  const perms = user?.permissions || [];
  const canDebts = isAdmin || perms.includes('manage_finances');
  const canProducts = canDebts || perms.includes('manage_store');

  const [students, setStudents] = useState([]);
  const [products, setProducts] = useState([]);
  const [promotions, setPromotions] = useState([]);
  const [search, setSearch] = useState('');
  const [families, setFamilies] = useState([]); // FamilyAccountDto[] (se pueden sumar varias)
  const [debtSel, setDebtSel] = useState({}); // id -> { on, partial, amount, discount }
  const [lines, setLines] = useState([]); // productos
  const [promotionId, setPromotionId] = useState('');
  const [payer, setPayer] = useState({ name: '', document: '' });
  const [pay, setPay] = useState({ method: 'Cash', operationNumber: '', voucherUrl: '', notes: '' });
  const [uploading, setUploading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [preview, setPreview] = useState(null);
  const [previewError, setPreviewError] = useState(null);
  const [previewing, setPreviewing] = useState(false);
  // Clave por intento de cobro: si se reintenta (doble clic, red lenta) el servidor devuelve el mismo recibo.
  const [requestId, setRequestId] = useState('');
  const previewSeq = useRef(0);

  useEffect(() => {
    if (!open) return;
    setFamilies([]); setDebtSel({}); setLines([]); setSearch(''); setPromotionId('');
    setPreview(null); setPreviewError(null);
    setRequestId(newKey());
    setPayer({ name: '', document: '' });
    setPay({ method: 'Cash', operationNumber: '', voucherUrl: '', notes: '' });
    api.get('/students').then(r => setStudents(r.data)).catch(() => {});
    api.get('/discounts', { params: { active: true } })
      .then(r => setPromotions(r.data.filter(d => d.kind === 'Promotion' && d.isCurrentlyValid))).catch(() => {});
    if (canProducts) api.get('/store/products').then(r => setProducts(r.data.filter(p => p.isActive !== false))).catch(() => {});
    if (initialStudentId) addFamily(initialStudentId, initialDebtIds, true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, initialStudentId]);

  const addFamily = async (studentId, preselect = null, fresh = false) => {
    if (!fresh && families.some(f => f.students.some(s => s.studentId === studentId))) {
      toast('Ese alumno ya está en el cobro.'); return;
    }
    try {
      const { data } = await api.get(`/receipts/family/${studentId}`);
      setFamilies(prev => fresh ? [data] : [...prev, data]);
      setPayer(p => p.name ? p : { name: data.payerName || '', document: data.payerDocument || '' });
      setDebtSel(prev => {
        const next = { ...prev };
        data.pendingDebts.forEach(d => {
          if (next[d.id]) return;
          const on = preselect && preselect.length ? preselect.includes(d.id) : d.studentId === studentId;
          next[d.id] = { on, partial: false, amount: '', discount: '' };
        });
        return next;
      });
      setSearch('');
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo cargar la cuenta del alumno.');
    }
  };

  const allDebts = useMemo(() => families.flatMap(f => f.pendingDebts), [families]);
  const familyStudents = useMemo(() => families.flatMap(f => f.students), [families]);
  const matches = search.trim().length >= 2
    ? students.filter(s => `${s.firstName} ${s.lastName} ${s.documentNumber || ''} ${s.guardianName || ''}`
        .toLowerCase().includes(search.trim().toLowerCase())).slice(0, 8)
    : [];

  const setDebt = (id, patch) => setDebtSel(p => ({ ...p, [id]: { ...p[id], ...patch } }));
  const addLine = () => setLines(p => [...p, { key: ++lineKey, productId: '', studentId: familyStudents[0]?.studentId || '', quantity: 1, discount: '', isGift: false }]);
  const setLine = (key, patch) => setLines(p => p.map(l => l.key === key ? { ...l, ...patch } : l));

  // Cuerpo del cobro (el mismo para la vista previa y para registrar).
  const body = useMemo(() => ({
    guardianId: families[0]?.guardianId || null,
    payerName: payer.name.trim() || null,
    payerDocument: payer.document.trim() || null,
    promotionId: promotionId || null,
    method: pay.method,
    operationNumber: pay.operationNumber.trim() || null,
    voucherUrl: pay.voucherUrl || null,
    notes: pay.notes.trim() || null,
    channel: 'web',
    debts: allDebts.filter(d => debtSel[d.id]?.on).map(d => ({
      paymentRecordId: d.id,
      amount: debtSel[d.id].partial ? num(debtSel[d.id].amount) : null, // vacío = cancelar completa
      discount: num(debtSel[d.id].discount),
    })),
    products: lines.filter(l => l.productId).map(l => ({
      productId: l.productId, studentId: l.studentId || null,
      quantity: Math.max(1, parseInt(l.quantity, 10) || 1), discount: l.isGift ? 0 : num(l.discount), isGift: l.isGift,
    })),
  }), [families, payer, promotionId, pay, allDebts, debtSel, lines]);

  const itemCount = body.debts.length + body.products.length;

  // Vista previa calculada por el servidor (con espera para no consultar en cada tecla).
  useEffect(() => {
    if (!open || itemCount === 0) { setPreview(null); setPreviewError(null); return; }
    const seq = ++previewSeq.current;
    setPreviewing(true);
    const t = setTimeout(async () => {
      try {
        const { data } = await api.post('/receipts/preview', body);
        if (seq === previewSeq.current) { setPreview(data); setPreviewError(null); }
      } catch (err) {
        if (seq === previewSeq.current) { setPreview(null); setPreviewError(err.response?.data?.message || 'No se pudo calcular el cobro.'); }
      } finally {
        if (seq === previewSeq.current) setPreviewing(false);
      }
    }, 350);
    return () => clearTimeout(t);
  }, [open, body, itemCount]);

  const previewOf = (debtId) => preview?.lines.find(l => l.paymentRecordId === debtId);

  const uploadVoucher = async (file) => {
    if (!file) return;
    setUploading(true);
    try {
      const form = new FormData();
      form.append('file', file);
      const { data } = await api.post('/finances/upload-voucher', form, { headers: { 'Content-Type': 'multipart/form-data' } });
      setPay(p => ({ ...p, voucherUrl: data.url }));
      toast.success('Voucher adjuntado.');
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo subir el voucher.');
    } finally { setUploading(false); }
  };

  const submit = async (e) => {
    e.preventDefault();
    if (itemCount === 0) { toast.error('Agrega al menos una deuda o un producto.'); return; }
    if (previewError) { toast.error(previewError); return; }
    if (DIGITAL.includes(pay.method) && !pay.operationNumber.trim() && !pay.voucherUrl) { toast.error('Indica el número de operación del pago digital.'); return; }
    setSaving(true);
    try {
      const { data } = await api.post('/receipts', { ...body, clientRequestId: requestId });
      toast.success(`Recibo N° ${data.number} emitido.`);
      printReceipt(data);
      onDone?.(data);
      onClose();
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo registrar el cobro.');
    } finally { setSaving(false); }
  };

  if (!open) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 860 }}>
        <div className="flex justify-between items-center mb-4 border-b border-border pb-3">
          <h3 className="modal-title m-0 flex items-center gap-2"><Receipt size={18} /> Nuevo cobro</h3>
          <button onClick={onClose} className="btn btn-ghost btn-sm"><X size={16} /></button>
        </div>

        <form onSubmit={submit}>
          {/* Buscar alumno / familia */}
          <div className="form-group relative">
            <label className="form-label">{families.length ? 'Agregar otro alumno (otra familia)' : 'Alumno'}</label>
            <div className="relative">
              <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
              <input className="form-control pl-9" placeholder="Nombre, DNI o apoderado…" value={search} onChange={e => setSearch(e.target.value)} />
            </div>
            {matches.length > 0 && (
              <div className="absolute z-10 left-0 right-0 mt-1 bg-bg-card border border-border rounded-lg shadow-lg max-h-64 overflow-auto">
                {matches.map(s => (
                  <button type="button" key={s.id} onClick={() => addFamily(s.id)}
                    className="w-full text-left px-3 py-2 hover:bg-bg-surface text-sm flex justify-between gap-2">
                    <span><b>{s.lastName}, {s.firstName}</b> <span className="text-text-muted">· {s.categoryName}</span></span>
                    <span className="text-text-muted text-xs">{s.guardianName}</span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {families.length > 0 && (
            <>
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Pagador</label>
                  <input className="form-control" value={payer.name} onChange={e => setPayer({ ...payer, name: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="form-label">DNI del pagador (opcional)</label>
                  <input className="form-control" maxLength={15} value={payer.document} onChange={e => setPayer({ ...payer, document: e.target.value })} />
                </div>
              </div>

              {/* Deudas */}
              <div className="flex items-center gap-2 mb-2 mt-1">
                <Users size={15} className="text-primary" />
                <span className="font-bold text-sm">Deudas pendientes</span>
                <span className="text-xs text-text-muted">{familyStudents.map(s => s.name).join(' · ')}</span>
              </div>
              {allDebts.length === 0 ? (
                <div className="text-sm text-success bg-success/10 rounded p-3 mb-3 flex items-center gap-2"><CheckCircle size={15} /> La familia no tiene deudas pendientes.</div>
              ) : !canDebts ? (
                <div className="text-sm text-text-muted bg-bg-surface rounded p-3 mb-3">No tienes permiso para cobrar mensualidades.</div>
              ) : (
                <div className="table-container mb-3">
                  <table className="table">
                    <thead><tr><th></th><th>Alumno</th><th>Concepto</th><th>Pendiente</th><th style={{ width: 150 }}>Pago</th><th style={{ width: 100 }}>Desc. caja</th><th className="text-right">A cobrar</th></tr></thead>
                    <tbody>
                      {allDebts.map(d => {
                        const sel = debtSel[d.id] || {};
                        const pv = sel.on ? previewOf(d.id) : null;
                        return (
                          <tr key={d.id} className={sel.on ? 'bg-primary/5' : ''}>
                            <td><input type="checkbox" checked={!!sel.on} onChange={e => setDebt(d.id, { on: e.target.checked })} /></td>
                            <td className="text-sm font-medium">{d.studentName}</td>
                            <td className="text-sm">
                              {d.description}
                              {d.status === 'Vencido' && <span className="ml-2 badge badge-danger text-xs">Vencido {d.daysOverdue}d</span>}
                              {d.discountDetail && <div className="text-xs text-success">{d.discountDetail}</div>}
                              {pv?.discountLabel && <div className="text-xs text-warning flex items-center gap-1"><Tag size={11} /> {pv.discountLabel}: -{money(pv.discount)}</div>}
                            </td>
                            <td className="text-sm font-bold text-danger">{money(d.amountPending)}</td>
                            <td>
                              {sel.partial ? (
                                <div className="flex items-center gap-1">
                                  <input type="number" step="0.01" min="0.01" className="form-control py-1 text-sm font-bold" placeholder="Abono"
                                    value={sel.amount} onChange={e => setDebt(d.id, { amount: e.target.value })} disabled={!sel.on} />
                                  <button type="button" className="btn btn-sm btn-ghost text-xs" title="Cancelar completa" onClick={() => setDebt(d.id, { partial: false, amount: '' })}>✕</button>
                                </div>
                              ) : (
                                <button type="button" className="btn btn-sm btn-ghost text-xs" disabled={!sel.on}
                                  onClick={() => setDebt(d.id, { partial: true, amount: '' })}>Completa · abono parcial</button>
                              )}
                            </td>
                            <td><input type="number" step="0.01" min="0" className="form-control py-1 text-sm" disabled={!sel.on} placeholder="0.00"
                              value={sel.discount} onChange={e => setDebt(d.id, { discount: e.target.value })} /></td>
                            <td className="text-right font-bold">{sel.on ? (pv ? money(pv.total) : '…') : '—'}</td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                  <p className="text-xs text-text-muted mt-1">Pronto pago y promociones se aplican al cancelar la mensualidad completa (no en abonos parciales) y no se acumulan: se aplica el mayor.</p>
                </div>
              )}
            </>
          )}

          {/* Productos */}
          {canProducts && (
            <div className="mb-3">
              <div className="flex items-center justify-between mb-2">
                <span className="font-bold text-sm flex items-center gap-2"><ShoppingBag size={15} className="text-primary" /> Productos</span>
                <button type="button" className="btn btn-sm btn-ghost text-primary flex items-center gap-1" onClick={addLine}><Plus size={13} /> Agregar producto</button>
              </div>
              {lines.map(l => {
                const pv = preview?.lines.find(x => x.type === 'Product' && x.productId === l.productId && (x.studentId || '') === (l.studentId || ''));
                return (
                  <div key={l.key} className="grid gap-2 items-center mb-2" style={{ gridTemplateColumns: '2fr 1.4fr 70px 90px auto auto' }}>
                    <select className="form-control py-1 text-sm" value={l.productId} onChange={e => setLine(l.key, { productId: e.target.value })}>
                      <option value="">Producto…</option>
                      {products.map(x => <option key={x.id} value={x.id} disabled={x.stock <= 0}>{x.name} — {money(x.price)} ({x.stock} disp.)</option>)}
                    </select>
                    <select className="form-control py-1 text-sm" value={l.studentId} onChange={e => setLine(l.key, { studentId: e.target.value })}>
                      <option value="">Sin alumno</option>
                      {familyStudents.map(s => <option key={s.studentId} value={s.studentId}>{s.name}</option>)}
                    </select>
                    <input type="number" min="1" className="form-control py-1 text-sm" value={l.quantity} onChange={e => setLine(l.key, { quantity: e.target.value })} title="Cantidad" />
                    <input type="number" min="0" step="0.01" className="form-control py-1 text-sm" placeholder="Desc." disabled={l.isGift} value={l.discount} onChange={e => setLine(l.key, { discount: e.target.value })} title="Descuento" />
                    <label className="text-xs flex items-center gap-1 whitespace-nowrap"><input type="checkbox" checked={l.isGift} onChange={e => setLine(l.key, { isGift: e.target.checked })} /> Obsequio</label>
                    <button type="button" className="btn btn-sm btn-ghost text-danger" onClick={() => setLines(x => x.filter(y => y.key !== l.key))}><Trash2 size={13} /></button>
                    {pv && <span className="text-xs text-text-muted" style={{ gridColumn: '1 / -1', marginTop: -4 }}>
                      {pv.discountLabel ? <><Tag size={11} className="inline" /> {pv.discountLabel}: -{money(pv.discount)} · </> : null}Total {money(pv.total)}
                    </span>}
                  </div>
                );
              })}
              {lines.length === 0 && <p className="text-xs text-text-muted">Uniformes, indumentaria u otros productos en el mismo recibo.</p>}
            </div>
          )}

          {/* Promoción */}
          {promotions.length > 0 && (
            <div className="form-group">
              <label className="form-label flex items-center gap-1"><Tag size={13} /> Promoción (opcional)</label>
              <select className="form-control" value={promotionId} onChange={e => setPromotionId(e.target.value)}>
                <option value="">Sin promoción</option>
                {promotions.map(p => <option key={p.id} value={p.id}>{p.label}{p.scope === 'Products' ? ' · productos' : p.scope === 'All' ? ' · todo' : ' · mensualidades'}{p.categoryName ? ` · ${p.categoryName}` : ''}</option>)}
              </select>
            </div>
          )}

          {/* Pago */}
          <div className="form-row">
            <div className="form-group">
              <label className="form-label">Medio de pago *</label>
              <select className="form-control" value={pay.method} onChange={e => setPay({ ...pay, method: e.target.value })}>
                {METHODS.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label className="form-label">N° de operación {DIGITAL.includes(pay.method) ? '*' : '(opcional)'}</label>
              <input className="form-control" value={pay.operationNumber} onChange={e => setPay({ ...pay, operationNumber: e.target.value })} />
            </div>
          </div>
          <div className="form-row">
            <div className="form-group">
              <label className="form-label">Nota (opcional)</label>
              <input className="form-control" maxLength={300} value={pay.notes} onChange={e => setPay({ ...pay, notes: e.target.value })} />
            </div>
            <div className="form-group">
              <label className="form-label">Voucher (opcional)</label>
              <label className="btn btn-ghost btn-sm flex items-center gap-2 w-max cursor-pointer">
                <Upload size={13} /> {uploading ? 'Subiendo…' : pay.voucherUrl ? 'Voucher adjunto ✓' : 'Adjuntar imagen'}
                <input type="file" accept="image/*,application/pdf" hidden onChange={e => uploadVoucher(e.target.files?.[0])} />
              </label>
            </div>
          </div>

          {previewError && (
            <div className="text-sm text-danger bg-danger/10 rounded p-3 mb-3 flex items-center gap-2"><AlertTriangle size={15} /> {previewError}</div>
          )}

          {/* Totales */}
          <div className="bg-bg-dark border border-border rounded p-3 flex flex-wrap justify-between items-center gap-3">
            <div className="text-sm text-text-muted">
              {itemCount} ítem(s){preview && <> · Subtotal {money(preview.subtotal)}{preview.discountTotal > 0 && <> · Descuentos <b className="text-warning">-{money(preview.discountTotal)}</b></>}</>}
            </div>
            <div className="text-xl font-bold">{previewing && !preview ? 'Calculando…' : `TOTAL ${money(preview?.total)}`}</div>
          </div>

          <div className="modal-footer mt-4 flex justify-end gap-2">
            <button type="button" onClick={onClose} className="btn btn-ghost text-danger">Cancelar</button>
            <button type="submit" disabled={saving || itemCount === 0 || !preview || previewing || !!previewError} className="btn btn-success flex items-center gap-2">
              <CheckCircle size={15} /> {saving ? 'Registrando…' : `Cobrar ${money(preview?.total)} y emitir recibo`}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
