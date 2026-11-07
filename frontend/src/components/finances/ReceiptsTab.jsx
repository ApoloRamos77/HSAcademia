import { useCallback, useEffect, useState } from 'react';
import { Search, Download, Ban, Plus, X, FileText, Hash } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../api/axios';
import { useAuth } from '../../context/AuthContext';
import { printReceipt } from '../../utils/pdfGenerator';
import CheckoutModal from './CheckoutModal';

const METHOD = { Cash: 'Efectivo', BankTransfer: 'Transferencia', Yape: 'Yape', Plin: 'Plin', Card: 'Tarjeta', Other: 'Otro' };
const CHANNEL = { web: 'Caja', app: 'App', tienda: 'Tienda', comprobante: 'Comprobante', historico: 'Histórico (antes de los comprobantes)' };
const money = (n) => `S/. ${(Number(n) || 0).toFixed(2)}`;
const isoDay = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

/** Historial de comprobantes: reimpresión, búsqueda por número (incluye recibos antiguos) y anulación completa. */
export default function ReceiptsTab({ onChanged }) {
  const { user } = useAuth();
  const canVoid = user?.role === 'AcademyAdmin' || (user?.permissions || []).includes('manage_finances');
  const today = new Date();
  const [from, setFrom] = useState(isoDay(new Date(today.getFullYear(), today.getMonth(), 1)));
  const [to, setTo] = useState(isoDay(today));
  const [search, setSearch] = useState('');
  const [list, setList] = useState([]);
  const [loading, setLoading] = useState(true);
  const [number, setNumber] = useState('');
  const [voiding, setVoiding] = useState(null);
  const [reason, setReason] = useState('');
  const [checkout, setCheckout] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await api.get('/receipts', { params: { from, to, search: search.trim() || undefined } });
      setList(data);
    } catch { toast.error('No se pudieron cargar los recibos.'); }
    finally { setLoading(false); }
  }, [from, to, search]);

  useEffect(() => { const t = setTimeout(load, 300); return () => clearTimeout(t); }, [load]);

  const findByNumber = async (e) => {
    e.preventDefault();
    if (!number.trim()) return;
    try {
      const { data } = await api.get(`/receipts/by-number/${encodeURIComponent(number.trim())}`);
      printReceipt(data);
      if (data.isLegacy) toast('Recibo anterior a los comprobantes con varios ítems: solo se puede reimprimir.');
    } catch (err) { toast.error(err.response?.data?.message || 'Recibo no encontrado.'); }
  };

  const doVoid = async (e) => {
    e.preventDefault();
    if (reason.trim().length < 5) { toast.error('Indica el motivo (mínimo 5 caracteres).'); return; }
    try {
      const { data } = await api.post(`/receipts/${voiding.id}/void`, { reason: reason.trim() });
      toast.success(`Recibo N° ${data.number} anulado. Se revirtieron sus pagos y el stock.`);
      setVoiding(null); setReason('');
      load(); onChanged?.();
    } catch (err) { toast.error(err.response?.data?.message || 'No se pudo anular el recibo.'); }
  };

  const issued = list.filter(r => r.status === 'Issued');
  const totalIssued = issued.reduce((a, r) => a + r.total, 0);

  return (
    <div className="fade-in">
      <div className="card">
        <div className="p-4 border-b border-border/50 flex flex-col lg:flex-row gap-3 justify-between">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="card-title m-0 whitespace-nowrap">Recibos emitidos</h3>
            <input type="date" className="form-control py-1.5 text-sm w-auto" value={from} onChange={e => setFrom(e.target.value)} />
            <span className="text-text-muted text-sm">a</span>
            <input type="date" className="form-control py-1.5 text-sm w-auto" value={to} onChange={e => setTo(e.target.value)} />
            <div className="relative">
              <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
              <input className="form-control pl-8 py-1.5 text-sm" placeholder="N°, pagador o alumno" value={search} onChange={e => setSearch(e.target.value)} />
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <form onSubmit={findByNumber} className="flex items-center gap-1">
              <Hash size={14} className="text-text-muted" />
              <input className="form-control py-1.5 text-sm w-28" placeholder="Reimprimir N°" value={number} onChange={e => setNumber(e.target.value)} />
              <button className="btn btn-sm btn-ghost" type="submit"><Download size={13} /></button>
            </form>
            <button className="btn btn-sm btn-primary flex items-center gap-1" onClick={() => setCheckout(true)}><Plus size={13} /> Nuevo cobro</button>
          </div>
        </div>

        <div className="px-4 py-2 text-sm text-text-muted border-b border-border/50">
          {issued.length} recibo(s) vigentes · <b className="text-text-primary">{money(totalIssued)}</b>
          {list.length > issued.length && <> · {list.length - issued.length} anulado(s)</>}
        </div>

        <div className="table-container">
          <table className="table">
            <thead><tr><th>N°</th><th>Fecha</th><th>Pagador</th><th>Detalle</th><th>Medio</th><th>Total</th><th>Estado</th><th className="text-right">Acciones</th></tr></thead>
            <tbody>
              {list.map(r => (
                <tr key={r.id} className={r.status === 'Voided' ? 'opacity-60' : ''}>
                  <td className="font-bold">{r.number}</td>
                  <td className="text-sm whitespace-nowrap">{new Date(r.issuedAt).toLocaleDateString('es-PE')} {new Date(r.issuedAt).toLocaleTimeString('es-PE', { hour: '2-digit', minute: '2-digit' })}</td>
                  <td className="text-sm">{r.payerName}<div className="text-xs text-text-muted">{CHANNEL[r.channel] || r.channel}{r.createdByName ? ` · ${r.createdByName}` : ''}</div></td>
                  <td className="text-xs">
                    {r.lines.map((l, i) => (
                      <div key={i}>{l.quantity > 1 ? `${l.quantity}× ` : ''}{l.description}{l.studentName ? ` — ${l.studentName}` : ''} <b>{money(l.total)}</b></div>
                    ))}
                  </td>
                  <td className="text-sm">{METHOD[r.method] || r.method}{r.operationNumber && <div className="text-xs text-text-muted">Op. {r.operationNumber}</div>}</td>
                  <td className="font-bold">{money(r.total)}</td>
                  <td>
                    {r.status === 'Voided'
                      ? <span className="badge badge-danger" title={r.voidReason || ''}>Anulado</span>
                      : <span className="badge badge-success">Vigente</span>}
                  </td>
                  <td className="text-right whitespace-nowrap">
                    <button className="btn btn-sm btn-ghost text-primary" title="Reimprimir" onClick={() => printReceipt(r)}><FileText size={13} /></button>
                    {canVoid && r.status === 'Issued' && (
                      <button className="btn btn-sm btn-ghost text-danger" title="Anular recibo" onClick={() => { setVoiding(r); setReason(''); }}><Ban size={13} /></button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!loading && list.length === 0 && <div className="empty-state"><FileText size={36} className="text-text-muted" /><p>No hay recibos en este rango.</p></div>}
      </div>

      {voiding && (
        <div className="modal-overlay" onClick={() => setVoiding(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 460 }}>
            <div className="flex justify-between items-center mb-4 border-b border-border pb-3">
              <h3 className="modal-title m-0 flex items-center gap-2"><Ban size={18} /> Anular recibo N° {voiding.number}</h3>
              <button onClick={() => setVoiding(null)} className="btn btn-ghost btn-sm"><X size={16} /></button>
            </div>
            <p className="text-sm text-text-secondary mb-2">Se anulará el recibo completo ({money(voiding.total)}):</p>
            <ul className="text-sm mb-3 list-disc pl-5">
              {voiding.lines.map((l, i) => <li key={i}>{l.description}{l.studentName ? ` — ${l.studentName}` : ''}: {money(l.total)}</li>)}
            </ul>
            {voiding.cashHandoverId && <p className="text-xs text-warning mb-2">Este recibo ya está en una rendición de caja. Si la rendición aún no se recibió, se descuenta de ella; si ya se recibió, coordina la devolución del dinero.</p>}
            <p className="text-xs text-text-muted mb-3">Las mensualidades vuelven a quedar pendientes y los productos regresan al stock. El número no se reutiliza y la anulación queda registrada en auditoría.</p>
            <form onSubmit={doVoid}>
              <div className="form-group">
                <label className="form-label">Motivo *</label>
                <input className="form-control" autoFocus maxLength={300} value={reason} onChange={e => setReason(e.target.value)} placeholder="Ej. monto mal digitado" />
              </div>
              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={() => setVoiding(null)} className="btn btn-ghost">Cancelar</button>
                <button type="submit" className="btn btn-danger flex items-center gap-2"><Ban size={15} /> Anular recibo</button>
              </div>
            </form>
          </div>
        </div>
      )}

      <CheckoutModal open={checkout} onClose={() => setCheckout(false)} onDone={() => { load(); onChanged?.(); }} />
    </div>
  );
}
