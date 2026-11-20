import { useEffect, useState } from 'react';
import { BellRing, Cake, ChevronDown, ChevronUp, Trophy } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../api/axios';

const HOURS = Array.from({ length: 16 }, (_, i) => i + 5); // 5:00 a 20:00
const hourLabel = h => `${String(h).padStart(2, '0')}:00`;

/** Avisos automáticos de la academia: recordatorio de encuentros y saludos de cumpleaños (solo el administrador). */
export default function NotificationSettings() {
  const [form, setForm] = useState(null);
  const [saving, setSaving] = useState(false);
  const [open, setOpen] = useState(false);
  const set = patch => setForm(f => ({ ...f, ...patch }));

  useEffect(() => {
    api.get('/academy-config/notification-settings')
      .then(({ data }) => setForm({ ...data, birthdayMessage: data.birthdayMessage || '' }))
      .catch(() => toast.error('No se pudo cargar la configuración de avisos.'));
  }, []);

  const save = async e => {
    e.preventDefault();
    setSaving(true);
    try {
      const { data } = await api.put('/academy-config/notification-settings', { ...form, birthdayMessage: form.birthdayMessage.trim() || null });
      setForm({ ...data, birthdayMessage: data.birthdayMessage || '' });
      toast.success('Avisos automáticos guardados.');
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo guardar.');
    } finally {
      setSaving(false);
    }
  };

  if (!form) return null;
  const summary = [
    form.matchEnabled ? `Encuentros: ${form.matchHoursBefore === 1 ? '1 hora' : `${form.matchHoursBefore} horas`} antes` : 'Encuentros: apagado',
    form.birthdayEnabled || form.birthdayStaff ? `Cumpleaños: ${hourLabel(form.birthdayHour)}` : 'Cumpleaños: apagado',
  ].join(' · ');
  if (!open) {
    return (
      <div className="card notification-settings ns-collapsed">
        <div className="flex items-center gap-2 min-w-0">
          <BellRing size={20} className="text-primary shrink-0" />
          <div className="min-w-0">
            <div className="font-bold">Avisos automáticos</div>
            <div className="text-xs text-text-muted">{summary}</div>
          </div>
        </div>
        <button type="button" className="btn btn-outline btn-sm" onClick={() => setOpen(true)} aria-expanded="false">
          Configurar <ChevronDown size={14} />
        </button>
      </div>
    );
  }
  const preview = (form.birthdayMessage.trim() || form.defaultBirthdayMessage)
    .replaceAll('{nombre}', 'Tomás').replaceAll('{academia}', 'tu academia').replaceAll('{edad}', '11');

  return (
    <form className="card notification-settings" onSubmit={save}>
      <div className="flex items-center justify-between gap-2 mb-1">
        <h3 className="card-title flex items-center gap-2 m-0"><BellRing size={20} className="text-primary" /> Avisos automáticos</h3>
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => setOpen(false)} aria-expanded="true" aria-label="Plegar">
          <ChevronUp size={16} />
        </button>
      </div>
      <p className="text-sm text-text-muted mt-0 mb-4">Notificaciones que la academia envía sola a la app de las familias y del personal.</p>

      <div className="ns-block">
        <label className="ns-toggle">
          <input type="checkbox" checked={form.matchEnabled} onChange={e => set({ matchEnabled: e.target.checked })} />
          <Trophy size={16} className="text-primary" /> <b>Recordatorio de encuentros</b>
        </label>
        <p className="text-xs text-text-muted m-0">A las familias de los convocados y al entrenador responsable (amistosos y torneos).</p>
        {form.matchEnabled && (
          <div className="form-row">
            <div className="form-group">
              <label className="form-label" htmlFor="ns-hours">Enviar antes del encuentro</label>
              <select id="ns-hours" className="form-control" value={form.matchHoursBefore} onChange={e => set({ matchHoursBefore: Number(e.target.value) })}>
                {[1, 2, 3, 4, 6, 12, 24].map(h => <option key={h} value={h}>{h === 1 ? '1 hora antes' : `${h} horas antes`}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label className="form-label" htmlFor="ns-notime">Si aún no tiene hora, avisar ese día a las</label>
              <select id="ns-notime" className="form-control" value={form.matchNoTimeHour} onChange={e => set({ matchNoTimeHour: Number(e.target.value) })}>
                {HOURS.map(h => <option key={h} value={h}>{hourLabel(h)}</option>)}
              </select>
              <small className="text-muted">"Hoy hay encuentro… El horario aún no está confirmado: consúltalo con el coordinador."</small>
            </div>
          </div>
        )}
      </div>

      <div className="ns-block">
        <label className="ns-toggle">
          <input type="checkbox" checked={form.birthdayEnabled} onChange={e => set({ birthdayEnabled: e.target.checked })} />
          <Cake size={16} className="text-primary" /> <b>Saludo de cumpleaños a la familia</b>
        </label>
        <label className="ns-toggle">
          <input type="checkbox" checked={form.birthdayStaff} onChange={e => set({ birthdayStaff: e.target.checked })} />
          Avisar al personal (entrenadores de su categoría, coordinadores y administradores)
        </label>
        <p className="text-xs text-text-muted m-0">Los compañeros no reciben notificación: ven "Hoy cumple…" en el inicio de su app (cuidamos los datos de los menores).</p>
        {(form.birthdayEnabled || form.birthdayStaff) && (
          <div className="form-group" style={{ maxWidth: 260 }}>
            <label className="form-label" htmlFor="ns-bday-hour">Enviar a las</label>
            <select id="ns-bday-hour" className="form-control" value={form.birthdayHour} onChange={e => set({ birthdayHour: Number(e.target.value) })}>
              {HOURS.map(h => <option key={h} value={h}>{hourLabel(h)}</option>)}
            </select>
          </div>
        )}
        {form.birthdayEnabled && (
          <div className="form-group">
            <label className="form-label" htmlFor="ns-bday-msg">Saludo (opcional)</label>
            <textarea id="ns-bday-msg" className="form-control" rows={3} maxLength={300} placeholder={form.defaultBirthdayMessage}
              value={form.birthdayMessage} onChange={e => set({ birthdayMessage: e.target.value })} />
            <small className="text-muted">Vacío = el saludo de Clubio. Puedes usar {'{nombre}'}, {'{academia}'} y {'{edad}'}. {form.birthdayMessage.length}/300</small>
            <div className="ns-preview"><b>Así lo verá la familia:</b> {preview}</div>
          </div>
        )}
      </div>

      <button type="submit" className="btn btn-primary" disabled={saving}>{saving ? 'Guardando…' : 'Guardar avisos'}</button>
    </form>
  );
}
