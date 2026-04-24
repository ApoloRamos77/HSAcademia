import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import toast from 'react-hot-toast';

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [form, setForm] = useState({ email: '', password: '' });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async e => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const user = await login(form.email, form.password);
      toast.success(`Bienvenido, ${user.fullName}`);
      navigate(user.role === 'SuperAdmin' ? '/super-admin/dashboard' : '/dashboard');
    } catch (err) {
      setError(err.response?.data?.message || 'Error al iniciar sesión.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="login-page">
      <div className="login-card card">
        <div className="login-logo">
          <h1>ADHSOFT SPORT</h1>
          <p>Plataforma de Gestión Deportiva</p>
        </div>

        {error && <div className="alert alert-danger">{error}</div>}

        <form onSubmit={handleSubmit}>
          <div className="form-group">
            <label className="form-label">Correo electrónico</label>
            <input
              className="form-control"
              type="email"
              placeholder="correo@ejemplo.com"
              value={form.email}
              onChange={e => setForm({ ...form, email: e.target.value })}
              required
            />
          </div>
          <div className="form-group">
            <label className="form-label">Contraseña</label>
            <input
              className="form-control"
              type="password"
              placeholder="••••••••"
              value={form.password}
              onChange={e => setForm({ ...form, password: e.target.value })}
              required
            />
          </div>
          <button className="btn btn-primary" style={{ width: '100%', justifyContent: 'center', padding: '12px' }} disabled={loading}>
            {loading ? <span className="spinner" /> : 'Iniciar Sesión'}
          </button>
        </form>

        <p className="text-muted text-center" style={{ marginTop: 20 }}>
          ¿Su academia aún no está registrada?{' '}
          <a href="/registro" style={{ color: 'var(--primary-light)', textDecoration: 'none' }}>Solicitar registro</a>
        </p>
      </div>
    </div>
  );
}
