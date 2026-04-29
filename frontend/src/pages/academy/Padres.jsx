import React, { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { Users, Mail, Phone, Search } from 'lucide-react';
import toast from 'react-hot-toast';

export default function Padres() {
  const [padres, setPadres] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      setLoading(true);
      const { data } = await api.get('/academy-config/users');
      // Filtramos solo los Apoderados (Guardians)
      setPadres(data.filter(u => u.systemRole === 'Guardian'));
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
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {filtered.map(p => (
                <div key={p.id} className="card p-5 bg-bg-dark hover:bg-bg-surface border border-border/50 transition-colors">
                  <div className="flex gap-4 items-start">
                    <div className="w-12 h-12 rounded-full bg-indigo-500/20 text-indigo-400 flex items-center justify-center font-bold text-xl shrink-0 border border-indigo-500/30">
                      {p.firstName[0]}{p.lastName[0]}
                    </div>
                    <div className="flex-1 overflow-hidden">
                      <h3 className="font-bold text-white text-lg truncate" title={`${p.firstName} ${p.lastName}`}>
                        {p.firstName} {p.lastName}
                      </h3>
                      <span className="badge mt-1 inline-block bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
                        Apoderado
                      </span>
                      
                      <div className="flex flex-col gap-2 mt-4 text-sm text-text-secondary">
                        <div className="flex items-center gap-2 truncate">
                          <Mail size={14} className="text-primary-400 shrink-0"/> 
                          <span className="truncate">{p.email || 'Sin correo'}</span>
                        </div>
                        <div className="flex items-center gap-2 truncate">
                          <Phone size={14} className="text-primary-400 shrink-0"/> 
                          <span>{p.phone || 'Sin teléfono'}</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </AppLayout>
  );
}
