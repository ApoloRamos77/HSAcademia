import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { PlusCircle, Search, Edit2, Trash2, ClipboardList, MapPin } from 'lucide-react';
import toast from 'react-hot-toast';

export default function Categorias() {
  const [categorias, setCategorias] = useState([]);
  const [sedes, setSedes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [filterSede, setFilterSede] = useState('');
  const [formData, setFormData] = useState({ headquarterId: '', name: '', startDateOfBirth: '', endDateOfBirth: '' });

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      const [catRes, sedesRes] = await Promise.all([
        api.get('/academy-config/categories'),
        api.get('/academy-config/headquarters')
      ]);
      setCategorias(catRes.data);
      setSedes(sedesRes.data);
      if (sedesRes.data.length > 0) {
        setFormData(prev => ({ ...prev, headquarterId: sedesRes.data[0].id }));
      }
    } catch (err) {
      toast.error('Error al cargar datos');
    } finally {
      setLoading(false);
    }
  };

  const handleEdit = (categoria) => {
    setFormData({ 
      headquarterId: categoria.headquarterId, 
      name: categoria.name, 
      startDateOfBirth: categoria.startDateOfBirth ? categoria.startDateOfBirth.split('T')[0] : '', 
      endDateOfBirth: categoria.endDateOfBirth ? categoria.endDateOfBirth.split('T')[0] : '' 
    });
    setEditingId(categoria.id);
    setShowModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (new Date(formData.startDateOfBirth) > new Date(formData.endDateOfBirth)) {
      return toast.error('La fecha de inicio no puede ser mayor a la fecha de fin.');
    }
    
    try {
      if (editingId) {
        await api.put(`/academy-config/categories/${editingId}`, formData);
        toast.success('Categoría actualizada');
      } else {
        await api.post('/academy-config/categories', formData);
        toast.success('Categoría creada');
      }
      setShowModal(false);
      setEditingId(null);
      setFormData({ headquarterId: sedes.length > 0 ? sedes[0].id : '', name: '', startDateOfBirth: '', endDateOfBirth: '' });
      fetchData();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al guardar categoría.');
    }
  };

  const getSedeName = (id) => sedes.find(s => s.id === id)?.name || 'Desconocida';

  const filteredCategorias = filterSede 
    ? categorias.filter(c => c.headquarterId === filterSede) 
    : categorias;

  if (loading) return <AppLayout title="Categorías"><div className="text-center p-8"><span className="spinner" style={{borderColor: 'var(--primary)', borderTopColor: 'transparent'}}></span></div></AppLayout>;

  return (
    <AppLayout title="Categorías y Grupos">
      <div className="fade-in">
        <div className="card">
          <div className="card-header">
            <div>
              <h3 className="card-title flex items-center gap-2">
                <ClipboardList className="text-primary-400" size={20} /> Categorías
              </h3>
              <p className="text-muted mt-1">Configura los grupos de entrenamiento por edades.</p>
            </div>
            <button onClick={() => { setEditingId(null); setFormData({ headquarterId: sedes.length > 0 ? sedes[0].id : '', name: '', startDateOfBirth: '', endDateOfBirth: '' }); setShowModal(true); }} className="btn btn-primary">
              <PlusCircle size={16} /> Nueva Categoría
            </button>
          </div>

          <div className="flex gap-4 mb-6">
            <div className="flex-1 max-w-xs">
              <label className="text-xs text-text-muted block mb-1">Filtrar por Sede</label>
              <select className="form-control" value={filterSede} onChange={e => setFilterSede(e.target.value)}>
                <option value="">Todas las sedes</option>
                {sedes.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
              </select>
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {filteredCategorias.map(cat => (
              <div key={cat.id} className="card p-5" style={{ background: 'var(--bg-surface)' }}>
                <div className="flex justify-between items-start mb-4">
                  <div>
                    <h3 className="font-bold text-lg text-primary-100">{cat.name}</h3>
                    <div className="flex items-center gap-1 text-xs text-text-muted mt-1">
                      <MapPin size={12} /> Sede {cat.headquarterName}
                    </div>
                  </div>
                  <span className={`badge ${cat.isActive ? 'badge-success' : 'badge-danger'}`}>
                    {cat.isActive ? 'Activa' : 'Inactiva'}
                  </span>
                </div>
                
                <div className="flex justify-center items-center py-4 bg-bg-dark rounded-lg mb-4 border border-border">
                  <div className="text-center px-4 border-r border-border">
                    <span className="block text-xs text-text-muted mb-1">F. Nacimiento Inicial</span>
                    <span className="text-sm font-bold">{cat.startDateOfBirth ? cat.startDateOfBirth.split('T')[0] : 'N/A'}</span>
                  </div>
                  <div className="text-center px-4">
                    <span className="block text-xs text-text-muted mb-1">F. Nacimiento Final</span>
                    <span className="text-sm font-bold">{cat.endDateOfBirth ? cat.endDateOfBirth.split('T')[0] : 'N/A'}</span>
                  </div>
                </div>

                <div className="flex justify-end gap-2 border-t border-border pt-4">
                  <button onClick={() => handleEdit(cat)} className="btn btn-ghost btn-sm" title="Editar"><Edit2 size={14}/></button>
                </div>
              </div>
            ))}
          </div>
          {filteredCategorias.length === 0 && <div className="empty-state"><ClipboardList size={40}/><p>No hay categorías registradas en esta vista</p></div>}
        </div>

        {showModal && (
          <div className="modal-overlay" onClick={() => setShowModal(false)}>
            <div className="modal" onClick={e => e.stopPropagation()}>
              <h3 className="modal-title">{editingId ? 'Editar Categoría' : 'Nueva Categoría'}</h3>
              
              <form onSubmit={handleSubmit}>
                <div className="form-group">
                  <label className="form-label">Sede a la que pertenece *</label>
                  <select required className="form-control" value={formData.headquarterId} onChange={e => setFormData({...formData, headquarterId: e.target.value})}>
                    <option value="">Seleccione una sede...</option>
                    {sedes.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label className="form-label">Nombre de Categoría *</label>
                  <input required type="text" className="form-control" placeholder="Ej. Sub-12" value={formData.name} onChange={e => setFormData({...formData, name: e.target.value})} />
                </div>
                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Desde (Fecha de Nacimiento) *</label>
                    <input required type="date" min="1900-01-01" max="2100-12-31" className="form-control" value={formData.startDateOfBirth} onChange={e => setFormData({...formData, startDateOfBirth: e.target.value})} />
                  </div>
                  <div className="form-group">
                    <label className="form-label">Hasta (Fecha de Nacimiento) *</label>
                    <input required type="date" min="1900-01-01" max="2100-12-31" className="form-control" value={formData.endDateOfBirth} onChange={e => setFormData({...formData, endDateOfBirth: e.target.value})} />
                  </div>
                </div>
                
                <div className="modal-footer">
                  <button type="button" onClick={() => setShowModal(false)} className="btn btn-ghost">Cancelar</button>
                  <button type="submit" className="btn btn-primary">{editingId ? 'Guardar Cambios' : 'Crear Categoría'}</button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
