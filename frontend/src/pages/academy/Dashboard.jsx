import React, { useState, useEffect } from 'react';
import { Building2, Users, ClipboardList, ShieldCheck } from 'lucide-react';
import AppLayout from '../../components/AppLayout';
import api from '../../api/axios';
import toast from 'react-hot-toast';

export default function Dashboard() {
  const [stats, setStats] = useState({
    headquarters: 0,
    categories: 0,
    roles: 0,
    staff: 0
  });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchDashboardData = async () => {
      try {
        setLoading(true);
        const [hqRes, catRes, roleRes, userRes] = await Promise.all([
          api.get('/academy-config/headquarters'),
          api.get('/academy-config/categories'),
          api.get('/academy-config/roles'),
          api.get('/academy-config/users')
        ]);

        const staffCount = userRes.data.filter(
          u => u.systemRole === 'Staff' || u.systemRole === 'AcademyAdmin'
        ).length;

        setStats({
          headquarters: hqRes.data.filter(h => h.isActive).length || hqRes.data.length,
          categories: catRes.data.length,
          roles: roleRes.data.length,
          staff: staffCount
        });
      } catch (error) {
        toast.error('Error al cargar datos del dashboard');
      } finally {
        setLoading(false);
      }
    };

    fetchDashboardData();
  }, []);

  return (
    <AppLayout title="Dashboard Academia">
      <div className="fade-in">
        <h1 className="text-2xl font-bold mb-2">Bienvenido a tu Academia</h1>
        <p className="text-text-secondary mb-8">Desde aquí puedes configurar la estructura inicial de tu organización.</p>

        {loading ? (
          <div className="flex justify-center p-8">
            <span className="spinner" style={{borderColor: 'var(--primary)', borderTopColor: 'transparent'}}></span>
          </div>
        ) : (
          <div className="stats-grid">
            <div className="stat-card">
              <div className="stat-label">Gestión</div>
              <div className="stat-value text-primary-400 text-3xl font-bold">{stats.headquarters}</div>
              <div className="text-sm text-text-muted mt-1">Sedes Activas</div>
              <div className="stat-icon text-primary-400"><Building2 size={32} /></div>
            </div>

            <div className="stat-card">
              <div className="stat-label">Grupos</div>
              <div className="stat-value text-accent text-3xl font-bold">{stats.categories}</div>
              <div className="text-sm text-text-muted mt-1">Categorías</div>
              <div className="stat-icon text-accent"><ClipboardList size={32} /></div>
            </div>

            <div className="stat-card">
              <div className="stat-label">Cargos</div>
              <div className="stat-value text-success text-3xl font-bold">{stats.roles}</div>
              <div className="text-sm text-text-muted mt-1">Roles Internos</div>
              <div className="stat-icon text-success"><ShieldCheck size={32} /></div>
            </div>

            <div className="stat-card">
              <div className="stat-label">Personal</div>
              <div className="stat-value text-warning text-3xl font-bold">{stats.staff}</div>
              <div className="text-sm text-text-muted mt-1">Staff Registrado</div>
              <div className="stat-icon text-warning"><Users size={32} /></div>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
