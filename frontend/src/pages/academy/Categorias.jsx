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
  const [formData, setFormData] = useState({ headquarterId: '', name: '', startDateOfBirth: '', endDateOfBirth: '', monthlyFee: '', isFree: false, minAge: '', sessionFee: '', packageSessions: '', packagePrice: '' });

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
      endDateOfBirth: categoria.endDateOfBirth ? categoria.endDateOfBirth.split('T')[0] : '',
      monthlyFee: categoria.monthlyFee ?? '',
      isFree: !!categoria.isFree,
      minAge: categoria.minAge ?? '',
      sessionFee: categoria.sessionFee ? categoria.sessionFee : '',
      packageSessions: categoria.packageSessions ?? '',
      packagePrice: categoria.packagePrice ?? ''
    });
    // Una categoría libre no muestra la franja amplia que el servidor envía a las apps anteriores.
    if (categoria.isFree) setFormData(f => ({ ...f, startDateOfBirth: '', endDateOfBirth: '' }));
    setEditingId(categoria.id);
    setShowModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!formData.isFree && new Date(formData.startDateOfBirth) > new Date(formData.endDateOfBirth)) {
      return toast.error('La fecha de inicio no puede ser mayor a la fecha de fin.');
    }

    // Libre: sin franja de nacimiento; por edad: sin edad mínima.
    const payload = {
      ...formData,
      startDateOfBirth: formData.isFree ? null : formData.startDateOfBirth,
      endDateOfBirth: formData.isFree ? null : formData.endDateOfBirth,
      minAge: formData.isFree && formData.minAge !== '' ? parseInt(formData.minAge, 10) : null,
      monthlyFee: formData.monthlyFee === '' ? null : parseFloat(formData.monthlyFee),
      // Precio por sesión siempre se envía (0 = no ofrece); el paquete va completo o vacío.
      sessionFee: formData.sessionFee === '' || formData.sessionFee == null ? 0 : parseFloat(formData.sessionFee),
      packageSessions: formData.packageSessions === '' || formData.packageSessions == null ? null : parseInt(formData.packageSessions, 10),
      packagePrice: formData.packagePrice === '' || formData.packagePrice == null ? null : parseFloat(formData.packagePrice),
    };
    try {
      if (editingId) {
        await api.put(`/academy-config/categories/${editingId}`, payload);
        toast.success('Categoría actualizada');
      } else {
        await api.post('/academy-config/categories', payload);
        toast.success('Categoría creada');
      }
      setShowModal(false);
      setEditingId(null);
      setFormData({ headquarterId: sedes.length > 0 ? sedes[0].id : '', name: '', startDateOfBirth: '', endDateOfBirth: '', monthlyFee: '', isFree: false, minAge: '', sessionFee: '', packageSessions: '', packagePrice: '' });
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
            <button onClick={() => { setEditingId(null); setFormData({ headquarterId: sedes.length > 0 ? sedes[0].id : '', name: '', startDateOfBirth: '', endDateOfBirth: '', monthlyFee: '', isFree: false, minAge: '', sessionFee: '', packageSessions: '', packagePrice: '' }); setShowModal(true); }} className="btn btn-primary">
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
                
                {cat.isFree ? (
                  <div className="flex justify-center items-center py-4 bg-bg-dark rounded-lg mb-4 border border-border">
                    <div className="text-center px-4">
                      <span className="block text-xs text-text-muted mb-1">Categoría libre</span>
                      <span className="text-sm font-bold">{cat.minAge ? `Desde ${cat.minAge} años` : 'Sin límite de edad'}</span>
                    </div>
                  </div>
                ) : (
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
                )}

                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
                  <span className="text-xs text-text-muted">Mensualidad</span>
                  <span style={{ fontWeight: 700, fontSize: 18 }}>{Number(cat.monthlyFee) > 0 ? `S/ ${Number(cat.monthlyFee).toFixed(2)}` : "Sin tarifa"}</span>
                </div>
                {(Number(cat.sessionFee) > 0 || cat.packageSessions) && (
                  <div className="text-xs text-text-muted" style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 12, gap: 8, flexWrap: 'wrap' }}>
                    {Number(cat.sessionFee) > 0 && <span>Por sesión: <b>S/ {Number(cat.sessionFee).toFixed(2)}</b></span>}
                    {cat.packageSessions && <span>Paquete: <b>{cat.packageSessions} sesiones · S/ {Number(cat.packagePrice).toFixed(2)}</b></span>}
                  </div>
                )}

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
                <div className="form-group">
                  <label className="form-label">¿Cómo se agrupan los alumnos?</label>
                  <div className="flex gap-4 text-sm" style={{ flexWrap: 'wrap' }}>
                    <label className="flex items-center gap-2" style={{ cursor: 'pointer' }}>
                      <input type="radio" name="ageMode" checked={!formData.isFree} onChange={() => setFormData({ ...formData, isFree: false })} /> Por edad (fecha de nacimiento)
                    </label>
                    <label className="flex items-center gap-2" style={{ cursor: 'pointer' }}>
                      <input type="radio" name="ageMode" checked={!!formData.isFree} onChange={() => setFormData({ ...formData, isFree: true })} /> Libre (sin edad)
                    </label>
                  </div>
                </div>
                {formData.isFree ? (
                  <div className="form-group">
                    <label className="form-label">Edad mínima (opcional)</label>
                    <input type="number" min="1" max="99" step="1" className="form-control" placeholder="Ej. 18 para adultos" value={formData.minAge} onChange={e => setFormData({ ...formData, minAge: e.target.value })} />
                    <small className="text-xs text-text-muted">Sin edad mínima, puede inscribirse cualquier alumno. Si alguno no la cumple, el sistema solo avisa.</small>
                  </div>
                ) : (
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
                )}
                
                <div className="form-group">
                  <label className="form-label">Mensualidad (S/) *</label>
                  <input required type="number" min="0" step="0.01" className="form-control" placeholder="Ej. 120.00" value={formData.monthlyFee} onChange={e => setFormData({...formData, monthlyFee: e.target.value})} />
                  <small className="text-xs text-text-muted">Monto base que se cobra cada mes a los alumnos de la categoría (salvo tarifa preferencial o beca). También es el tope de quienes pagan por sesión.</small>
                </div>

                <div className="form-group">
                  <label className="form-label">Precio por sesión (S/, opcional)</label>
                  <input type="number" min="0" step="0.01" className="form-control" placeholder="Ej. 15.00" value={formData.sessionFee ?? ''} onChange={e => setFormData({ ...formData, sessionFee: e.target.value })} />
                  <small className="text-xs text-text-muted">Para alumnos que pagan solo los días que asisten. Vacío: la categoría no ofrece pago por sesión.</small>
                </div>
                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Paquete: sesiones (opcional)</label>
                    <input type="number" min="1" max="60" step="1" className="form-control" placeholder="Ej. 8" value={formData.packageSessions ?? ''} onChange={e => setFormData({ ...formData, packageSessions: e.target.value })} />
                  </div>
                  <div className="form-group">
                    <label className="form-label">Paquete: precio al mes (S/)</label>
                    <input type="number" min="0" step="0.01" className="form-control" placeholder="Ej. 90.00" value={formData.packagePrice ?? ''} onChange={e => setFormData({ ...formData, packagePrice: e.target.value })} />
                  </div>
                </div>
                <small className="text-xs text-text-muted" style={{ display: 'block', marginTop: -8, marginBottom: 12 }}>
                  El paquete se cobra cada mes; las sesiones que lo superen se cobran a precio por sesión, sin pasar de la mensualidad.
                </small>

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
