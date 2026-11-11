import { useState } from 'react';
import { FileSpreadsheet, Download, AlertTriangle, Users, Wallet, CalendarCheck } from 'lucide-react';
import toast from 'react-hot-toast';
import AppLayout from '../../components/AppLayout';
import { useAuth } from '../../context/AuthContext';
import { downloadFile } from '../../utils/files';

const MONTHS = ['Enero', 'Febrero', 'Marzo', 'Abril', 'Mayo', 'Junio', 'Julio', 'Agosto', 'Septiembre', 'Octubre', 'Noviembre', 'Diciembre'];

/** Reportes descargables en CSV (se abren directamente en Excel). */
export default function Reportes() {
  const { user } = useAuth();
  const now = new Date();
  const [period, setPeriod] = useState({ year: now.getFullYear(), month: now.getMonth() + 1 });
  const [includeInactive, setIncludeInactive] = useState(false);
  const [busy, setBusy] = useState(null);

  const isAdmin = user?.role === 'AcademyAdmin';
  const can = p => isAdmin || user?.permissions?.includes(p);

  const download = async (key, url, name) => {
    setBusy(key);
    try { await downloadFile(url, name); }
    catch (err) {
      const msg = err.response?.status === 403 ? 'No tienes permiso para este reporte.' : 'No se pudo generar el reporte.';
      toast.error(msg);
    } finally { setBusy(null); }
  };

  const ym = `${period.year}-${String(period.month).padStart(2, '0')}`;
  const reports = [
    {
      key: 'overdue', icon: AlertTriangle, perm: 'reports.finance', title: 'Morosidad por antigüedad',
      desc: 'Deuda vencida por alumno en tramos de 0-30, 31-60, 61-90 y más de 90 días, con datos del apoderado para gestionar la cobranza.',
      action: () => download('overdue', '/reports/overdue', 'morosidad.csv'),
    },
    {
      key: 'payments', icon: Wallet, perm: 'reports.finance', usesPeriod: true, title: 'Cobros del mes',
      desc: 'Libro de caja: cada abono de mensualidad y venta de tienda con fecha, recibo, medio de pago y N° de operación.',
      action: () => download('payments', `/reports/payments?year=${period.year}&month=${period.month}`, `cobros_${ym}.csv`),
    },
    {
      key: 'attendance', icon: CalendarCheck, perm: 'reports.attendance', usesPeriod: true, title: 'Asistencia mensual',
      desc: 'Presentes, tardanzas, faltas y justificadas por alumno, con alerta de asistencia menor al 70%.',
      action: () => download('attendance', `/reports/attendance?year=${period.year}&month=${period.month}`, `asistencia_${ym}.csv`),
    },
    {
      key: 'students', icon: Users, perm: 'reports.students', title: 'Padrón de alumnos',
      desc: 'Listado completo con categoría, sede, apoderado, contactos, beneficio, mensualidad y contacto de emergencia.',
      action: () => download('students', `/reports/students?includeInactive=${includeInactive}`, 'padron_alumnos.csv'),
      // Solo el administrador ve a los alumnos retirados.
      extra: isAdmin && (
        <label className="flex items-center gap-2 text-xs mt-2" style={{ cursor: 'pointer' }}>
          <input type="checkbox" checked={includeInactive} onChange={e => setIncludeInactive(e.target.checked)} /> Incluir retirados
        </label>
      ),
    },
  ].filter(r => can(r.perm));

  return (
    <AppLayout title="Reportes">
      <div className="fade-in flex flex-col gap-5">
        <div className="card">
          <div className="card-header">
            <div>
              <h3 className="card-title flex items-center gap-2"><FileSpreadsheet size={20} className="text-primary" /> Reportes</h3>
              <p className="text-muted mt-1">Descarga en formato CSV; se abren directamente en Excel.</p>
            </div>
            <div className="flex gap-2 items-center">
              <span className="text-sm text-text-muted">Periodo</span>
              <select className="form-control" value={period.month} onChange={e => setPeriod({ ...period, month: Number(e.target.value) })}>
                {MONTHS.map((m, i) => <option key={m} value={i + 1}>{m}</option>)}
              </select>
              <select className="form-control" value={period.year} onChange={e => setPeriod({ ...period, year: Number(e.target.value) })}>
                {[0, 1, 2].map(d => now.getFullYear() - d).map(y => <option key={y} value={y}>{y}</option>)}
              </select>
            </div>
          </div>

          {reports.length === 0 ? (
            <div className="empty-state"><FileSpreadsheet size={40} /><p>No tienes permisos para descargar reportes.</p></div>
          ) : (
            <div className="grid gap-4" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))' }}>
              {reports.map(r => (
                <div key={r.key} className="rounded-xl border border-border p-4 flex flex-col gap-2">
                  <div className="flex items-center gap-2 font-bold text-text-primary"><r.icon size={18} className="text-primary" /> {r.title}</div>
                  <p className="text-sm text-text-secondary m-0 flex-1">{r.desc}</p>
                  {r.usesPeriod && <span className="text-xs text-text-muted">Periodo: {MONTHS[period.month - 1]} {period.year}</span>}
                  {r.extra}
                  <button className="btn btn-primary btn-sm flex items-center gap-2 mt-2" style={{ alignSelf: 'flex-start' }} onClick={r.action} disabled={busy === r.key}>
                    <Download size={14} /> {busy === r.key ? 'Generando…' : 'Descargar'}
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </AppLayout>
  );
}
