import { useEffect, useState } from 'react';
import { X, ShieldAlert } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../api/axios';

const money = (n) => `S/. ${(Number(n) || 0).toFixed(2)}`;

/**
 * Regularizar una mensualidad vencida (política: se paga del 1 al día de vencimiento).
 * Administración decide cobrar mora o no; en ambos casos registra la observación.
 */
export default function RegularizeModal({ debt, onClose, onDone }) {
  const [charge, setCharge] = useState(true);
  const [amount, setAmount] = useState('');
  const [note, setNote] = useState('');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    api.get('/finances/config').then(r => {
      const fee = Number(r.data.lateFeeAmount) || 0;
      setAmount(fee > 0 ? fee.toFixed(2) : '');
      if (fee <= 0) setCharge(false);
    }).catch(() => {});
  }, []);

  const submit = async (e) => {
    e.preventDefault();
    if (note.trim().length < 5) { toast.error('Escribe la observación de la regularización.'); return; }
    if (charge && !(parseFloat(amount) > 0)) { toast.error('Indica el monto de la mora.'); return; }
    setSaving(true);
    try {
      await api.post(`/finances/debts/${debt.id}/regularize`, {
        chargeLateFee: charge,
        lateFeeAmount: charge ? parseFloat(amount) : null,
        note: note.trim(),
      });
      toast.success(charge ? `Regularizado con mora de ${money(amount)}.` : 'Regularizado sin mora.');
      onDone?.();
      onClose();
    } catch (err) { toast.error(err.response?.data?.message || 'No se pudo regularizar.'); }
    finally { setSaving(false); }
  };

  return (
    <div className="modal-overlay" style={{ zIndex: 1100 }} onClick={onClose}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 460 }}>
        <div className="flex justify-between items-center mb-4 border-b border-border pb-3">
          <h3 className="modal-title m-0 flex items-center gap-2"><ShieldAlert size={18} /> Regularizar pago vencido</h3>
          <button onClick={onClose} className="btn btn-ghost btn-sm"><X size={16} /></button>
        </div>
        <div className="bg-bg-dark p-3 rounded border border-border mb-4 text-sm">
          <p className="font-bold">{debt.studentName}</p>
          <p className="text-text-muted">{debt.description} · venció el {new Date(debt.dueDate).toLocaleDateString('es-PE', { timeZone: 'UTC' })} ({debt.daysOverdue} día(s))</p>
          <p>Pendiente: <b className="text-danger">{money(debt.amountPending)}</b></p>
        </div>
        <form onSubmit={submit}>
          <div className="form-group">
            <label className="form-label">Decisión</label>
            <div className="flex gap-2">
              <button type="button" className={`btn btn-sm ${charge ? 'btn-warning' : 'btn-ghost'}`} onClick={() => setCharge(true)}>Cobrar mora</button>
              <button type="button" className={`btn btn-sm ${!charge ? 'btn-primary' : 'btn-ghost'}`} onClick={() => setCharge(false)}>Sin mora</button>
            </div>
          </div>
          {charge && (
            <div className="form-group">
              <label className="form-label">Monto de la mora (S/.)</label>
              <input className="form-control" type="number" step="0.01" min="0.01" value={amount} onChange={e => setAmount(e.target.value)} />
              <span className="text-xs text-text-muted">Se crea una deuda aparte "Mora · {debt.description}" que se cobra junto con la mensualidad.</span>
            </div>
          )}
          <div className="form-group">
            <label className="form-label">Observación *</label>
            <textarea className="form-control" required minLength={5} maxLength={500} value={note} onChange={e => setNote(e.target.value)}
              placeholder={charge ? 'Ej. Pagó fuera de fecha sin aviso.' : 'Ej. Viajó por salud; se exonera la mora.'} />
          </div>
          <div className="modal-footer mt-4 flex justify-end gap-2">
            <button type="button" onClick={onClose} className="btn btn-ghost">Cancelar</button>
            <button type="submit" disabled={saving} className="btn btn-primary">{saving ? 'Guardando…' : 'Regularizar'}</button>
          </div>
        </form>
      </div>
    </div>
  );
}
