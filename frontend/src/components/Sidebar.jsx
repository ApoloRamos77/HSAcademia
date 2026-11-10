import { NavLink, useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { FINANCE_ACCESS, STORE_ACCESS, REPORTS_ACCESS } from '../utils/permissions';
import {
  LayoutDashboard, Building2, Users, ClipboardList, LogOut,
  ShieldCheck, ShoppingCart, DollarSign, CalendarCheck, CalendarDays, Crown, X, Megaphone, FileSpreadsheet
} from 'lucide-react';

export default function Sidebar({ isOpen, onClose }) {
  const { user, logout, can } = useAuth();
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
        <img src="/logo.png" alt="Clubio" />
        <div className="sidebar-logo-text">
          <h1>Clubio</h1>
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

        {/* AcademyAdmin */}
        {user?.role === 'AcademyAdmin' && (
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
              <NavLink to="/academy/asistencia-entrenadores" className={navCls} onClick={onClose}>
                <CalendarCheck size={16} /> Asistencia entrenadores
              </NavLink>
              <NavLink to="/academy/directorio" className={navCls} onClick={onClose}>
                <ClipboardList size={16} /> Directorio
              </NavLink>
              <NavLink to="/academy/comunicados" className={navCls} onClick={onClose}>
                <Megaphone size={16} /> Comunicados
              </NavLink>
              <NavLink to="/academy/reportes" className={navCls} onClick={onClose}>
                <FileSpreadsheet size={16} /> Reportes
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
              <div className="nav-label">🔐 Seguridad</div>
              <NavLink to="/academy/accesos" className={navCls} onClick={onClose}>
                <ShieldCheck size={16} /> Accesos y seguridad
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
              <NavLink to="/academy/torneos" className={navCls} onClick={onClose}>
                <Crown size={16} /> Torneos
              </NavLink>
              <NavLink to="/academy/calendario" className={navCls} onClick={onClose}>
                <CalendarDays size={16} /> Calendario
              </NavLink>
            </div>
          </>
        )}

        {/* Staff / Personal */}
        {user?.role === 'Staff' && (
          <>
            <div className="nav-section">
              <div className="nav-label">🏠 Principal</div>
              <NavLink to="/dashboard" className={navCls} onClick={onClose} end>
                <LayoutDashboard size={16} /> Dashboard
              </NavLink>
            </div>

            <div className="nav-section">
              <div className="nav-label">👥 Gestión y Portal</div>
              <NavLink to="/academy/comunicados" className={navCls} onClick={onClose}>
                <Megaphone size={16} /> Comunicados
              </NavLink>
              {can(...REPORTS_ACCESS) && (
                <NavLink to="/academy/reportes" className={navCls} onClick={onClose}>
                  <FileSpreadsheet size={16} /> Reportes
                </NavLink>
              )}
              {can('students.view') && (
                <>
                  <NavLink to="/academy/alumnos" className={navCls} onClick={onClose}>
                    <Users size={16} /> Alumnos
                  </NavLink>
                  <NavLink to="/academy/apoderados" className={navCls} onClick={onClose}>
                    <Users size={16} /> Apoderados
                  </NavLink>
                </>
              )}
              {can('attendance.take') && (
                <NavLink to="/academy/asistencia" className={navCls} onClick={onClose}>
                  <CalendarCheck size={16} /> Asistencia
                </NavLink>
              )}
              {can('staff_attendance.mark') && (
                <NavLink to="/academy/asistencia-entrenadores" className={navCls} onClick={onClose}>
                  <CalendarCheck size={16} /> Asistencia entrenadores
                </NavLink>
              )}
              {can('events.manage') && (
                <NavLink to="/academy/calendario" className={navCls} onClick={onClose}>
                  <CalendarDays size={16} /> Calendario
                </NavLink>
              )}
              {can(...STORE_ACCESS) && (
                <NavLink to="/academy/tienda" className={navCls} onClick={onClose}>
                  <ShoppingCart size={16} /> Tienda
                </NavLink>
              )}
              {can(...FINANCE_ACCESS) && (
                <NavLink to="/academy/finanzas" className={navCls} onClick={onClose}>
                  <DollarSign size={16} /> Finanzas
                </NavLink>
              )}
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
          <NavLink to="/mi-perfil" onClick={onClose} title="Mi perfil" style={{ display: 'flex', alignItems: 'center', gap: 10, flex: 1, minWidth: 0, color: 'inherit', textDecoration: 'none' }}>
          <div className="avatar">{initials}</div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div className="user-name" style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {user?.fullName}
            </div>
            {isSuperAdmin ? (
              <span className="superadmin-pill"><Crown size={10} /> SuperAdmin</span>
            ) : (
              <div className="user-role">
                {user?.isOwner ? 'Propietario' : user?.role === 'AcademyAdmin' ? 'Administrador' : 
                 user?.role === 'Staff' ? 'Personal' : 
                 user?.role === 'Student' ? 'Alumno' : 
                 user?.role === 'Guardian' ? 'Apoderado' : 'Usuario'}
              </div>
            )}
          </div>
          </NavLink>
          <button onClick={handleLogout} className="btn btn-ghost btn-sm" title="Cerrar sesión" style={{ padding: '6px' }}>
            <LogOut size={15} />
          </button>
        </div>
      </div>
    </aside>
  );
}
