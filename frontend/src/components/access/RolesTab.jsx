import { Fragment, useEffect, useState } from 'react';
import { Check, Copy, Edit2, LayoutGrid, Lock, PlusCircle, Power, ShieldCheck, Table2, Trash2, Users } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../api/axios';
import ConfirmDialog from '../ConfirmDialog';

const EMPTY_FORM = { name: '', description: '', permissions: [] };

export default function RolesTab() {
  const [roles, setRoles] = useState([]);
  const [catalog, setCatalog] = useState([]);
  const [loading, setLoading] = useState(true);
  const [view, setView] = useState('cards');
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [formData, setFormData] = useState(EMPTY_FORM);
  const [toDelete, setToDelete] = useState(null);

  const fetchRoles = async () => {
    try {
      const { data } = await api.get('/academy-config/roles');
      setRoles(data);
    } catch {
      toast.error('Error al cargar los roles.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    api.get('/academy-config/permissions').then(r => setCatalog(r.data)).catch(() => toast.error('Error al cargar los permisos.'));
    fetchRoles();
  }, []);

  const failed = (err, fallback) => toast.error(err.response?.data?.message || fallback);
  const openNew = () => { setEditingId(null); setFormData(EMPTY_FORM); setShowModal(true); };
  const openEdit = (role) => {
    setFormData({ name: role.name, description: role.description || '', permissions: role.permissions || [] });
    setEditingId(role.id);
    setShowModal(true);
  };

  const toggle = (ids, on) => setFormData(prev => ({
    ...prev,
    permissions: on ? [...new Set([...prev.permissions, ...ids])] : prev.permissions.filter(p => !ids.includes(p)),
  }));

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editingId) await api.put(`/academy-config/roles/${editingId}`, formData);
      else await api.post('/academy-config/roles', formData);
      toast.success(editingId ? 'Rol actualizado' : 'Rol creado');
      setShowModal(false);
      fetchRoles();
    } catch (err) { failed(err, 'Error al guardar el rol.'); }
  };

  const clone = async (role) => {
    try {
      const { data } = await api.post(`/academy-config/roles/${role.id}/clone`);
      toast.success(`Creado "${data.name}"`);
      await fetchRoles();
      openEdit(data);
    } catch (err) { failed(err, 'No se pudo clonar el rol.'); }
  };

  const setActive = async (role) => {
    try {
      await api.put(`/academy-config/roles/${role.id}/status`, { isActive: !role.isActive });
      toast.success(role.isActive ? 'Rol desactivado' : 'Rol activado');
      fetchRoles();
    } catch (err) { failed(err, 'No se pudo cambiar el estado.'); }
  };

  const confirmDelete = async () => {
    const target = toDelete;
    setToDelete(null);
    try {
      await api.delete(`/academy-config/roles/${target.id}`);
      toast.success('Rol eliminado');
      fetchRoles();
    } catch (err) { failed(err, 'No se pudo eliminar el rol.'); }
  };

  if (loading) return <div className="text-center p-8"><span className="spinner" style={{ borderColor: 'var(--primary)', borderTopColor: 'transparent' }} /></div>;

  const groupsOf = (role) => catalog
    .map(g => ({ name: g.name, items: g.items.filter(i => role.permissions?.includes(i.id)) }))
    .filter(g => g.items.length > 0);
  const selectedCount = formData.permissions.filter(p => catalog.some(g => g.items.some(i => i.id === p))).length;
  const matrixRoles = roles.filter(r => r.isActive);

  return (
    <div className="card">
      <div className="card-header flex-wrap gap-3">
        <div>
          <h3 className="card-title flex items-center gap-2"><ShieldCheck size={18} className="text-primary" /> Roles y permisos</h3>
          <p className="text-muted mt-1">Un rol define qué puede hacer una persona. Si tiene varios, sus permisos se suman.</p>
        </div>
        <div className="flex gap-2">
          <div className="flex gap-1" role="radiogroup" aria-label="Vista de roles">
            <button role="radio" aria-checked={view === 'cards'} onClick={() => setView('cards')} className={`btn btn-sm ${view === 'cards' ? 'btn-secondary' : 'btn-ghost'}`}><LayoutGrid size={14} /> Tarjetas</button>
            <button role="radio" aria-checked={view === 'matrix'} onClick={() => setView('matrix')} className={`btn btn-sm ${view === 'matrix' ? 'btn-secondary' : 'btn-ghost'}`}><Table2 size={14} /> Matriz</button>
          </div>
          <button onClick={openNew} className="btn btn-primary"><PlusCircle size={16} /> Nuevo rol</button>
        </div>
      </div>

      {view === 'matrix' ? (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th style={{ position: 'sticky', left: 0, zIndex: 1, minWidth: 240 }}>Permiso</th>
                {matrixRoles.map(r => <th key={r.id} className="text-center" style={{ minWidth: 92, textTransform: 'none' }}>{r.name}</th>)}
              </tr>
            </thead>
            <tbody>
              {catalog.map(group => (
                <Fragment key={group.name}>
                  <tr><td colSpan={matrixRoles.length + 1} className="text-xs font-bold uppercase" style={{ background: 'var(--bg-surface)', position: 'sticky', left: 0 }}>{group.name}</td></tr>
                  {group.items.map(item => (
                    <tr key={item.id}>
                      <td style={{ position: 'sticky', left: 0, background: 'var(--bg-card)' }}>{item.label}</td>
                      {matrixRoles.map(r => (
                        <td key={r.id} className="text-center">
                          {r.permissions?.includes(item.id)
                            ? <Check size={16} aria-label="Sí" style={{ color: 'var(--success)', display: 'inline' }} />
                            : <span aria-label="No" style={{ color: 'var(--text-muted)' }}>·</span>}
                        </td>
                      ))}
                    </tr>
                  ))}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {roles.map(role => (
            <div key={role.id} className="p-5 rounded-xl border border-border flex flex-col" style={{ background: 'var(--bg-card2)', opacity: role.isActive ? 1 : 0.65 }}>
              <div className="flex justify-between items-start gap-2 mb-1">
                <h4 className="font-bold text-base m-0 flex items-center gap-2">{role.isSystem && <Lock size={13} aria-label="Predefinido" />} {role.name}</h4>
                <div className="flex gap-1 shrink-0">
                  <span className={`badge ${role.isSystem ? 'badge-info' : 'badge-muted'}`}>{role.isSystem ? 'Predefinido' : 'Propio'}</span>
                  {!role.isActive && <span className="badge badge-danger">Inactivo</span>}
                </div>
              </div>
              <p className="text-sm text-text-secondary mb-2">{role.description || 'Sin descripción'}</p>
              <p className="text-xs text-text-muted mb-3 flex items-center gap-1"><Users size={12} /> {role.userCount} {role.userCount === 1 ? 'persona' : 'personas'}</p>
              <div className="flex flex-col gap-2 mb-4 flex-1">
                {groupsOf(role).length === 0 && <span className="text-xs text-text-muted">Sin permisos asignados</span>}
                {groupsOf(role).map(g => (
                  <div key={g.name}>
                    <div className="text-xs font-bold text-text-muted">{g.name}</div>
                    <div className="text-xs text-text-secondary">{g.items.map(i => i.label).join(' · ')}</div>
                  </div>
                ))}
              </div>
              <div className="flex justify-end gap-1 border-t border-border pt-3">
                <button onClick={() => clone(role)} className="btn-icon" title="Crear una copia editable" aria-label={`Clonar ${role.name}`}><Copy size={15} /></button>
                {!role.isSystem && (
                  <>
                    <button onClick={() => openEdit(role)} className="btn-icon" title="Editar" aria-label={`Editar ${role.name}`}><Edit2 size={15} /></button>
                    <button onClick={() => setActive(role)} className="btn-icon" title={role.isActive ? 'Desactivar' : 'Activar'} aria-label={`${role.isActive ? 'Desactivar' : 'Activar'} ${role.name}`}><Power size={15} /></button>
                    <button onClick={() => setToDelete(role)} className="btn-icon" disabled={role.userCount > 0} title={role.userCount > 0 ? 'Tiene personal asignado' : 'Eliminar'} aria-label={`Eliminar ${role.name}`}><Trash2 size={15} /></button>
                  </>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 720 }}>
            <h3 className="modal-title">{editingId ? 'Editar rol' : 'Nuevo rol'}</h3>
            <form onSubmit={handleSubmit}>
              <div className="form-group">
                <label className="block"><span className="form-label">Nombre *</span><input required maxLength={100} className="form-control" placeholder="Ej. Asistente de sede" value={formData.name} onChange={e => setFormData({ ...formData, name: e.target.value })} />
                </label>
              </div>
              <div className="form-group">
                <label className="block"><span className="form-label">Descripción</span><textarea maxLength={500} className="form-control" value={formData.description} onChange={e => setFormData({ ...formData, description: e.target.value })} />
                </label>
              </div>
              <div className="form-group">
                <span className="form-label">Permisos ({selectedCount})</span>
                <div className="access-box flex flex-col gap-4" style={{ maxHeight: '42vh' }}>
                  {catalog.map(group => {
                    const ids = group.items.map(i => i.id);
                    const all = ids.every(id => formData.permissions.includes(id));
                    return (
                      <div key={group.name}>
                        <div className="flex justify-between items-center mb-1">
                          <span className="text-xs font-bold uppercase text-text-muted">{group.name}</span>
                          <button type="button" className="btn btn-ghost btn-sm" onClick={() => toggle(ids, !all)}>{all ? 'Quitar todos' : 'Marcar todos'}</button>
                        </div>
                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-1">
                          {group.items.map(perm => (
                            <label key={perm.id} className="access-check">
                              <input type="checkbox" className="accent-primary w-4 h-4" checked={formData.permissions.includes(perm.id)} onChange={e => toggle([perm.id], e.target.checked)} />
                              {perm.label}
                            </label>
                          ))}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" onClick={() => setShowModal(false)} className="btn btn-ghost">Cancelar</button>
                <button type="submit" className="btn btn-primary">{editingId ? 'Guardar cambios' : 'Crear rol'}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      <ConfirmDialog
        isOpen={!!toDelete}
        title="Eliminar rol"
        message={`¿Eliminar el rol "${toDelete?.name}"?`}
        detail="Esta acción no se puede deshacer."
        confirmLabel="Eliminar"
        onConfirm={confirmDelete}
        onCancel={() => setToDelete(null)}
      />
    </div>
  );
}
