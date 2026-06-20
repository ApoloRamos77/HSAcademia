import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import api from '../api/axios';
import toast from 'react-hot-toast';
import { Mail, Lock, Eye, EyeOff, ShieldAlert, Users, GraduationCap, User } from 'lucide-react';
import './LoginPage.css';

/* ────────────────────────────────────────────
   CLUBiO Premium Split-Screen Login Page
   ──────────────────────────────────────────── */

const SUPERADMIN_HINT_EMAIL = '@adhsoft';

export default function LoginPage() {
  const { login, logout } = useAuth();
  const navigate = useNavigate();

  const [emailOrPhone, setEmailOrPhone] = useState('');
  const [password, setPassword] = useState('');
  const [showPass, setShowPass] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  // Derived state
  const isSuperAdmin = emailOrPhone.toLowerCase().includes(SUPERADMIN_HINT_EMAIL);

  // Role selector state
  const [selectedRole, setSelectedRole] = useState(null);

  const toggleRole = (role) => {
    setSelectedRole(prev => prev === role ? null : role);
  };

  const handleSubmit = async e => {
    e.preventDefault();
    setError('');

    if (!emailOrPhone.trim() || !password.trim()) {
      setError('Por favor completa todos los campos.');
      return;
    }

    setLoading(true);
    try {
      // Limpiamos tenantId previo por si acaso
      localStorage.removeItem('tenantId');
      delete api.defaults.headers.common['X-Tenant-Id'];

      const user = await login(emailOrPhone, password, selectedRole);

      // Validación: Si no seleccionó rol, y NO es SuperAdmin ni AcademyAdmin,
      // obligar a que seleccione su rol.
      if (!selectedRole && user.role !== 'SuperAdmin' && user.role !== 'AcademyAdmin') {
        logout(); // Revertimos el inicio de sesión
        setError('Por favor selecciona tu rol para iniciar sesión.');
        setLoading(false);
        return;
      }

      // Si el backend retorna un tenantId luego del login, lo asignamos
      if (user && user.tenantId) {
        localStorage.setItem('tenantId', user.tenantId);
        api.defaults.headers.common['X-Tenant-Id'] = user.tenantId;
      }
      
      if (user.requirePasswordChange) {
        sessionStorage.setItem('_lp', password);
        navigate('/cambiar-password');
      } else {
        toast.success(`¡Bienvenido, ${user.fullName}!`);
        if (user.role === 'SuperAdmin') {
          navigate('/super-admin/dashboard');
        } else {
          // Normal users get redirected to the Academy Landing Page or their Dashboard
          navigate(user.tenantId ? `/a/${user.tenantId}` : '/dashboard');
        }
      }
    } catch (err) {
      setError(err.response?.data?.message || 'Error al iniciar sesión. Verifica tus credenciales.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-container">
      {/* Lado Izquierdo - Imagen motivacional */}
      <div className="login-visual">
        <div className="login-visual-overlay"></div>
        <div className="login-visual-content">
          <div className="login-logo-brand">
             <span className="brand-text">CLUB<span>iO</span></span>
          </div>
          <h2>La herramienta definitiva para potenciar tu Academia</h2>
          <p>Gestiona alumnos, entrenamientos, finanzas y comunícate de manera efectiva. Todo en una sola plataforma profesional.</p>
        </div>
      </div>

      {/* Lado Derecho - Formulario */}
      <div className="login-form-side">
        <div className="login-form-wrapper">
          <div className="login-form-header">
            <h3>Bienvenido de nuevo</h3>
            <p>Ingresa tus credenciales para acceder al portal</p>
          </div>

          {error && (
            <div className="login-error-alert">
              <ShieldAlert size={20} />
              <span>{error}</span>
            </div>
          )}

          <form onSubmit={handleSubmit} className="login-form">
            <div className="form-group">
              <label>Correo Electrónico o Celular</label>
              <div className="input-with-icon">
                <Mail size={18} className="input-icon" />
                <input
                  type="text"
                  placeholder="ejemplo@correo.com o 987654321"
                  value={emailOrPhone}
                  onChange={e => setEmailOrPhone(e.target.value)}
                  required
                />
              </div>
            </div>

            <div className="form-group">
              <label>Contraseña</label>
              <div className="input-with-icon">
                <Lock size={18} className="input-icon" />
                <input
                  type={showPass ? 'text' : 'password'}
                  placeholder="••••••••"
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                  required
                />
                <button type="button" className="btn-icon" onClick={() => setShowPass(!showPass)}>
                  {showPass ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
            </div>

            {/* ── SUPERADMIN BANNER ── */}
            {isSuperAdmin ? (
              <div className="superadmin-banner">
                <div className="sa-icon">👑</div>
                <div className="sa-text">
                  <h4>Acceso SuperAdmin</h4>
                  <p>Panel de control global del sistema</p>
                </div>
              </div>
            ) : (
              /* ── ROLE SELECTOR ── */
              <div className="form-group">
                <label>Selecciona tu Rol (Opcional)</label>
                <div className="role-selector-container">
                  <button 
                    type="button" 
                    className={`role-btn ${selectedRole === 'STAFF' ? 'active' : ''}`}
                    onClick={() => toggleRole('STAFF')}
                  >
                    <Users size={20} />
                    <span>Staff</span>
                  </button>
                  <button 
                    type="button" 
                    className={`role-btn ${selectedRole === 'ALUMNO' ? 'active' : ''}`}
                    onClick={() => toggleRole('ALUMNO')}
                  >
                    <GraduationCap size={20} />
                    <span>Alumno</span>
                  </button>
                  <button 
                    type="button" 
                    className={`role-btn ${selectedRole === 'APODERADO' ? 'active' : ''}`}
                    onClick={() => toggleRole('APODERADO')}
                  >
                    <User size={20} />
                    <span>Apoderado</span>
                  </button>
                </div>
              </div>
            )}

            <button type="submit" className={`btn-login-submit ${loading ? 'loading' : ''}`} disabled={loading}>
              {loading ? <span className="spinner"></span> : (isSuperAdmin ? 'Ingresar al Panel Global' : 'Iniciar Sesión')}
            </button>
          </form>

          <div className="login-footer">
            ¿Tu academia no está registrada? <a href="/registro">Solicitar afiliación</a>
          </div>
        </div>
      </div>
    </div>
  );
}
