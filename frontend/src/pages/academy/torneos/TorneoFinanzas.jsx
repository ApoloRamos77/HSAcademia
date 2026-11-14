import { useCallback, useEffect, useState } from 'react';
import toast from 'react-hot-toast';
import { Plus, Pencil, Trash2, RefreshCw, X, AlertTriangle, Eye, Check, Ban } from 'lucide-react';
import api from '../../../api/axios';
import ConfirmDialog from '../../../components/ConfirmDialog';
import { errMsg, fmtDate, timeOf, dateOf, todayLima } from './torneoUtils';

const money = v => `S/ ${Number(v || 0).toFixed(2)}`;
const SUGGESTED = ['Inscripción', 'Arbitraje', 'Cancha', 'Pasaje / movilidad', 'Camiseta', 'Seguro'];
const CHARGE_STATUS = {
  1: { label: 'Cobrado', badge: 'badge-info' },
  2: { label: 'Por revisar', badge: 'badge-warning' },
  3: { label: 'No se cobra', badge: 'badge-muted' },
  4: { label: 'Exonerado', badge: 'badge-muted' },
};

function Modal({ title, onClose, children, footer, maxWidth = 560 }) {
  return (
    <div className="modal-overlay" onClick={e => e.target === e.currentTarget && onClose()}>
      <div className="modal-box" style={{ maxWidth }}>
        <div className="modal-header">
          <h3>{title}</h3>
          <button className="btn-icon" onClick={onClose} aria-label="Cerrar"><X size={18} /></button>
        </div>
        <div className="modal-body">{children}</div>
        {footer && <div className="modal-footer">{footer}</div>}
      </div>
    </div>
  );
}

export const feeAmountText = f => (f.amountMode === 2 ? `${money(f.amount)} a repartir` : `${money(f.amount)} por jugador`);

