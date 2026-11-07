import { useEffect, useState } from 'react';
import { CheckCircle2, XCircle, FileText, ExternalLink, Inbox } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../api/axios';
import { fileUrl } from '../../utils/files';

const STATUS = {
  Pending: { label: 'Por validar', cls: 'badge-warning' },
  Approved: { label: 'Aprobado', cls: 'badge-success' },
  Rejected: { label: 'Rechazado', cls: 'badge-danger' },
};
const METHOD = { Yape: 'Yape', Plin: 'Plin', BankTransfer: 'Transferencia', Card: 'Tarjeta', Other: 'Otro', Cash: 'Efectivo' };
const money = v => `S/ ${Number(v).toFixed(2)}`;
const day = d => new Date(d).toLocaleDateString('es-PE', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' });

/**
 * Comprobantes enviados por las familias desde la app o el portal.
 * Aprobar registra el abono (con número de recibo); rechazar informa el motivo a la familia.
 */
export default function PaymentProofsTab({ onChanged }) {
  const [status, setStatus] = useState('Pending');
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [selected, setSelected] = useState(null);
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  // Pago hecho después del vencimiento: decisión de mora obligatoria al aprobar.
  const [reg, setReg] = useState({ charge: false, amount: '', note: '' });
  const [lateFeeDefault, setLateFeeDefault] = useState(0);
  useEffect(() => { api.get('/finances/config').then(r => setLateFeeDefault(Number(r.data.lateFeeAmount) || 0)).catch(() => {}); }, []);

  const load = () => {
    setLoading(true);
    api.get(`/finances/proofs${status ? `?status=${status}` : ''}`)
      .then(r => setItems(r.data))
      .catch(() => toast.error('No se pudieron cargar los comprobantes.'))
      .finally(() => setLoading(false));
  };
  useEffect(load, [status]);

  const open = p => {
    setSelected(p); setAmount(String(Math.min(p.amount, p.debtPending))); setReason('');
    setReg({ charge: lateFeeDefault > 0, amount: lateFeeDefault > 0 ? lateFeeDefault.toFixed(2) : '', note: '' });
  };

  const approve = async () => {
    if (selected.requiresRegularization) {
      if (reg.note.trim().length < 5) return toast.error('Pago fuera de fecha: escribe la observación de la regularización.');
      if (reg.charge && !(parseFloat(reg.amount) > 0)) return toast.error('Indica el monto de la mora.');
    }
    setBusy(true);
    try {
      const r = await api.post(`/finances/proofs/${selected.id}/approve`, {
        amount: parseFloat(amount),
        regularization: selected.requiresRegularization
          ? { chargeLateFee: reg.charge, lateFeeAmount: reg.charge ? parseFloat(reg.amount) : null, note: reg.note.trim() }
          : null,
      });
      toast.success(`Pago registrado · recibo N° ${r.data.receiptNumber || '—'}`);
      setSelected(null); load(); onChanged?.();
    } catch (err) { toast.error(err.response?.data?.message || 'No se pudo aprobar.'); }
    finally { setBusy(false); }
  };

  const reject = async () => {
    if (!reason.trim()) return toast.error('Indica el motivo del rechazo.');
    setBusy(true);
    try {
      await api.post(`/finances/proofs/${selected.id}/reject`, { reason });
      toast.success('Comprobante rechazado. La familia verá el motivo.');
      setSelected(null); load(); onChanged?.();
    } catch (err) { toast.error(err.response?.data?.message || 'No se pudo rechazar.'); }
    finally { setBusy(false); }
  };

  const isPdf = url => /\.pdf$/i.test(url || '');

  return (
    <div className="card">
      <div className="card-header">
        <div>
          <h3 className="card-title flex items-center gap-2"><Inbox size={20} className="text-primary" /> Comprobantes de pago</h3>
          <p className="text-muted mt-1">Pagos por Yape, Plin o transferencia enviados por las familias. Verifica el comprobante antes de aprobar.</p>
        </div>
        <select className="form-control" style={{ maxWidth: 200 }} value={status} onChange={e => setStatus(e.target.value)}>
          <option value="Pending">Por validar</option>
          <option value="Approved">Aprobados</option>
          <option value="Rejected">Rechazados</option>
          <option value="">Todos</option>
        </select>
      </div>

      {loading ? (
        <div className="text-center p-8"><span className="spinner" style={{ borderColor: 'var(--primary)', borderTopColor: 'transparent' }} /></div>
      ) : items.length === 0 ? (
        <div className="empty-state"><Inbox size={40} /><p>{status === 'Pending' ? 'No hay comprobantes por validar.' : 'Sin comprobantes en esta vista.'}</p></div>
      ) : (
        <div className="table-wrapper">
          <table className="table">
            <thead>
              <tr><th>Enviado</th><th>Alumno</th><th>Concepto</th><th>Medio</th><th>N° operación</th><th className="text-right">Monto</th><th>Estado</th><th></th></tr>
            </thead>
            <tbody>
              {items.map(p => (
                <tr key={p.id}>
                  <td>{new Date(p.createdAt).toLocaleDateString('es-PE')}<div className="text-xs text-text-muted">pagó el {day(p.paidOn)}</div></td>
                  <td>{p.studentName}<div className="text-xs text-text-muted">por {p.submittedByName}</div></td>
                  <td>{p.debtDescription}</td>
                  <td>{METHOD[p.method] || p.method}</td>
                  <td>{p.operationNumber || '—'}</td>
                  <td className="text-right font-bold">{money(p.amount)}</td>
                  <td>
                    <span className={`badge ${STATUS[p.status]?.cls}`}>{STATUS[p.status]?.label}</span>
                    {p.receiptNumber && <div className="text-xs text-text-muted">Recibo {p.receiptNumber}</div>}
                    {p.rejectionReason && <div className="text-xs text-danger">{p.rejectionReason}</div>}
                  </td>
                  <td className="text-right">
                    <button className="btn btn-ghost btn-sm" onClick={() => open(p)}>
                      {p.status === 'Pending' ? 'Revisar' : 'Ver'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {selected && (
        <div className="modal-overlay" onClick={() => !busy && setSelected(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 720 }}>
            <h3 className="modal-title">Comprobante de {selected.studentName}</h3>
            <div className="grid gap-4" style={{ gridTemplateColumns: 'minmax(0,1fr) minmax(0,1fr)' }}>
              <div>
                {isPdf(selected.voucherUrl) ? (
                  <a className="btn btn-ghost" href={fileUrl(selected.voucherUrl)} target="_blank" rel="noreferrer"><FileText size={16} /> Abrir PDF</a>
                ) : (
                  <a href={fileUrl(selected.voucherUrl)} target="_blank" rel="noreferrer" title="Abrir en tamaño completo">
                    <img src={fileUrl(selected.voucherUrl)} alt="Comprobante de pago" style={{ width: '100%', maxHeight: 420, objectFit: 'contain', borderRadius: 12, border: '1px solid var(--border)' }} />
                  </a>
                )}
                <a className="text-xs flex items-center gap-1 mt-2" href={fileUrl(selected.voucherUrl)} target="_blank" rel="noreferrer"><ExternalLink size={12} /> Ver archivo original</a>
              </div>
              <div className="flex flex-col gap-2 text-sm">
                <div><span className="text-text-muted">Concepto:</span> <b>{selected.debtDescription}</b></div>
                <div><span className="text-text-muted">Saldo pendiente de la deuda:</span> <b>{money(selected.debtPending)}</b></div>
                <div><span className="text-text-muted">Monto declarado:</span> <b>{money(selected.amount)}</b></div>
                <div><span className="text-text-muted">Medio:</span> {METHOD[selected.method] || selected.method} · <span className="text-text-muted">Operación:</span> {selected.operationNumber || '—'}</div>
                <div><span className="text-text-muted">Fecha de pago:</span> {day(selected.paidOn)}</div>
                {selected.notes && <div><span className="text-text-muted">Nota de la familia:</span> {selected.notes}</div>}

                {selected.status === 'Pending' && (
                  <>
                    <div className="form-group mt-2">
                      <label className="form-label">Monto a registrar (S/)</label>
                      <input type="number" min="0.01" step="0.01" className="form-control" value={amount} onChange={e => setAmount(e.target.value)} />
                      <small className="text-xs text-text-muted">Ajústalo si el monto recibido es distinto al declarado.</small>
                    </div>
                    {selected.requiresRegularization && (
                      <div className="p-3 rounded border border-warning/40 bg-warning/10 flex flex-col gap-2">
                        <div className="text-sm font-bold text-warning">Pagó el {day(selected.paidOn)}, después del vencimiento ({day(selected.debtDueDate)}). ¿Se cobra mora?</div>
                        <div className="flex gap-2 items-center flex-wrap">
                          <button type="button" className={`btn btn-sm ${reg.charge ? 'btn-warning' : 'btn-ghost'}`} onClick={() => setReg({ ...reg, charge: true })}>Cobrar mora</button>
                          <button type="button" className={`btn btn-sm ${!reg.charge ? 'btn-primary' : 'btn-ghost'}`} onClick={() => setReg({ ...reg, charge: false })}>Sin mora</button>
                          {reg.charge && <input type="number" step="0.01" min="0.01" className="form-control py-1 text-sm w-28" placeholder="Mora S/." value={reg.amount} onChange={e => setReg({ ...reg, amount: e.target.value })} />}
                        </div>
                        <input className="form-control text-sm" maxLength={500} placeholder="Observación obligatoria" value={reg.note} onChange={e => setReg({ ...reg, note: e.target.value })} />
                        {reg.charge && <small className="text-xs text-text-muted">La mora queda como deuda pendiente de la familia.</small>}
                      </div>
                    )}
                    <div className="form-group">
                      <label className="form-label">Motivo (solo si rechazas)</label>
                      <input className="form-control" maxLength={500} placeholder="Ej. El número de operación no figura en el estado de cuenta" value={reason} onChange={e => setReason(e.target.value)} />
                    </div>
                  </>
                )}
              </div>
            </div>
            <div className="modal-footer">
              <button className="btn btn-ghost" onClick={() => setSelected(null)} disabled={busy}>Cerrar</button>
              {selected.status === 'Pending' && (
                <>
                  <button className="btn btn-danger flex items-center gap-2" onClick={reject} disabled={busy}><XCircle size={16} /> Rechazar</button>
                  <button className="btn btn-primary flex items-center gap-2" onClick={approve} disabled={busy}><CheckCircle2 size={16} /> Aprobar y registrar pago</button>
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
