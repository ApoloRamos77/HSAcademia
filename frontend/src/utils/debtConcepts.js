// Conceptos de las deudas (para filtrar Cobranzas y el cobro) y textos de prorrateo.

const TYPE_CONCEPT = { 1: 'Mensualidad', 2: 'Productos', 3: 'Matrícula', 4: 'Otros', 5: 'Mora', 6: 'Sesiones', 7: 'Torneo' };
const ORDER = ['Mensualidad', 'Paquete', 'Sesiones', 'Mora', 'Matrícula', 'Productos'];

/** Concepto de una deuda: lo manda el servidor; si es una versión anterior se deduce del tipo. */
export const conceptOf = (d) => {
  if (d.concept) return d.concept;
  if (d.type === 1 && /^paquete/i.test(d.description || '')) return 'Paquete';
  return TYPE_CONCEPT[d.type] || 'Otros';
};

/** Orden de la lista: primero los conceptos fijos; los de torneo (Arbitraje, Inscripción…) después. */
export const conceptRank = (c) => { const i = ORDER.indexOf(c); return i === -1 ? (c === 'Otros' ? 99 : 50) : i; };

const ddmm = (iso) => { const [, m, d] = String(iso || '').slice(0, 10).split('-'); return d ? `${d}/${m}` : ''; };

/** "Retirado el 10/11 · cobra 4 de 12 sesiones" o "Ingresó el 15/11 · cobra 16 de 30 días". */
export const prorationLabel = (d) => {
  const [y, m] = String(d.dueDate || '').slice(0, 7).split('-').map(Number);
  const daysInMonth = y && m ? new Date(y, m, 0).getDate() : 0;
  const unit = d.proratedTotalDays === daysInMonth ? 'días' : 'sesiones';
  const amount = `cobra ${d.proratedDaysCharged ?? '?'} de ${d.proratedTotalDays ?? '?'} ${unit}`;
  const withdrawnHere = !d.studentIsActive && d.studentWithdrawalDate
    && String(d.studentWithdrawalDate).slice(0, 7) === String(d.dueDate || '').slice(0, 7);
  const parts = [];
  const start = String(d.proratedStartDate || '').slice(8, 10);
  if (start && start !== '01') parts.push(`Ingresó el ${ddmm(d.proratedStartDate)}`);
  if (withdrawnHere) parts.push(`Retirado el ${ddmm(d.studentWithdrawalDate)}`);
  return `${parts.length ? parts.join(' · ') : 'Prorrateo'} · ${amount}`;
};