export default function TorneoFinanzas({ t, can }) {
  const [data, setData] = useState(null);
  const [modal, setModal] = useState(null);
  const [confirm, setConfirm] = useState(null);
  const [busy, setBusy] = useState(null);
  const canGenerate = can('collections.generate');
  const canAdjust = can('collections.adjust');
  const canExpenses = can('finances.expenses');

  const load = useCallback(async () => {
    try { setData((await api.get(`/tournaments/${t.id}/finance`)).data); }
    catch (e) { toast.error(errMsg(e, 'No se pudo cargar el balance')); setData({ error: true }); }
  }, [t.id]);
  useEffect(() => { load(); }, [load]);

  const sync = async () => {
    setBusy('sync');
    try {
      const { data: r } = await api.post(`/tournaments/${t.id}/fees/sync`);
      toast.success(`Recalculado · ${r.created} nuevo(s), ${r.updated} actualizado(s), ${r.voided} anulado(s)`);
      r.warnings?.forEach(w => toast(w, { icon: '⚠️' }));
      load();
    } catch (e) { toast.error(errMsg(e, 'No se pudo recalcular')); }
    finally { setBusy(null); }
  };

  const decide = async (row, charge) => {
    setBusy(row.id);
    try {
      await api.post(`/tournaments/charges/${row.id}/decision`, { charge });
      toast.success(charge ? 'Se le cobrará' : 'No se le cobrará');
      load();
    } catch (e) { toast.error(errMsg(e, 'No se pudo guardar la decisión')); }
    finally { setBusy(null); }
  };

  const doConfirm = async () => {
    const c = confirm; setConfirm(null);
    try {
      if (c.kind === 'fee') await api.delete(`/tournaments/fees/${c.item.id}`);
      if (c.kind === 'expense') await api.delete(`/tournaments/${t.id}/expenses/${c.item.id}`);
      toast.success('Eliminado');
      load();
    } catch (e) { toast.error(errMsg(e, 'No se pudo eliminar')); }
  };

  if (!data) return <p>Cargando…</p>;
  if (data.error) return <div className="card" style={{ padding: 18 }}><p className="text-muted" style={{ margin: 0 }}>No tienes acceso al balance de este torneo.</p></div>;

  return (
    <>
      <div className="stats-grid" style={{ marginBottom: 16 }}>
        <Kpi label="Cargado a familias" value={money(data.charged)} />
        <Kpi label="Cobrado" value={money(data.collected)} tone="var(--success, #10b981)" />
        <Kpi label="Por cobrar" value={money(data.pending)} tone="var(--warning, #f59e0b)" />
        <Kpi label="Gastos" value={money(data.expenses)} />
        <Kpi label="Resultado (cobrado − gastos)" value={money(data.result)} tone={data.result < 0 ? 'var(--danger, #ef4444)' : 'var(--success, #10b981)'}
          hint={`Si se cobra todo: ${money(data.projected)}`} />
      </div>

      {data.reviews.length > 0 && (
        <div className="card" style={{ padding: 16, marginBottom: 16, borderColor: '#f59e0b' }}>
          <h4 style={{ margin: '0 0 4px', display: 'flex', gap: 8, alignItems: 'center' }}><AlertTriangle size={16} /> Por revisar ({data.reviews.length})</h4>
          <p className="text-muted" style={{ margin: '0 0 10px', fontSize: 13 }}>Confirmaron su asistencia y no fueron. Decide si se les cobra.</p>
          <div className="table-wrap">
            <table className="table" style={{ width: '100%', fontSize: 13 }}>
              <thead><tr><th>Alumno</th><th>Concepto</th><th>Partido</th><th style={{ width: 200 }} /></tr></thead>
              <tbody>
                {data.reviews.map(r => (
                  <tr key={r.id}>
                    <td>{r.studentName}</td>
                    <td>{r.feeName}</td>
                    <td>{r.matchLabel}</td>
                    <td>
                      {canAdjust ? (
                        <div style={{ display: 'flex', gap: 6, justifyContent: 'flex-end', whiteSpace: 'nowrap' }}>
                          <button className="btn btn-sm btn-primary" disabled={busy === r.id} onClick={() => decide(r, true)}><Check size={13} /> Cobrar</button>
                          <button className="btn btn-sm btn-secondary" disabled={busy === r.id} onClick={() => decide(r, false)}><Ban size={13} /> No cobrar</button>
                        </div>
                      ) : <span className="text-muted" style={{ fontSize: 12 }}>Decide administración</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <div className="card" style={{ padding: 16, marginBottom: 16 }}>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginBottom: 10, flexWrap: 'wrap' }}>
          <h4 style={{ margin: 0 }}>Cobros a las familias</h4>
          {canGenerate && (
            <div style={{ marginLeft: 'auto', display: 'flex', gap: 8 }}>
              <button className="btn btn-secondary btn-sm" onClick={sync} disabled={busy === 'sync'} title="Vuelve a revisar planteles y asistencia">
                <RefreshCw size={14} /> Recalcular
              </button>
              <button className="btn btn-primary btn-sm" onClick={() => setModal({ kind: 'fee' })}><Plus size={14} /> Nuevo concepto</button>
            </div>
          )}
        </div>
        {data.fees.length === 0 ? (
          <p className="text-muted" style={{ margin: 0, fontSize: 13 }}>
            Agrega lo que se cobra a las familias: inscripción o camiseta (una sola vez), arbitraje, cancha o pasaje (por partido).
            Cada cobro aparece en Cobranzas y en la app de la familia.
          </p>
        ) : (
          <div className="table-wrap">
            <table className="table" style={{ width: '100%', fontSize: 13 }}>
              <thead>
                <tr><th>Concepto</th><th>Se cobra</th><th>Monto</th><th>Cargado</th><th>Cobrado</th><th>Por cobrar</th><th>Jugadores</th><th style={{ width: 100 }} /></tr>
              </thead>
              <tbody>
                {data.fees.map(f => (
                  <tr key={f.id} style={f.isActive ? undefined : { opacity: 0.6 }}>
                    <td>
                      <strong>{f.name}</strong>{!f.isActive && <span className="badge badge-muted" style={{ marginLeft: 6 }}>Inactivo</span>}
                      <div className="text-muted" style={{ fontSize: 11 }}>{f.teamName || 'Todos los equipos'}</div>
                    </td>
                    <td>{f.frequency === 1 ? 'Una vez' : 'Por partido'}</td>
                    <td>{feeAmountText(f)}</td>
                    <td>{money(f.charged)}</td>
                    <td>{money(f.collected)}</td>
                    <td>{money(f.pending)}</td>
                    <td style={{ fontSize: 12 }}>
                      {f.chargedCount}
                      {f.reviewCount > 0 && <span style={{ color: '#f59e0b' }}> · {f.reviewCount} por revisar</span>}
                      {f.exemptCount > 0 && <span className="text-muted"> · {f.exemptCount} exon.</span>}
                      {f.waivedCount > 0 && <span className="text-muted"> · {f.waivedCount} sin cobro</span>}
                    </td>
                    <td>
                      <div style={{ display: 'flex', gap: 2, justifyContent: 'flex-end' }}>
                        <button className="btn-icon" onClick={() => setModal({ kind: 'charges', fee: f })} aria-label="Ver cobros" title="Ver cobros"><Eye size={15} /></button>
                        {canGenerate && <button className="btn-icon" onClick={() => setModal({ kind: 'fee', fee: f })} aria-label="Editar concepto"><Pencil size={15} /></button>}
                        {canGenerate && <button className="btn-icon text-danger" onClick={() => setConfirm({ kind: 'fee', item: f })} aria-label="Eliminar concepto"><Trash2 size={15} /></button>}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div className="card" style={{ padding: 16 }}>
        <div style={{ display: 'flex', alignItems: 'center', marginBottom: 10 }}>
          <h4 style={{ margin: 0 }}>Gastos del torneo</h4>
          {canExpenses && <button className="btn btn-primary btn-sm" style={{ marginLeft: 'auto' }} onClick={() => setModal({ kind: 'expense' })}><Plus size={14} /> Registrar gasto</button>}
        </div>
        {data.expenseItems.length === 0 ? (
          <p className="text-muted" style={{ margin: 0, fontSize: 13 }}>Inscripción pagada al organizador, arbitraje, cancha, movilidad… Se registran también en Finanzas → Gastos.</p>
        ) : (
          <div className="table-wrap">
            <table className="table" style={{ width: '100%', fontSize: 13 }}>
              <thead><tr><th>Fecha</th><th>Detalle</th><th>Partido</th><th>Proveedor</th><th style={{ textAlign: 'right' }}>Monto</th><th style={{ width: 40 }} /></tr></thead>
              <tbody>
                {data.expenseItems.map(e => (
                  <tr key={e.id}>
                    <td>{fmtDate(e.date)}</td>
                    <td>{e.description.replace(`${t.name} · `, '')}</td>
                    <td>{e.matchLabel || <span className="text-muted">—</span>}</td>
                    <td>{e.supplier || <span className="text-muted">—</span>}</td>
                    <td style={{ textAlign: 'right' }}>{money(e.amount)}</td>
                    <td>{canExpenses && <button className="btn-icon text-danger" onClick={() => setConfirm({ kind: 'expense', item: e })} aria-label="Eliminar gasto"><Trash2 size={15} /></button>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {modal?.kind === 'fee' && <FeeModal t={t} fee={modal.fee} onClose={() => setModal(null)} onSaved={() => { setModal(null); load(); }} />}
      {modal?.kind === 'charges' && <ChargesModal fee={modal.fee} canAdjust={canAdjust} onClose={() => setModal(null)} onChanged={load} />}
      {modal?.kind === 'expense' && <ExpenseModal t={t} onClose={() => setModal(null)} onSaved={() => { setModal(null); load(); }} />}

      <ConfirmDialog
        isOpen={!!confirm}
        title={confirm?.kind === 'fee' ? 'Eliminar concepto' : 'Eliminar gasto'}
        message={confirm?.kind === 'fee' ? `¿Eliminar "${confirm.item.name}"?` : '¿Eliminar este gasto?'}
        detail={confirm?.kind === 'fee' ? 'Se anulan sus deudas sin pagos. Si alguna ya tiene pagos, desactívalo en su lugar.' : 'Se quita también de Finanzas → Gastos.'}
        confirmLabel="Sí, eliminar"
        onConfirm={doConfirm}
        onCancel={() => setConfirm(null)}
      />
    </>
  );
}

function Kpi({ label, value, tone, hint }) {
  return (
    <div className="stat-card">
      <div>
        <div className="stat-value" style={{ fontSize: 20, color: tone }}>{value}</div>
        <div className="stat-label">{label}</div>
        {hint && <div className="text-muted" style={{ fontSize: 11, marginTop: 2 }}>{hint}</div>}
      </div>
    </div>
  );
}

function FeeModal({ t, fee, onClose, onSaved }) {
  const [form, setForm] = useState(() => ({
    name: fee?.name || '', teamId: fee?.teamId || '', frequency: fee?.frequency || 2, amountMode: fee?.amountMode || 1,
    amount: fee?.amount ?? '', dueDate: dateOf(fee?.dueDate) || '', exemptScholarship: fee?.exemptScholarship ?? true,
    isActive: fee?.isActive ?? true, notes: fee?.notes || '',
  }));
  const [saving, setSaving] = useState(false);
  const set = patch => setForm(f => ({ ...f, ...patch }));
  const hasCharges = fee && (fee.chargedCount + fee.reviewCount + fee.waivedCount + fee.exemptCount) > 0;

  const save = async () => {
    if (!form.name.trim()) return toast.error('Ponle un nombre al concepto');
    if (!(Number(form.amount) > 0)) return toast.error('Indica el monto');
    const body = { ...form, teamId: form.teamId || null, amount: Number(form.amount), dueDate: form.frequency === 1 && form.dueDate ? form.dueDate : null };
    setSaving(true);
    try {
      const { data } = fee ? await api.put(`/tournaments/fees/${fee.id}`, body) : await api.post(`/tournaments/${t.id}/fees`, body);
      toast.success(`${fee ? 'Concepto actualizado' : 'Concepto creado'} · ${data.chargedCount} cobro(s)`);
      onSaved(data);
    } catch (e) { toast.error(errMsg(e, 'No se pudo guardar el concepto')); }
    finally { setSaving(false); }
  };

  const perMatch = form.frequency === 2;
  const example = form.amountMode === 2
    ? (perMatch ? `Ej.: cancha S/ 120 entre 12 que jugaron = S/ 10 c/u.` : `Ej.: inscripción del equipo S/ 600 entre 15 del plantel = S/ 40 c/u.`)
    : (perMatch ? `Ej.: arbitraje S/ 10 a cada uno que jugó.` : `Ej.: camiseta S/ 35 a cada jugador del plantel.`);

  return (
    <Modal title={fee ? `Editar ${fee.name}` : 'Nuevo concepto de cobro'} onClose={onClose} maxWidth={620}
      footer={<>
        <button className="btn btn-secondary" onClick={onClose}>Cancelar</button>
        <button className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Guardando…' : 'Guardar'}</button>
      </>}>
      <div className="form-row-2">
        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <label>Concepto *</label>
          <input className="form-control" value={form.name} onChange={e => set({ name: e.target.value })} placeholder="Ej: Arbitraje" />
          {!fee && (
            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', marginTop: 6 }}>
              {SUGGESTED.map(s => <button key={s} type="button" className="btn btn-ghost btn-sm" style={{ padding: '2px 8px' }}
                onClick={() => set({ name: s, frequency: ['Inscripción', 'Camiseta', 'Seguro'].includes(s) ? 1 : 2 })}>{s}</button>)}
            </div>
          )}
        </div>
        <div className="form-group">
          <label>¿Cuándo se cobra?</label>
          <select className="form-control" value={form.frequency} disabled={hasCharges} onChange={e => set({ frequency: Number(e.target.value) })}>
            <option value={1}>Una sola vez (al plantel)</option>
            <option value={2}>En cada partido (a quien jugó)</option>
          </select>
          {hasCharges && <small className="text-muted">Ya tiene cobros: para cambiarlo crea otro concepto.</small>}
        </div>
        <div className="form-group">
          <label>Monto</label>
          <select className="form-control" value={form.amountMode} onChange={e => set({ amountMode: Number(e.target.value) })}>
            <option value={1}>Por jugador</option>
            <option value={2}>Costo total a repartir</option>
          </select>
        </div>
        <div className="form-group">
          <label>{form.amountMode === 2 ? (perMatch ? 'Costo total por partido (S/) *' : 'Costo total (S/) *') : 'Monto por jugador (S/) *'}</label>
          <input type="number" min={0} step="0.10" className="form-control" value={form.amount} onChange={e => set({ amount: e.target.value })} />
        </div>
        <div className="form-group">
          <label>Equipo</label>
          <select className="form-control" value={form.teamId} onChange={e => set({ teamId: e.target.value })}>
            <option value="">Todos los equipos</option>
            {t.teams.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}
          </select>
        </div>
        {!perMatch && (
          <div className="form-group">
            <label>Vence</label>
            <input type="date" className="form-control" value={form.dueDate} onChange={e => set({ dueDate: e.target.value })} />
          </div>
        )}
        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <small className="text-muted">{example}{form.amountMode === 2 ? ' El reparto se recalcula si cambia quién paga (lo ya pagado no se toca).' : ''}</small>
        </div>
        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <label style={{ display: 'flex', gap: 8, alignItems: 'center', cursor: 'pointer' }}>
            <input type="checkbox" checked={form.exemptScholarship} onChange={e => set({ exemptScholarship: e.target.checked })} />
            Becados e invitados no pagan este concepto
          </label>
          {fee && (
            <label style={{ display: 'flex', gap: 8, alignItems: 'center', cursor: 'pointer', marginTop: 6 }}>
              <input type="checkbox" checked={form.isActive} onChange={e => set({ isActive: e.target.checked })} />
              Activo (si lo desactivas se anulan las deudas sin pagos)
            </label>
          )}
        </div>
        {perMatch && (
          <div className="alert alert-warning" style={{ gridColumn: '1/-1', fontSize: 12, margin: 0 }}>
            Se cobra a quien quede Presente o Tarde en la lista del partido. Quien confirmó y no fue queda "Por revisar" para que decidas.
            Partidos postergados o suspendidos no se cobran.
          </div>
        )}
      </div>
    </Modal>
  );
}

function ChargesModal({ fee, canAdjust, onClose, onChanged }) {
  const [rows, setRows] = useState(null);
  const load = useCallback(() => api.get(`/tournaments/fees/${fee.id}/charges`).then(r => setRows(r.data)).catch(() => setRows([])), [fee.id]);
  useEffect(() => { load(); }, [load]);
  const decide = async (r, charge) => {
    try { await api.post(`/tournaments/charges/${r.id}/decision`, { charge }); await load(); onChanged(); }
    catch (e) { toast.error(errMsg(e, 'No se pudo guardar')); }
  };
  return (
    <Modal title={`${fee.name} · ${feeAmountText(fee)}`} onClose={onClose} maxWidth={760}>
      {rows === null ? <p>Cargando…</p> : rows.length === 0 ? <p className="text-muted">Aún no hay cobros. Por partido se generan al tomar la lista.</p> : (
        <div className="table-wrap" style={{ maxHeight: 440, overflowY: 'auto' }}>
          <table className="table" style={{ width: '100%', fontSize: 13 }}>
            <thead><tr><th>Alumno</th>{fee.frequency === 2 && <th>Partido</th>}<th>Estado</th><th style={{ textAlign: 'right' }}>Monto</th><th style={{ textAlign: 'right' }}>Pagado</th><th /></tr></thead>
            <tbody>
              {rows.map(r => {
                const st = CHARGE_STATUS[r.status] || CHARGE_STATUS[1];
                return (
                  <tr key={r.id}>
                    <td>{r.studentName}{r.note && <div className="text-muted" style={{ fontSize: 11 }}>{r.note}{r.decidedByName ? ` · ${r.decidedByName}` : ''}</div>}</td>
                    {fee.frequency === 2 && <td>{r.matchLabel}</td>}
                    <td><span className={`badge ${st.badge}`}>{st.label}</span>{r.isPaid && <span className="badge badge-success" style={{ marginLeft: 4 }}>Pagado</span>}</td>
                    <td style={{ textAlign: 'right' }}>{r.status === 1 ? money(r.amount) : '—'}</td>
                    <td style={{ textAlign: 'right' }}>{r.amountPaid > 0 ? money(r.amountPaid) : '—'}</td>
                    <td style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
                      {canAdjust && r.status === 2 && <>
                        <button className="btn btn-sm btn-primary" onClick={() => decide(r, true)}>Cobrar</button>{' '}
                        <button className="btn btn-sm btn-secondary" onClick={() => decide(r, false)}>No cobrar</button>
                      </>}
                      {canAdjust && r.status === 3 && r.eventId && r.note !== 'Deuda anulada en Cobranzas' &&
                        <button className="btn btn-sm btn-ghost" onClick={() => decide(r, true)}>Cobrar</button>}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      <small className="text-muted">Para cobrar o exonerar una deuda puntual usa Cobranzas; aquí verás el cambio.</small>
    </Modal>
  );
}

function ExpenseModal({ t, onClose, onSaved }) {
  const [form, setForm] = useState({ date: todayLima(), amount: '', description: '', supplier: t.organizer || '', eventId: '' });
  const [saving, setSaving] = useState(false);
  const set = patch => setForm(f => ({ ...f, ...patch }));
  const save = async () => {
    if (!form.description.trim()) return toast.error('Describe el gasto');
    if (!(Number(form.amount) > 0)) return toast.error('Indica el monto');
    setSaving(true);
    try {
      await api.post(`/tournaments/${t.id}/expenses`, { ...form, amount: Number(form.amount), eventId: form.eventId || null });
      toast.success('Gasto registrado');
      onSaved();
    } catch (e) { toast.error(errMsg(e, 'No se pudo registrar el gasto')); }
    finally { setSaving(false); }
  };
  return (
    <Modal title="Registrar gasto del torneo" onClose={onClose}
      footer={<>
        <button className="btn btn-secondary" onClick={onClose}>Cancelar</button>
        <button className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Guardando…' : 'Registrar'}</button>
      </>}>
      <div className="form-row-2">
        <div className="form-group" style={{ gridColumn: '1/-1' }}>
          <label>Detalle *</label>
          <input className="form-control" value={form.description} onChange={e => set({ description: e.target.value })} placeholder="Ej: Inscripción pagada a la liga, arbitraje fecha 3, movilidad" />
        </div>
        <div className="form-group"><label>Fecha *</label><input type="date" className="form-control" value={form.date} onChange={e => set({ date: e.target.value })} /></div>
        <div className="form-group"><label>Monto (S/) *</label><input type="number" min={0} step="0.10" className="form-control" value={form.amount} onChange={e => set({ amount: e.target.value })} /></div>
        <div className="form-group"><label>Proveedor</label><input className="form-control" value={form.supplier} onChange={e => set({ supplier: e.target.value })} /></div>
        <div className="form-group">
          <label>Partido (opcional)</label>
          <select className="form-control" value={form.eventId} onChange={e => set({ eventId: e.target.value })}>
            <option value="">Del torneo en general</option>
            {t.matches.map(m => <option key={m.id} value={m.id}>{fmtDate(m.startTime)} {timeOf(m.startTime)} · {m.teamName || ''} {m.round || ''}</option>)}
          </select>
        </div>
      </div>
    </Modal>
  );
}
