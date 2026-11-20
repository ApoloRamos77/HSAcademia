import { useEffect, useState } from 'react';
import { Navigate } from 'react-router-dom';
import { canConfigureCollections } from '../../utils/financeSections';
import toast from 'react-hot-toast';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import ReceiptProfileCard from '../../components/finances/ReceiptProfileCard';
import DiscountsTab from '../../components/finances/DiscountsTab';
import { useAuth } from '../../context/AuthContext';

/**
 * Configuración → Cobros y descuentos: lo que se define una vez (día de cobro, mora sugerida, datos de los recibos,
 * descuentos, promociones y referidos). Las acciones de cada mes quedan en Cobranzas → Acciones del mes.
 */
export default function CobrosConfig() {
  const { isAdmin, can, academyWide } = useAuth();
  // Igual que la antigua pestaña: exige el permiso y alcance de toda la academia (el servidor también lo exige al guardar).
  const allowed = canConfigureCollections({ can, academyWide });
  const [config, setConfig] = useState(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!allowed) return;
    api.get('/finances/config').then(r => setConfig(r.data)).catch(() => toast.error('No se pudo cargar la configuración de cobros.'));
  }, [allowed]);

  if (!allowed) return <Navigate to="/dashboard" replace />;

  const save = async (e) => {
    e.preventDefault();
    setSaving(true);
    try { await api.put('/finances/config', config); toast.success('Configuración guardada.'); }
    catch (err) { toast.error(err.response?.data?.message || 'No se pudo guardar la configuración.'); }
    finally { setSaving(false); }
  };

  return (
    <AppLayout title="Cobros y descuentos">
      <div className="fade-in flex flex-col gap-6">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 items-start">
          <div className="card">
            <div className="card-header border-b border-border/50 pb-4 mb-4"><h3 className="card-title">Día de cobro y mora</h3></div>
            {!config ? <div className="text-center p-4"><span className="spinner" style={{ borderColor: 'var(--primary)', borderTopColor: 'transparent' }} /></div> : (
              <form onSubmit={save}>
                <div className="form-group">
                  <label className="form-label" htmlFor="cfg-day">Día de cobro mensual</label>
                  <p className="text-sm text-text-muted mb-2">Las mensualidades vencen este día de cada mes.</p>
                  <select id="cfg-day" className="form-control" value={config.defaultPaymentDay}
                    onChange={e => setConfig({ ...config, defaultPaymentDay: parseInt(e.target.value, 10) })}>
                    {[...Array(31).keys()].map(i => <option key={i + 1} value={i + 1}>Día {i + 1}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label className="form-label" htmlFor="cfg-late">Mora sugerida (S/)</label>
                  <p className="text-sm text-text-muted mb-2">Pasado el vencimiento, la mensualidad queda "por regularizar": al cobrarla, administración decide cobrar esta mora (puede cambiar el monto) o exonerarla, siempre con una observación.</p>
                  <input id="cfg-late" type="number" step="0.01" min="0" className="form-control" value={config.lateFeeAmount ?? 0}
                    onChange={e => setConfig({ ...config, lateFeeAmount: e.target.value === '' ? 0 : parseFloat(e.target.value) })} />
                </div>
                <button type="submit" className="btn btn-primary mt-2" disabled={saving}>{saving ? 'Guardando…' : 'Guardar'}</button>
              </form>
            )}
          </div>
          {isAdmin && <ReceiptProfileCard />}
        </div>
        <DiscountsTab />
      </div>
    </AppLayout>
  );
}
