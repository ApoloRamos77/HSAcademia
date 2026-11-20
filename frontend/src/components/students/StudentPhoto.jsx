import { useEffect, useRef, useState } from 'react';
import { Camera, Trash2 } from 'lucide-react';
import { fileUrl } from '../../utils/files';

const initialsOf = (first, last) =>
  `${(first || '').trim()[0] || ''}${(last || '').trim()[0] || ''}`.toUpperCase() || '?';

/** Foto del alumno en círculo; sin foto (o si no carga) muestra sus iniciales. */
export function StudentAvatar({ photoUrl, firstName, lastName, size = 40, className = '' }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [photoUrl]);
  const style = { width: size, height: size, fontSize: Math.max(11, Math.round(size * 0.36)) };
  if (photoUrl && !failed) {
    return (
      <img src={photoUrl.startsWith('blob:') ? photoUrl : fileUrl(photoUrl)} alt={`Foto de ${firstName || ''} ${lastName || ''}`.trim()}
        className={`student-avatar ${className}`} style={style} loading="lazy" onError={() => setFailed(true)} />
    );
  }
  return <div className={`student-avatar student-avatar-initials ${className}`} style={style} aria-hidden="true">{initialsOf(firstName, lastName)}</div>;
}

/**
 * Recorta la imagen al centro en cuadrado y la reduce a 480×480 JPEG (~40–80 KB):
 * sube rápido aunque la señal sea mala y no ocupa espacio de más en el servidor.
 */
export async function shrinkPhoto(file, side = 480) {
  const url = URL.createObjectURL(file);
  try {
    const img = await new Promise((resolve, reject) => {
      const i = new Image();
      i.onload = () => resolve(i);
      i.onerror = () => reject(new Error('No se pudo leer la imagen. Usa una foto JPG o PNG.'));
      i.src = url;
    });
    const crop = Math.min(img.naturalWidth, img.naturalHeight);
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = Math.min(side, crop);
    canvas.getContext('2d').drawImage(img, (img.naturalWidth - crop) / 2, (img.naturalHeight - crop) / 2, crop, crop, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', 0.82));
    if (!blob) throw new Error('No se pudo procesar la imagen.');
    return blob;
  } finally {
    URL.revokeObjectURL(url);
  }
}

/** Sube la foto ya reducida. Devuelve la ruta nueva. */
export async function uploadStudentPhoto(api, studentId, blob) {
  const form = new FormData();
  form.append('file', blob, 'foto.jpg');
  const res = await api.post(`/students/${studentId}/photo`, form, { headers: { 'Content-Type': 'multipart/form-data' } });
  return res.data.photoUrl;
}

/**
 * Selector de foto del formulario del alumno. No sube nada por sí mismo: avisa con onChange
 * ({ blob, preview } para una foto nueva, { remove: true } para quitarla) y el formulario la sube al guardar.
 */
export function StudentPhotoPicker({ photoUrl, pending, firstName, lastName, disabled, onChange, onError }) {
  const input = useRef(null);
  const [busy, setBusy] = useState(false);
  const shown = pending?.remove ? null : pending?.preview || photoUrl;

  const pick = async e => {
    const file = e.target.files?.[0];
    e.target.value = '';
    if (!file) return;
    if (!file.type.startsWith('image/')) { onError?.('Elige una imagen (JPG, PNG o WEBP).'); return; }
    setBusy(true);
    try {
      const blob = await shrinkPhoto(file);
      onChange({ blob, preview: URL.createObjectURL(blob) });
    } catch (err) {
      onError?.(err.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="student-photo-picker">
      <StudentAvatar photoUrl={shown} firstName={firstName} lastName={lastName} size={84} />
      {!disabled && (
        <div className="flex flex-col gap-2">
          <input ref={input} type="file" accept="image/*" className="hidden" onChange={pick} aria-label="Elegir foto del alumno" />
          <button type="button" className="btn btn-outline btn-sm" onClick={() => input.current?.click()} disabled={busy}>
            <Camera size={15} /> {busy ? 'Procesando…' : shown ? 'Cambiar foto' : 'Agregar foto'}
          </button>
          {shown && (
            <button type="button" className="btn btn-ghost btn-sm text-danger" onClick={() => onChange(photoUrl ? { remove: true } : null)}>
              <Trash2 size={15} /> Quitar foto
            </button>
          )}
          <span className="text-xs text-text-muted">Se recorta cuadrada. En el celular puedes tomarla con la cámara.</span>
        </div>
      )}
    </div>
  );
}
