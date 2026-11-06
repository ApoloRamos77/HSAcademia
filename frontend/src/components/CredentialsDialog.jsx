import { KeyRound, Copy, X } from 'lucide-react';
import toast from 'react-hot-toast';

/**
 * CredentialsDialog — muestra UNA sola vez las contraseñas temporales generadas por el sistema.
 * El usuario deberá cambiarlas en su primer inicio de sesión.
 *
 * Props:
 *   credentials {Array<{ role, fullName, login, temporaryPassword }>} — null/[] para ocultar
 *   onClose     {Function}
 */
export default function CredentialsDialog({ credentials, onClose }) {
  if (!credentials || credentials.length === 0) return null;

  const asText = credentials
    .map(c => `${c.role}: ${c.fullName}\nUsuario: ${c.login}\nContraseña temporal: ${c.temporaryPassword}`)
    .join('\n\n');

  const copy = async (text) => {
    try {
      await navigator.clipboard.writeText(text);
      toast.success('Copiado al portapapeles');
    } catch {
      toast.error('No se pudo copiar. Selecciona el texto manualmente.');
    }
  };

  return (
    <div className="modal-overlay">
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 480 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <h3 className="modal-title" style={{ margin: 0, display: 'flex', alignItems: 'center', gap: 8 }}>
            <KeyRound size={20} /> Accesos generados
          </h3>
          <button onClick={onClose} className="btn btn-ghost btn-sm" aria-label="Cerrar"><X size={16} /></button>
        </div>

        <p style={{ fontSize: 14, marginBottom: 16 }}>
          Entrega estos datos a cada usuario. <strong>Solo se muestran ahora</strong>; al ingresar por
          primera vez el sistema le pedirá crear su propia contraseña.
        </p>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          {credentials.map(c => (
            <div key={c.login} style={{ border: '1px solid var(--border, #e2e8f0)', borderRadius: 10, padding: 12 }}>
              <div style={{ fontSize: 12, opacity: 0.7, marginBottom: 4 }}>{c.role}</div>
              <div style={{ fontWeight: 600, marginBottom: 8 }}>{c.fullName}</div>
              <div style={{ fontSize: 14 }}>Usuario: <code>{c.login}</code></div>
              <div style={{ fontSize: 14, display: 'flex', alignItems: 'center', gap: 8, marginTop: 4 }}>
                Contraseña temporal:
                <code style={{ fontSize: 16, fontWeight: 700, letterSpacing: 1 }}>{c.temporaryPassword}</code>
                <button type="button" className="btn btn-ghost btn-sm" onClick={() => copy(c.temporaryPassword)} aria-label="Copiar contraseña">
                  <Copy size={14} />
                </button>
              </div>
            </div>
          ))}
        </div>

        <div className="modal-footer" style={{ marginTop: 20, display: 'flex', justifyContent: 'flex-end', gap: 12 }}>
          <button type="button" className="btn btn-ghost" onClick={() => copy(asText)}>
            <Copy size={14} /> Copiar todo
          </button>
          <button type="button" className="btn btn-primary" onClick={onClose}>Listo</button>
        </div>
      </div>
    </div>
  );
}
