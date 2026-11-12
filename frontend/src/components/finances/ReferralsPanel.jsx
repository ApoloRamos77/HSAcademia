import { useEffect, useMemo, useState } from 'react';
import { Plus, Pencil, Trash2, X, Gift, UserPlus, Ban, RefreshCw, Trophy } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../api/axios';

const money = (v) => `S/ ${Number(v || 0).toFixed(2)}`;
const fmtDate = (d) => d ? new Date(d).toLocaleDateString('es-PE', { timeZone: 'UTC' }) : '';
const fmtDateTime = (d) => d ? new Date(d).toLocaleDateString('es-PE') : '';
const todayISO = () => new Date().toLocaleDateString('en-CA', { timeZone: 'America/Lima' });
const STATUS = {
  Registered: { label: 'Registrado', cls: 'badge-warning', help: 'El alumno nuevo aún no paga su primer cobro.' },
  Earned: { label: 'Ganado', cls: 'badge-info', help: 'El alumno nuevo pagó; falta aplicar parte del beneficio en la siguiente mensualidad.' },
  Completed: { label: 'Completado', cls: 'badge-success', help: 'Beneficios aplicados.' },
  Annulled: { label: 'Anulado', cls: 'badge-danger', help: 'Anulado por administración.' },
};
const emptyCampaign = () => ({ name: '', description: '', validFrom: todayISO(), validTo: '', referrerAmount: '', referredAmount: '0', categoryId: '', isActive: true });

