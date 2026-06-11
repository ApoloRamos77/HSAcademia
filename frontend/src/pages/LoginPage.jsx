import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import api from '../api/axios';
import toast from 'react-hot-toast';

/* ────────────────────────────────────────────
   Premium Login Page — ADHSOFT SPORT
   • Single View Logic
   • SuperAdmin → no academy required
   • Normal user → academy selector
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
      // Inject tenantId into axios headers if academy selected
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
        } else if (user.role === 'Student' || user.role === 'Guardian') {
          navigate('/student/dashboard');
        } else {
          navigate('/dashboard');
        }
      }
    } catch (err) {
      setError(err.response?.data?.message || 'Error al iniciar sesión. Verifica tus credenciales.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={styles.page}>
      {/* Background decorations */}
      <div style={styles.bgCircle1} />
      <div style={styles.bgCircle2} />

      <div style={styles.card}>
        {/* Logo / Brand */}
        <div style={styles.brand}>
          <img src="/logo.png" alt="ADHSOFT SPORT" style={styles.logoImg} />
          <h1 style={styles.brandName}>ADHSOFT SPORT</h1>
          <p style={styles.brandSub}>Plataforma de Gestión Deportiva</p>
        </div>

        {error && (
          <div style={styles.errorBox}>
            <span style={{ marginRight: 8 }}>⚠️</span>{error}
          </div>
        )}

        <form onSubmit={handleSubmit} style={styles.form}>
          
          {/* Email / Phone */}
          <div style={styles.inputGroup}>
            <label style={styles.label}>Correo Electrónico o Celular</label>
            <div style={styles.inputWrapper}>
              <span style={styles.inputIcon}>✉️</span>
              <input
                style={styles.input}
                type="text"
                placeholder="correo@ejemplo.com o 987654321"
                value={emailOrPhone}
                onChange={e => setEmailOrPhone(e.target.value)}
                required
              />
            </div>
          </div>

          {/* Password */}
          <div style={styles.inputGroup}>
            <label style={styles.label}>
              <span style={{ marginRight: 6 }}>🔒</span>Contraseña
            </label>
            <div style={styles.inputWrapper}>
              <span style={styles.inputIcon}>🔑</span>
              <input
                style={styles.input}
                type={showPass ? 'text' : 'password'}
                placeholder="••••••••"
                value={password}
                onChange={e => setPassword(e.target.value)}
                required
              />
              <button type="button" style={styles.eyeBtn} onClick={() => setShowPass(!showPass)}>
                {showPass ? '🙈' : '👁️'}
              </button>
            </div>
          </div>

          {/* ── SUPERADMIN BANNER ── */}
          {isSuperAdmin ? (
            <div style={styles.superAdminBanner}>
              <div style={styles.superAdminIcon}>👑</div>
              <div>
                <p style={styles.superAdminTitle}>Acceso SuperAdmin</p>
                <p style={styles.superAdminSub}>Panel de control global del sistema</p>
              </div>
              <div style={styles.superAdminBadge}>GLOBAL</div>
            </div>
          ) : (
            /* ── ACADEMY SELECTOR ── */
            <div style={styles.inputGroup} ref={dropdownRef}>
              <label style={styles.label}>
                <span style={{ marginRight: 6 }}>🏟️</span>Academia
              </label>
              <div style={{ ...styles.inputWrapper, ...(showDropdown ? styles.inputWrapperFocused : {}) }}
                onClick={() => { setShowDropdown(true); if (academies.length === 0) fetchAcademies(); }}>
                <span style={styles.inputIcon}>🔍</span>
                <input
                  style={styles.input}
                  type="text"
                  placeholder="Busca tu academia..."
                  value={selectedAcademy ? selectedAcademy.name : academySearch}
                  onChange={e => handleAcademySearch(e.target.value)}
                  onFocus={() => { setShowDropdown(true); if (academies.length === 0) fetchAcademies(); }}
                />
                {selectedAcademy
                  ? <span style={{ color: '#6AB657', fontWeight: 700, fontSize: 18 }}>✓</span>
                  : <span style={{ color: '#94A3B8', fontSize: 12 }}>{showDropdown ? '▲' : '▼'}</span>
                }
              </div>

              {/* Dropdown */}
              {showDropdown && (
                <div style={styles.dropdown}>
                  {loadingAcademies && (
                    <div style={styles.dropdownEmpty}>
                      <div style={styles.spinner} />
                      <span>Buscando academias...</span>
                    </div>
                  )}
                  {!loadingAcademies && academies.length === 0 && (
                    <div style={styles.dropdownEmpty}>🏫 No se encontraron academias</div>
                  )}
                  {academies.map(ac => (
                    <div key={ac.id}
                      style={{ ...styles.dropdownItem, ...(selectedAcademy?.id === ac.id ? styles.dropdownItemActive : {}) }}
                      onMouseDown={() => { setSelectedAcademy(ac); setAcademySearch(ac.name); setShowDropdown(false); }}>
                      <div style={styles.dropdownItemIcon}>🏟️</div>
                      <div style={{ flex: 1 }}>
                        <p style={styles.dropdownItemName}>{ac.name}</p>
                        {(ac.city || ac.sport) && (
                          <p style={styles.dropdownItemMeta}>{[ac.city, ac.sport].filter(Boolean).join(' · ')}</p>
                        )}
                      </div>
                      {selectedAcademy?.id === ac.id && <span style={{ color: '#6AB657' }}>✓</span>}
                    </div>
                  ))}
                </div>
              )}

              {/* Selected badge */}
              {selectedAcademy && (
                <div style={styles.selectedBadge}>
                  <span style={{ fontSize: 12 }}>📍</span>
                  <span>{selectedAcademy.city || ''}{selectedAcademy.sport ? ` · ${selectedAcademy.sport}` : ''}</span>
                </div>
              )}
            </div>
          )}

          <button style={{ ...styles.btn, ...(loading ? styles.btnLoading : {}) }} type="submit" disabled={loading}>
            {loading ? <span style={styles.spinnerInline} /> : (isSuperAdmin ? '👑 Ingresar al Panel Global' : '🚀 Iniciar Sesión')}
          </button>
        </form>

        <p style={styles.registerLink}>
          ¿Academia no registrada?{' '}
          <a href="/registro" style={{ color: '#42B0E6', textDecoration: 'none', fontWeight: 600 }}>
            Solicitar registro
          </a>
        </p>
      </div>
    </div>
  );
}

