// Fechas en hora LOCAL del navegador (Perú).
// No usar `new Date().toISOString().split('T')[0]`: toISOString convierte a UTC y
// después de las 7 p. m. devuelve el día siguiente.

const pad = n => String(n).padStart(2, '0');

/** Fecha local de un Date (o de un string que entienda Date) en formato YYYY-MM-DD. */
export function toLocalISODate(value = new Date()) {
  const d = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

/** Hoy en formato YYYY-MM-DD (hora local). */
export const todayISO = () => toLocalISODate(new Date());
