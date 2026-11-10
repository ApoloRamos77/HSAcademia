import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, Crown, Edit2, KeyRound, MapPin, PlusCircle, Search, ShieldCheck, UserCheck, UserX, Users, Clock } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../api/axios';
import { useAuth } from '../../context/AuthContext';
import ConfirmDialog from '../ConfirmDialog';
import CredentialsDialog from '../CredentialsDialog';

const EMPTY_FORM = {
  firstName: '', lastName: '', email: '', phone: '', systemRole: 'Staff',
  academyRoleIds: [], headquarterIds: [], categoryIds: [], status: 'Active', suspensionReason: '',
};
const STATUS = {
  Active: { label: 'Activo', cls: 'badge-success' },
  Suspended: { label: 'Suspendido', cls: 'badge-warning' },
  Inactive: { label: 'Inactivo', cls: 'badge-danger' },
};
// Funciones de contabilidad general: no se pueden acotar a una sede.
const ACADEMY_WIDE = ['finances.config', 'finances.expenses', 'finances.staff_payments', 'finances.summary', 'finances.close', 'cash.receive', 'collections.generate'];

const initials = u => `${u.firstName?.[0] ?? ''}${u.lastName?.[0] ?? ''}`.toUpperCase();
const lastLogin = value => value
  ? new Date(value).toLocaleString('es-PE', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })
  : 'Nunca';

