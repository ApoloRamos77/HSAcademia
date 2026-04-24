import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { LayoutDashboard, Building2, Users, ClipboardList, LogOut, ShieldCheck, ShoppingCart, DollarSign, CalendarCheck, CalendarDays } from 'lucide-react';

export default function Sidebar() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => { logout(); navigate('/login'); };

  const initials = user?.fullName?.split(' ').map(n => n[0]).join('').slice(0, 2).toUpperCase();

  return (
    <aside className="sidebar">
      <div className="sidebar-logo">
        <h1>ADHSOFT SPORT</h1>
        <span>Plataforma Multi-Academia</span>
      </div>

      <nav className="sidebar-nav">
        {user?.role === 'SuperAdmin' && (
          <div className="nav-section">
            <div className="nav-label">Super Admin</div>
            <NavLink to="/super-admin/dashboard" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <LayoutDashboard size={17} /> Dashboard
            </NavLink>
            <NavLink to="/super-admin/solicitudes" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <ClipboardList size={17} /> Solicitudes
            </NavLink>
            <NavLink to="/super-admin/academias" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <Building2 size={17} /> Academias
            </NavLink>
            <NavLink to="/super-admin/usuarios" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <Users size={17} /> Usuarios
            </NavLink>
          </div>
        )}

        {user?.role === 'AcademyAdmin' && (
          <div className="nav-section">
            <div className="nav-label">Academia</div>
            <NavLink to="/dashboard" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`} end>
              <LayoutDashboard size={17} /> Dashboard
            </NavLink>
            <NavLink to="/academy/sedes" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <Building2 size={17} /> Sedes
            </NavLink>
            <NavLink to="/academy/categorias" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <ClipboardList size={17} /> Categorías
            </NavLink>
            <NavLink to="/academy/roles" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <ShieldCheck size={17} /> Roles
            </NavLink>
            <NavLink to="/academy/usuarios" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <Users size={17} /> Usuarios
            </NavLink>
            <NavLink to="/academy/alumnos" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <Users size={17} /> Alumnos
            </NavLink>
            <NavLink to="/academy/asistencia" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <CalendarCheck size={17} /> Asistencia
            </NavLink>
            <NavLink to="/academy/calendario" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <CalendarDays size={17} /> Calendario
            </NavLink>
            <NavLink to="/academy/tienda" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <ShoppingCart size={17} /> Tienda / Inventario
            </NavLink>
            <NavLink to="/academy/finanzas" className={({ isActive }) => `nav-item ${isActive ? 'active' : ''}`}>
              <DollarSign size={17} /> Finanzas y Cobranzas
            </NavLink>
          </div>
        )}
      </nav>

      <div className="sidebar-footer">
        <div className="user-info">
          <div className="avatar">{initials}</div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="user-name" style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{user?.fullName}</div>
            <div className="user-role">{user?.role === 'SuperAdmin' ? 'Super Admin' : 'Admin Academia'}</div>
          </div>
          <button onClick={handleLogout} className="btn btn-ghost btn-sm" title="Cerrar sesión" style={{ padding: '6px' }}>
            <LogOut size={15} />
          </button>
        </div>
      </div>
    </aside>
  );
}
