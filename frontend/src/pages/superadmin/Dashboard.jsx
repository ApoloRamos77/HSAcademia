import { useEffect, useState } from 'react';
import AppLayout from '../../components/AppLayout';
import api from '../../api/axios';
import { Building2, Users, ClipboardList, CheckCircle, TrendingUp, AlertTriangle } from 'lucide-react';

export default function SuperAdminDashboard() {
  const [stats, setStats] = useState(null);

  useEffect(() => {
    api.get('/super-admin/dashboard').then(r => setStats(r.data)).catch(() => {});
  }, []);

  const cards = stats ? [
    { label: 'Academias Activas', value: stats.activeAcademies, icon: <CheckCircle size={32} />, color: '#10b981' },
    { label: 'Total Academias', value: stats.totalAcademies, icon: <Building2 size={32} />, color: '#6366f1' },
    { label: 'Solicitudes Pendientes', value: stats.pendingRequests, icon: <ClipboardList size={32} />, color: '#f59e0b' },
    { label: 'Academias Suspendidas', value: stats.suspendedAcademies, icon: <AlertTriangle size={32} />, color: '#ef4444' },
    { label: 'Usuarios Activos', value: stats.activeUsers, icon: <Users size={32} />, color: '#0ea5e9' },
    { label: 'Total Usuarios', value: stats.totalUsers, icon: <TrendingUp size={32} />, color: '#8b5cf6' },
  ] : [];

  return (
    <AppLayout title="Dashboard Super Admin">
      <div className="stats-grid">
        {cards.map(c => (
          <div className="stat-card" key={c.label}>
            <div className="stat-value" style={{ color: c.color }}>{c.value ?? '—'}</div>
            <div className="stat-label">{c.label}</div>
            <div className="stat-icon" style={{ color: c.color }}>{c.icon}</div>
          </div>
        ))}
      </div>

      <div className="card">
        <div className="card-header">
          <h3 className="card-title">Bienvenido al Panel de Control</h3>
        </div>
        <p className="text-muted">
          Desde aquí puede gestionar todas las academias de la plataforma, revisar solicitudes de registro,
          controlar el acceso de usuarios y monitorear el estado del sistema.
        </p>
        <div style={{ display: 'flex', gap: 12, marginTop: 20 }}>
          <a href="/super-admin/solicitudes" className="btn btn-primary">
            <ClipboardList size={15} /> Ver Solicitudes Pendientes
          </a>
          <a href="/super-admin/academias" className="btn btn-ghost">
            <Building2 size={15} /> Gestionar Academias
          </a>
        </div>
      </div>
    </AppLayout>
  );
}
