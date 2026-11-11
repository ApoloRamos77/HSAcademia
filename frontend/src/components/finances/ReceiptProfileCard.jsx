import { useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import api from '../../api/axios';
import { saveReceiptProfile } from '../../utils/pdfGenerator';

/**
 * Dirección y teléfono que salen en TODOS los recibos, se cobren en campo, en otra sede o en oficina.
 * Solo el administrador la cambia.
 */
export default function ReceiptProfileCard() {
  const [form, setForm] = useState({ name: '', address: '', phone: '' });
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    api.get('/academy-config/receipt-profile')
      .then(r => setForm({ name: r.data.name || '', address: r.data.address || '', phone: r.data.phone || '' }))
      .catch(() => toast.error('No se pudieron cargar los datos de los recibos.'))
      .finally(() => setLoading(false));
  }, []);

  const submit = async (e) => {
    e.preventDefault();
    setSaving(true);
    try {
      const { data } = await api.put('/academy-config/receipt-profile', { name: form.name, address: form.address, phone: form.phone });
      saveReceiptProfile(data);
      toast.success('Datos de los recibos guardados.');
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo guardar.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="card">
      <div className="card-header border-b border-border/50 pb-4 mb-4"><h3 className="card-title">Datos de los recibos</h3></div>
      {loading ? <div className="spinner w-6 h-6" /> : (
        <form onSubmit={submit}>
          <p className="text-sm text-text-muted mb-3">
            Salen en todos los recibos de <b>{form.name}</b>, sin importar la sede ni si el cobro se hizo en campo.
          </p>
          <div className="form-group">
            <label className="form-label">Dirección</label>
            <textarea className="form-control" rows={2} maxLength={200} required value={form.address}
              onChange={e => setForm({ ...form, address: e.target.value })}
              placeholder="Ej. Jr. J.C. Mariátegui 315 Block E1, Dpto 102, Condominio La Pradera Club, Puente Piedra" />
            <span className="text-xs text-text-muted">{form.address.length}/200 · se imprime en hasta 3 líneas.</span>
          </div>
          <div className="form-group">
            <label className="form-label">Teléfono (opcional)</label>
            <input className="form-control" maxLength={50} value={form.phone} onChange={e => setForm({ ...form, phone: e.target.value })} />
          </div>
          <button type="submit" className="btn btn-primary w-full" disabled={saving}>{saving ? 'Guardando...' : 'Guardar'}</button>
        </form>
      )}
    </div>
  );
}
