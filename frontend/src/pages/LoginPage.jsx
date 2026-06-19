import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import api from '../api/axios';
import toast from 'react-hot-toast';
import { Mail, Lock, Eye, EyeOff, Search, ChevronDown, Check, ShieldAlert } from 'lucide-react';
import './LoginPage.css';

/* ────────────────────────────────────────────
   CLUBiO Premium Split-Screen Login Page
   ──────────────────────────────────────────── */

const SUPERADMIN_HINT_EMAIL = '@adhsoft';

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();

  const [emailOrPhone, setEmailOrPhone] = useState('');
  const [password, setPassword] = useState('');
  const [showPass, setShowPass] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  // Derived state
  const isSuperAdmin = emailOrPhone.toLowerCase().includes(SUPERADMIN_HINT_EMAIL);

  // Academy picker state
  const [academies, setAcademies] = useState([]);
  const [academySearch, setAcademySearch] = useState('');
  const [selectedAcademy, setSelectedAcademy] = useState(null);
  const [showDropdown, setShowDropdown] = useState(false);
  const [loadingAcademies, setLoadingAcademies] = useState(false);
  const dropdownRef = useRef(null);
  const searchTimeout = useRef(null);

  // Close dropdown on outside click
  useEffect(() => {
    const handler = e => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target))
        setShowDropdown(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  const fetchAcademies = useCallback(async (q = '') => {
    try {
      setLoadingAcademies(true);
      const res = await api.get(`/auth/academies${q ? `?search=${encodeURIComponent(q)}` : ''}`);
      setAcademies(res.data || []);
    } catch {
      setAcademies([]);
    } finally {
      setLoadingAcademies(false);
    }
  }, []);

  // Fetch initial academies
  useEffect(() => {
    fetchAcademies('');
  }, [fetchAcademies]);

  const handleAcademySearch = v => {
    setAcademySearch(v);
    setSelectedAcademy(null);
    clearTimeout(searchTimeout.current);
    searchTimeout.current = setTimeout(() => fetchAcademies(v), 300);
  };

  const handleSubmit = async e => {
    e.preventDefault();
    setError('');

    if (!emailOrPhone.trim() || !password.trim()) {
      setError('Por favor completa todos los campos.');
      return;
    }

    if (!isSuperAdmin && !selectedAcademy) {
      setError('Por favor selecciona una academia.');
      return;
    }

    setLoading(true);
    try {
      if (selectedAcademy) {
        localStorage.setItem('tenantId', selectedAcademy.tenantId);
        api.defaults.headers.common['X-Tenant-Id'] = selectedAcademy.tenantId;
      } else {
        localStorage.removeItem('tenantId');
        delete api.defaults.headers.common['X-Tenant-Id'];
      }

      const user = await login(emailOrPhone, password);
      if (user.requirePasswordChange) {
        sessionStorage.setItem('_lp', password);
        navigate('/cambiar-password');
      } else {
        toast.success(`¡Bienvenido, ${user.fullName}!`);
        if (user.role === 'SuperAdmin') {
          navigate('/super-admin/dashboard');
        } else {
          // Normal users get redirected to the Academy Landing Page
          navigate(`/a/${selectedAcademy.tenantId}`);
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
              /* ── ACADEMY SELECTOR ── */
              <div className="form-group" ref={dropdownRef}>
                <label>Selecciona tu Academia</label>
                <div 
                  className={`input-with-icon academy-select ${showDropdown ? 'focused' : ''}`}
                  onClick={() => { setShowDropdown(true); if (academies.length === 0) fetchAcademies(); }}
                >
                  <Search size={18} className="input-icon" />
                  <input
                    type="text"
                    placeholder="Busca el nombre de tu academia..."
                    value={selectedAcademy ? selectedAcademy.name : academySearch}
                    onChange={e => handleAcademySearch(e.target.value)}
                    onFocus={() => { setShowDropdown(true); if (academies.length === 0) fetchAcademies(); }}
                  />
                  {selectedAcademy ? (
                    <Check size={18} className="icon-success" />
                  ) : (
                    <ChevronDown size={18} className="input-icon-right" />
                  )}
                </div>

                {/* Dropdown */}
                {showDropdown && (
                  <div className="academy-dropdown">
                    {loadingAcademies && (
                      <div className="dropdown-message">Buscando academias...</div>
                    )}
                    {!loadingAcademies && academies.length === 0 && (
                      <div className="dropdown-message">No se encontraron resultados</div>
                    )}
                    {academies.map(ac => (
                      <div 
                        key={ac.id}
                        className={`dropdown-item ${selectedAcademy?.id === ac.id ? 'active' : ''}`}
                        onMouseDown={() => { setSelectedAcademy(ac); setAcademySearch(ac.name); setShowDropdown(false); }}
                      >
                        <div className="dropdown-item-content">
                          <strong>{ac.name}</strong>
                          {(ac.city || ac.sport) && (
                            <span>{[ac.city, ac.sport].filter(Boolean).join(' · ')}</span>
                          )}
                        </div>
                        {selectedAcademy?.id === ac.id && <Check size={16} className="icon-success" />}
                      </div>
                    ))}
                  </div>
                )}
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
