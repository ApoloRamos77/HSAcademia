import { useEffect, useState } from 'react';
import { AppState } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { api, errorMessage } from '../api/client';
import type { AttendanceCode, RosterStudent, StaffDay, StaffOption, StaffSession, TrainingSession } from '../api/types';

/**
 * Asistencia sin señal: guarda en el celular las listas de hoy y una cola de marcas por enviar.
 * Cada sesión tiene una sola entrada en la cola; las marcas nuevas se fusionan con las anteriores.
 * El envío se reintenta al volver a la app y cada 30 segundos mientras haya algo pendiente.
 */
const PREFIX = 'clubio.attendance';
const RETRY_MS = 30000;
const ROSTER_MAX_AGE_MS = 14 * 24 * 3600 * 1000;

export interface QueuedRecord {
  status: AttendanceCode;
  via?: 'QR';
  markedAt?: string;
  /** Falta automática al cerrar: el servidor la ignora si el alumno ya tiene marca. */
  ifUnmarked?: boolean;
  /** Asistencia de entrenadores: quién dictó (sesión sin entrenador) y quién reemplazó. */
  staffId?: string | null;
  substituteStaffId?: string | null;
}

export interface QueuedSave {
  eventId: string;
  date: string;
  title: string;
  records: Record<string, QueuedRecord>;
  close: boolean;
  /** 'staff': marca de entrenador; sus registros van por id de sesión. */
  kind?: 'staff';
  rev: number;
  /** Motivo por el que el servidor la rechazó: no se reintenta hasta que el usuario decida. */
  error?: string;
}

export type SendResult = { state: 'sent' } | { state: 'queued' } | { state: 'rejected'; message: string };
type Cached<T> = { value: T; cachedAt: number };

let owner: string | null = null;
let revCounter = 0;
let queueLock: Promise<unknown> = Promise.resolve();
let running: Promise<void> | null = null;
const listeners = new Set<(items: QueuedSave[]) => void>();

const key = (name: string) => `${PREFIX}.${owner}.${name}`;
const nextRev = () => Date.now() * 1000 + (revCounter++ % 1000);

/** La cola y las listas son de cada usuario: un celular compartido no mezcla entrenadores. */
export function setOfflineOwner(userId: string | null) {
  owner = userId;
}

/** Sin respuesta del servidor: no hay señal o se agotó la espera. */
export const isOfflineError = (e: unknown) => !(e as { response?: unknown })?.response;

async function readJson<T>(name: string): Promise<T | null> {
  const raw = await AsyncStorage.getItem(key(name));
  return raw ? (JSON.parse(raw) as T) : null;
}

const writeCached = (name: string, value: unknown) =>
  AsyncStorage.setItem(key(name), JSON.stringify({ value, cachedAt: Date.now() } satisfies Cached<unknown>));

const readQueue = async () => (await readJson<QueuedSave[]>('queue')) ?? [];

/** Las escrituras de la cola van una detrás de otra. */
function withQueue<T>(change: (items: QueuedSave[]) => T): Promise<T> {
  const task = queueLock.then(async () => {
    const items = await readQueue();
    const result = change(items);
    await AsyncStorage.setItem(key('queue'), JSON.stringify(items));
    listeners.forEach(l => l(items));
    return result;
  });
  queueLock = task.catch(() => {});
  return task;
}

export const saveSessions = (sessions: TrainingSession[]) => writeCached('sessions', sessions);
export const readSessions = () => readJson<Cached<TrainingSession[]>>('sessions');
export const saveRoster = (eventId: string, roster: RosterStudent[]) => writeCached(`roster.${eventId}`, roster);
export const readRoster = (eventId: string) => readJson<Cached<RosterStudent[]>>(`roster.${eventId}`);
export const fetchRoster = (eventId: string, date: string) =>
  api.get<RosterStudent[]>('/attendance/mobile/my-students', { params: { eventId, date } }).then(r => r.data);

/** Con señal, descarga las listas de hoy para poder abrirlas después en la cancha. */
export async function preloadRosters(sessions: TrainingSession[]) {
  for (const s of sessions.filter(x => !x.attendanceClosed)) {
    try {
      await saveRoster(s.eventId, await fetchRoster(s.eventId, s.date));
    } catch {
      return; // sin señal: se intentará en la próxima carga
    }
  }
  await pruneRosters();
}

async function pruneRosters() {
  const names = (await AsyncStorage.getAllKeys()).filter(k => k.startsWith(`${PREFIX}.${owner}.roster.`));
  const limit = Date.now() - ROSTER_MAX_AGE_MS;
  for (const name of names) {
    const raw = await AsyncStorage.getItem(name);
    if (raw && (JSON.parse(raw) as Cached<unknown>).cachedAt < limit) await AsyncStorage.removeItem(name);
  }
}

export const pendingFor = async (eventId: string) => (await readQueue()).find(i => i.eventId === eventId) ?? null;

