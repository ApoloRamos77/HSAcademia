import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import api from '../api/axios';
import toast from 'react-hot-toast';

/* ────────────────────────────────────────────
   Premium Login Page — ADHSOFT SPORT
   • Step 1: email/phone input
   • Step 2a: SuperAdmin → no academy required
   • Step 2b: Normal user → academy selector
   ──────────────────────────────────────────── */

const SUPERADMIN_HINT_EMAIL = '@adhsoft';   // emails containing this skip academy step

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();

  const [step, setStep] = useState(1);          // 1 = email, 2 = password+academy
  const [isSuperAdmin, setIsSuperAdmin] = useState(false);

  const [emailOrPhone, setEmailOrPhone] = useState('');
  const [password, setPassword] = useState('');
  const [showPass, setShowPass] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

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

  // Detect SuperAdmin by email pattern and advance to step 2
  const handleContinue = e => {
    e.preventDefault();
    if (!emailOrPhone.trim()) return;
    const superAdmin = emailOrPhone.toLowerCase().includes(SUPERADMIN_HINT_EMAIL);
    setIsSuperAdmin(superAdmin);
    setStep(2);
    if (!superAdmin) {
      fetchAcademies();
    }
  };

  const handleAcademySearch = v => {
    setAcademySearch(v);
    setSelectedAcademy(null);
    clearTimeout(searchTimeout.current);
    searchTimeout.current = setTimeout(() => fetchAcademies(v), 300);
  };

  const handleSubmit = async e => {
    e.preventDefault();
    setError('');

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
        // Save password for the change-password page
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
          <div style={styles.logoRing}>
            <span style={styles.logoIcon}>⚽</span>
          </div>
          <h1 style={styles.brandName}>ADHSOFT SPORT</h1>
          <p style={styles.brandSub}>Plataforma de Gestión Deportiva</p>
        </div>

        {/* ── STEP INDICATOR ── */}
        <div style={styles.stepRow}>
          <div style={{ ...styles.stepDot, background: '#24588C' }} />
          <div style={{ ...styles.stepLine, background: step === 2 ? '#24588C' : '#E2E8F0' }} />
          <div style={{ ...styles.stepDot, background: step === 2 ? '#24588C' : '#E2E8F0' }} />
        </div>

        {error && (
          <div style={styles.errorBox}>
            <span style={{ marginRight: 8 }}>⚠️</span>{error}
          </div>
        )}

        {/* ══════════ STEP 1 — Email ══════════ */}
        {step === 1 && (
          <form onSubmit={handleContinue} style={styles.form}>
            <p style={styles.stepTitle}>Ingresa tu identificador</p>
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
                  autoFocus
                  required
                />
              </div>
            </div>
            <button style={styles.btn} type="submit">
              Continuar →
            </button>
          </form>
        )}

        {/* ══════════ STEP 2 — Password + Academy ══════════ */}
        {step === 2 && (
          <form onSubmit={handleSubmit} style={styles.form}>
            {/* Back button */}
            <button type="button" style={styles.backBtn} onClick={() => { setStep(1); setError(''); setPassword(''); }}>
              ← Volver
            </button>

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
                  autoFocus
                  required
                />
                <button type="button" style={styles.eyeBtn} onClick={() => setShowPass(!showPass)}>
                  {showPass ? '🙈' : '👁️'}
                </button>
              </div>
            </div>

            <button style={{ ...styles.btn, ...(loading ? styles.btnLoading : {}) }} type="submit" disabled={loading}>
              {loading ? <span style={styles.spinnerInline} /> : (isSuperAdmin ? '👑 Ingresar al Panel Global' : '🚀 Iniciar Sesión')}
            </button>
          </form>
        )}

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
    position: 'relative', overflow: 'hidden', padding: '24px',
  },
  bgCircle1: {
    position: 'absolute', width: 500, height: 500, borderRadius: '50%',
    background: 'rgba(66,176,230,0.08)', top: -150, right: -100,
  },
  bgCircle2: {
    position: 'absolute', width: 300, height: 300, borderRadius: '50%',
    background: 'rgba(106,182,87,0.06)', bottom: -80, left: -60,
  },
  card: {
    background: 'rgba(255,255,255,0.97)', borderRadius: 28, padding: '40px 36px',
    width: '100%', maxWidth: 440, boxShadow: '0 32px 80px rgba(0,0,0,0.35)',
    position: 'relative', backdropFilter: 'blur(12px)',
  },
  brand: { textAlign: 'center', marginBottom: 28 },
  logoRing: {
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center',
    width: 72, height: 72, borderRadius: '50%',
    background: 'linear-gradient(135deg, #24588C, #42B0E6)',
    boxShadow: '0 8px 24px rgba(36,88,140,0.4)', marginBottom: 14,
  },
  logoIcon: { fontSize: 36 },
  brandName: { fontSize: 22, fontWeight: 800, color: '#1E293B', margin: 0, letterSpacing: 1 },
  brandSub: { fontSize: 13, color: '#64748B', margin: '4px 0 0', fontWeight: 500 },
  stepRow: { display: 'flex', alignItems: 'center', marginBottom: 24 },
  stepDot: { width: 10, height: 10, borderRadius: '50%', transition: 'background 0.3s' },
  stepLine: { flex: 1, height: 2, transition: 'background 0.3s' },
  errorBox: {
    background: '#FEF2F2', border: '1px solid #FECACA', borderRadius: 12,
    padding: '12px 16px', marginBottom: 16, color: '#DC2626', fontSize: 13, fontWeight: 500,
  },
  form: { display: 'flex', flexDirection: 'column', gap: 0 },
  stepTitle: { fontSize: 15, fontWeight: 600, color: '#64748B', marginBottom: 20, textAlign: 'center' },
  backBtn: {
    background: 'none', border: 'none', color: '#64748B', cursor: 'pointer',
    fontSize: 13, fontWeight: 600, padding: '0 0 16px', textAlign: 'left',
  },
  inputGroup: { marginBottom: 18, position: 'relative' },
  label: { display: 'block', fontSize: 13, fontWeight: 700, color: '#1E293B', marginBottom: 8 },
  inputWrapper: {
    display: 'flex', alignItems: 'center', border: '1.5px solid #E2E8F0',
    borderRadius: 14, background: '#F8FAFC', paddingLeft: 12, paddingRight: 12,
    height: 52, transition: 'border-color 0.2s',
  },
  inputWrapperFocused: { borderColor: '#42B0E6', boxShadow: '0 0 0 3px rgba(66,176,230,0.12)' },
  inputIcon: { fontSize: 16, marginRight: 8 },
  input: {
    flex: 1, border: 'none', background: 'transparent', fontSize: 14,
    color: '#1E293B', outline: 'none',
  },
  eyeBtn: { background: 'none', border: 'none', cursor: 'pointer', fontSize: 16, padding: 4 },
  // Academy dropdown
  dropdown: {
    position: 'absolute', top: '100%', left: 0, right: 0, zIndex: 999,
    background: '#FFF', border: '1.5px solid #42B0E6', borderRadius: 16,
    boxShadow: '0 16px 48px rgba(36,88,140,0.2)', maxHeight: 220, overflowY: 'auto', marginTop: 4,
  },
  dropdownEmpty: {
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    gap: 8, padding: '20px', color: '#94A3B8', fontSize: 13,
  },
  dropdownItem: {
    display: 'flex', alignItems: 'center', gap: 12, padding: '12px 16px',
    cursor: 'pointer', borderBottom: '1px solid #F0F4F8', transition: 'background 0.15s',
  },
  dropdownItemActive: { background: '#EFF6FF' },
  dropdownItemIcon: { fontSize: 20 },
  dropdownItemName: { fontSize: 14, fontWeight: 700, color: '#1E293B', margin: 0 },
  dropdownItemMeta: { fontSize: 12, color: '#94A3B8', margin: '2px 0 0' },
  selectedBadge: {
    display: 'flex', alignItems: 'center', gap: 6,
    marginTop: 6, paddingLeft: 4, fontSize: 12, color: '#24588C', fontWeight: 500,
  },
  // SuperAdmin banner
  superAdminBanner: {
    display: 'flex', alignItems: 'center', gap: 14,
    background: 'linear-gradient(135deg, #0F2A47, #24588C)',
    borderRadius: 18, padding: '16px 20px', marginBottom: 20, position: 'relative', overflow: 'hidden',
  },
  superAdminIcon: { fontSize: 32 },
  superAdminTitle: { fontSize: 15, fontWeight: 800, color: '#FFF', margin: 0 },
  superAdminSub: { fontSize: 12, color: 'rgba(255,255,255,0.7)', margin: '2px 0 0' },
  superAdminBadge: {
    position: 'absolute', top: 10, right: 12,
    background: 'rgba(106,182,87,0.25)', color: '#6AB657',
    fontSize: 10, fontWeight: 800, letterSpacing: 1,
    padding: '3px 8px', borderRadius: 20, border: '1px solid rgba(106,182,87,0.4)',
  },
  btn: {
    background: 'linear-gradient(135deg, #24588C, #1a4070)',
    color: '#FFF', border: 'none', borderRadius: 14, height: 52,
    fontSize: 15, fontWeight: 700, cursor: 'pointer', marginTop: 8,
    boxShadow: '0 8px 24px rgba(36,88,140,0.4)',
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    transition: 'opacity 0.2s',
  },
  btnLoading: { opacity: 0.7, cursor: 'not-allowed' },
  spinner: {
    width: 18, height: 18, borderRadius: '50%',
    border: '2px solid rgba(255,255,255,0.3)', borderTopColor: '#FFF',
    animation: 'spin 0.8s linear infinite',
  },
  spinnerInline: {
    display: 'inline-block', width: 18, height: 18, borderRadius: '50%',
    border: '2px solid rgba(255,255,255,0.3)', borderTopColor: '#FFF',
  },
  registerLink: { textAlign: 'center', marginTop: 20, fontSize: 13, color: '#64748B' },
};
