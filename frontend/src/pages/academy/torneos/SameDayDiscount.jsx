const PRESETS = [0, 25, 50, 75, 100];

/**
 * Regla del torneo para un concepto por partido: desde el 2.º encuentro del mismo día de un alumno,
 * % de descuento (100 = se exonera). value: número (0 = sin descuento).
 */
export default function SameDayDiscount({ value, onChange, id = 'same-day-discount' }) {
  const v = Number(value) || 0;
  const custom = !PRESETS.includes(v);
  return (
    <div className="form-group">
      <label htmlFor={id}>Si juega más de un encuentro el mismo día</label>
      <div style={{ display: 'flex', gap: 8 }}>
        <select id={id} className="form-control" value={custom ? 'otro' : String(v)}
          onChange={e => onChange(e.target.value === 'otro' ? (custom ? v : 10) : Number(e.target.value))}>
          <option value="0">Se cobra completo cada encuentro</option>
          <option value="25">Desde el 2.º: 25 % de descuento</option>
          <option value="50">Desde el 2.º: 50 % de descuento</option>
          <option value="75">Desde el 2.º: 75 % de descuento</option>
          <option value="100">Desde el 2.º: se exonera</option>
          <option value="otro">Otro porcentaje…</option>
        </select>
        {custom && (
          <input type="number" min={1} max={99} step={1} className="form-control" style={{ maxWidth: 90 }} aria-label="Porcentaje de descuento"
            value={v} onChange={e => onChange(Math.min(99, Math.max(1, Number(e.target.value) || 1)))} />
        )}
      </div>
      <small className="text-muted">Se aplica solo. Al cobrar igual puedes dar otro descuento o exonerar, con su motivo.</small>
    </div>
  );
}
