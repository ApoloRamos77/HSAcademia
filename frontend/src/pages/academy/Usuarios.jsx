import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { PlusCircle, Users, Mail, Phone, MapPin, Shield, Edit2, CheckSquare, Crown } from 'lucide-react';
import toast from 'react-hot-toast';
import { useAuth } from '../../context/AuthContext';

export default function Usuarios() {
  const { user: me } = useAuth();
  const [usuarios, setUsuarios] = useState([]);
  const [roles, setRoles] = useState([]);
  const [sedes, setSedes] = useState([]);
  const [categorias, setCategorias] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editingId, setEditingId] = useState(null);
  
  const initialForm = {
    firstName: '', lastName: '', email: '', password: '', phone: '', systemRole: 'Staff', academyRoleIds: [], headquarterIds: [], categoryIds: [],
    paymentType: 0, paymentRate: '', status: 'Active'
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
      // Solo mostramos Personal y Administradores en esta vista
      const staffOnly = uRes.data.filter(u => u.systemRole === 'Staff' || u.systemRole === 'AcademyAdmin');
      setUsuarios(staffOnly);
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
      academyRoleIds: user.academyRoleIds || [],
      headquarterIds: (() => {
        const hSet = new Set(user.headquarterIds || []);
        if (user.categoryIds) {
          user.categoryIds.forEach(catId => {
            const cat = categorias.find(c => c.id === catId);
            if (cat && cat.headquarterId) hSet.add(cat.headquarterId);
          });
        }
        return Array.from(hSet);
      })(),
      categoryIds: user.categoryIds || [],
      paymentType: user.paymentType ?? 0,
      paymentRate: user.paymentRate?.toString() || '',
      status: user.status || 'Active'
    });
    setEditingId(user.id);
    setShowModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (formData.systemRole === 'Staff' && formData.academyRoleIds.length === 0) {
        toast.error('Asigna al menos un rol.');
        return;
      }
      const payload = {
        ...formData,
        // El administrador tiene acceso completo: los roles solo aplican al personal.
        academyRoleIds: formData.systemRole === 'Staff' ? formData.academyRoleIds : [],
        headquarterIds: formData.headquarterIds,
        paymentType: parseInt(formData.paymentType),
        paymentRate: parseFloat(formData.paymentRate) || 0,
        status: formData.status
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

  const transferOwnership = async (target) => {
    if (!window.confirm(`¿Transferir la propiedad de la academia a ${target.firstName} ${target.lastName}? Dejarás de ser el propietario.`)) return;
    try {
      await api.post(`/academy-config/users/${target.id}/transfer-ownership`);
      toast.success('Propiedad transferida');
      window.location.reload();
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo transferir la propiedad.');
    }
  };

  const toggleRole = (roleId) => {
    setFormData(prev => ({
      ...prev,
      academyRoleIds: prev.academyRoleIds.includes(roleId)
        ? prev.academyRoleIds.filter(id => id !== roleId)
        : [...prev.academyRoleIds, roleId]
    }));
  };

  const toggleCategory = (catId) => {
    setFormData(prev => ({
      ...prev,
      categoryIds: prev.categoryIds.includes(catId) 
        ? prev.categoryIds.filter(id => id !== catId)
        : [...prev.categoryIds, catId]
    }));
  };

  const toggleHeadquarter = (hqId) => {
    setFormData(prev => {
      const newHqIds = prev.headquarterIds.includes(hqId)
        ? prev.headquarterIds.filter(id => id !== hqId)
        : [...prev.headquarterIds, hqId];
      
      // Remove categories that do not belong to the newly selected headquarters
      // If newHqIds is empty, we allow all categories
      let newCatIds = prev.categoryIds;
      if (newHqIds.length > 0) {
        newCatIds = prev.categoryIds.filter(catId => {
          const cat = categorias.find(c => c.id === catId);
          return cat && newHqIds.includes(cat.headquarterId);
        });
      }
      return { ...prev, headquarterIds: newHqIds, categoryIds: newCatIds };
    });
  };

  const categoriasFiltradas = formData.headquarterIds.length === 0
    ? categorias
    : categorias.filter(c => formData.headquarterIds.includes(c.headquarterId));

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

          <div className="table-container">
            <table className="table">
              <thead>
                <tr>
                  <th>Personal</th>
                  <th>Contacto</th>
                  <th>Asignación</th>
                  <th>Condición</th>
                  <th className="text-right">Acciones</th>
                </tr>
              </thead>
              <tbody>
                {usuarios.map(user => (
                  <tr key={user.id}>
                    <td>
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 rounded-full bg-primary-light/20 text-primary-dark flex items-center justify-center font-bold text-sm shrink-0 border border-primary/30">
                          {user.firstName[0]}{user.lastName[0]}
                        </div>
                        <div>
                          <div className="font-bold text-text-primary text-sm">{user.firstName} {user.lastName}</div>
                          <div className="text-xs mt-0.5">
                            <span className={`badge inline-flex items-center gap-1 ${user.systemRole === 'AcademyAdmin' ? 'badge-info' : 'badge-muted'}`}>
                              {user.isOwner && <Crown size={11} />}
                              {user.isOwner ? 'Propietario' : user.systemRole === 'AcademyAdmin' ? 'Administrador' : 'Personal'}
                            </span>
                          </div>
                        </div>
                      </div>
                    </td>
                    <td>
                      <div className="text-sm text-text-secondary flex flex-col gap-1">
                        <div className="flex items-center gap-1.5"><Mail size={13} className="text-primary"/> <span className="truncate max-w-[150px]">{user.email}</span></div>
                        <div className="flex items-center gap-1.5"><Phone size={13} className="text-primary"/> {user.phone || '-'}</div>
                      </div>
                    </td>
                    <td>
                      <div className="text-sm text-text-secondary flex flex-col gap-1">
                        <div className="flex items-center gap-1.5"><Shield size={13} className="text-warning"/> <span className="truncate max-w-[150px]" title={user.academyRoleNames?.join(', ')}>{user.systemRole === 'AcademyAdmin' ? 'Acceso completo' : (user.academyRoleNames?.join(', ') || 'Sin rol')}</span></div>
                        <div className="flex items-center gap-1.5"><MapPin size={13} className="text-success"/> <span className="truncate max-w-[150px]" title={user.headquarterNames?.join(', ')}>{user.headquarterNames?.length ? user.headquarterNames.join(', ') : 'Todas las Sedes'}</span></div>
                      </div>
                    </td>
                    <td>
                      <div className="flex flex-col gap-1 items-start">
                        <span className={`badge ${user.status === 'Active' ? 'badge-success' : 'badge-danger'}`}>
                          {user.status === 'Active' ? 'Activo' : user.status}
                        </span>
                        <span className="text-xs text-text-muted font-medium">
                          {user.paymentType === 1 ? '⏱ Por Sesión' : '📅 Mensual'}
                        </span>
                      </div>
                    </td>
                    <td className="text-right align-middle">
                      {me?.isOwner && user.systemRole === 'AcademyAdmin' && !user.isOwner && user.status === 'Active' && (
                        <button onClick={() => transferOwnership(user)} className="btn btn-ghost btn-sm" title="Transferir la propiedad"><Crown size={16}/></button>
                      )}
                      <button onClick={() => handleEdit(user)} className="btn btn-ghost btn-sm" title="Editar"><Edit2 size={16}/></button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
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
                    <select className="form-control" value={formData.systemRole} disabled={!me?.isOwner} onChange={e => setFormData({ ...formData, systemRole: e.target.value })}>
                      <option value="Staff">Personal (según sus roles)</option>
                      <option value="AcademyAdmin">Administrador (acceso completo)</option>
                    </select>
                  </div>
                </div>

                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label mb-2">Roles {formData.systemRole === 'Staff' && '*'}</label>
                    {formData.systemRole === 'AcademyAdmin' ? (
                      <p className="text-xs text-primary-400 mt-1" style={{ opacity: 0.8 }}>El administrador tiene acceso completo a la academia.</p>
                    ) : (
                      <div className="grid grid-cols-1 gap-1 bg-bg-dark p-3 rounded-lg border border-border/50 max-h-32 overflow-y-auto">
                        {roles.filter(r => r.isActive || formData.academyRoleIds.includes(r.id)).map(r => (
                          <label key={r.id} className="flex items-center gap-2 cursor-pointer hover:bg-bg-surface p-1 rounded transition-colors text-sm" title={r.description || ''}>
                            <input type="checkbox" className="rounded border-border bg-bg-surface text-primary h-4 w-4" checked={formData.academyRoleIds.includes(r.id)} onChange={() => toggleRole(r.id)} />
                            <span className="truncate flex-1">{r.name}{r.isSystem ? '' : ' (propio)'}{r.isActive ? '' : ' · inactivo'}</span>
                          </label>
                        ))}
                      </div>
                    )}
                  </div>
                  <div className="form-group">
                    <label className="form-label mb-2">Sedes asignadas (sin marcar = todas)</label>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 bg-bg-dark p-3 rounded-lg border border-border/50 max-h-32 overflow-y-auto">
                      {sedes.map(s => (
                        <label key={s.id} className="flex items-center gap-2 cursor-pointer hover:bg-bg-surface p-1.5 rounded transition-colors text-sm">
                          <input 
                            type="checkbox" 
                            className="rounded border-border bg-bg-surface text-primary focus:ring-primary/20 h-4 w-4"
                            checked={formData.headquarterIds.includes(s.id)}
                            onChange={() => toggleHeadquarter(s.id)}
                          />
                          <span className="truncate flex-1">{s.name}</span>
                        </label>
                      ))}
                    </div>
                  </div>
                </div>

                {/* Payment Configuration */}
                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Tipo de Pago</label>
                    <select className="form-control" value={formData.paymentType} onChange={e => setFormData({...formData, paymentType: parseInt(e.target.value)})}>
                      <option value={0}>📅 Mensual (Sueldo Fijo)</option>
                      <option value={1}>⏱ Por Sesión / Hora</option>
                    </select>
                  </div>
                  <div className="form-group">
                    <label className="form-label">
                      {formData.paymentType === 1 ? 'Tarifa por Sesión (S/)' : 'Sueldo Mensual (S/)'}
                    </label>
                    <input
                      type="number" step="0.01" min="0"
                      className="form-control"
                      placeholder="0.00"
                      value={formData.paymentRate}
                      onChange={e => setFormData({...formData, paymentRate: e.target.value})}
                    />
                    {formData.paymentType === 1 && (
                      <p className="text-xs text-primary-400 mt-1">El monto se calculará automáticamente según las sesiones dictadas.</p>
                    )}
                  </div>
                </div>

                {/* Estado activo/inactivo — solo al editar */}
                {editingId && (
                  <div className="form-group mt-2">
                    <label className="form-label mb-2">Estado del Personal</label>
                    <div className="flex gap-3">
                      {['Active', 'Inactive', 'Suspended'].map(st => {
                        const labels = { Active: '✅ Activo', Inactive: '⛔ Inactivo', Suspended: '⏸ Suspendido' };
                        const colors = { Active: 'var(--success)', Inactive: 'var(--danger)', Suspended: 'var(--warning)' };
                        const selected = formData.status === st;
                        return (
                          <button
                            key={st}
                            type="button"
                            onClick={() => setFormData({ ...formData, status: st })}
                            style={{
                              padding: '6px 14px',
                              borderRadius: '8px',
                              border: `2px solid ${selected ? colors[st] : 'var(--border)'}`,
                              background: selected ? `${colors[st]}22` : 'var(--bg-dark)',
                              color: selected ? colors[st] : 'var(--text-muted)',
                              fontWeight: selected ? 600 : 400,
                              cursor: 'pointer',
                              fontSize: '13px',
                              transition: 'all 0.15s'
                            }}
                          >
                            {labels[st]}
                          </button>
                        );
                      })}
                    </div>
                  </div>
                )}

                <div className="form-group mt-2">
                  <label className="form-label flex items-center gap-2 mb-3">
                    <CheckSquare size={16} className="text-primary-400" />
                    Asignación a Categorías (Opcional)
                  </label>
                  {categoriasFiltradas.length === 0 ? (
                    <p className="text-sm text-text-muted italic">No hay categorías para las sedes seleccionadas.</p>
                  ) : (
                    <div className="grid grid-cols-2 gap-2 bg-bg-dark p-3 rounded-lg border border-border/50 max-h-40 overflow-y-auto">
                      {categoriasFiltradas.map(cat => (
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