export default function TeamTab() {
  const { user: me } = useAuth();
  const [team, setTeam] = useState([]);
  const [roles, setRoles] = useState([]);
  const [sedes, setSedes] = useState([]);
  const [categorias, setCategorias] = useState([]);
  const [catalog, setCatalog] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState('all');
  const [roleFilter, setRoleFilter] = useState('all');
  const [showForm, setShowForm] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [credentials, setCredentials] = useState(null);
  const [toReset, setToReset] = useState(null);
  const [toTransfer, setToTransfer] = useState(null);

  const load = useCallback(async () => {
    try {
      const [users, rolesRes, sedesRes, catsRes, catalogRes] = await Promise.all([
        api.get('/academy-config/users'),
        api.get('/academy-config/roles'),
        api.get('/academy-config/headquarters'),
        api.get('/academy-config/categories'),
        api.get('/academy-config/permissions'),
      ]);
      setTeam(users.data.filter(u => u.systemRole === 'Staff' || u.systemRole === 'AcademyAdmin'));
      setRoles(rolesRes.data);
      setSedes(sedesRes.data);
      setCategorias(catsRes.data);
      setCatalog(catalogRes.data);
    } catch {
      toast.error('No se pudo cargar el equipo.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const failed = (err, fallback) => toast.error(err.response?.data?.message || fallback);
  const canManage = u => u.systemRole !== 'AcademyAdmin' || me?.isOwner || u.id === me?.userId;
  const fullName = u => `${u.firstName} ${u.lastName}`;

  const stats = useMemo(() => ({
    active: team.filter(u => u.status === 'Active').length,
    pending: team.filter(u => u.status === 'Active' && u.mustChangePassword).length,
    blocked: team.filter(u => u.status !== 'Active').length,
    noRole: team.filter(u => u.systemRole === 'Staff' && (u.academyRoleIds?.length ?? 0) === 0).length,
  }), [team]);

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    return team
      .filter(u => statusFilter === 'all' || u.status === statusFilter)
      .filter(u => roleFilter === 'all'
        || (roleFilter === 'admin' ? u.systemRole === 'AcademyAdmin' : u.academyRoleIds?.includes(roleFilter)))
      .filter(u => !term || `${fullName(u)} ${u.email ?? ''} ${u.phone ?? ''}`.toLowerCase().includes(term))
      .sort((a, b) => Number(b.isOwner) - Number(a.isOwner) || fullName(a).localeCompare(fullName(b)));
  }, [team, search, statusFilter, roleFilter]);

  const openNew = () => { setEditing(null); setForm(EMPTY_FORM); setShowForm(true); };
  const openEdit = (u) => {
    setEditing(u);
    setForm({
      firstName: u.firstName, lastName: u.lastName, email: u.email, phone: u.phone || '', systemRole: u.systemRole,
      academyRoleIds: u.academyRoleIds || [], headquarterIds: u.headquarterIds || [], categoryIds: u.categoryIds || [],
      status: u.status, suspensionReason: u.suspensionReason || '',
    });
    setShowForm(true);
  };

  const toggleIn = (field, id) => setForm(prev => ({
    ...prev,
    [field]: prev[field].includes(id) ? prev[field].filter(x => x !== id) : [...prev[field], id],
  }));

  const toggleSede = (id) => setForm(prev => {
    const headquarterIds = prev.headquarterIds.includes(id) ? prev.headquarterIds.filter(x => x !== id) : [...prev.headquarterIds, id];
    // Las categorías deben pertenecer a las sedes marcadas.
    const categoryIds = headquarterIds.length === 0 ? prev.categoryIds : prev.categoryIds.filter(c => {
      const cat = categorias.find(x => x.id === c);
      return cat && headquarterIds.includes(cat.headquarterId);
    });
    return { ...prev, headquarterIds, categoryIds };
  });

  const availableCategories = form.headquarterIds.length === 0
    ? categorias
    : categorias.filter(c => form.headquarterIds.includes(c.headquarterId));

  const isStaff = form.systemRole === 'Staff';
  const selectedPermissions = useMemo(() => new Set(
    roles.filter(r => form.academyRoleIds.includes(r.id) && r.isActive).flatMap(r => r.permissions || [])
  ), [roles, form.academyRoleIds]);
  const preview = catalog
    .map(g => ({ name: g.name, items: g.items.filter(i => selectedPermissions.has(i.id)) }))
    .filter(g => g.items.length > 0);
  const losesAccounting = isStaff && form.headquarterIds.length > 0 && ACADEMY_WIDE.some(p => selectedPermissions.has(p));
  const isSelf = editing?.id === me?.userId;

  const submit = async (e) => {
    e.preventDefault();
    if (isStaff && form.academyRoleIds.length === 0) { toast.error('Asigna al menos un rol.'); return; }
    if (editing && form.status === 'Suspended' && !form.suspensionReason.trim()) { toast.error('Indica el motivo de la suspensión.'); return; }
    setSaving(true);
    try {
      const payload = { ...form, password: '', academyRoleIds: isStaff ? form.academyRoleIds : [] };
      if (editing) {
        await api.put(`/academy-config/users/${editing.id}`, payload);
        toast.success('Cuenta actualizada');
      } else {
        const { data } = await api.post('/academy-config/users', payload);
        setCredentials([{ role: isStaff ? 'Personal' : 'Administrador', fullName: fullName(form), login: form.email.trim().toLowerCase(), temporaryPassword: data.temporaryPassword }]);
      }
      setShowForm(false);
      load();
    } catch (err) {
      failed(err, 'No se pudo guardar la cuenta.');
    } finally {
      setSaving(false);
    }
  };

  const confirmReset = async () => {
    const target = toReset;
    setToReset(null);
    try {
      const { data } = await api.post(`/academy-config/users/${target.id}/reset-password`);
      setCredentials([{ role: 'Contraseña restablecida', fullName: fullName(target), login: target.email, temporaryPassword: data.temporaryPassword }]);
      load();
    } catch (err) { failed(err, 'No se pudo restablecer la contraseña.'); }
  };

  const confirmTransfer = async () => {
    const target = toTransfer;
    setToTransfer(null);
    try {
      await api.post(`/academy-config/users/${target.id}/transfer-ownership`);
      toast.success('Propiedad transferida');
      window.location.reload();
    } catch (err) { failed(err, 'No se pudo transferir la propiedad.'); }
  };

  if (loading) return <div className="text-center p-8"><span className="spinner" style={{ borderColor: 'var(--primary)', borderTopColor: 'transparent' }} /></div>;

  const scopeText = u => {
    if (u.systemRole === 'AcademyAdmin') return 'Toda la academia';
    return u.headquarterNames?.length ? u.headquarterNames.join(', ') : 'Todas las sedes';
  };

  return (
    <>
      <div className="stats-grid">
        <Stat icon={UserCheck} value={stats.active} label="Cuentas activas" color="var(--success)" />
        <Stat icon={Clock} value={stats.pending} label="Por activar su contraseña" color="var(--primary)" />
        <Stat icon={UserX} value={stats.blocked} label="Suspendidas o inactivas" color="var(--warning)" />
        <Stat icon={AlertTriangle} value={stats.noRole} label="Personal sin rol" color="var(--danger)" />
      </div>

      <div className="card">
        <div className="card-header flex-wrap gap-3">
          <div>
            <h3 className="card-title flex items-center gap-2"><Users size={18} className="text-primary" /> Equipo de la academia</h3>
            <p className="text-muted mt-1">Quién puede ingresar, con qué roles y sobre qué sedes y categorías.</p>
          </div>
          <button onClick={openNew} className="btn btn-primary"><PlusCircle size={16} /> Nueva cuenta</button>
        </div>

        <div className="access-filters">
          <div className="access-search">
            <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
            <input className="form-control pl-9" placeholder="Buscar por nombre, correo o teléfono" aria-label="Buscar en el equipo" value={search} onChange={e => setSearch(e.target.value)} />
          </div>
          <select className="form-control" aria-label="Filtrar por estado" value={statusFilter} onChange={e => setStatusFilter(e.target.value)}>
            <option value="all">Todos los estados</option>
            {Object.entries(STATUS).map(([value, s]) => <option key={value} value={value}>{s.label}</option>)}
          </select>
          <select className="form-control" aria-label="Filtrar por rol" value={roleFilter} onChange={e => setRoleFilter(e.target.value)}>
            <option value="all">Todos los roles</option>
            <option value="admin">Administradores</option>
            {roles.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}
          </select>
        </div>

        <div className="table-wrap">
          <table>
            <thead>
              <tr><th>Persona</th><th>Acceso</th><th>Alcance</th><th>Último ingreso</th><th>Estado</th><th className="text-right">Acciones</th></tr>
            </thead>
            <tbody>
              {visible.length === 0 && <tr><td colSpan="6" className="text-center p-8 text-text-muted">No hay cuentas que coincidan.</td></tr>}
              {visible.map(u => (
                <tr key={u.id}>
                  <td>
                    <div className="flex items-center gap-3">
                      <div className="avatar shrink-0">{initials(u)}</div>
                      <div style={{ minWidth: 0, maxWidth: 210 }}>
                        <div className="font-bold text-sm truncate" title={fullName(u)}>{fullName(u)}</div>
                        <div className="text-xs text-text-muted truncate" title={u.email || u.phone || ''}>{u.email || u.phone || 'Sin contacto'}</div>
                      </div>
                    </div>
                  </td>
                  <td>
                    <div className="flex flex-wrap gap-1" style={{ maxWidth: 190 }}>
                      {u.isOwner && <span className="badge badge-info gap-1"><Crown size={11} /> Propietario</span>}
                      {u.systemRole === 'AcademyAdmin' && !u.isOwner && <span className="badge badge-info gap-1"><ShieldCheck size={11} /> Administrador</span>}
                      {u.systemRole === 'Staff' && (u.academyRoleNames?.length
                        ? u.academyRoleNames.map(n => <span key={n} className="badge badge-muted">{n}</span>)
                        : <span className="badge badge-danger">Sin rol</span>)}
                    </div>
                  </td>
                  <td>
                    <div className="text-xs flex items-center gap-1"><MapPin size={12} className="text-success shrink-0" /> <span className="truncate" style={{ maxWidth: 170 }} title={scopeText(u)}>{scopeText(u)}</span></div>
                    {u.systemRole === 'Staff' && u.categoryNames?.length > 0 && (
                      <div className="text-xs text-text-muted mt-1 truncate" style={{ maxWidth: 190 }} title={u.categoryNames.join(', ')}>{u.categoryNames.length} {u.categoryNames.length === 1 ? 'categoría' : 'categorías'}: {u.categoryNames.join(', ')}</div>
                    )}
                  </td>
                  <td className="text-xs whitespace-nowrap">{lastLogin(u.lastLoginAt)}</td>
                  <td>
                    <div className="flex flex-col gap-1 items-start">
                      <span className={`badge ${STATUS[u.status]?.cls ?? 'badge-muted'}`} title={u.suspensionReason || ''}>{STATUS[u.status]?.label ?? u.status}</span>
                      {u.status === 'Active' && u.mustChangePassword && <span className="text-xs text-text-muted">Clave temporal</span>}
                    </div>
                  </td>
                  <td className="text-right whitespace-nowrap">
                    {canManage(u) && (
                      <>
                        {me?.isOwner && u.systemRole === 'AcademyAdmin' && !u.isOwner && u.status === 'Active' && (
                          <button onClick={() => setToTransfer(u)} className="btn-icon inline-flex" title="Transferir la propiedad" aria-label={`Transferir la propiedad a ${fullName(u)}`}><Crown size={16} /></button>
                        )}
                        <button onClick={() => setToReset(u)} className="btn-icon inline-flex" title="Generar contraseña temporal" aria-label={`Generar contraseña temporal de ${fullName(u)}`}><KeyRound size={16} /></button>
                        <button onClick={() => openEdit(u)} className="btn-icon inline-flex" title="Editar" aria-label={`Editar a ${fullName(u)}`}><Edit2 size={16} /></button>
                      </>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {showForm && (
        <div className="modal-overlay" onClick={() => setShowForm(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 820 }}>
            <h3 className="modal-title">{editing ? `Editar a ${fullName(editing)}` : 'Nueva cuenta'}</h3>
            <form onSubmit={submit}>
              <Section title="Datos de la persona">
                <div className="form-row">
                  <Field label="Nombre *"><input required className="form-control" value={form.firstName} onChange={e => setForm({ ...form, firstName: e.target.value })} /></Field>
                  <Field label="Apellido *"><input required className="form-control" value={form.lastName} onChange={e => setForm({ ...form, lastName: e.target.value })} /></Field>
                </div>
                <div className="form-row">
                  <Field label="Correo *"><input required type="email" className="form-control" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} /></Field>
                  <Field label="Teléfono"><input className="form-control" value={form.phone} onChange={e => setForm({ ...form, phone: e.target.value })} /></Field>
                </div>
                {!editing && <p className="text-xs text-text-muted">Al guardar se genera una contraseña temporal que verás una sola vez. La persona la cambiará en su primer ingreso.</p>}
              </Section>

              <Section title="Qué puede hacer">
                <Field label="Nivel de acceso">
                  <select className="form-control" value={form.systemRole} disabled={!me?.isOwner || isSelf} onChange={e => setForm({ ...form, systemRole: e.target.value })}>
                    <option value="Staff">Personal (según sus roles)</option>
                    <option value="AcademyAdmin">Administrador (acceso completo)</option>
                  </select>
                  {!me?.isOwner && <p className="text-xs text-text-muted mt-1">Solo el propietario gestiona a los administradores.</p>}
                </Field>
                {isStaff ? (
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <span className="form-label">Roles *</span>
                      <CheckList items={roles.filter(r => r.isActive || form.academyRoleIds.includes(r.id)).map(r => ({ id: r.id, label: `${r.name}${r.isActive ? '' : ' (inactivo)'}`, hint: r.description, tag: r.isSystem ? null : 'Propio' }))} selected={form.academyRoleIds} onToggle={id => toggleIn('academyRoleIds', id)} />
                    </div>
                    <div>
                      <span className="form-label">Podrá</span>
                      <div className="access-box">
                        {preview.length === 0 && <p className="text-xs text-text-muted m-0">Elige un rol para ver sus permisos.</p>}
                        {preview.map(g => (
                          <div key={g.name} className="mb-2">
                            <div className="text-xs font-bold text-text-muted">{g.name}</div>
                            <div className="text-xs text-text-secondary">{g.items.map(i => i.label).join(' · ')}</div>
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                ) : <p className="text-sm text-text-secondary m-0">Un administrador tiene acceso completo a la academia.</p>}
              </Section>

              {isStaff && (
                <Section title="Sobre qué lo puede hacer">
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <span className="form-label">Sedes</span>
                      <CheckList items={sedes.map(s => ({ id: s.id, label: s.name }))} selected={form.headquarterIds} onToggle={toggleSede} />
                      <p className="text-xs text-text-muted mt-1">Limita cobranza y reportes. Sin marcar: toda la academia.</p>
                    </div>
                    <div>
                      <span className="form-label">Categorías</span>
                      <CheckList items={availableCategories.map(c => ({ id: c.id, label: c.name, tag: c.headquarterName }))} selected={form.categoryIds} onToggle={id => toggleIn('categoryIds', id)} empty="No hay categorías en las sedes marcadas." />
                      <p className="text-xs text-text-muted mt-1">Limita alumnos, asistencia y evaluaciones.</p>
                    </div>
                  </div>
                  {losesAccounting && (
                    <div className="access-warning">
                      <AlertTriangle size={16} className="shrink-0 mt-0.5" />
                      <span>Con sedes marcadas no podrá usar gastos, nómina, resumen financiero, rendiciones ni configuración de cobros, aunque sus roles los incluyan. Esas funciones abarcan toda la academia.</span>
                    </div>
                  )}
                </Section>
              )}

              {editing && !isSelf && !editing.isOwner && (
                <Section title="Estado de la cuenta">
                  <div className="flex flex-wrap gap-2" role="radiogroup" aria-label="Estado de la cuenta">
                    {Object.entries(STATUS).map(([value, s]) => (
                      <button key={value} type="button" role="radio" aria-checked={form.status === value} onClick={() => setForm({ ...form, status: value })} className={`btn btn-sm ${form.status === value ? 'btn-primary' : 'btn-ghost'}`}>{s.label}</button>
                    ))}
                  </div>
                  {form.status === 'Suspended' && (
                    <div className="mt-3">
                      <Field label="Motivo de la suspensión *"><input className="form-control" maxLength={1000} placeholder="La persona lo verá al intentar ingresar" value={form.suspensionReason} onChange={e => setForm({ ...form, suspensionReason: e.target.value })} /></Field>
                    </div>
                  )}
                </Section>
              )}

              <div className="modal-footer">
                <button type="button" onClick={() => setShowForm(false)} className="btn btn-ghost">Cancelar</button>
                <button type="submit" disabled={saving} className="btn btn-primary">{saving ? 'Guardando…' : editing ? 'Guardar cambios' : 'Crear cuenta'}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      <ConfirmDialog
        isOpen={!!toReset}
        title="Generar contraseña temporal"
        message={`¿Generar una contraseña temporal para ${toReset ? fullName(toReset) : ''}?`}
        detail="Su sesión abierta se cerrará. La contraseña se mostrará una sola vez."
        confirmLabel="Generar"
        onConfirm={confirmReset}
        onCancel={() => setToReset(null)}
      />
      <ConfirmDialog
        isOpen={!!toTransfer}
        title="Transferir la propiedad"
        message={`¿Transferir la propiedad de la academia a ${toTransfer ? fullName(toTransfer) : ''}?`}
        detail="Seguirás siendo administrador, pero ya no podrás gestionar a los demás administradores."
        confirmLabel="Transferir"
        onConfirm={confirmTransfer}
        onCancel={() => setToTransfer(null)}
      />
      <CredentialsDialog credentials={credentials} onClose={() => setCredentials(null)} />
    </>
  );
}

function Stat({ icon: Icon, value, label, color }) {
  return (
    <div className="stat-card">
      <div className="stat-value" style={{ color }}>{value}</div>
      <div className="stat-label">{label}</div>
      <div className="stat-icon"><Icon size={28} /></div>
    </div>
  );
}

function Section({ title, children }) {
  return (
    <section className="access-section">
      <h4>{title}</h4>
      {children}
    </section>
  );
}

function Field({ label, children }) {
  return <div className="form-group"><label className="block"><span className="form-label">{label}</span>{children}</label></div>;
}

function CheckList({ items, selected, onToggle, empty = 'No hay opciones.' }) {
  return (
    <div className="access-box">
      {items.length === 0 && <p className="text-xs text-text-muted m-1">{empty}</p>}
      {items.map(item => (
        <label key={item.id} className="access-check" title={item.hint || ''}>
          <input type="checkbox" className="accent-primary w-4 h-4 shrink-0" checked={selected.includes(item.id)} onChange={() => onToggle(item.id)} />
          <span className="truncate flex-1">{item.label}</span>
          {item.tag && <span className="text-xs text-text-muted shrink-0">{item.tag}</span>}
        </label>
      ))}
    </div>
  );
}
