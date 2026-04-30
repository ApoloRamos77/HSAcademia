import React, { useState, useEffect } from 'react';
import { Building2, Users, ClipboardList, ShieldCheck } from 'lucide-react';
import AppLayout from '../../components/AppLayout';
import api from '../../api/axios';
import toast from 'react-hot-toast';
import { useAuth } from '../../context/AuthContext';

export default function Dashboard() {
  const { user } = useAuth();
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
        // Traer headquarters y categories (disponible para Admin y Staff)
        const [hqRes, catRes] = await Promise.all([
          api.get('/academy-config/headquarters'),
          api.get('/academy-config/categories')
        ]);

        let rolesCount = 0;
        let staffCount = 0;

        // Si es AcademyAdmin, intentar traer roles y usuarios (si hay endpoint protegido)
        if (user?.role === 'AcademyAdmin') {
          try {
            const roleRes = await api.get('/academy-config/roles');
            rolesCount = roleRes.data.length;
          } catch(e) { }

          try {
            const userRes = await api.get('/academy-config/users');
            staffCount = userRes.data.filter(
              u => u.systemRole === 'Staff' || u.systemRole === 'AcademyAdmin'
            ).length;
          } catch(e) { }
        }

        setStats({
          headquarters: hqRes.data.filter(h => h.isActive).length || hqRes.data.length,
          categories: catRes.data.length,
          roles: rolesCount,
          staff: staffCount
        });
      } catch (error) {
        toast.error('Error al cargar datos del dashboard');
      } finally {
        setLoading(false);
      }
    };

    fetchDashboardData();
  }, [user]);

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

            {user?.role === 'AcademyAdmin' && (
              <>
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
              </>
            )}
          </div>
        )}
      </div>
    </AppLayout>
  );
}
