import { useEffect, useState } from 'react';
import { Plus, Pencil, Trash2, X, Tag, Users, Clock, Megaphone } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../api/axios';
import ReferralsPanel from './ReferralsPanel';

const KINDS = {
  Sibling: { label: 'Hermanos', icon: Users, help: 'Automático al generar la mensualidad. Los hijos de mayor tarifa pagan completo; el descuento aplica desde el hijo indicado.' },
  EarlyPayment: { label: 'Pronto pago', icon: Clock, help: 'Automático al cancelar la mensualidad completa hasta X días antes del vencimiento.' },
  Promotion: { label: 'Promoción', icon: Megaphone, help: 'El cajero la elige al cobrar mientras esté vigente. Puede aplicar a mensualidades, productos o ambos.' },
};
const SCOPES = { MonthlyFees: 'Mensualidades', Products: 'Productos', All: 'Mensualidades y productos' };
const empty = { name: '', description: '', kind: 'Promotion', valueType: 'Percent', value: '', scope: 'MonthlyFees', fromSiblingNumber: 2, daysBeforeDue: 0, validFrom: '', validTo: '', categoryId: '', isActive: true };
const fmtDate = (d) => d ? new Date(d).toLocaleDateString('es-PE', { timeZone: 'UTC' }) : '';

