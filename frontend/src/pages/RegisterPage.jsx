import { useState } from 'react';
import api from '../api/axios';
import toast from 'react-hot-toast';

const SPORTS = ['Fútbol','Baloncesto','Natación','Tenis','Volleyball','Atletismo','Artes Marciales','Gimnasia','Otro'];

export default function RegisterPage() {
  const [form, setForm] = useState({
    academyName: '', description: '', contactName: '', contactEmail: '',
    contactPhone: '', city: '', country: 'Colombia', sport: '', website: '', additionalInfo: ''
  });
  const [loading, setLoading] = useState(false);
  const [submitted, setSubmitted] = useState(false);

  const handleSubmit = async e => {
    e.preventDefault();
    setLoading(true);
    try {
      await api.post('/public/registration-request', form);
      setSubmitted(true);
      toast.success('Solicitud enviada exitosamente');
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al enviar la solicitud.');
    } finally {
      setLoading(false);
    }
  };

  if (submitted) return (
    <div className="login-page">
      <div className="card" style={{ maxWidth: 480, textAlign: 'center', padding: 48 }}>
        <div style={{ fontSize: 64, marginBottom: 16 }}>✅</div>
        <h2 style={{ marginBottom: 12 }}>¡Solicitud enviada!</h2>
        <p className="text-muted" style={{ lineHeight: 1.6 }}>
          Hemos recibido su solicitud de registro para <strong>{form.academyName}</strong>.<br />
          El equipo de ADHSOFT SPORT la revisará pronto y le notificará por correo.
        </p>
        <a href="/login" className="btn btn-primary" style={{ marginTop: 24, justifyContent: 'center' }}>
          Volver al inicio de sesión
        </a>
      </div>
    </div>
  );

  return (
    <div className="login-page" style={{ padding: '40px 20px' }}>
      <div className="card" style={{ maxWidth: 640, width: '100%' }}>
        <div className="login-logo">
          <h1>ADHSOFT SPORT</h1>
          <p>Solicitud de Registro de Academia</p>
        </div>

        <form onSubmit={handleSubmit}>
          <h3 style={{ marginBottom: 16, fontSize: 14, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: 1 }}>Datos de la Academia</h3>
          <div className="form-group">
            <label className="form-label">Nombre de la Academia *</label>
            <input className="form-control" required value={form.academyName}
              onChange={e => setForm({ ...form, academyName: e.target.value })} />
          </div>
          <div className="form-row">
            <div className="form-group">
              <label className="form-label">Deporte Principal</label>
              <select className="form-control" value={form.sport}
                onChange={e => setForm({ ...form, sport: e.target.value })}>
                <option value="">Seleccionar...</option>
                {SPORTS.map(s => <option key={s}>{s}</option>)}
              </select>
            </div>
            <div className="form-group">
              <label className="form-label">Ciudad</label>
              <input className="form-control" value={form.city}
                onChange={e => setForm({ ...form, city: e.target.value })} />
            </div>
          </div>
          <div className="form-row">
            <div className="form-group">
              <label className="form-label">País</label>
              <input className="form-control" value={form.country}
                onChange={e => setForm({ ...form, country: e.target.value })} />
            </div>
            <div className="form-group">
              <label className="form-label">Sitio Web</label>
              <input className="form-control" type="url" placeholder="https://..." value={form.website}
                onChange={e => setForm({ ...form, website: e.target.value })} />
            </div>
          </div>
          <div className="form-group">
            <label className="form-label">Descripción</label>
            <textarea className="form-control" rows={3} value={form.description}
              onChange={e => setForm({ ...form, description: e.target.value })} />
          </div>

          <h3 style={{ margin: '24px 0 16px', fontSize: 14, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: 1 }}>Datos de Contacto</h3>
          <div className="form-group">
            <label className="form-label">Nombre del Responsable *</label>
            <input className="form-control" required value={form.contactName}
              onChange={e => setForm({ ...form, contactName: e.target.value })} />
          </div>
          <div className="form-row">
            <div className="form-group">
              <label className="form-label">Correo Electrónico *</label>
              <input className="form-control" type="email" required value={form.contactEmail}
                onChange={e => setForm({ ...form, contactEmail: e.target.value })} />
            </div>
            <div className="form-group">
              <label className="form-label">Teléfono</label>
              <input className="form-control" value={form.contactPhone}
                onChange={e => setForm({ ...form, contactPhone: e.target.value })} />
            </div>
          </div>
          <div className="form-group">
            <label className="form-label">Información Adicional</label>
            <textarea className="form-control" rows={3} placeholder="Cuéntenos más sobre su academia..." value={form.additionalInfo}
              onChange={e => setForm({ ...form, additionalInfo: e.target.value })} />
          </div>

          <button className="btn btn-primary" style={{ width: '100%', justifyContent: 'center', padding: '12px' }} disabled={loading}>
            {loading ? <span className="spinner" /> : 'Enviar Solicitud de Registro'}
          </button>
          <p className="text-muted text-center" style={{ marginTop: 12 }}>
            ¿Ya tiene cuenta? <a href="/login" style={{ color: 'var(--primary-light)', textDecoration: 'none' }}>Iniciar sesión</a>
          </p>
        </form>
      </div>
    </div>
  );
}
