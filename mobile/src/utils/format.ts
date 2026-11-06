/**
 * Formatos de fecha y dinero (es-PE), sin depender de librerías externas.
 *
 * Convención del sistema (igual que la web):
 * - Horas de EVENTOS (startTime de calendario/convocatorias): se guardan como "hora de pared" con sufijo Z
 *   → se muestran con las partes UTC (las 18:00 de Lima vienen como 18:00Z).
 * - Fechas tipo "YYYY-MM-DD": fecha calendario, sin zona.
 * - Instantes reales (pagos, comunicados): UTC real → se muestran en hora local del teléfono.
 */
const DAYS = ['domingo', 'lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado'];
const DAYS_SHORT = ['dom', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb'];
const MONTHS = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];
const MONTHS_SHORT = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'];

const pad = (n: number) => String(n).padStart(2, '0');

export function money(v: number | null | undefined): string {
  const n = Number(v ?? 0);
  const [int, dec] = Math.abs(n).toFixed(2).split('.');
  return `${n < 0 ? '-' : ''}S/ ${int.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}.${dec}`;
}

/** "YYYY-MM-DD" → Date local a mediodía (evita saltos de día por zona horaria). */
export function parseDay(s: string): Date {
  const [y, m, d] = s.slice(0, 10).split('-').map(Number);
  return new Date(y, m - 1, d, 12, 0, 0);
}

/** Fecha de hoy "YYYY-MM-DD" en hora local del teléfono. */
export function todayISO(): string {
  const d = new Date();
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** "lunes 10 de noviembre" para una fecha calendario. */
export function longDay(s: string): string {
  const d = parseDay(s);
  return `${DAYS[d.getDay()]} ${d.getDate()} de ${MONTHS[d.getMonth()]}`;
}

/** "lun 10 nov" para una fecha calendario. */
export function shortDay(s: string): string {
  const d = parseDay(s);
  return `${DAYS_SHORT[d.getDay()]} ${d.getDate()} ${MONTHS_SHORT[d.getMonth()]}`;
}

/** Instante de evento (hora de pared con Z) → partes. */
export function eventParts(iso: string) {
  const d = new Date(iso);
  const day = `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}`;
  return { day, time: `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}` };
}

export function eventLabel(iso: string): string {
  const { day, time } = eventParts(iso);
  return `${shortDay(day)} · ${time}`;
}

/** Instante real (UTC) → fecha local corta, ej. "10 nov 2026". */
export function instantDate(iso: string): string {
  const d = new Date(iso);
  return `${d.getDate()} ${MONTHS_SHORT[d.getMonth()]} ${d.getFullYear()}`;
}

export function monthName(month: number): string {
  return MONTHS[month - 1] ?? '';
}

/** Saludo según la hora local. */
export function greeting(): string {
  const h = new Date().getHours();
  return h < 12 ? 'Buenos días' : h < 19 ? 'Buenas tardes' : 'Buenas noches';
}

export function firstName(full?: string | null): string {
  return (full ?? '').trim().split(/\s+/)[0] ?? '';
}

export function initials(full?: string | null): string {
  const parts = (full ?? '').trim().split(/\s+/).filter(Boolean);
  return ((parts[0]?.[0] ?? '') + (parts[1]?.[0] ?? '')).toUpperCase() || '?';
}