/* ── STYLES ── */
const styles = {
  page: {
    minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center',
    background: 'linear-gradient(135deg, #24588C 0%, #153A61 100%)',
    position: 'relative', overflow: 'hidden', padding: '16px',
  },
  bgCircle1: {
    position: 'absolute', width: 500, height: 500, borderRadius: '50%',
    background: 'radial-gradient(circle, rgba(62,177,229,0.3) 0%, transparent 70%)',
    top: -150, right: -150,
  },
  bgCircle2: {
    position: 'absolute', width: 400, height: 400, borderRadius: '50%',
    background: 'radial-gradient(circle, rgba(118,184,88,0.3) 0%, transparent 70%)',
    bottom: -100, left: -100,
  },
  card: {
    background: '#FFFFFF',
    borderRadius: 28, padding: '40px 32px',
    width: '100%', maxWidth: 440,
    boxShadow: '0 32px 80px rgba(0,0,0,0.3), 0 0 40px rgba(62,177,229,0.15)',
    position: 'relative', backdropFilter: 'blur(12px)',
    border: '1px solid rgba(255,255,255,0.8)',
  },
  brand: { textAlign: 'center', marginBottom: 24 },
  logoImg: {
    width: 90, height: 90, borderRadius: '50%',
    border: '3px solid #3EB1E5',
    boxShadow: '0 8px 24px rgba(36,88,140,0.15)',
    marginBottom: 14, objectFit: 'cover', background: '#fff'
  },
  brandName: { fontSize: 22, fontWeight: 900, color: '#24588C', margin: 0, letterSpacing: 0.5 },
  brandSub: { fontSize: 13, color: '#76B858', margin: '5px 0 0', fontWeight: 600 },
  errorBox: {
    background: 'rgba(239,68,68,0.15)', border: '1px solid rgba(239,68,68,0.4)', borderRadius: 12,
    padding: '12px 16px', marginBottom: 16, color: '#ef4444', fontSize: 13, fontWeight: 500,
  },
  form: { display: 'flex', flexDirection: 'column', gap: 0 },
  inputGroup: { marginBottom: 16, position: 'relative' },
  label: { display: 'block', fontSize: 13, fontWeight: 700, color: '#334155', marginBottom: 6 },
  inputWrapper: {
    display: 'flex', alignItems: 'center',
    border: '1px solid rgba(62,177,229,0.45)',
    borderRadius: 12, background: '#E8F2F7',
    paddingLeft: 12, paddingRight: 12, height: 48, transition: 'border-color 0.2s, box-shadow 0.2s',
  },
  inputWrapperFocused: { borderColor: '#3EB1E5', boxShadow: '0 0 0 3px rgba(62,177,229,0.15)' },
  inputIcon: { fontSize: 16, marginRight: 8, color: '#64748B' },
  input: {
    flex: 1, border: 'none', background: 'transparent',
    fontSize: 14, color: '#1E293B', outline: 'none', width: '100%',
  },
  eyeBtn: { background: 'none', border: 'none', cursor: 'pointer', fontSize: 16, padding: 4 },
  // Academy dropdown
  dropdown: {
    position: 'absolute', top: '100%', left: 0, right: 0, zIndex: 999,
    background: '#FFF', border: '1px solid #3EB1E5', borderRadius: 12,
    boxShadow: '0 16px 48px rgba(36,88,140,0.2)', maxHeight: 200, overflowY: 'auto', marginTop: 4,
  },
  dropdownEmpty: {
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    gap: 8, padding: '16px', color: '#64748B', fontSize: 13,
  },
  dropdownItem: {
    display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px',
    cursor: 'pointer', borderBottom: '1px solid #E8F2F7', transition: 'background 0.15s',
  },
  dropdownItemActive: { background: '#E0F4FF' },
  dropdownItemIcon: { fontSize: 18 },
  dropdownItemName: { fontSize: 13, fontWeight: 700, color: '#1E293B', margin: 0 },
  dropdownItemMeta: { fontSize: 11, color: '#64748B', margin: '2px 0 0' },
  selectedBadge: {
    display: 'flex', alignItems: 'center', gap: 6,
    marginTop: 6, paddingLeft: 4, fontSize: 12, color: '#3EB1E5', fontWeight: 500,
  },
  // SuperAdmin banner
  superAdminBanner: {
    display: 'flex', alignItems: 'center', gap: 12,
    background: 'linear-gradient(135deg, #E0F4FF, #3EB1E5)',
    borderRadius: 14, padding: '14px 16px', marginBottom: 16, position: 'relative', overflow: 'hidden',
  },
  superAdminIcon: { fontSize: 26 },
  superAdminTitle: { fontSize: 14, fontWeight: 800, color: '#24588C', margin: 0 },
  superAdminSub: { fontSize: 11, color: '#1E293B', margin: '2px 0 0' },
  superAdminBadge: {
    position: 'absolute', top: 8, right: 10,
    background: '#76B858', color: '#FFF',
    fontSize: 9, fontWeight: 800, letterSpacing: 1,
    padding: '2px 6px', borderRadius: 16, border: 'none',
  },
  btn: {
    background: 'linear-gradient(135deg, #3EB1E5, #24588C)',
    color: '#FFF', border: 'none', borderRadius: 12, height: 48,
    fontSize: 14, fontWeight: 700, cursor: 'pointer', marginTop: 8,
    boxShadow: '0 8px 24px rgba(62,177,229,0.3)',
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    transition: 'opacity 0.2s', width: '100%'
  },
  btnLoading: { opacity: 0.7, cursor: 'not-allowed' },
  spinner: {
    width: 16, height: 16, borderRadius: '50%',
    border: '2px solid rgba(36,88,140,0.3)', borderTopColor: '#24588C',
    animation: 'spin 0.8s linear infinite',
  },
  spinnerInline: {
    display: 'inline-block', width: 16, height: 16, borderRadius: '50%',
    border: '2px solid rgba(255,255,255,0.3)', borderTopColor: '#FFF',
    animation: 'spin 0.8s linear infinite',
  },
  registerLink: { textAlign: 'center', marginTop: 16, fontSize: 12, color: '#64748B' },
};
