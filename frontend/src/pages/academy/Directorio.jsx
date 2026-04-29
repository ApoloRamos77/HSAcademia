import React, { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { Search, Filter, Shield, User, GraduationCap, Users } from 'lucide-react';
import toast from 'react-hot-toast';

export default function Directorio() {
  const [usuarios, setUsuarios] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [filtroRol, setFiltroRol] = useState('Todos');

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      setLoading(true);
      const { data } = await api.get('/academy-config/users');
      setUsuarios(data);
    } catch (err) {
      toast.error('Error al cargar directorio');
    } finally {
      setLoading(false);
    }
  };

  const getRoleInfo = (role) => {
    switch(role) {
      case 'AcademyAdmin': return { label: 'Administrador', icon: <Shield size={14}/>, cls: 'bg-red-500/20 text-red-400 border-red-500/30' };
      case 'Staff': return { label: 'Personal', icon: <User size={14}/>, cls: 'bg-blue-500/20 text-blue-400 border-blue-500/30' };
      case 'Student': return { label: 'Alumno', icon: <GraduationCap size={14}/>, cls: 'bg-green-500/20 text-green-400 border-green-500/30' };
      case 'Guardian': return { label: 'Apoderado', icon: <Users size={14}/>, cls: 'bg-indigo-500/20 text-indigo-400 border-indigo-500/30' };
      default: return { label: role, icon: <User size={14}/>, cls: 'badge-muted' };
    }
  };

  // Filtrar duplicados por nombre completo y luego aplicar los filtros de búsqueda y rol
  const uniqueUsers = [];
  const seenNames = new Set();

  usuarios.forEach(u => {
    const fullName = `${u.firstName} ${u.lastName}`.toLowerCase().trim();
    if (!seenNames.has(fullName)) {
      seenNames.add(fullName);
      uniqueUsers.push(u);
    }
  });

  const filtered = uniqueUsers.filter(u => {
    const matchesSearch = (u.firstName + ' ' + u.lastName).toLowerCase().includes(search.toLowerCase()) ||
                          (u.email || '').toLowerCase().includes(search.toLowerCase());
    const matchesRole = filtroRol === 'Todos' || u.systemRole === filtroRol;
    return matchesSearch && matchesRole;
  });

  if (loading) return <AppLayout title="Directorio Global"><div className="text-center p-8"><span className="spinner" style={{borderColor: 'var(--primary)', borderTopColor: 'transparent'}}></span></div></AppLayout>;

  return (
    <AppLayout title="Directorio Global de Usuarios">
      <div className="fade-in space-y-6">
        <div className="card">
          <div className="card-header border-b border-border/50 pb-4 mb-4 flex flex-col md:flex-row gap-4 justify-between items-start md:items-center">
            <div>
              <h3 className="card-title flex items-center gap-2">
                <Users className="text-primary-400" size={20} /> Directorio Unificado
              </h3>
              <p className="text-sm text-text-secondary mt-1">
                Visualiza y filtra a todos los usuarios vinculados a tu academia.
              </p>
            </div>
            
            <div className="flex flex-col sm:flex-row gap-3 w-full md:w-auto">
              <div className="relative flex-1 sm:w-64">
                <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted"/>
                <input 
                  type="text" placeholder="Buscar usuario o correo..." 
                  className="form-control pl-9 text-sm"
                  value={search} onChange={e => setSearch(e.target.value)}
                />
              </div>
              <select 
                className="form-control text-sm sm:w-48"
                value={filtroRol}
                onChange={e => setFiltroRol(e.target.value)}
              >
                <option value="Todos">Todos los roles</option>
                <option value="AcademyAdmin">Administradores</option>
                <option value="Staff">Personal / Staff</option>
                <option value="Student">Alumnos</option>
                <option value="Guardian">Apoderados</option>
              </select>
            </div>
          </div>

          <div className="table-container">
            <table className="table">
              <thead>
                <tr>
                  <th>Usuario</th>
                  <th>Rol</th>
                  <th>Contacto</th>
                  <th>Estado</th>
                </tr>
              </thead>
              <tbody>
                {filtered.length === 0 ? (
                  <tr>
                    <td colSpan="4" className="text-center p-8 text-text-muted">No se encontraron usuarios que coincidan con la búsqueda.</td>
                  </tr>
                ) : (
                  filtered.map(u => {
                    const rInfo = getRoleInfo(u.systemRole);
                    return (
                      <tr key={u.id} className="hover:bg-white/5 transition-colors">
                        <td>
                          <div className="font-bold text-white flex items-center gap-2">
                            {u.firstName} {u.lastName}
                          </div>
                          <div className="text-xs text-text-muted mt-1">{u.email || 'Sin correo'}</div>
                        </td>
                        <td>
                          <span className={`badge border flex w-max items-center gap-1.5 ${rInfo.cls}`}>
                            {rInfo.icon} {rInfo.label}
                          </span>
                        </td>
                        <td>
                          <div className="text-sm text-text-secondary">
                            {u.phone || '-'}
                          </div>
                        </td>
                        <td>
                          <span className={`badge ${u.status === 'Active' ? 'badge-success' : 'badge-danger'}`}>
                            {u.status}
                          </span>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </AppLayout>
  );
}