/** Configuración de descuentos: hermanos, pronto pago y promociones con vigencia. */
export default function DiscountsTab() {
  const [rules, setRules] = useState([]);
  const [categories, setCategories] = useState([]);
  const [form, setForm] = useState(null); // null | { id?, ...campos }
  const [saving, setSaving] = useState(false);

  const load = () => api.get('/discounts').then(r => setRules(r.data)).catch(() => toast.error('No se pudieron cargar los descuentos.'));
  useEffect(() => {
    load();
    api.get('/academy-config/categories').then(r => setCategories(r.data.filter(c => c.isActive))).catch(() => {});
  }, []);

  const edit = (r) => setForm({
    ...empty, ...r, value: String(r.value), description: r.description || '',
    validFrom: r.validFrom ? r.validFrom.slice(0, 10) : '', validTo: r.validTo ? r.validTo.slice(0, 10) : '',
    categoryId: r.categoryId || '', fromSiblingNumber: r.fromSiblingNumber ?? 2, daysBeforeDue: r.daysBeforeDue ?? 0,
  });

  const save = async (e) => {
    e.preventDefault();
    setSaving(true);
    const payload = {
      ...form,
      value: parseFloat(String(form.value).replace(',', '.')) || 0,
      fromSiblingNumber: form.kind === 'Sibling' ? parseInt(form.fromSiblingNumber, 10) : null,
      daysBeforeDue: form.kind === 'EarlyPayment' ? parseInt(form.daysBeforeDue, 10) : null,
      validFrom: form.validFrom || null, validTo: form.validTo || null, categoryId: form.categoryId || null,
      scope: form.kind === 'Promotion' ? form.scope : 'MonthlyFees',
    };
    try {
      if (form.id) await api.put(`/discounts/${form.id}`, payload); else await api.post('/discounts', payload);
      toast.success('Descuento guardado.');
      setForm(null); load();
    } catch (err) { toast.error(err.response?.data?.message || 'No se pudo guardar.'); }
    finally { setSaving(false); }
  };

  const remove = async (r) => {
    if (!window.confirm(`¿Eliminar "${r.name}"? Los cobros ya registrados no cambian.`)) return;
    try { await api.delete(`/discounts/${r.id}`); toast.success('Descuento eliminado.'); load(); }
    catch (err) { toast.error(err.response?.data?.message || 'No se pudo eliminar.'); }
  };

  const toggle = async (r) => {
    try { await api.put(`/discounts/${r.id}`, { ...r, isActive: !r.isActive }); load(); }
    catch (err) { toast.error(err.response?.data?.message || 'No se pudo actualizar.'); }
  };

  return (
    <div className="fade-in">
      <div className="card">
        <div className="p-4 border-b border-border/50 flex flex-wrap justify-between items-center gap-3">
          <div>
            <h3 className="card-title m-0 flex items-center gap-2"><Tag size={16} /> Descuentos y promociones</h3>
            <p className="text-xs text-text-muted mt-1">El descuento por hermanos se aplica al generar las mensualidades (vuelve a ejecutar el motor del mes para actualizar las ya generadas). Pronto pago y promociones no se acumulan entre sí: se aplica el mayor.</p>
          </div>
          <button className="btn btn-primary btn-sm flex items-center gap-1" onClick={() => setForm({ ...empty })}><Plus size={14} /> Nuevo descuento</button>
        </div>
        <div className="table-container">
          <table className="table">
            <thead><tr><th>Nombre</th><th>Tipo</th><th>Valor</th><th>Condición</th><th>Vigencia</th><th>Estado</th><th className="text-right">Acciones</th></tr></thead>
            <tbody>
              {rules.map(r => {
                const K = KINDS[r.kind] || KINDS.Promotion;
                return (
                  <tr key={r.id} className={!r.isActive ? 'opacity-60' : ''}>
                    <td className="font-medium">{r.name}{r.description && <div className="text-xs text-text-muted">{r.description}</div>}</td>
                    <td className="text-sm"><span className="flex items-center gap-1"><K.icon size={13} /> {K.label}</span></td>
                    <td className="font-bold">{r.valueType === 'Percent' ? `${r.value}%` : `S/. ${Number(r.value).toFixed(2)}`}</td>
                    <td className="text-xs">
                      {r.kind === 'Sibling' && `Desde el ${r.fromSiblingNumber}.º hijo`}
                      {r.kind === 'EarlyPayment' && (r.daysBeforeDue ? `Pagando ${r.daysBeforeDue} día(s) antes del vencimiento` : 'Pagando hasta el día del vencimiento')}
                      {r.kind === 'Promotion' && SCOPES[r.scope]}
                      {r.categoryName && <div>Solo {r.categoryName}</div>}
                    </td>
                    <td className="text-xs">{r.validFrom || r.validTo ? `${fmtDate(r.validFrom) || '…'} — ${fmtDate(r.validTo) || '…'}` : 'Sin límite'}</td>
                    <td>
                      <button className={`badge ${r.isActive ? (r.isCurrentlyValid ? 'badge-success' : 'badge-warning') : 'badge-danger'}`} onClick={() => toggle(r)} title="Activar / desactivar">
                        {r.isActive ? (r.isCurrentlyValid ? 'Vigente' : 'Fuera de fecha') : 'Inactivo'}
                      </button>
                    </td>
                    <td className="text-right whitespace-nowrap">
                      <button className="btn btn-sm btn-ghost text-primary" onClick={() => edit(r)}><Pencil size={13} /></button>
                      <button className="btn btn-sm btn-ghost text-danger" onClick={() => remove(r)}><Trash2 size={13} /></button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        {rules.length === 0 && <div className="empty-state"><Tag size={36} className="text-text-muted" /><p>Aún no hay descuentos. Crea, por ejemplo, "Hermanos 10% desde el 2.º hijo" o "Pronto pago 5%".</p></div>}
      </div>

      <ReferralsPanel />

      {form && (
        <div className="modal-overlay" onClick={() => setForm(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 560 }}>
            <div className="flex justify-between items-center mb-4 border-b border-border pb-3">
              <h3 className="modal-title m-0">{form.id ? 'Editar descuento' : 'Nuevo descuento'}</h3>
              <button onClick={() => setForm(null)} className="btn btn-ghost btn-sm"><X size={16} /></button>
            </div>
            <form onSubmit={save}>
              <div className="form-group">
                <label className="form-label">Tipo</label>
                <div className="flex gap-2 flex-wrap">
                  {Object.entries(KINDS).map(([k, v]) => (
                    <button type="button" key={k} onClick={() => setForm({ ...form, kind: k })}
                      className={`btn btn-sm flex items-center gap-1 ${form.kind === k ? 'btn-primary' : 'btn-ghost'}`}><v.icon size={13} /> {v.label}</button>
                  ))}
                </div>
                <p className="text-xs text-text-muted mt-2">{KINDS[form.kind].help}</p>
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Nombre *</label>
                  <input className="form-control" required maxLength={100} value={form.name} onChange={e => setForm({ ...form, name: e.target.value })}
                    placeholder={form.kind === 'Sibling' ? 'Hermanos' : form.kind === 'EarlyPayment' ? 'Pronto pago' : 'Campaña verano'} />
                </div>
                <div className="form-group">
                  <label className="form-label">Valor *</label>
                  <div className="flex gap-2">
                    <select className="form-control" style={{ maxWidth: 90 }} value={form.valueType} onChange={e => setForm({ ...form, valueType: e.target.value })}>
                      <option value="Percent">%</option><option value="Amount">S/.</option>
                    </select>
                    <input className="form-control" required type="number" step="0.01" min="0.01" value={form.value} onChange={e => setForm({ ...form, value: e.target.value })} />
                  </div>
                </div>
              </div>
              {form.kind === 'Sibling' && (
                <div className="form-group">
                  <label className="form-label">Aplica desde el hijo N°</label>
                  <input className="form-control" type="number" min="2" max="10" value={form.fromSiblingNumber} onChange={e => setForm({ ...form, fromSiblingNumber: e.target.value })} />
                  <span className="text-xs text-text-muted">Ej.: 2 = el segundo hijo y siguientes. Puedes crear otra regla "desde el 3.º" con más descuento.</span>
                </div>
              )}
              {form.kind === 'EarlyPayment' && (
                <div className="form-group">
                  <label className="form-label">Días antes del vencimiento</label>
                  <input className="form-control" type="number" min="0" max="31" value={form.daysBeforeDue} onChange={e => setForm({ ...form, daysBeforeDue: e.target.value })} />
                  <span className="text-xs text-text-muted">0 = pagando hasta el mismo día del vencimiento.</span>
                </div>
              )}
              {form.kind === 'Promotion' && (
                <div className="form-group">
                  <label className="form-label">Aplica a</label>
                  <select className="form-control" value={form.scope} onChange={e => setForm({ ...form, scope: e.target.value })}>
                    {Object.entries(SCOPES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
                  </select>
                </div>
              )}
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Vigente desde</label>
                  <input className="form-control" type="date" value={form.validFrom} onChange={e => setForm({ ...form, validFrom: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="form-label">Vigente hasta</label>
                  <input className="form-control" type="date" value={form.validTo} onChange={e => setForm({ ...form, validTo: e.target.value })} />
                </div>
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Solo para la categoría (opcional)</label>
                  <select className="form-control" value={form.categoryId} onChange={e => setForm({ ...form, categoryId: e.target.value })}>
                    <option value="">Todas</option>
                    {categories.map(c => <option key={c.id} value={c.id}>{c.name} · {c.headquarterName}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label className="form-label">Descripción (opcional)</label>
                  <input className="form-control" maxLength={300} value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} />
                </div>
              </div>
              <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={form.isActive} onChange={e => setForm({ ...form, isActive: e.target.checked })} /> Activo</label>
              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={() => setForm(null)} className="btn btn-ghost">Cancelar</button>
                <button type="submit" disabled={saving} className="btn btn-primary">{saving ? 'Guardando…' : 'Guardar'}</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