export function enqueue(save: { eventId: string; date: string; title: string; records: Record<string, QueuedRecord>; close: boolean; kind?: 'staff' }) {
  return withQueue(items => {
    let item = items.find(i => i.eventId === save.eventId);
    if (!item) {
      item = { eventId: save.eventId, date: save.date, title: save.title, records: {}, close: false, kind: save.kind, rev: 0 };
      items.push(item);
    }
    for (const [studentId, record] of Object.entries(save.records)) {
      const previous = item.records[studentId];
      if (!(record.ifUnmarked && previous && !previous.ifUnmarked)) item.records[studentId] = record;
    }
    item.close = item.close || save.close;
    item.rev = nextRev();
    delete item.error;
    return item.rev;
  });
}

export const discardPending = (eventId: string) => withQueue(items => {
  const index = items.findIndex(i => i.eventId === eventId);
  if (index >= 0) items.splice(index, 1);
});

async function send(item: QueuedSave) {
  if (item.kind === 'staff') {
    await api.post('/staff-attendance/sessions', { records: Object.entries(item.records).map(([eventId, r]) => ({ eventId, ...r })) });
    return;
  }
  const records = Object.entries(item.records).map(([studentId, r]) => ({ studentId, ...r }));
  if (records.length > 0) await api.post('/attendance/mobile/my-students', { date: item.date, eventId: item.eventId, records });
  if (item.close) {
    try {
      await api.post(`/attendance/events/${item.eventId}/close`);
    } catch (e) {
      if ((e as { response?: { status?: number } })?.response?.status !== 409) throw e; // 409: ya estaba cerrada
    }
  }
}

const isRetryable = (e: unknown) => {
  const status = (e as { response?: { status?: number } })?.response?.status;
  return !status || status === 401 || status === 408 || status === 429 || status >= 500;
};

async function drain() {
  const attempted = new Set<number>();
  for (;;) {
    const item = (await readQueue()).find(i => !i.error && !attempted.has(i.rev));
    if (!item) return;
    attempted.add(item.rev);
    try {
      await send(item);
      // Si llegaron marcas nuevas mientras se enviaba, la entrada se queda para el siguiente envío.
      await withQueue(items => {
        const index = items.findIndex(i => i.eventId === item.eventId && i.rev === item.rev);
        if (index >= 0) items.splice(index, 1);
      });
    } catch (e) {
      if (isRetryable(e)) return;
      const message = errorMessage(e);
      await withQueue(items => {
        const current = items.find(i => i.eventId === item.eventId && i.rev === item.rev);
        if (current) current.error = message;
      });
    }
  }
}

export function syncQueue(): Promise<void> {
  if (!owner) return Promise.resolve();
  if (!running) running = drain().catch(() => {}).finally(() => { running = null; });
  return running;
}

/** Guarda primero en el celular y luego intenta enviar: una marca nunca se pierde por falta de señal. */
export async function saveAndSend(save: Parameters<typeof enqueue>[0]): Promise<SendResult> {
  await enqueue(save);
  await syncQueue();
  const left = await pendingFor(save.eventId);
  if (!left) return { state: 'sent' };
  return left.error ? { state: 'rejected', message: left.error } : { state: 'queued' };
}

// ── Asistencia de entrenadores ───────────────────────────────────
export const staffQueueKey = (eventId: string) => `staff:${eventId}`;

/** Cada sesión va en su propia entrada: un rechazo del servidor no arrastra a las demás. */
export const saveStaffMark = (session: StaffSession, record: QueuedRecord) => saveAndSend({
  eventId: staffQueueKey(session.eventId),
  date: session.date,
  title: `Entrenador · ${session.title}`,
  close: false,
  kind: 'staff',
  records: { [session.eventId]: record },
});

export async function fetchStaffDay(date: string): Promise<StaffDay> {
  const [sessions, trainers] = await Promise.all([
    api.get<StaffSession[]>('/staff-attendance/sessions', { params: { date } }).then(r => r.data),
    api.get<StaffOption[]>('/staff-attendance/trainers').then(r => r.data),
  ]);
  const day = { sessions, trainers };
  await writeCached(`staff.${date}`, day);
  return day;
}

export const readStaffDay = (date: string) => readJson<Cached<StaffDay>>(`staff.${date}`);

/** Mantiene el envío automático mientras la sesión del personal esté abierta. */
export function useAttendanceSync(userId: string) {
  useEffect(() => {
    setOfflineOwner(userId);
    syncQueue();
    const subscription = AppState.addEventListener('change', state => { if (state === 'active') syncQueue(); });
    const timer = setInterval(() => { if (AppState.currentState === 'active') syncQueue(); }, RETRY_MS);
    return () => {
      subscription.remove();
      clearInterval(timer);
      setOfflineOwner(null);
    };
  }, [userId]);
}

export function usePendingAttendance(): QueuedSave[] {
  const [items, setItems] = useState<QueuedSave[]>([]);
  useEffect(() => {
    let active = true;
    const update = (next: QueuedSave[]) => { if (active) setItems([...next]); };
    listeners.add(update);
    readQueue().then(update).catch(() => {});
    return () => {
      active = false;
      listeners.delete(update);
    };
  }, []);
  return items;
}
