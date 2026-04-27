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
        navigate(user.role === 'SuperAdmin' ? '/super-admin/dashboard' : '/dashboard');
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
    background: 'linear-gradient(135deg, #0F2A47 0%, #1a4070 40%, #24588C 100%)',
    position: 'relative', overflow: 'hidden', padding: '16px',
  },
  bgCircle1: {
    position: 'absolute', width: 400, height: 400, borderRadius: '50%',
    background: 'rgba(66,176,230,0.08)', top: -100, right: -80,
  },
  bgCircle2: {
    position: 'absolute', width: 250, height: 250, borderRadius: '50%',
    background: 'rgba(106,182,87,0.06)', bottom: -50, left: -40,
  },
  card: {
    background: 'rgba(19,35,64,0.97)', borderRadius: 24, padding: '32px 24px',
    width: '100%', maxWidth: 440,
    boxShadow: '0 32px 80px rgba(0,0,0,0.6), 0 0 30px rgba(66,176,230,0.15)',
    position: 'relative', backdropFilter: 'blur(12px)',
    border: '1px solid rgba(66,176,230,0.25)',
  },
  brand: { textAlign: 'center', marginBottom: 24 },
  logoImg: {
    width: 80, height: 80, borderRadius: '50%',
    border: '3px solid rgba(66,176,230,0.7)',
    boxShadow: '0 0 32px rgba(66,176,230,0.5)',
    marginBottom: 12, objectFit: 'cover',
  },
  brandName: { fontSize: 20, fontWeight: 800, color: '#FFFFFF', margin: 0, letterSpacing: 1 },
  brandSub: { fontSize: 13, color: '#42B0E6', margin: '4px 0 0', fontWeight: 500 },
  errorBox: {
    background: 'rgba(239,68,68,0.15)', border: '1px solid rgba(239,68,68,0.4)', borderRadius: 12,
    padding: '12px 16px', marginBottom: 16, color: '#f87171', fontSize: 13, fontWeight: 500,
  },
  form: { display: 'flex', flexDirection: 'column', gap: 0 },
  inputGroup: { marginBottom: 16, position: 'relative' },
  label: { display: 'block', fontSize: 13, fontWeight: 700, color: '#94A3B8', marginBottom: 6 },
  inputWrapper: {
    display: 'flex', alignItems: 'center',
    border: '1.5px solid rgba(66,176,230,0.3)',
    borderRadius: 12, background: 'rgba(26,47,80,0.8)',
    paddingLeft: 12, paddingRight: 12, height: 48, transition: 'border-color 0.2s',
  },
  inputWrapperFocused: { borderColor: '#42B0E6', boxShadow: '0 0 0 3px rgba(66,176,230,0.15)' },
  inputIcon: { fontSize: 16, marginRight: 8 },
  input: {
    flex: 1, border: 'none', background: 'transparent',
    fontSize: 14, color: '#F1F5F9', outline: 'none', width: '100%',
  },
  eyeBtn: { background: 'none', border: 'none', cursor: 'pointer', fontSize: 16, padding: 4 },
  // Academy dropdown
  dropdown: {
    position: 'absolute', top: '100%', left: 0, right: 0, zIndex: 999,
    background: '#FFF', border: '1.5px solid #42B0E6', borderRadius: 12,
    boxShadow: '0 16px 48px rgba(36,88,140,0.2)', maxHeight: 200, overflowY: 'auto', marginTop: 4,
  },
  dropdownEmpty: {
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    gap: 8, padding: '16px', color: '#94A3B8', fontSize: 13,
  },
  dropdownItem: {
    display: 'flex', alignItems: 'center', gap: 10, padding: '10px 14px',
    cursor: 'pointer', borderBottom: '1px solid #F0F4F8', transition: 'background 0.15s',
  },
  dropdownItemActive: { background: '#EFF6FF' },
  dropdownItemIcon: { fontSize: 18 },
  dropdownItemName: { fontSize: 13, fontWeight: 700, color: '#1E293B', margin: 0 },
  dropdownItemMeta: { fontSize: 11, color: '#94A3B8', margin: '2px 0 0' },
  selectedBadge: {
    display: 'flex', alignItems: 'center', gap: 6,
    marginTop: 6, paddingLeft: 4, fontSize: 12, color: '#42B0E6', fontWeight: 500,
  },
  // SuperAdmin banner
  superAdminBanner: {
    display: 'flex', alignItems: 'center', gap: 12,
    background: 'linear-gradient(135deg, #0F2A47, #24588C)',
    borderRadius: 14, padding: '14px 16px', marginBottom: 16, position: 'relative', overflow: 'hidden',
  },
  superAdminIcon: { fontSize: 26 },
  superAdminTitle: { fontSize: 14, fontWeight: 800, color: '#FFF', margin: 0 },
  superAdminSub: { fontSize: 11, color: 'rgba(255,255,255,0.7)', margin: '2px 0 0' },
  superAdminBadge: {
    position: 'absolute', top: 8, right: 10,
    background: 'rgba(106,182,87,0.25)', color: '#6AB657',
    fontSize: 9, fontWeight: 800, letterSpacing: 1,
    padding: '2px 6px', borderRadius: 16, border: '1px solid rgba(106,182,87,0.4)',
  },
  btn: {
    background: 'linear-gradient(135deg, #24588C, #1a4070)',
    color: '#FFF', border: 'none', borderRadius: 12, height: 48,
    fontSize: 14, fontWeight: 700, cursor: 'pointer', marginTop: 8,
    boxShadow: '0 8px 24px rgba(36,88,140,0.4)',
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    transition: 'opacity 0.2s', width: '100%'
  },
  btnLoading: { opacity: 0.7, cursor: 'not-allowed' },
  spinner: {
    width: 16, height: 16, borderRadius: '50%',
    border: '2px solid rgba(255,255,255,0.3)', borderTopColor: '#FFF',
    animation: 'spin 0.8s linear infinite',
  },
  spinnerInline: {
    display: 'inline-block', width: 16, height: 16, borderRadius: '50%',
    border: '2px solid rgba(255,255,255,0.3)', borderTopColor: '#FFF',
    animation: 'spin 0.8s linear infinite',
  },
  registerLink: { textAlign: 'center', marginTop: 16, fontSize: 12, color: '#64748B' },
};
