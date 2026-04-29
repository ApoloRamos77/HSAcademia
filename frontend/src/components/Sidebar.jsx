import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import {
  LayoutDashboard, Building2, Users, ClipboardList, LogOut,
  ShieldCheck, ShoppingCart, DollarSign, CalendarCheck, CalendarDays, Crown, X
} from 'lucide-react';

export default function Sidebar({ isOpen, onClose }) {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => { logout(); navigate('/login'); };
  const initials = user?.fullName?.split(' ').map(n => n[0]).join('').slice(0, 2).toUpperCase();
  const isSuperAdmin = user?.role === 'SuperAdmin';

  // Helper: crea className para NavLink y cierra el drawer en móvil
  const navCls = ({ isActive }) => `nav-item ${isActive ? 'active' : ''}`;

  return (
    <aside className={`sidebar${isOpen ? ' sidebar-open' : ''}`}>

      {/* ── Botón cerrar (solo visible en móvil) ── */}
      <button
        className="sidebar-close-btn"
        onClick={onClose}
        aria-label="Cerrar menú"
      >
        <X size={18} />
      </button>

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
            <NavLink to="/super-admin/dashboard" className={navCls} onClick={onClose}>
              <LayoutDashboard size={16} /> Dashboard
            </NavLink>
            <NavLink to="/super-admin/solicitudes" className={navCls} onClick={onClose}>
              <ClipboardList size={16} /> Solicitudes
            </NavLink>
            <NavLink to="/super-admin/academias" className={navCls} onClick={onClose}>
              <Building2 size={16} /> Academias
            </NavLink>
            <NavLink to="/super-admin/usuarios" className={navCls} onClick={onClose}>
              <Users size={16} /> Usuarios
            </NavLink>
          </div>
        )}

        {/* AcademyAdmin / Staff */}
        {(user?.role === 'AcademyAdmin' || user?.role === 'Staff') && (
          <>
            <div className="nav-section">
              <div className="nav-label">🏠 Principal</div>
              <NavLink to="/dashboard" className={navCls} onClick={onClose} end>
                <LayoutDashboard size={16} /> Dashboard
              </NavLink>
            </div>

            <div className="nav-section">
              <div className="nav-label">👥 Gestión</div>
              <NavLink to="/academy/alumnos" className={navCls} onClick={onClose}>
                <Users size={16} /> Alumnos
              </NavLink>
              <NavLink to="/academy/apoderados" className={navCls} onClick={onClose}>
                <Users size={16} /> Apoderados
              </NavLink>
              <NavLink to="/academy/asistencia" className={navCls} onClick={onClose}>
                <CalendarCheck size={16} /> Asistencia
              </NavLink>
              <NavLink to="/academy/usuarios" className={navCls} onClick={onClose}>
                <ShieldCheck size={16} /> Personal
              </NavLink>
              <NavLink to="/academy/directorio" className={navCls} onClick={onClose}>
                <ClipboardList size={16} /> Directorio
              </NavLink>
            </div>

            <div className="nav-section">
              <div className="nav-label">💰 Finanzas</div>
              <NavLink to="/academy/finanzas" className={navCls} onClick={onClose}>
                <DollarSign size={16} /> Cobranzas
              </NavLink>
              <NavLink to="/academy/tienda" className={navCls} onClick={onClose}>
                <ShoppingCart size={16} /> Tienda
              </NavLink>
            </div>

            <div className="nav-section">
              <div className="nav-label">📋 Configuración</div>
              <NavLink to="/academy/sedes" className={navCls} onClick={onClose}>
                <Building2 size={16} /> Sedes
              </NavLink>
              <NavLink to="/academy/categorias" className={navCls} onClick={onClose}>
                <ClipboardList size={16} /> Categorías
              </NavLink>
              <NavLink to="/academy/roles" className={navCls} onClick={onClose}>
                <ShieldCheck size={16} /> Roles
              </NavLink>
              <NavLink to="/academy/calendario" className={navCls} onClick={onClose}>
                <CalendarDays size={16} /> Calendario
              </NavLink>
            </div>
          </>
        )}

        {/* Student / Guardian */}
        {(user?.role === 'Student' || user?.role === 'Guardian') && (
          <div className="nav-section">
            <div className="nav-label">🎓 Portal</div>
            <NavLink to="/student/dashboard" className={navCls} onClick={onClose} end>
              <LayoutDashboard size={16} /> Inicio
            </NavLink>
          </div>
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
              <div className="user-role">
                {user?.role === 'AcademyAdmin' ? 'Admin Academia' : 
                 user?.role === 'Staff' ? 'Personal' : 
                 user?.role === 'Student' ? 'Alumno' : 
                 user?.role === 'Guardian' ? 'Apoderado' : 'Usuario'}
              </div>
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
