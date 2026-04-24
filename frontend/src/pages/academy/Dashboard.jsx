import { Building2, Users, ClipboardList, ShieldCheck } from 'lucide-react';
import AppLayout from '../../components/AppLayout';

export default function Dashboard() {
  return (
    <AppLayout title="Dashboard Academia">
      <div className="fade-in">
        <h1 className="text-2xl font-bold mb-2">Bienvenido a tu Academia</h1>
        <p className="text-text-secondary mb-8">Desde aquí puedes configurar la estructura inicial de tu organización.</p>

        <div className="stats-grid">
          <div className="stat-card">
            <div className="stat-label">Gestión</div>
            <div className="stat-value text-primary-400 text-lg">Sedes Activas</div>
            <div className="stat-icon text-primary-400"><Building2 size={32} /></div>
          </div>

          <div className="stat-card">
            <div className="stat-label">Grupos</div>
            <div className="stat-value text-accent text-lg">Categorías</div>
            <div className="stat-icon text-accent"><ClipboardList size={32} /></div>
          </div>

          <div className="stat-card">
            <div className="stat-label">Cargos</div>
            <div className="stat-value text-success text-lg">Roles Internos</div>
            <div className="stat-icon text-success"><ShieldCheck size={32} /></div>
          </div>

          <div className="stat-card">
            <div className="stat-label">Personal</div>
            <div className="stat-value text-warning text-lg">Staff</div>
            <div className="stat-icon text-warning"><Users size={32} /></div>
          </div>
        </div>
      </div>
    </AppLayout>
  );
}
