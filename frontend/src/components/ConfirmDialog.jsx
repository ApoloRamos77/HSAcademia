import { AlertTriangle, X } from 'lucide-react';

/**
 * ConfirmDialog — reemplaza window.confirm() en toda la app.
 *
 * Props:
 *   isOpen       {boolean}  — mostrar/ocultar el diálogo
 *   title        {string}   — título del diálogo (default: "¿Confirmar acción?")
 *   message      {string}   — texto principal del cuerpo
 *   detail       {string}   — texto secundario (pequeño, opcional)
 *   confirmLabel {string}   — etiqueta del botón de confirmación (default: "Confirmar")
 *   confirmClass {string}   — clase CSS del botón de confirmación (default: "btn-danger")
 *   icon         {ReactNode} — icono personalizado (default: AlertTriangle)
 *   onConfirm    {Function} — callback al confirmar
 *   onCancel     {Function} — callback al cancelar / cerrar
 */
export default function ConfirmDialog({
  isOpen,
  title = '¿Confirmar acción?',
  message,
  detail,
  confirmLabel = 'Confirmar',
  confirmClass = 'btn-danger',
  icon: Icon = AlertTriangle,
  onConfirm,
  onCancel,
}) {
  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div
        className="modal"
        onClick={e => e.stopPropagation()}
        style={{ maxWidth: 420 }}
      >
        {/* Header */}
        <div className="flex justify-between items-center mb-5 border-b border-border pb-3">
          <h3 className="modal-title m-0 flex items-center gap-2 text-danger">
            <Icon size={20} /> {title}
          </h3>
          <button onClick={onCancel} className="btn btn-ghost btn-sm">
            <X size={16} />
          </button>
        </div>

        {/* Body */}
        <div className="flex flex-col items-center text-center gap-4 py-2">
          <div className="p-4 bg-danger/15 rounded-full">
            <Icon size={32} className="text-danger" />
          </div>
          <div>
            {message && (
              <p className="text-text-main font-medium mb-1">{message}</p>
            )}
            {detail && (
              <p className="text-xs text-text-muted mt-2">{detail}</p>
            )}
          </div>
        </div>

        {/* Footer */}
        <div className="modal-footer mt-6 flex justify-end gap-3">
          <button type="button" onClick={onCancel} className="btn btn-ghost">
            Cancelar
          </button>
          <button
            type="button"
            onClick={onConfirm}
            className={`btn ${confirmClass} flex items-center gap-2`}
          >
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
