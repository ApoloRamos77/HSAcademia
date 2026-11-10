import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { PlusCircle, ShieldCheck, Edit2, Copy, Trash2, Power, Lock, Users } from 'lucide-react';
import toast from 'react-hot-toast';

const EMPTY_FORM = { name: '', description: '', permissions: [] };

export default function Roles() {
  const [roles, setRoles] = useState([]);
  const [catalog, setCatalog] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [formData, setFormData] = useState(EMPTY_FORM);
  const [toDelete, setToDelete] = useState(null);

  const fetchRoles = async () => {
    try {
      const { data } = await api.get('/academy-config/roles');
      setRoles(data);
    } catch {
      toast.error('Error al cargar roles');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    api.get('/academy-config/permissions').then(r => setCatalog(r.data)).catch(() => toast.error('Error al cargar permisos'));
    fetchRoles();
  }, []);

  const labels = Object.fromEntries(catalog.flatMap(g => g.items.map(i => [i.id, i.label])));
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
    } catch (err) { failed(err, 'Error al guardar rol.'); }
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
    try {
      await api.delete(`/academy-config/roles/${toDelete.id}`);
      toast.success('Rol eliminado');
      fetchRoles();
    } catch (err) { failed(err, 'No se pudo eliminar el rol.'); }
    finally { setToDelete(null); }
  };

  if (loading) return <AppLayout title="Roles"><div className="text-center p-8"><span className="spinner" style={{ borderColor: 'var(--primary)', borderTopColor: 'transparent' }}></span></div></AppLayout>;

  const groupsOf = (role) => catalog
    .map(g => ({ name: g.name, items: g.items.filter(i => role.permissions?.includes(i.id)) }))
    .filter(g => g.items.length > 0);

  return (
    <AppLayout title="Roles y permisos">
      <div className="fade-in">
        <div className="card">
          <div className="card-header">
            <div>
              <h3 className="card-title flex items-center gap-2">
                <ShieldCheck className="text-primary-400" size={20} /> Roles y permisos
              </h3>
              <p className="text-muted mt-1">Un rol define qué puede hacer una persona. Si tiene varios, sus permisos se suman.</p>
            </div>
            <button onClick={openNew} className="btn btn-primary">
              <PlusCircle size={16} /> Nuevo rol
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {roles.map(role => (
              <div key={role.id} className="card p-5" style={{ background: 'var(--bg-surface)', opacity: role.isActive ? 1 : 0.65 }}>
                <div className="flex justify-between items-start gap-2 mb-1">
                  <h3 className="font-bold text-lg text-primary-100 flex items-center gap-2">
                    {role.isSystem && <Lock size={14} title="Predefinido por Clubio" />} {role.name}
                  </h3>
                  <div className="flex gap-1 shrink-0">
                    <span className={`badge ${role.isSystem ? 'badge-info' : 'badge-muted'}`}>{role.isSystem ? 'Predefinido' : 'Propio'}</span>
                    {!role.isActive && <span className="badge badge-danger">Inactivo</span>}
                  </div>
                </div>
                <p className="text-sm text-text-secondary mb-3">{role.description || 'Sin descripción'}</p>
                <p className="text-xs text-text-muted mb-3 flex items-center gap-1">
                  <Users size={12} /> {role.userCount} {role.userCount === 1 ? 'persona' : 'personas'}
                </p>
                <div className="mb-4 flex flex-col gap-2">
                  {groupsOf(role).length === 0 && <span className="text-xs text-text-muted">Sin permisos asignados</span>}
                  {groupsOf(role).map(g => (
                    <div key={g.name}>
                      <p className="text-xs text-text-muted font-bold uppercase tracking-wider mb-1">{g.name}</p>
                      <div className="flex flex-wrap gap-1">
                        {g.items.map(i => <span key={i.id} className="badge bg-primary/20 text-primary-100 text-xs px-2 py-0.5">{i.label}</span>)}
                      </div>
                    </div>
                  ))}
                </div>
                <div className="flex justify-end gap-2 border-t border-border pt-4">
                  <button onClick={() => clone(role)} className="btn btn-ghost btn-sm" title="Crear una copia editable"><Copy size={14} /></button>
                  {!role.isSystem && (
                    <>
                      <button onClick={() => openEdit(role)} className="btn btn-ghost btn-sm" title="Editar"><Edit2 size={14} /></button>
                      <button onClick={() => setActive(role)} className="btn btn-ghost btn-sm" title={role.isActive ? 'Desactivar' : 'Activar'}><Power size={14} /></button>
                      <button onClick={() => setToDelete(role)} className="btn btn-ghost btn-sm text-danger" title="Eliminar" disabled={role.userCount > 0}><Trash2 size={14} /></button>
                    </>
                  )}
                </div>
              </div>
            ))}
          </div>
        </div>

        {showModal && (
          <div className="modal-overlay" onClick={() => setShowModal(false)}>
            <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 720 }}>
              <h3 className="modal-title">{editingId ? 'Editar rol' : 'Nuevo rol'}</h3>
              <form onSubmit={handleSubmit}>
                <div className="form-group">
                  <label className="form-label">Nombre *</label>
                  <input required maxLength={100} type="text" className="form-control" placeholder="Ej. Asistente de sede" value={formData.name} onChange={e => setFormData({ ...formData, name: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="form-label">Descripción</label>
                  <textarea maxLength={500} className="form-control" value={formData.description} onChange={e => setFormData({ ...formData, description: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="form-label">Permisos ({formData.permissions.filter(p => labels[p]).length})</label>
                  <div className="flex flex-col gap-4 bg-bg-dark border border-border p-3 rounded-lg overflow-y-auto" style={{ maxHeight: '45vh' }}>
                    {catalog.map(group => {
                      const ids = group.items.map(i => i.id);
                      const all = ids.every(id => formData.permissions.includes(id));
                      return (
                        <div key={group.name}>
                          <div className="flex justify-between items-center mb-1">
                            <span className="text-xs font-bold uppercase tracking-wider text-text-muted">{group.name}</span>
                            <button type="button" className="btn btn-ghost btn-sm" onClick={() => toggle(ids, !all)}>{all ? 'Quitar todos' : 'Marcar todos'}</button>
                          </div>
                          <div className="grid grid-cols-1 sm:grid-cols-2 gap-1">
                            {group.items.map(perm => (
                              <label key={perm.id} className="flex items-center gap-2 cursor-pointer p-1 hover:bg-bg-surface rounded">
                                <input type="checkbox" className="accent-primary w-4 h-4" checked={formData.permissions.includes(perm.id)} onChange={e => toggle([perm.id], e.target.checked)} />
                                <span className="text-sm text-text-main">{perm.label}</span>
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

        {toDelete && (
          <div className="modal-overlay" onClick={() => setToDelete(null)}>
            <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 420 }}>
              <h3 className="modal-title">Eliminar rol</h3>
              <p className="text-sm text-text-secondary">¿Eliminar el rol "{toDelete.name}"? Esta acción no se puede deshacer.</p>
              <div className="modal-footer">
                <button type="button" onClick={() => setToDelete(null)} className="btn btn-ghost">Cancelar</button>
                <button type="button" onClick={confirmDelete} className="btn btn-danger">Eliminar</button>
              </div>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
