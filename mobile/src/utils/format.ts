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

/** Instante real (UTC) → hora local, ej. "18:05". */
export function clockTime(iso: string): string {
  const d = new Date(iso);
  return `${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function monthName(month: number): string {
  return MONTHS[month - 1] ?? '';
}

/** Plazo para marcar asistencia: por qué no se puede ahora, o null. Compara en "hora de pared", como los eventos. */
export function markingBlock(s: { markFrom?: string | null; markUntil?: string | null }): string | null {
  const d = new Date();
  const now = `${todayISO()}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  if (s.markFrom && now < s.markFrom.slice(0, 16)) return `La lista abre a las ${s.markFrom.slice(11, 16)}, 15 minutos antes del inicio.`;
  if (s.markUntil && now >= s.markUntil.slice(0, 16)) return 'El plazo para marcar venció. Solo un administrador puede modificarla.';
  return null;
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

// ── Fechas escritas a mano (DD/MM/AAAA) ────────────────────────────

/** Aplica la máscara DD/MM/AAAA mientras se escribe (solo dígitos). */
export function maskDate(input: string): string {
  const d = input.replace(/\D/g, '').slice(0, 8);
  if (d.length <= 2) return d;
  if (d.length <= 4) return `${d.slice(0, 2)}/${d.slice(2)}`;
  return `${d.slice(0, 2)}/${d.slice(2, 4)}/${d.slice(4)}`;
}

/** "DD/MM/AAAA" → "YYYY-MM-DD"; null si está vacío; 'invalid' si no es una fecha real. */
export function parseDmy(value: string): string | null | 'invalid' {
  const v = value.trim();
  if (!v) return null;
  const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(v);
  if (!m) return 'invalid';
  const [, dd, mm, yyyy] = m;
  const d = new Date(Number(yyyy), Number(mm) - 1, Number(dd));
  if (d.getFullYear() !== Number(yyyy) || d.getMonth() !== Number(mm) - 1 || d.getDate() !== Number(dd)) return 'invalid';
  return `${yyyy}-${mm}-${dd}`;
}

/** "YYYY-MM-DD..." → "DD/MM/AAAA" (vacío si no hay fecha). */
export function toDmy(iso?: string | null): string {
  if (!iso) return '';
  const [y, m, d] = iso.slice(0, 10).split('-');
  return y && m && d ? `${d}/${m}/${y}` : '';
}

/** Edad cumplida a la fecha de hoy. */
export function ageFrom(isoDate: string): number {
  const b = parseDay(isoDate);
  const t = new Date();
  let age = t.getFullYear() - b.getFullYear();
  if (t.getMonth() < b.getMonth() || (t.getMonth() === b.getMonth() && t.getDate() < b.getDate())) age--;
  return age;
}

/** Solo dígitos (para celulares). */
export const digitsOnly = (s?: string | null) => (s ?? '').replace(/\D/g, '');
