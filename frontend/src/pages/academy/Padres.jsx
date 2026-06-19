import React, { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { Users, Mail, Phone, Search, Edit2, User } from 'lucide-react';
import toast from 'react-hot-toast';

export default function Padres() {
  const [padres, setPadres] = useState([]);
  const [students, setStudents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  
  const [showEditModal, setShowEditModal] = useState(false);
  const [showStudentsModal, setShowStudentsModal] = useState(false);
  const [editingPadre, setEditingPadre] = useState(null);
  const [selectedStudents, setSelectedStudents] = useState([]);
  const [formData, setFormData] = useState({ firstName: '', lastName: '', email: '', phone: '' });

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      setLoading(true);
      const [uRes, sRes] = await Promise.all([
        api.get('/academy-config/users'),
        api.get('/students')
      ]);
      setPadres(uRes.data.filter(u => u.systemRole === 'Guardian'));
      setStudents(sRes.data || []);
    } catch (err) {
      toast.error('Error al cargar apoderados');
    } finally {
      setLoading(false);
    }
  };

  const filtered = padres.filter(p => 
    (p.firstName + ' ' + p.lastName).toLowerCase().includes(search.toLowerCase()) ||
    (p.email || '').toLowerCase().includes(search.toLowerCase())
  );

  const handleEdit = (p) => {
    setEditingPadre(p);
    setFormData({
      firstName: p.firstName, lastName: p.lastName,
      email: p.email || '', phone: p.phone || ''
    });
    setShowEditModal(true);
  };

  const handleSave = async (e) => {
    e.preventDefault();
    try {
      const payload = {
        ...editingPadre,
        firstName: formData.firstName,
        lastName: formData.lastName,
        email: formData.email,
        phone: formData.phone
      };
      await api.put(`/academy-config/users/${editingPadre.id}`, payload);
      toast.success('Apoderado actualizado correctamente');
      setShowEditModal(false);
      fetchData(); // Recargar para obtener datos actualizados
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al actualizar apoderado');
    }
  };

  const handleViewStudents = (p) => {
    const associated = students.filter(s => s.guardianId === p.id);
    setSelectedStudents(associated);
    setEditingPadre(p);
    setShowStudentsModal(true);
  };

  if (loading) return <AppLayout title="Apoderados"><div className="text-center p-8"><span className="spinner" style={{borderColor: 'var(--primary)', borderTopColor: 'transparent'}}></span></div></AppLayout>;

  return (
    <AppLayout title="Gestión de Apoderados">
      <div className="fade-in space-y-6">
        <div className="card">
          <div className="card-header border-b border-border/50 pb-4 mb-4 flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
            <div>
              <h3 className="card-title flex items-center gap-2">
                <Users className="text-primary-400" size={20} /> Padres y Apoderados
              </h3>
              <p className="text-muted mt-1 text-sm text-text-secondary">Directorio de familiares vinculados a alumnos.</p>
            </div>
            <div className="relative w-full sm:w-auto">
              <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted"/>
              <input 
                type="text" 
                placeholder="Buscar por nombre o correo..." 
                className="form-control pl-9 py-2 text-sm w-full sm:w-64"
                value={search} 
                onChange={e => setSearch(e.target.value)}
              />
            </div>
          </div>

          {filtered.length === 0 ? (
            <div className="text-center p-8 text-text-muted">
              No se encontraron apoderados registrados.
            </div>
          ) : (
            <div className="table-container">
              <table className="table">
                <thead>
                  <tr>
                    <th>Apoderado</th>
                    <th>Contacto</th>
                    <th>Alumnos a cargo</th>
                    <th className="text-right">Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map(p => {
                    const linkedStudentsCount = students.filter(s => s.guardianId === p.id).length;
                    return (
                      <tr key={p.id}>
                        <td>
                          <div className="flex items-center gap-3">
                            <div className="w-10 h-10 rounded-full bg-indigo-500/10 text-indigo-500 flex items-center justify-center font-bold text-sm shrink-0 border border-indigo-500/20">
                              {p.firstName[0]}{p.lastName[0]}
                            </div>
                            <div>
                              <div className="font-bold text-text-primary text-sm">{p.firstName} {p.lastName}</div>
                              <div className="text-xs mt-0.5">
                                <span className="badge inline-block bg-indigo-500/10 text-indigo-500">
                                  Apoderado
                                </span>
                              </div>
                            </div>
                          </div>
                        </td>
                        <td>
                          <div className="text-sm text-text-secondary flex flex-col gap-1">
                            <div className="flex items-center gap-1.5"><Mail size={13} className="text-primary"/> <span className="truncate max-w-[200px]">{p.email || 'Sin correo'}</span></div>
                            <div className="flex items-center gap-1.5"><Phone size={13} className="text-primary"/> {p.phone || 'Sin teléfono'}</div>
                          </div>
                        </td>
                        <td>
                          <div className="text-sm text-text-secondary">
                            <span className="badge badge-muted flex items-center gap-1 w-fit">
                              <User size={12} /> {linkedStudentsCount} {linkedStudentsCount === 1 ? 'Alumno' : 'Alumnos'}
                            </span>
                          </div>
                        </td>
                        <td className="text-right align-middle">
                          <button onClick={() => handleViewStudents(p)} className="btn btn-ghost btn-sm mr-2" title="Ver Alumnos Vinculados">
                            <Users size={16} />
                          </button>
                          <button onClick={() => handleEdit(p)} className="btn btn-ghost btn-sm" title="Editar Apoderado">
                            <Edit2 size={16} />
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {showEditModal && (
        <div className="modal-overlay" onClick={() => setShowEditModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '500px' }}>
            <h3 className="modal-title">Editar Apoderado</h3>
            <form onSubmit={handleSave}>
              <div className="form-group">
                <label className="form-label">Nombre *</label>
                <input required type="text" className="form-control" value={formData.firstName} onChange={e => setFormData({...formData, firstName: e.target.value})} />
              </div>
              <div className="form-group">
                <label className="form-label">Apellido *</label>
                <input required type="text" className="form-control" value={formData.lastName} onChange={e => setFormData({...formData, lastName: e.target.value})} />
              </div>
              <div className="form-group">
                <label className="form-label">Correo Electrónico</label>
                <input type="email" className="form-control" value={formData.email} onChange={e => setFormData({...formData, email: e.target.value})} />
              </div>
              <div className="form-group">
                <label className="form-label">Celular / Teléfono</label>
                <input type="text" className="form-control" value={formData.phone} onChange={e => setFormData({...formData, phone: e.target.value})} />
              </div>
              
              <div className="modal-footer mt-6 flex justify-between items-center border-t border-border pt-5">
                <button type="button" onClick={() => setShowEditModal(false)} className="btn btn-ghost text-danger hover:bg-danger/10 border-transparent hover:border-danger/20 rounded-xl px-5 py-2 font-medium transition-all">Cancelar</button>
                <button type="submit" className="btn btn-primary px-6" style={{ height: '44px', borderRadius: '10px' }}>Guardar Cambios</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {showStudentsModal && (
        <div className="modal-overlay" onClick={() => setShowStudentsModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '600px' }}>
            <h3 className="modal-title mb-2">Alumnos Vinculados</h3>
            <p className="text-sm text-text-muted mb-6">
              Alumnos registrados a cargo de <strong>{editingPadre?.firstName} {editingPadre?.lastName}</strong>.
            </p>
            
            {selectedStudents.length === 0 ? (
              <div className="text-center p-6 bg-bg-surface rounded-lg text-text-muted">
                No hay alumnos vinculados a este apoderado.
              </div>
            ) : (
              <div className="space-y-3 max-h-96 overflow-y-auto pr-2">
                {selectedStudents.map(s => (
                  <div key={s.id} className="flex items-center gap-4 p-4 bg-bg-surface rounded-xl border border-border">
                    <div className="w-12 h-12 rounded-full bg-primary/10 text-primary flex items-center justify-center font-bold text-lg border border-primary/20">
                      {s.firstName[0]}{s.lastName[0]}
                    </div>
                    <div>
                      <div className="font-bold text-base text-text-primary">{s.firstName} {s.lastName}</div>
                      <div className="text-sm text-text-muted mt-1">
                        Sede: {s.headquarterName} · Categoría: {s.categoryName}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
            
            <div className="modal-footer mt-6 flex justify-end border-t border-border pt-5">
              <button type="button" onClick={() => setShowStudentsModal(false)} className="btn btn-ghost hover:bg-bg-surface rounded-xl px-6 py-2 transition-all">Cerrar</button>
            </div>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
