import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { PlusCircle, Users, Mail, Phone, MapPin, Shield, Edit2, CheckSquare } from 'lucide-react';
import toast from 'react-hot-toast';

export default function Usuarios() {
  const [usuarios, setUsuarios] = useState([]);
  const [roles, setRoles] = useState([]);
  const [sedes, setSedes] = useState([]);
  const [categorias, setCategorias] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState(null);
  
  const initialForm = {
    firstName: '', lastName: '', email: '', password: '', phone: '', systemRole: 'Staff', academyRoleId: '', headquarterId: '', categoryIds: []
  };
  const [formData, setFormData] = useState(initialForm);

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      const [uRes, rRes, sRes, cRes] = await Promise.all([
        api.get('/academy-config/users'),
        api.get('/academy-config/roles'),
        api.get('/academy-config/headquarters'),
        api.get('/academy-config/categories')
      ]);
      setUsuarios(uRes.data);
      setRoles(rRes.data);
      setSedes(sRes.data);
      setCategorias(cRes.data);
    } catch (err) {
      toast.error('Error al cargar datos');
    } finally {
      setLoading(false);
    }
  };

  const handleEdit = (user) => {
    setFormData({
      firstName: user.firstName,
      lastName: user.lastName,
      email: user.email,
      password: '', // Leave empty unless changing
      phone: user.phone || '',
      systemRole: user.systemRole,
      // If AcademyAdmin with no custom role, pre-select the virtual admin option
      academyRoleId: user.academyRoleId || (user.systemRole === 'AcademyAdmin' ? '__admin__' : ''),
      headquarterId: user.headquarterId || '',
      categoryIds: user.categoryIds || []
    });
    setEditingId(user.id);
    setShowModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      // __admin__ is a UI-only sentinel: AcademyAdmin users don't need a custom academyRoleId
      const payload = {
        ...formData,
        academyRoleId: formData.academyRoleId === '__admin__' ? '' : formData.academyRoleId
      };
      if (editingId) {
        await api.put(`/academy-config/users/${editingId}`, payload);
        toast.success('Usuario actualizado');
      } else {
        await api.post('/academy-config/users', payload);
        toast.success('Usuario registrado');
      }
      setShowModal(false);
      setEditingId(null);
      setFormData(initialForm);
      fetchData();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al guardar usuario.');
    }
  };

  const toggleCategory = (catId) => {
    setFormData(prev => ({
      ...prev,
      categoryIds: prev.categoryIds.includes(catId) 
        ? prev.categoryIds.filter(id => id !== catId)
        : [...prev.categoryIds, catId]
    }));
  };

  if (loading) return <AppLayout title="Staff"><div className="text-center p-8"><span className="spinner" style={{borderColor: 'var(--primary)', borderTopColor: 'transparent'}}></span></div></AppLayout>;

  return (
    <AppLayout title="Módulo de Personal (Staff)">
      <div className="fade-in">
        <div className="card">
          <div className="card-header">
            <div>
              <h3 className="card-title flex items-center gap-2">
                <Users className="text-primary-400" size={20} /> Staff / Personal
              </h3>
              <p className="text-muted mt-1">Registra y asigna el personal a sedes y categorías.</p>
            </div>
            <button onClick={() => { setEditingId(null); setFormData(initialForm); setShowModal(true); }} className="btn btn-primary">
              <PlusCircle size={16} /> Nuevo Personal
            </button>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            {usuarios.map(user => (
              <div key={user.id} className="card p-5" style={{ background: 'var(--bg-surface)' }}>
                <div className="flex gap-4 items-start">
                  <div className="w-12 h-12 rounded-full bg-primary-500/20 text-primary-400 flex items-center justify-center font-bold text-xl shrink-0 border border-primary/30">
                    {user.firstName[0]}{user.lastName[0]}
                  </div>
                  <div className="flex-1">
                    <div className="flex justify-between items-start">
                      <div>
                        <h3 className="font-bold text-lg">{user.firstName} {user.lastName}</h3>
                        <span className={`badge mt-1 inline-block ${user.systemRole === 'AcademyAdmin' ? 'bg-primary/20 text-primary-100' : 'badge-muted'}`}>
                          {user.systemRole === 'AcademyAdmin' ? 'Admin' : 'Staff'}
                        </span>
                      </div>
                      <div className="flex flex-col items-end gap-2">
                        <span className={`badge ${user.status === 'Active' ? 'badge-success' : 'badge-danger'}`}>
                          {user.status}
                        </span>
                        <button onClick={() => handleEdit(user)} className="btn btn-ghost btn-sm" title="Editar"><Edit2 size={14}/></button>
                      </div>
                    </div>
                    
                    <div className="grid grid-cols-2 gap-3 mt-4 text-sm text-text-secondary">
                      <div className="flex items-center gap-2"><Mail size={14} className="text-primary-400"/> <span className="truncate">{user.email}</span></div>
                      <div className="flex items-center gap-2"><Phone size={14} className="text-primary-400"/> {user.phone || '-'}</div>
                      <div className="flex items-center gap-2"><Shield size={14} className="text-warning"/> {user.academyRoleName || 'Sin Cargo'}</div>
                      <div className="flex items-center gap-2"><MapPin size={14} className="text-success"/> {user.headquarterName || 'Todas las Sedes'}</div>
                    </div>
                    
                    {user.categoryNames?.length > 0 && (
                      <div className="mt-3 pt-3 border-t border-border/50 text-xs text-text-muted">
                        <span className="font-semibold text-text-secondary mb-1 block">Categorías asignadas:</span>
                        <div className="flex flex-wrap gap-1">
                          {user.categoryNames.map((name, i) => (
                            <span key={i} className="px-2 py-0.5 bg-border rounded-md">{name}</span>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>

        {showModal && (
          <div className="modal-overlay" onClick={() => setShowModal(false)}>
            <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '650px' }}>
              <h3 className="modal-title">{editingId ? 'Editar Personal' : 'Registrar Personal'}</h3>
              
              <form onSubmit={handleSubmit}>
                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Nombre *</label>
                    <input required type="text" className="form-control" value={formData.firstName} onChange={e => setFormData({...formData, firstName: e.target.value})} />
                  </div>
                  <div className="form-group">
                    <label className="form-label">Apellido *</label>
                    <input required type="text" className="form-control" value={formData.lastName} onChange={e => setFormData({...formData, lastName: e.target.value})} />
                  </div>
                </div>
                
                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Correo Electrónico *</label>
                    <input required type="email" className="form-control" value={formData.email} onChange={e => setFormData({...formData, email: e.target.value})} />
                  </div>
                  <div className="form-group">
                    <label className="form-label">{editingId ? 'Nueva Contraseña (Opcional)' : 'Contraseña Temporal *'}</label>
                    <input required={!editingId} type="password" className="form-control" placeholder={editingId ? 'Dejar en blanco para no cambiar' : ''} value={formData.password} onChange={e => setFormData({...formData, password: e.target.value})} />
                  </div>
                </div>

                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Teléfono (Opcional)</label>
                    <input type="text" className="form-control" value={formData.phone} onChange={e => setFormData({...formData, phone: e.target.value})} />
                  </div>
                  <div className="form-group">
                    <label className="form-label">Nivel de Acceso *</label>
                    <select className="form-control" value={formData.systemRole} onChange={e => {
                      const newRole = e.target.value;
                      setFormData(prev => ({
                        ...prev,
                        systemRole: newRole,
                        // Auto-select admin cargo when switching to AcademyAdmin
                        academyRoleId: newRole === 'AcademyAdmin'
                          ? (prev.academyRoleId && prev.academyRoleId !== '__admin__' ? prev.academyRoleId : '__admin__')
                          : (prev.academyRoleId === '__admin__' ? '' : prev.academyRoleId)
                      }));
                    }}>
                      <option value="Staff">Personal Normal (Staff)</option>
                      <option value="AcademyAdmin">Administrador Total (AcademyAdmin)</option>
                    </select>
                  </div>
                </div>

                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">
                      Cargo / Tipo de Personal
                      {formData.systemRole !== 'AcademyAdmin' && ' *'}
                    </label>
                    <select
                      required={formData.systemRole !== 'AcademyAdmin'}
                      className="form-control"
                      value={formData.academyRoleId}
                      onChange={e => setFormData({...formData, academyRoleId: e.target.value})}
                    >
                      <option value="">-- Seleccione un Cargo --</option>
                      {/* Opción fija para administradores de academia */}
                      {formData.systemRole === 'AcademyAdmin' && (
                        <option value="__admin__">👑 Administrador de Academia</option>
                      )}
                      {roles.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}
                    </select>
                    {formData.systemRole === 'AcademyAdmin' && (
                      <p className="text-xs text-primary-400 mt-1" style={{ opacity: 0.8 }}>
                        El administrador tiene acceso completo a la academia.
                      </p>
                    )}
                  </div>
                  <div className="form-group">
                    <label className="form-label">Sede Asignada</label>
                    <select className="form-control" value={formData.headquarterId} onChange={e => setFormData({...formData, headquarterId: e.target.value})}>
                      <option value="">-- Opcional (o Todas) --</option>
                      {sedes.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
                    </select>
                  </div>
                </div>

                <div className="form-group mt-2">
                  <label className="form-label flex items-center gap-2 mb-3">
                    <CheckSquare size={16} className="text-primary-400" />
                    Asignación a Categorías (Opcional)
                  </label>
                  {categorias.length === 0 ? (
                    <p className="text-sm text-text-muted italic">No hay categorías creadas aún.</p>
                  ) : (
                    <div className="grid grid-cols-2 gap-2 bg-bg-dark p-3 rounded-lg border border-border/50 max-h-40 overflow-y-auto">
                      {categorias.map(cat => (
                        <label key={cat.id} className="flex items-center gap-2 cursor-pointer hover:bg-bg-surface p-1.5 rounded transition-colors text-sm">
                          <input 
                            type="checkbox" 
                            className="rounded border-border bg-bg-surface text-primary focus:ring-primary/20 focus:ring-offset-bg-dark h-4 w-4"
                            checked={formData.categoryIds.includes(cat.id)}
                            onChange={() => toggleCategory(cat.id)}
                          />
                          <span className="truncate flex-1" title={`${cat.name} (${cat.headquarterName})`}>
                            {cat.name} <span className="text-text-muted text-xs">({cat.headquarterName})</span>
                          </span>
                        </label>
                      ))}
                    </div>
                  )}
                </div>
                
                <div className="modal-footer mt-6">
                  <button type="button" onClick={() => setShowModal(false)} className="btn btn-ghost">Cancelar</button>
                  <button type="submit" className="btn btn-primary">{editingId ? 'Guardar Cambios' : 'Registrar Staff'}</button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
