import { useEffect, useState } from 'react';
import { Crown, KeyRound, MapPin, ShieldCheck, Smartphone, Globe, User } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../api/axios';
import AppLayout from '../components/AppLayout';
import { useAuth } from '../context/AuthContext';

const EMPTY_PASSWORD = { currentPassword: '', newPassword: '', confirmPassword: '' };
const ROLE_LABEL = { AcademyAdmin: 'Administrador', Staff: 'Personal', Student: 'Alumno', Guardian: 'Apoderado', SuperAdmin: 'Plataforma' };

export default function MiPerfil() {
  const { refresh } = useAuth();
  const [profile, setProfile] = useState(null);
  const [form, setForm] = useState({ firstName: '', lastName: '', phone: '' });
  const [password, setPassword] = useState(EMPTY_PASSWORD);
  const [savingData, setSavingData] = useState(false);
  const [savingPassword, setSavingPassword] = useState(false);

  useEffect(() => {
    api.get('/auth/profile')
      .then(({ data }) => { setProfile(data); setForm({ firstName: data.firstName, lastName: data.lastName, phone: data.phone || '' }); })
      .catch(() => toast.error('No se pudo cargar tu perfil.'));
  }, []);

  const saveData = async (e) => {
    e.preventDefault();
    setSavingData(true);
    try {
      await api.put('/auth/profile', form);
      await refresh();
      toast.success('Perfil actualizado');
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo guardar.');
    } finally {
      setSavingData(false);
    }
  };

  const savePassword = async (e) => {
    e.preventDefault();
    if (password.newPassword.length < 8 || !/[A-Za-zÁÉÍÓÚáéíóúÑñ]/.test(password.newPassword) || !/\d/.test(password.newPassword)) {
      toast.error('La nueva contraseña debe tener al menos 8 caracteres y combinar letras y números.');
      return;
    }
    if (password.newPassword !== password.confirmPassword) { toast.error('Las contraseñas no coinciden.'); return; }
    setSavingPassword(true);
    try {
      await api.post('/auth/change-password', { currentPassword: password.currentPassword, newPassword: password.newPassword });
      setPassword(EMPTY_PASSWORD);
      toast.success('Contraseña actualizada');
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo cambiar la contraseña.');
    } finally {
      setSavingPassword(false);
    }
  };

  if (!profile) return <AppLayout title="Mi perfil"><div className="text-center p-8"><span className="spinner" style={{ borderColor: 'var(--primary)', borderTopColor: 'transparent' }} /></div></AppLayout>;

  const isTeam = profile.role === 'Staff' || profile.role === 'AcademyAdmin' || profile.role === 'SuperAdmin';

  return (
    <AppLayout title="Mi perfil">
      <div className="fade-in access-scope grid grid-cols-1 lg:grid-cols-2 gap-5 items-start">
        <div className="flex flex-col gap-5">
          <div className="card">
            <h3 className="card-title flex items-center gap-2 mb-4"><User size={18} className="text-primary" /> Mis datos</h3>
            <form onSubmit={saveData}>
              <div className="form-row">
                <div className="form-group"><label className="block"><span className="form-label">Nombre *</span><input required className="form-control" value={form.firstName} onChange={e => setForm({ ...form, firstName: e.target.value })} /></label></div>
                <div className="form-group"><label className="block"><span className="form-label">Apellido *</span><input required className="form-control" value={form.lastName} onChange={e => setForm({ ...form, lastName: e.target.value })} /></label></div>
              </div>
              <div className="form-group"><label className="block"><span className="form-label">Teléfono</span><input className="form-control" value={form.phone} onChange={e => setForm({ ...form, phone: e.target.value })} /></label></div>
              {profile.email && <p className="text-sm text-text-secondary">Correo de ingreso: <strong>{profile.email}</strong></p>}
              {profile.academyName && <p className="text-sm text-text-secondary">Academia: <strong>{profile.academyName}</strong></p>}
              <button type="submit" disabled={savingData} className="btn btn-primary">{savingData ? 'Guardando…' : 'Guardar cambios'}</button>
            </form>
          </div>

          <div className="card">
            <h3 className="card-title flex items-center gap-2 mb-4"><KeyRound size={18} className="text-primary" /> Cambiar contraseña</h3>
            <form onSubmit={savePassword}>
              <div className="form-group"><label className="block"><span className="form-label">Contraseña actual</span><input required type="password" autoComplete="current-password" className="form-control" value={password.currentPassword} onChange={e => setPassword({ ...password, currentPassword: e.target.value })} /></label></div>
              <div className="form-row">
                <div className="form-group"><label className="block"><span className="form-label">Nueva contraseña</span><input required type="password" autoComplete="new-password" className="form-control" value={password.newPassword} onChange={e => setPassword({ ...password, newPassword: e.target.value })} /></label></div>
                <div className="form-group"><label className="block"><span className="form-label">Repítela</span><input required type="password" autoComplete="new-password" className="form-control" value={password.confirmPassword} onChange={e => setPassword({ ...password, confirmPassword: e.target.value })} /></label></div>
              </div>
              <button type="submit" disabled={savingPassword} className="btn btn-primary">{savingPassword ? 'Guardando…' : 'Actualizar contraseña'}</button>
            </form>
          </div>
        </div>

        {isTeam && (
          <div className="flex flex-col gap-5">
            <div className="card">
              <h3 className="card-title flex items-center gap-2 mb-4"><ShieldCheck size={18} className="text-primary" /> Mi acceso</h3>
              <div className="flex flex-wrap gap-1 mb-4">
                {profile.isOwner && <span className="badge badge-info gap-1"><Crown size={11} /> Propietario</span>}
                {!profile.isOwner && profile.fullAccess && <span className="badge badge-info">{ROLE_LABEL[profile.role]}</span>}
                {profile.roles.map(r => <span key={r} className="badge badge-muted">{r}</span>)}
                {!profile.fullAccess && profile.roles.length === 0 && <span className="badge badge-danger">Sin rol asignado</span>}
              </div>
              {profile.fullAccess ? (
                <p className="text-sm text-text-secondary m-0">Tienes acceso completo a la academia.</p>
              ) : (
                <>
                  <div className="text-sm mb-1 flex items-center gap-1"><MapPin size={13} className="text-success" /> <strong>Sedes:</strong> {profile.headquarters.length ? profile.headquarters.join(', ') : 'Todas'}</div>
                  <div className="text-sm mb-4"><strong>Categorías:</strong> {profile.categories.length ? profile.categories.join(', ') : 'Ninguna asignada'}</div>
                  <div className="flex flex-col gap-2">
                    {profile.permissionGroups.map(g => (
                      <div key={g.name}>
                        <div className="text-xs font-bold text-text-muted">{g.name}</div>
                        <div className="text-sm text-text-secondary">{g.items.join(' · ')}</div>
                      </div>
                    ))}
                  </div>
                </>
              )}
            </div>

            <div className="card">
              <h3 className="card-title mb-4">Ingresos recientes</h3>
              {profile.recentLogins.length === 0 && <p className="text-sm text-text-muted m-0">Aún no hay ingresos registrados.</p>}
              <div className="flex flex-col gap-3">
                {profile.recentLogins.map(l => (
                  <div key={l.at} className="flex items-center gap-3 text-sm">
                    {l.device === 'App móvil' ? <Smartphone size={16} className="text-primary" /> : <Globe size={16} className="text-primary" />}
                    <span className="flex-1">{l.device}</span>
                    <span className="text-xs text-text-muted">{new Date(l.at).toLocaleString('es-PE', { day: '2-digit', month: 'short', hour: '2-digit', minute: '2-digit' })}</span>
                  </div>
                ))}
              </div>
              <p className="text-xs text-text-muted mt-3 mb-0">Si ves un ingreso que no reconoces, cambia tu contraseña.</p>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
