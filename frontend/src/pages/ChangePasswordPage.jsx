import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../api/axios';
import { useAuth } from '../context/AuthContext';
import toast from 'react-hot-toast';

export default function ChangePasswordPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  // Read the password stored at login (sessionStorage) to use as currentPassword
  const storedPw = sessionStorage.getItem('_lp') || '12345';
  const [form, setForm] = useState({ currentPassword: storedPw, newPassword: '', confirmPassword: '' });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async e => {
    e.preventDefault();
    setError('');

    if (form.newPassword.length < 6) {
      setError('La nueva contraseña debe tener al menos 6 caracteres.');
      return;
    }
    if (form.newPassword !== form.confirmPassword) {
      setError('Las contraseñas no coinciden.');
      return;
    }

    setLoading(true);
    try {
      await api.post('/auth/change-password', {
        currentPassword: form.currentPassword,
        newPassword: form.newPassword
      });
      toast.success('Contraseña actualizada correctamente.');
      // Clear stored password
      sessionStorage.removeItem('_lp');
      
      // Force update user in localStorage to not require password change again (or just navigate)
      const u = JSON.parse(localStorage.getItem('user'));
      u.requirePasswordChange = false;
      localStorage.setItem('user', JSON.stringify(u));

      navigate(user?.role === 'SuperAdmin' ? '/super-admin/dashboard' : '/dashboard');
    } catch (err) {
      setError(err.response?.data?.message || 'Error al actualizar contraseña.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card card" style={{ maxWidth: 450 }}>
        <div className="login-logo mb-4">
          <h2 className="text-primary" style={{ textAlign: 'center' }}>Actualiza tu Contraseña</h2>
          <p className="text-center text-muted">Por seguridad, debes cambiar tu contraseña inicial antes de continuar.</p>
        </div>

        {error && <div className="alert alert-danger">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label className="form-label">Contraseña Actual (Por defecto)</label>
            <input
              className="form-control"
              type="password"
              value={form.currentPassword}
              onChange={e => setForm({ ...form, currentPassword: e.target.value })}
              required
            />
          </div>
          <div className="form-group">
            <label className="form-label">Nueva Contraseña</label>
            <input
              className="form-control"
              type="password"
              placeholder="Mínimo 6 caracteres"
              value={form.newPassword}
              onChange={e => setForm({ ...form, newPassword: e.target.value })}
              required
            />
          </div>
          <div className="form-group">
            <label className="form-label">Confirmar Nueva Contraseña</label>
            <input
              className="form-control"
              type="password"
              placeholder="Repita su nueva contraseña"
              value={form.confirmPassword}
              onChange={e => setForm({ ...form, confirmPassword: e.target.value })}
              required
            />
          </div>
          <button className="btn btn-primary mt-4" style={{ width: '100%', justifyContent: 'center', padding: '12px' }} disabled={loading}>
            {loading ? <span className="spinner" /> : 'Actualizar y Entrar'}
          </button>
        </form>
      </div>
    </div>
  );
}
