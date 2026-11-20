import { useRef, useState } from 'react';
import { Camera, Trash2 } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../api/axios';
import { StudentAvatar, shrinkPhoto } from './students/StudentPhoto';

/**
 * Foto que se guarda al instante (perfil propio, personal en Accesos).
 * `endpoint` recibe POST (multipart "file") y DELETE; responde { avatarUrl } o { photoUrl }.
 */
export default function PhotoUploader({ photoUrl, firstName, lastName, endpoint, onChanged, size = 84, hint }) {
  const input = useRef(null);
  const [busy, setBusy] = useState(false);

  const pick = async e => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    if (!file.type.startsWith('image/')) { toast.error('Elige una imagen (JPG, PNG o WEBP).'); return; }
    setBusy(true);
    try {
      const form = new FormData();
      form.append('file', await shrinkPhoto(file), 'foto.jpg');
      const { data } = await api.post(endpoint, form, { headers: { 'Content-Type': 'multipart/form-data' } });
      onChanged?.(data.avatarUrl ?? data.photoUrl ?? null);
      toast.success('Foto actualizada.');
    } catch (err) {
      toast.error(err.response?.data?.message || err.message || 'No se pudo subir la foto.');
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!window.confirm('¿Quitar la foto?')) return;
    setBusy(true);
    try {
      await api.delete(endpoint);
      onChanged?.(null);
      toast.success('Foto quitada.');
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo quitar la foto.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="student-photo-picker">
      <StudentAvatar photoUrl={photoUrl} firstName={firstName} lastName={lastName} size={size} />
      <div className="flex flex-col gap-2">
        <input ref={input} type="file" accept="image/*" className="hidden" onChange={pick} aria-label="Elegir foto" />
        <button type="button" className="btn btn-outline btn-sm" onClick={() => input.current?.click()} disabled={busy}>
          <Camera size={15} /> {busy ? 'Procesando…' : photoUrl ? 'Cambiar foto' : 'Agregar foto'}
        </button>
        {photoUrl && (
          <button type="button" className="btn btn-ghost btn-sm text-danger" onClick={remove} disabled={busy}>
            <Trash2 size={15} /> Quitar foto
          </button>
        )}
        {hint && <span className="text-xs text-text-muted">{hint}</span>}
      </div>
    </div>
  );
}
