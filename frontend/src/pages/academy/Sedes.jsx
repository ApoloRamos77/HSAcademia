import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { PlusCircle, Search, Edit2, Trash2, Building2, MapPin, Phone } from 'lucide-react';
import toast from 'react-hot-toast';

export default function Sedes() {
  const [sedes, setSedes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [formData, setFormData] = useState({ name: '', address: '', contactPhone: '' });

  useEffect(() => {
    fetchSedes();
  }, []);

  const fetchSedes = async () => {
    try {
      const { data } = await api.get('/academy-config/headquarters');
      setSedes(data);
    } catch (err) {
      toast.error('Error al cargar sedes');
    } finally {
      setLoading(false);
    }
  };

  const handleEdit = (sede) => {
    setFormData({ name: sede.name, address: sede.address || '', contactPhone: sede.contactPhone || '' });
    setEditingId(sede.id);
    setShowModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editingId) {
        await api.put(`/academy-config/headquarters/${editingId}`, formData);
        toast.success('Sede actualizada correctamente');
      } else {
        await api.post('/academy-config/headquarters', formData);
        toast.success('Sede creada correctamente');
      }
      setShowModal(false);
      setFormData({ name: '', address: '', contactPhone: '' });
      setEditingId(null);
      fetchSedes();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al guardar sede.');
    }
  };

  const handleDelete = async (id) => {
    if (!confirm('¿Estás seguro de eliminar esta sede?')) return;
    try {
      await api.delete(`/academy-config/headquarters/${id}`);
      toast.success('Sede eliminada');
      fetchSedes();
    } catch (err) {
      toast.error('Error al eliminar sede');
    }
  };

  if (loading) return <AppLayout title="Sedes"><div className="text-center p-8"><span className="spinner" style={{borderColor: 'var(--primary)', borderTopColor: 'transparent'}}></span></div></AppLayout>;

  return (
    <AppLayout title="Gestión de Sedes">
      <div className="fade-in">
        <div className="card">
          <div className="card-header">
            <div>
              <h3 className="card-title flex items-center gap-2">
                <Building2 className="text-primary-400" size={20} /> Sedes Físicas
              </h3>
              <p className="text-muted mt-1">Administra las ubicaciones físicas de tu academia.</p>
            </div>
            <button onClick={() => { setEditingId(null); setFormData({ name: '', address: '', contactPhone: '' }); setShowModal(true); }} className="btn btn-primary">
              <PlusCircle size={16} /> Nueva Sede
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {sedes.map(sede => (
              <div key={sede.id} className="card p-5" style={{ background: 'var(--bg-surface)' }}>
                <div className="flex justify-between items-start mb-4">
                  <h3 className="font-bold text-lg">{sede.name}</h3>
                  <span className={`badge ${sede.isActive ? 'badge-success' : 'badge-danger'}`}>
                    {sede.isActive ? 'Activa' : 'Inactiva'}
                  </span>
                </div>
                <div className="flex flex-col gap-2 text-sm text-text-secondary mb-4">
                  <div className="flex items-center gap-2"><MapPin size={15}/> {sede.address || 'No registrado'}</div>
                  <div className="flex items-center gap-2"><Phone size={15}/> {sede.contactPhone || 'No registrado'}</div>
                </div>
                <div className="flex justify-end gap-2 border-t border-border pt-4 mt-auto">
                  <button onClick={() => handleEdit(sede)} className="btn btn-ghost btn-sm" title="Editar"><Edit2 size={14}/></button>
                  <button onClick={() => handleDelete(sede.id)} className="btn btn-ghost btn-sm text-danger hover:bg-danger/10" title="Eliminar"><Trash2 size={14}/></button>
                </div>
              </div>
            ))}
          </div>
          {sedes.length === 0 && <div className="empty-state"><Building2 size={40}/><p>No hay sedes registradas</p></div>}
        </div>

        {showModal && (
          <div className="modal-overlay" onClick={() => setShowModal(false)}>
            <div className="modal" onClick={e => e.stopPropagation()}>
              <h3 className="modal-title">{editingId ? 'Editar Sede' : 'Nueva Sede'}</h3>
              
              <form onSubmit={handleSubmit}>
                <div className="form-group">
                  <label className="form-label">Nombre de la Sede *</label>
                  <input required type="text" className="form-control" placeholder="Ej. Sede Norte" value={formData.name} onChange={e => setFormData({...formData, name: e.target.value})} />
                </div>
                <div className="form-group">
                  <label className="form-label">Dirección (Opcional)</label>
                  <input type="text" className="form-control" placeholder="Ej. Av. Principal 123" value={formData.address} onChange={e => setFormData({...formData, address: e.target.value})} />
                </div>
                <div className="form-group">
                  <label className="form-label">Teléfono (Opcional)</label>
                  <input type="text" className="form-control" placeholder="Ej. +51 987654321" value={formData.contactPhone} onChange={e => setFormData({...formData, contactPhone: e.target.value})} />
                </div>
                
                <div className="modal-footer">
                  <button type="button" onClick={() => setShowModal(false)} className="btn btn-ghost">Cancelar</button>
                  <button type="submit" className="btn btn-primary">{editingId ? 'Guardar Cambios' : 'Crear Sede'}</button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
