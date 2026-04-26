import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import {
  LayoutDashboard, Building2, Users, ClipboardList, LogOut,
  ShieldCheck, ShoppingCart, DollarSign, CalendarCheck, CalendarDays, Crown
} from 'lucide-react';

export default function Sidebar() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => { logout(); navigate('/login'); };
  const initials = user?.fullName?.split(' ').map(n => n[0]).join('').slice(0, 2).toUpperCase();
  const isSuperAdmin = user?.role === 'SuperAdmin';

  return (
    <aside className="sidebar">
      {/* ── LOGO ── */}
      <div className="sidebar-logo">
        <img src="/logo.png" alt="ADHSOFT SPORT" />
        <div className="sidebar-logo-text">
          <h1>ADHSOFT SPORT</h1>
          <span>Gestión Deportiva</span>
        </div>
      </div>

      {/* ── NAV ── */}
      <nav className="sidebar-nav">

        {/* SuperAdmin */}
        {isSuperAdmin && (
          <div className="nav-section">
            <div className="nav-label">⚙️ Control Global</div>
            <NavLink to="/super-admin/dashboard" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <LayoutDashboard size={16} /> Dashboard
            </NavLink>
            <NavLink to="/super-admin/solicitudes" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <ClipboardList size={16} /> Solicitudes
            </NavLink>
            <NavLink to="/super-admin/academias" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <Building2 size={16} /> Academias
            </NavLink>
            <NavLink to="/super-admin/usuarios" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <Users size={16} /> Usuarios
            </NavLink>
          </div>
        )}

        {/* AcademyAdmin / Staff */}
        {(user?.role === 'AcademyAdmin' || user?.role === 'Staff') && (
          <>
            <div className="nav-section">
              <div className="nav-label">🏠 Principal</div>
              <NavLink to="/dashboard" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`} end>
                <LayoutDashboard size={16} /> Dashboard
              </NavLink>
            </div>

            <div className="nav-section">
              <div className="nav-label">👥 Gestión</div>
              <NavLink to="/academy/alumnos" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
                <Users size={16} /> Alumnos
              </NavLink>
              <NavLink to="/academy/asistencia" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
                <CalendarCheck size={16} /> Asistencia
              </NavLink>
              <NavLink to="/academy/usuarios" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
                <ShieldCheck size={16} /> Personal
              </NavLink>
            </div>

            <div className="nav-section">
              <div className="nav-label">💰 Finanzas</div>
              <NavLink to="/academy/finanzas" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
                <DollarSign size={16} /> Cobranzas
              </NavLink>
              <NavLink to="/academy/tienda" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
                <ShoppingCart size={16} /> Tienda
              </NavLink>
            </div>

            <div className="nav-section">
              <div className="nav-label">📋 Configuración</div>
              <NavLink to="/academy/sedes" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
                <Building2 size={16} /> Sedes
              </NavLink>
              <NavLink to="/academy/categorias" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
                <ClipboardList size={16} /> Categorías
              </NavLink>
              <NavLink to="/academy/roles" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
                <ShieldCheck size={16} /> Roles
              </NavLink>
              <NavLink to="/academy/calendario" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
                <CalendarDays size={16} /> Calendario
              </NavLink>
            </div>
          </>
        )}
      </nav>

      {/* ── FOOTER ── */}
      <div className="sidebar-footer">
        <div className="user-info">
          <div className="avatar">{initials}</div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="user-name" style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {user?.fullName}
            </div>
            {isSuperAdmin ? (
              <span className="superadmin-pill"><Crown size={10} /> SuperAdmin</span>
            ) : (
              <div className="user-role">Admin Academia</div>
            )}
          </div>
          <button onClick={handleLogout} className="btn btn-ghost btn-sm" title="Cerrar sesión" style={{ padding: '6px' }}>
            <LogOut size={15} />
          </button>
        </div>
      </div>
    </aside>
  );
}
