// Catálogos y utilidades del módulo de torneos.
// Las horas del calendario son "hora de pared" de la academia guardadas como UTC ("…Z"):
// se leen y escriben por texto, sin convertir zona horaria.

export const FORMATS = [
  { value: 1, label: 'Liga / campeonato' },
  { value: 2, label: 'Copa (grupos y eliminatorias)' },
  { value: 3, label: 'Relámpago / festival' },
  { value: 4, label: 'Cuadrangular / triangular' },
  { value: 0, label: 'Otro' },
];

export const SCHEDULE_MODES = [
  { value: 1, label: 'Un solo día', hint: 'Relámpago o festival de una jornada' },
  { value: 2, label: 'Rango de fechas', hint: 'Ej. sábados y domingos del 15/11 al 20/12' },
  { value: 3, label: 'Continuo', hint: 'Liga o temporada; el fin es opcional' },
];

export const STATUSES = [
  { value: 1, label: 'Inscripción', badge: 'badge-info' },
  { value: 2, label: 'En curso', badge: 'badge-success' },
  { value: 3, label: 'Finalizado', badge: 'badge-muted' },
  { value: 4, label: 'Cancelado', badge: 'badge-danger' },
];

export const MATCH_STATUSES = [
  { value: 1, label: 'Programado', badge: 'badge-info' },
  { value: 2, label: 'Jugado', badge: 'badge-success' },
  { value: 3, label: 'Postergado', badge: 'badge-warning' },
  { value: 4, label: 'Suspendido', badge: 'badge-danger' },
  { value: 5, label: 'W.O. a favor', badge: 'badge-success' },
  { value: 6, label: 'W.O. en contra', badge: 'badge-danger' },
];

export const DAYS = [
  { value: 1, short: 'Lun' }, { value: 2, short: 'Mar' }, { value: 3, short: 'Mié' },
  { value: 4, short: 'Jue' }, { value: 5, short: 'Vie' }, { value: 6, short: 'Sáb' }, { value: 0, short: 'Dom' },
];
const DAY_NAMES = ['dom', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb'];

export const labelOf = (list, value) => list.find(x => x.value === value) || list[0];
export const errMsg = (e, fallback) => e?.response?.data?.message || fallback;

export const dateOf = iso => (iso ? String(iso).slice(0, 10) : '');
export const timeOf = iso => (iso ? String(iso).slice(11, 16) : '');
export const wall = (date, time) => `${date}T${time}:00.000Z`;
export const addMinutesWall = (iso, minutes) => new Date(new Date(iso).getTime() + minutes * 60000).toISOString();

export const fmtDate = iso => {
  if (!iso) return '';
  const d = dateOf(iso);
  const dow = new Date(d + 'T00:00:00Z').getUTCDay();
  return `${DAY_NAMES[dow]} ${d.slice(8, 10)}/${d.slice(5, 7)}`;
};
const short = iso => `${dateOf(iso).slice(8, 10)}/${dateOf(iso).slice(5, 7)}/${dateOf(iso).slice(2, 4)}`;

export const playDaysText = days => {
  if (!days?.length) return '';
  return DAYS.filter(d => days.includes(d.value)).map(d => d.short).join(', ');
};

/** "Sáb, Dom · 15/11/26 – 20/12/26", "Un día · 15/11/26", "Continuo desde 15/11/26". */
export const scheduleText = t => {
  if (!t.startDate) return 'Sin fechas';
  const days = playDaysText(t.playDays);
  if (t.scheduleMode === 1) return `Un día · ${short(t.startDate)}`;
  if (t.scheduleMode === 3) return `Continuo desde ${short(t.startDate)}${t.endDate ? ` hasta ${short(t.endDate)}` : ''}${days ? ` · ${days}` : ''}`;
  return `${days ? `${days} · ` : ''}${short(t.startDate)} – ${t.endDate ? short(t.endDate) : '…'}`;
};

export const todayLima = () => new Date().toLocaleDateString('en-CA', { timeZone: 'America/Lima' });
