import { useState } from 'react';
import toast from 'react-hot-toast';
import api from '../../api/axios';

const MONTHS = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

/**
 * Liquidación de alumnos por sesión y de sesiones extra de paquete.
 * Se hace sola el día 3 de cada mes; aquí se repite a mano tras corregir asistencias.
 */
export default function SessionLiquidationCard() {
  const now = new Date();
  const prev = new Date(now.getFullYear(), now.getMonth() - 1, 1);
  const [period, setPeriod] = useState({ year: prev.getFullYear(), month: prev.getMonth() + 1 });
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState(null);

  // Solo meses terminados: hasta 12 meses atrás.
  const options = Array.from({ length: 12 }, (_, i) => new Date(now.getFullYear(), now.getMonth() - 1 - i, 1));

  const run = async () => {
    setBusy(true);
    setResult(null);
    try {
      const { data } = await api.post('/finances/sessions/liquidate', period);
      setResult(data);
      const changes = data.generated + data.updated + data.removed;
      toast.success(changes ? `Sesiones liquidadas: ${data.generated} nuevas, ${data.updated} actualizadas, ${data.removed} anuladas.` : 'Sin cambios: la liquidación ya estaba al día.');
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo liquidar.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="card">
      <div className="card-header border-b border-border/50 pb-4 mb-4"><h3 className="card-title">Liquidar sesiones</h3></div>
      <p className="text-sm text-text-muted mb-3">
        Cobra a los alumnos <b>por sesión</b> lo que asistieron (Presente o Tarde) y a los de <b>paquete</b> sus sesiones extra,
        sin pasar de la mensualidad y descontando lo cobrado en campo. Se hace sola el día 3 de cada mes.
        Repítela si corriges asistencias: nunca toca lo ya pagado.
      </p>
      <div className="form-group">
        <label className="form-label">Mes</label>
        <select className="form-control" value={`${period.year}-${period.month}`}
          onChange={e => { const [y, m] = e.target.value.split('-').map(Number); setPeriod({ year: y, month: m }); }}>
          {options.map(d => <option key={d.toISOString()} value={`${d.getFullYear()}-${d.getMonth() + 1}`}>{MONTHS[d.getMonth()]} {d.getFullYear()}</option>)}
        </select>
      </div>
      <button onClick={run} disabled={busy} className="btn btn-primary w-full">{busy ? 'Liquidando...' : `Liquidar ${MONTHS[period.month - 1]} ${period.year}`}</button>
      {result?.notes?.length > 0 && (
        <ul className="text-xs text-warning mt-3" style={{ paddingLeft: 16, listStyle: 'disc' }}>
          {result.notes.map(n => <li key={n}>{n}</li>)}
        </ul>
      )}
    </div>
  );
}
