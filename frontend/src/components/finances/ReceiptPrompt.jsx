import { CheckCircle, Printer } from 'lucide-react';

/**
 * Tras cobrar: pregunta si se imprime el recibo ahora (antes se abría el PDF siempre).
 * "Más tarde" no pierde nada: el recibo queda en Finanzas → Recibos y en la deuda pagada.
 */
export default function ReceiptPrompt({ number, title = 'Cobro registrado', onPrint, onClose }) {
  return (
    <div className="modal-overlay" style={{ zIndex: 1100 }} onClick={onClose}>
      <div className="modal receipt-prompt" role="dialog" aria-modal="true" aria-labelledby="receipt-prompt-title" onClick={e => e.stopPropagation()} style={{ maxWidth: 420 }}>
        <div className="flex flex-col items-center text-center gap-2">
          <CheckCircle size={44} className="text-success" />
          <h3 id="receipt-prompt-title" className="modal-title m-0">{title}</h3>
          {number && <p className="m-0 text-text-secondary">Recibo N° <b>{number}</b></p>}
          <p className="m-0 text-sm text-text-muted">¿Deseas imprimir el recibo ahora? También puedes imprimirlo después en <b>Finanzas → Recibos</b>.</p>
        </div>
        <div className="receipt-prompt-actions">
          <button type="button" className="btn btn-ghost" onClick={onClose}>Más tarde</button>
          <button type="button" className="btn btn-primary" autoFocus onClick={() => { onPrint(); onClose(); }}>
            <Printer size={16} /> Imprimir ahora
          </button>
        </div>
      </div>
    </div>
  );
}
