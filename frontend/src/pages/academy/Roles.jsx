import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { PlusCircle, ShieldCheck, Edit2 } from 'lucide-react';
import toast from 'react-hot-toast';

export default function Roles() {
  const [roles, setRoles] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [formData, setFormData] = useState({ name: '', description: '' });

  useEffect(() => {
    fetchRoles();
  }, []);

  const fetchRoles = async () => {
    try {
      const { data } = await api.get('/academy-config/roles');
      setRoles(data);
    } catch (err) {
      toast.error('Error al cargar roles');
    } finally {
      setLoading(false);
    }
  };

  const handleEdit = (role) => {
    setFormData({ name: role.name, description: role.description || '' });
    setEditingId(role.id);
    setShowModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editingId) {
        await api.put(`/academy-config/roles/${editingId}`, formData);
        toast.success('Rol actualizado');
      } else {
        await api.post('/academy-config/roles', formData);
        toast.success('Rol creado');
      }
      setShowModal(false);
      setEditingId(null);
      setFormData({ name: '', description: '' });
      fetchRoles();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al guardar rol.');
    }
  };

  if (loading) return <AppLayout title="Roles"><div className="text-center p-8"><span className="spinner" style={{borderColor: 'var(--primary)', borderTopColor: 'transparent'}}></span></div></AppLayout>;

  return (
    <AppLayout title="Cargos y Roles">
      <div className="fade-in">
        <div className="card">
          <div className="card-header">
            <div>
              <h3 className="card-title flex items-center gap-2">
                <ShieldCheck className="text-primary-400" size={20} /> Roles Internos
              </h3>
              <p className="text-muted mt-1">Crea cargos específicos (ej. Entrenador, Recepción) para tu staff.</p>
            </div>
            <button onClick={() => { setEditingId(null); setFormData({ name: '', description: '' }); setShowModal(true); }} className="btn btn-primary">
              <PlusCircle size={16} /> Nuevo Rol
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {roles.map(role => (
              <div key={role.id} className="card p-5" style={{ background: 'var(--bg-surface)' }}>
                <div className="flex justify-between items-start mb-2">
                  <h3 className="font-bold text-lg text-primary-100 flex items-center gap-2">
                    {role.name}
                  </h3>
                  <span className={`badge ${role.isActive ? 'badge-success' : 'badge-danger'}`}>
                    {role.isActive ? 'Activo' : 'Inactivo'}
                  </span>
                </div>
                <p className="text-sm text-text-secondary min-h-[40px] mb-4">
                  {role.description || 'Sin descripción'}
                </p>
                <div className="flex justify-end gap-2 border-t border-border pt-4">
                  <button onClick={() => handleEdit(role)} className="btn btn-ghost btn-sm" title="Editar"><Edit2 size={14}/></button>
                </div>
              </div>
            ))}
          </div>
          {roles.length === 0 && <div className="empty-state"><ShieldCheck size={40}/><p>No hay roles registrados</p></div>}
        </div>

        {showModal && (
          <div className="modal-overlay" onClick={() => setShowModal(false)}>
            <div className="modal" onClick={e => e.stopPropagation()}>
              <h3 className="modal-title">{editingId ? 'Editar Rol' : 'Nuevo Rol Personalizado'}</h3>
              
              <form onSubmit={handleSubmit}>
                <div className="form-group">
                  <label className="form-label">Nombre del Cargo *</label>
                  <input required type="text" className="form-control" placeholder="Ej. Entrenador Principal, Recepcionista" value={formData.name} onChange={e => setFormData({...formData, name: e.target.value})} />
                </div>
                <div className="form-group">
                  <label className="form-label">Descripción</label>
                  <textarea className="form-control" placeholder="Opcional..." value={formData.description} onChange={e => setFormData({...formData, description: e.target.value})} />
                </div>
                
                <div className="modal-footer">
                  <button type="button" onClick={() => setShowModal(false)} className="btn btn-ghost">Cancelar</button>
                  <button type="submit" className="btn btn-primary">{editingId ? 'Guardar Cambios' : 'Crear Rol'}</button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