/** Buscador de alumno: escribe el nombre y elige de la lista. */
export function StudentSelect({ students, value, onChange, placeholder = 'Buscar alumno…', exclude = [] }) {
  const [text, setText] = useState('');
  const selected = students.find(s => s.id === value);
  const options = useMemo(() => {
    const q = text.trim().toLowerCase();
    return students
      .filter(s => s.isActive !== false && !exclude.includes(s.id))
      .filter(s => !q || `${s.firstName} ${s.lastName} ${s.documentNumber || ''}`.toLowerCase().includes(q))
      .slice(0, 30);
  }, [students, text, exclude]);
  if (selected) {
    return (
      <div className="form-control flex items-center justify-between gap-2">
        <span className="truncate">{selected.firstName} {selected.lastName}{selected.categoryName ? ` · ${selected.categoryName}` : ''}</span>
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => { onChange(''); setText(''); }} title="Quitar"><X size={14} /></button>
      </div>
    );
  }
  return (
    <div>
      <input className="form-control" value={text} onChange={e => setText(e.target.value)} placeholder={placeholder} />
      {text.trim().length > 0 && (
        <div className="border border-border rounded mt-1 max-h-48 overflow-auto" style={{ background: "var(--bg-card)" }}>
          {options.length === 0 && <div className="p-2 text-xs text-text-muted">Sin resultados</div>}
          {options.map(s => (
            <button type="button" key={s.id} className="block w-full text-left px-3 py-2 text-sm hover:bg-bg-surface" style={{ background: "transparent", border: 0 }}
              onClick={() => { onChange(s.id); setText(''); }}>
              {s.firstName} {s.lastName}<span className="text-xs text-text-muted">{s.categoryName ? ` · ${s.categoryName}` : ''}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

/** Programa de referidos: campañas, registro, seguimiento e indicadores. */
export default function ReferralsPanel() {
  const [campaigns, setCampaigns] = useState([]);
  const [referrals, setReferrals] = useState([]);
  const [summary, setSummary] = useState(null);
  const [categories, setCategories] = useState([]);
  const [students, setStudents] = useState([]);
  const [filterCampaign, setFilterCampaign] = useState('');
  const [filterStatus, setFilterStatus] = useState('');
  const [campaignForm, setCampaignForm] = useState(null);
  const [registerForm, setRegisterForm] = useState(null);
  const [detail, setDetail] = useState(null);
  const [saving, setSaving] = useState(false);

  const loadCampaigns = () => api.get('/referrals/campaigns').then(r => setCampaigns(r.data)).catch(() => toast.error('No se pudieron cargar las campañas.'));
  const loadReferrals = () => {
    const qs = new URLSearchParams();
    if (filterCampaign) qs.set('campaignId', filterCampaign);
    if (filterStatus) qs.set('status', filterStatus);
    api.get(`/referrals?${qs}`).then(r => setReferrals(r.data)).catch(() => toast.error('No se pudieron cargar los referidos.'));
    api.get(`/referrals/summary${filterCampaign ? `?campaignId=${filterCampaign}` : ''}`).then(r => setSummary(r.data)).catch(() => {});
  };
  useEffect(() => {
    loadCampaigns();
    api.get('/academy-config/categories').then(r => setCategories(r.data.filter(c => c.isActive))).catch(() => {});
    api.get('/students').then(r => setStudents(r.data)).catch(() => {});
  }, []);
  useEffect(() => { loadReferrals(); }, [filterCampaign, filterStatus]);

  const current = campaigns.find(c => c.isCurrent);

  const saveCampaign = async (e) => {
    e.preventDefault();
    setSaving(true);
    const f = campaignForm;
    const payload = {
      name: f.name, description: f.description || null, validFrom: f.validFrom, validTo: f.validTo,
      referrerAmount: parseFloat(String(f.referrerAmount).replace(',', '.')) || 0,
      referredAmount: parseFloat(String(f.referredAmount || '0').replace(',', '.')) || 0,
      categoryId: f.categoryId || null, isActive: f.isActive,
    };
    try {
      if (f.id) await api.put(`/referrals/campaigns/${f.id}`, payload); else await api.post('/referrals/campaigns', payload);
      toast.success('Campaña guardada. Los referidos ya registrados conservan sus montos.');
      setCampaignForm(null); loadCampaigns(); loadReferrals();
    } catch (err) { toast.error(err.response?.data?.message || 'No se pudo guardar la campaña.'); }
    finally { setSaving(false); }
  };

  const removeCampaign = async (c) => {
    if (!window.confirm(`¿Eliminar la campaña "${c.name}"?`)) return;
    try { await api.delete(`/referrals/campaigns/${c.id}`); toast.success('Campaña eliminada.'); loadCampaigns(); }
    catch (err) { toast.error(err.response?.data?.message || 'No se pudo eliminar.'); }
  };

  const register = async (e) => {
    e.preventDefault();
    if (!registerForm.referredStudentId || !registerForm.referrerStudentId) { toast.error('Elige al alumno nuevo y a quien lo recomendó.'); return; }
    setSaving(true);
    try {
      await api.post('/referrals', registerForm);
      toast.success('Referido registrado.');
      setRegisterForm(null); loadCampaigns(); loadReferrals();
    } catch (err) { toast.error(err.response?.data?.message || 'No se pudo registrar.'); }
    finally { setSaving(false); }
  };

  const annul = async (r) => {
    const reason = window.prompt(`Motivo para anular el referido de ${r.referredName} (lo ya pagado no cambia):`);
    if (reason === null) return;
    try { await api.post(`/referrals/${r.id}/annul`, { reason }); toast.success('Referido anulado.'); setDetail(null); loadCampaigns(); loadReferrals(); }
    catch (err) { toast.error(err.response?.data?.message || 'No se pudo anular.'); }
  };

  const sync = async () => {
    try { await api.post('/referrals/sync'); toast.success('Beneficios recalculados.'); loadReferrals(); }
    catch (err) { toast.error(err.response?.data?.message || 'No se pudo recalcular.'); }
  };

  return (
    <div className="fade-in mt-4">
      {/* ── Campañas ── */}
      <div className="card">
        <div className="p-4 border-b border-border/50 flex flex-wrap justify-between items-center gap-3">
          <div>
            <h3 className="card-title m-0 flex items-center gap-2"><Gift size={16} /> Programa de referidos</h3>
            <p className="text-xs text-text-muted mt-1">
              Quien recomienda gana su descuento cuando el alumno nuevo paga su primer cobro, y se aplica en su siguiente mensualidad (si sobra, pasa a la siguiente).
              El alumno nuevo puede recibir un descuento de bienvenida en su primer cobro. Los montos quedan fijos al registrar el referido.
              No aplica entre hermanos ni a exalumnos que pagaron en el último año.
            </p>
          </div>
          <button className="btn btn-primary btn-sm flex items-center gap-1" onClick={() => setCampaignForm(emptyCampaign())}><Plus size={14} /> Nueva campaña</button>
        </div>
        <div className="table-container">
          <table className="table">
            <thead><tr><th>Campaña</th><th>Vigencia</th><th>Quien recomienda</th><th>Alumno nuevo</th><th>Referidos</th><th>Estado</th><th className="text-right">Acciones</th></tr></thead>
            <tbody>
              {campaigns.map(c => (
                <tr key={c.id} className={!c.isActive ? 'opacity-60' : ''}>
                  <td className="font-medium">{c.name}{c.description && <div className="text-xs text-text-muted">{c.description}</div>}{c.categoryName && <div className="text-xs">Solo {c.categoryName}</div>}</td>
                  <td className="text-xs">{fmtDate(c.validFrom)} — {fmtDate(c.validTo)}</td>
                  <td className="font-bold">{money(c.referrerAmount)}</td>
                  <td>{c.referredAmount > 0 ? money(c.referredAmount) : <span className="text-text-muted text-xs">Sin bienvenida</span>}</td>
                  <td className="text-sm">{c.referrals} <span className="text-xs text-text-muted">({c.earned} ganados)</span></td>
                  <td><span className={`badge ${c.isActive ? (c.isCurrent ? 'badge-success' : 'badge-warning') : 'badge-danger'}`}>{c.isActive ? (c.isCurrent ? 'Vigente' : 'Fuera de fecha') : 'Inactiva'}</span></td>
                  <td className="text-right whitespace-nowrap">
                    <button className="btn btn-sm btn-ghost text-primary" title="Editar" onClick={() => setCampaignForm({ ...c, validFrom: c.validFrom.slice(0, 10), validTo: c.validTo.slice(0, 10), referrerAmount: String(c.referrerAmount), referredAmount: String(c.referredAmount), categoryId: c.categoryId || '', description: c.description || '' })}><Pencil size={13} /></button>
                    <button className="btn btn-sm btn-ghost text-danger" title="Eliminar" onClick={() => removeCampaign(c)}><Trash2 size={13} /></button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {campaigns.length === 0 && <div className="empty-state"><Gift size={36} className="text-text-muted" /><p>Crea una campaña, por ejemplo "Referidos Diciembre": S/ 30 para quien recomienda y S/ 20 de bienvenida.</p></div>}
      </div>

      {/* ── Indicadores ── */}
      {summary && (summary.registered + summary.earned + summary.annulled) > 0 && (
        <div className="grid grid-cols-2 md:grid-cols-5 gap-3 mt-4">
          <div className="card p-3"><div className="text-xs text-text-muted">Referidos</div><div className="text-xl font-bold">{summary.registered + summary.earned}</div></div>
          <div className="card p-3"><div className="text-xs text-text-muted">Ya pagaron (conversión)</div><div className="text-xl font-bold">{summary.earned} <span className="text-sm text-text-muted">· {summary.conversion}%</span></div></div>
          <div className="card p-3"><div className="text-xs text-text-muted">Descuento a referentes</div><div className="text-xl font-bold">{money(summary.referrerCost)}</div></div>
          <div className="card p-3"><div className="text-xs text-text-muted">Bienvenidas</div><div className="text-xl font-bold">{money(summary.referredCost)}</div></div>
          <div className="card p-3"><div className="text-xs text-text-muted">Por aplicar</div><div className="text-xl font-bold">{money(summary.pendingToApply)}</div></div>
        </div>
      )}

      {/* ── Referidos ── */}
      <div className="card mt-4">
        <div className="p-4 border-b border-border/50 flex flex-wrap justify-between items-center gap-3">
          <h3 className="card-title m-0 flex items-center gap-2"><UserPlus size={16} /> Referidos</h3>
          <div className="flex flex-wrap gap-2 items-center">
            <select className="form-control" style={{ width: 'auto' }} value={filterCampaign} onChange={e => setFilterCampaign(e.target.value)}>
              <option value="">Todas las campañas</option>
              {campaigns.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
            <select className="form-control" style={{ width: 'auto' }} value={filterStatus} onChange={e => setFilterStatus(e.target.value)}>
              <option value="">Todos los estados</option>
              {Object.entries(STATUS).map(([k, v]) => <option key={k} value={k}>{v.label}</option>)}
            </select>
            <button className="btn btn-ghost btn-sm flex items-center gap-1" onClick={sync} title="Aplica beneficios pendientes"><RefreshCw size={14} /> Recalcular</button>
            <button className="btn btn-primary btn-sm flex items-center gap-1" disabled={!current} title={current ? '' : 'No hay campaña vigente hoy'}
              onClick={() => setRegisterForm({ referredStudentId: '', referrerStudentId: '', notes: '' })}><Plus size={14} /> Registrar referido</button>
          </div>
        </div>
        <div className="table-container">
          <table className="table">
            <thead><tr><th>Alumno nuevo</th><th>Recomendado por</th><th>Campaña</th><th>Beneficio referente</th><th>Bienvenida</th><th>Estado</th><th className="text-right"></th></tr></thead>
            <tbody>
              {referrals.map(r => {
                const st = STATUS[r.status] || STATUS.Registered;
                return (
                  <tr key={r.id} className="cursor-pointer" onClick={() => setDetail(r)}>
                    <td className="font-medium">{r.referredName}<div className="text-xs text-text-muted">{r.referredCategory} · {fmtDateTime(r.createdAt)}</div></td>
                    <td>{r.referrerName}<div className="text-xs text-text-muted">{r.referrerCategory}</div></td>
                    <td className="text-xs">{r.campaignName}</td>
                    <td className="text-sm"><b>{money(r.referrerAmount)}</b>{r.status !== 'Annulled' && r.status !== 'Registered' && <div className="text-xs text-text-muted">Aplicado {money(r.referrerApplied)}{r.referrerPending > 0 ? ` · pendiente ${money(r.referrerPending)}` : ''}</div>}</td>
                    <td className="text-sm">{r.referredAmount > 0 ? <>{money(r.referredAmount)}{r.referredApplied > 0 && <div className="text-xs text-text-muted">Aplicado {money(r.referredApplied)}</div>}</> : <span className="text-xs text-text-muted">—</span>}</td>
                    <td><span className={`badge ${st.cls}`} title={st.help}>{st.label}</span></td>
                    <td className="text-right" onClick={e => e.stopPropagation()}>
                      {r.status !== 'Annulled' && <button className="btn btn-sm btn-ghost text-danger" title="Anular" onClick={() => annul(r)}><Ban size={13} /></button>}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        {referrals.length === 0 && <div className="empty-state"><UserPlus size={36} className="text-text-muted" /><p>Aún no hay referidos{filterCampaign || filterStatus ? ' con estos filtros' : ''}. También puedes registrarlos al inscribir al alumno nuevo.</p></div>}
        {summary?.topReferrers?.length > 0 && (
          <div className="p-4 border-t border-border/50">
            <div className="text-sm font-bold flex items-center gap-2 mb-2"><Trophy size={14} /> Familias que más recomiendan</div>
            <div className="flex flex-wrap gap-2">
              {summary.topReferrers.map(t => <span key={t.studentId} className="badge badge-info">{t.name} · {t.earned}/{t.referrals}{t.benefit > 0 ? ` · ${money(t.benefit)}` : ''}</span>)}
            </div>
          </div>
        )}
      </div>

      {/* ── Modal campaña ── */}
      {campaignForm && (
        <div className="modal-overlay" onClick={() => setCampaignForm(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 560 }}>
            <div className="flex justify-between items-center mb-4 border-b border-border pb-3">
              <h3 className="modal-title m-0">{campaignForm.id ? 'Editar campaña' : 'Nueva campaña de referidos'}</h3>
              <button onClick={() => setCampaignForm(null)} className="btn btn-ghost btn-sm"><X size={16} /></button>
            </div>
            <form onSubmit={saveCampaign}>
              <div className="form-group">
                <label className="form-label">Nombre *</label>
                <input className="form-control" required maxLength={100} value={campaignForm.name} placeholder="Referidos Diciembre 2026" onChange={e => setCampaignForm({ ...campaignForm, name: e.target.value })} />
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Descuento para quien recomienda (S/) *</label>
                  <input className="form-control" required type="number" step="0.01" min="0.01" value={campaignForm.referrerAmount} onChange={e => setCampaignForm({ ...campaignForm, referrerAmount: e.target.value })} />
                  <span className="text-xs text-text-muted">Se aplica en su siguiente mensualidad.</span>
                </div>
                <div className="form-group">
                  <label className="form-label">Bienvenida al alumno nuevo (S/)</label>
                  <input className="form-control" type="number" step="0.01" min="0" value={campaignForm.referredAmount} onChange={e => setCampaignForm({ ...campaignForm, referredAmount: e.target.value })} />
                  <span className="text-xs text-text-muted">En su primer cobro. 0 = sin bienvenida.</span>
                </div>
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Vigente desde *</label>
                  <input className="form-control" type="date" required value={campaignForm.validFrom} onChange={e => setCampaignForm({ ...campaignForm, validFrom: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="form-label">Vigente hasta *</label>
                  <input className="form-control" type="date" required value={campaignForm.validTo} onChange={e => setCampaignForm({ ...campaignForm, validTo: e.target.value })} />
                </div>
              </div>
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Solo para alumnos nuevos de (opcional)</label>
                  <select className="form-control" value={campaignForm.categoryId} onChange={e => setCampaignForm({ ...campaignForm, categoryId: e.target.value })}>
                    <option value="">Todas las categorías</option>
                    {categories.map(c => <option key={c.id} value={c.id}>{c.name} · {c.headquarterName}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label className="form-label">Descripción (opcional)</label>
                  <input className="form-control" maxLength={300} value={campaignForm.description} onChange={e => setCampaignForm({ ...campaignForm, description: e.target.value })} />
                </div>
              </div>
              <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={campaignForm.isActive} onChange={e => setCampaignForm({ ...campaignForm, isActive: e.target.checked })} /> Activa</label>
              {campaignForm.id && <p className="text-xs text-warning mt-2">Cambiar los montos solo afecta a los referidos que se registren desde ahora.</p>}
              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={() => setCampaignForm(null)} className="btn btn-ghost">Cancelar</button>
                <button type="submit" disabled={saving} className="btn btn-primary">{saving ? 'Guardando…' : 'Guardar'}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── Modal registrar ── */}
      {registerForm && (
        <div className="modal-overlay" onClick={() => setRegisterForm(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 520 }}>
            <div className="flex justify-between items-center mb-4 border-b border-border pb-3">
              <h3 className="modal-title m-0">Registrar referido</h3>
              <button onClick={() => setRegisterForm(null)} className="btn btn-ghost btn-sm"><X size={16} /></button>
            </div>
            <form onSubmit={register}>
              {current && <p className="text-sm mb-3">Campaña <b>{current.name}</b>: {money(current.referrerAmount)} para quien recomienda{current.referredAmount > 0 ? ` y ${money(current.referredAmount)} de bienvenida` : ''}.</p>}
              <div className="form-group">
                <label className="form-label">Alumno nuevo *</label>
                <StudentSelect students={students} value={registerForm.referredStudentId} exclude={[registerForm.referrerStudentId]}
                  onChange={id => setRegisterForm({ ...registerForm, referredStudentId: id })} placeholder="Nombre o documento del alumno nuevo" />
                <span className="text-xs text-text-muted">Debe registrarse antes de su primer cobro.</span>
              </div>
              <div className="form-group">
                <label className="form-label">Recomendado por *</label>
                <StudentSelect students={students} value={registerForm.referrerStudentId} exclude={[registerForm.referredStudentId]}
                  onChange={id => setRegisterForm({ ...registerForm, referrerStudentId: id })} placeholder="Alumno activo que lo recomendó" />
              </div>
              <div className="form-group">
                <label className="form-label">Observación (opcional)</label>
                <input className="form-control" maxLength={300} value={registerForm.notes} onChange={e => setRegisterForm({ ...registerForm, notes: e.target.value })} />
              </div>
              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={() => setRegisterForm(null)} className="btn btn-ghost">Cancelar</button>
                <button type="submit" disabled={saving} className="btn btn-primary">{saving ? 'Registrando…' : 'Registrar'}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── Detalle ── */}
      {detail && (
        <div className="modal-overlay" onClick={() => setDetail(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 600 }}>
            <div className="flex justify-between items-center mb-4 border-b border-border pb-3">
              <h3 className="modal-title m-0">{detail.referredName}</h3>
              <button onClick={() => setDetail(null)} className="btn btn-ghost btn-sm"><X size={16} /></button>
            </div>
            <div className="text-sm space-y-1">
              <div>Recomendado por <b>{detail.referrerName}</b> · campaña {detail.campaignName}</div>
              <div>Registrado el {fmtDateTime(detail.createdAt)}{detail.createdByName ? ` por ${detail.createdByName}` : ''}{detail.earnedAt ? ` · primer pago el ${fmtDateTime(detail.earnedAt)}` : ''}</div>
              <div>Estado: <span className={`badge ${(STATUS[detail.status] || STATUS.Registered).cls}`}>{(STATUS[detail.status] || STATUS.Registered).label}</span> <span className="text-xs text-text-muted">{(STATUS[detail.status] || STATUS.Registered).help}</span></div>
              {detail.notes && <div className="text-text-muted">“{detail.notes}”</div>}
              {detail.annulReason && <div className="text-danger">Anulado: {detail.annulReason}</div>}
            </div>
            <h4 className="font-bold mt-4 mb-2 text-sm">Descuentos aplicados</h4>
            {detail.benefits.length === 0 ? <p className="text-xs text-text-muted">Aún no se aplicó ningún descuento.</p> : (
              <table className="table">
                <thead><tr><th>Para</th><th>Deuda</th><th>Monto</th><th>Estado</th></tr></thead>
                <tbody>
                  {detail.benefits.map(b => (
                    <tr key={b.id} className={b.revertedAt ? 'opacity-60' : ''}>
                      <td className="text-xs">{b.side === 'Referrer' ? 'Quien recomienda' : 'Alumno nuevo'}</td>
                      <td className="text-xs">{b.debt}</td>
                      <td className="font-bold">{money(b.amount)}</td>
                      <td className="text-xs">{b.revertedAt ? `Revertido: ${b.revertReason}` : b.debtPaid ? 'Pagado' : 'Aplicado (por cobrar)'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            <div className="modal-footer mt-4 flex justify-end gap-2">
              {detail.status !== 'Annulled' && <button className="btn btn-ghost text-danger flex items-center gap-1" onClick={() => annul(detail)}><Ban size={14} /> Anular</button>}
              <button className="btn btn-primary" onClick={() => setDetail(null)}>Cerrar</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
