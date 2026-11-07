import { useCallback, useEffect, useState } from 'react';
import { Wallet, Send, X, AlertTriangle } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../api/axios';

const money = (n) => `S/. ${(Number(n) || 0).toFixed(2)}`;

/** "Mi caja": lo que cobré y aún no rindo, con el botón para rendir. Se oculta si no hay nada pendiente. */
export default function MyCashPanel({ refreshKey, onChanged }) {
  const [mine, setMine] = useState(null);
  const [declare, setDeclare] = useState(null);
  const [busy, setBusy] = useState(false);

  const load = useCallback(() => api.get('/cash/mine').then(r => setMine(r.data)).catch(() => {}), []);
  useEffect(() => { load(); }, [load, refreshKey]);

  const doDeclare = async (e) => {
    e.preventDefault();
    setBusy(true);
    try {
      await api.post('/cash/handovers', { declaredCash: parseFloat(declare.declaredCash) || 0, notes: declare.notes || null });
      toast.success('Rendición registrada. Entrega el efectivo a finanzas.');
      setDeclare(null); load(); onChanged?.();
    } catch (err) { toast.error(err.response?.data?.message || 'No se pudo rendir la caja.'); }
    finally { setBusy(false); }
  };

  if (!mine || mine.pendingReceipts === 0) return null;

  return (
    <>
      <div className="card mb-4 p-4 flex flex-wrap items-center justify-between gap-3" style={{ borderLeft: '4px solid var(--warning)' }}>
        <div>
          <div className="font-bold flex items-center gap-2"><Wallet size={16} /> Mi caja sin rendir</div>
          <div className="text-sm text-text-muted">{mine.pendingReceipts} recibo(s) · Efectivo <b className="text-text-primary">{money(mine.pendingCash)}</b> · Digital {money(mine.pendingDigital)}</div>
        </div>
        <button className="btn btn-warning btn-sm flex items-center gap-1" onClick={() => setDeclare({ declaredCash: mine.pendingCash.toFixed(2), notes: '' })}><Send size={13} /> Rendir mi caja</button>
      </div>

      {declare && (
        <div className="modal-overlay" onClick={() => setDeclare(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 440 }}>
            <div className="flex justify-between items-center mb-4 border-b border-border pb-3">
              <h3 className="modal-title m-0">Rendir mi caja</h3>
              <button onClick={() => setDeclare(null)} className="btn btn-ghost btn-sm"><X size={16} /></button>
            </div>
            <p className="text-sm mb-3">Según tus recibos debes entregar <b>{money(mine.pendingCash)}</b> en efectivo ({mine.pendingReceipts} recibo(s)). Los pagos digitales ({money(mine.pendingDigital)}) se incluyen para conciliación.</p>
            <form onSubmit={doDeclare}>
              <div className="form-group">
                <label className="form-label">Efectivo que entregas *</label>
                <input className="form-control text-lg font-bold" type="number" step="0.01" min="0" required value={declare.declaredCash} onChange={e => setDeclare({ ...declare, declaredCash: e.target.value })} />
              </div>
              {Math.abs((parseFloat(declare.declaredCash) || 0) - mine.pendingCash) >= 0.01 && (
                <div className="text-sm text-warning flex items-center gap-2 mb-2"><AlertTriangle size={14} /> No coincide con lo cobrado: explica la diferencia.</div>
              )}
              <div className="form-group">
                <label className="form-label">Nota</label>
                <input className="form-control" maxLength={300} value={declare.notes} onChange={e => setDeclare({ ...declare, notes: e.target.value })} />
              </div>
              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={() => setDeclare(null)} className="btn btn-ghost">Cancelar</button>
                <button type="submit" disabled={busy} className="btn btn-warning">Rendir caja</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
