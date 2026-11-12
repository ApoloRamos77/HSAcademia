import { useEffect, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { api, errorMessage } from '../api/client';
import type { Receipt } from '../api/types';

/**
 * Cobro de sesión en un paso, también sin señal.
 * Cada cobro se guarda primero en el celular con una clave única y luego se envía; el servidor usa esa clave
 * para no cobrar dos veces aunque el envío se repita. Lo cobrado sin señal se envía solo al volver el internet
 * (hasta 7 días después), con la hora real del cobro.
 */
const PREFIX = 'clubio.charges';

export type ChargeMethod = 'Cash' | 'Yape' | 'Plin';

export interface QueuedCharge {
  id: string; // clave única (clientRequestId)
  studentId: string;
  studentName: string;
  eventId: string;
  sessionTitle: string;
  date: string;
  method: ChargeMethod;
  operationNumber?: string | null;
  /** Monto estimado al cobrar (el definitivo lo calcula el servidor). */
  amount: number;
  chargedAt: string;
  /** Motivo por el que el servidor lo rechazó: no se reintenta hasta que el usuario decida. */
  error?: string;
}

export interface ChargeResponse {
  paid: boolean;
  alreadyPaid?: boolean;
  alreadyRegistered?: boolean;
  coveredByCredit?: boolean;
  receipt?: Receipt | null;
}

export type ChargeResult =
  | { state: 'sent'; response: ChargeResponse }
  | { state: 'queued' }
  | { state: 'rejected'; message: string };

let owner: string | null = null;
let lock: Promise<unknown> = Promise.resolve();
let running: Promise<void> | null = null;
const listeners = new Set<(items: QueuedCharge[]) => void>();

const key = () => `${PREFIX}.${owner}.queue`;
export const newChargeId = () => `app-ses-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;

/** La cola es de cada usuario: un celular compartido no mezcla cobradores. */
export function setChargesOwner(userId: string | null) {
  owner = userId;
}

const read = async (): Promise<QueuedCharge[]> => {
  if (!owner) return [];
  const raw = await AsyncStorage.getItem(key());
  return raw ? (JSON.parse(raw) as QueuedCharge[]) : [];
};

function withQueue<T>(change: (items: QueuedCharge[]) => T): Promise<T> {
  const task = lock.then(async () => {
    const items = await read();
    const result = change(items);
    await AsyncStorage.setItem(key(), JSON.stringify(items));
    listeners.forEach(l => l(items));
    return result;
  });
  lock = task.catch(() => {});
  return task;
}

const isOffline = (e: unknown) => !(e as { response?: unknown })?.response;
const isRetryable = (e: unknown) => {
  const status = (e as { response?: { status?: number } })?.response?.status;
  return !status || status === 401 || status === 408 || status === 429 || status >= 500;
};

const post = (c: QueuedCharge) => api.post<ChargeResponse>('/finances/sessions/charge-and-pay', {
  studentId: c.studentId, eventId: c.eventId, method: c.method, operationNumber: c.operationNumber || null,
  clientRequestId: c.id, chargedAt: c.chargedAt, channel: 'app',
}).then(r => r.data);

async function drain() {
  const attempted = new Set<string>();
  for (;;) {
    const item = (await read()).find(i => !i.error && !attempted.has(i.id));
    if (!item) return;
    attempted.add(item.id);
    try {
      await post(item);
      await withQueue(items => { const i = items.findIndex(x => x.id === item.id); if (i >= 0) items.splice(i, 1); });
    } catch (e) {
      if (isRetryable(e)) return;
      const message = errorMessage(e);
      await withQueue(items => { const c = items.find(x => x.id === item.id); if (c) c.error = message; });
    }
  }
}

export function syncCharges(): Promise<void> {
  if (!owner) return Promise.resolve();
  if (!running) running = drain().catch(() => {}).finally(() => { running = null; });
  return running;
}

/** Guarda el cobro en el celular y lo envía: con señal devuelve el recibo; sin señal queda en cola. */
export async function chargeSession(charge: QueuedCharge): Promise<ChargeResult> {
  await withQueue(items => { items.push(charge); });
  try {
    const response = await post(charge);
    await withQueue(items => { const i = items.findIndex(x => x.id === charge.id); if (i >= 0) items.splice(i, 1); });
    return { state: 'sent', response };
  } catch (e) {
    if (isOffline(e) || isRetryable(e)) return { state: 'queued' };
    const message = errorMessage(e);
    // Rechazado con señal (ej. tope del mes): no queda nada pendiente.
    await withQueue(items => { const i = items.findIndex(x => x.id === charge.id); if (i >= 0) items.splice(i, 1); });
    return { state: 'rejected', message };
  }
}

export const discardCharge = (id: string) => withQueue(items => {
  const i = items.findIndex(x => x.id === id);
  if (i >= 0) items.splice(i, 1);
});

export function usePendingCharges(): QueuedCharge[] {
  const [items, setItems] = useState<QueuedCharge[]>([]);
  useEffect(() => {
    let active = true;
    const update = (next: QueuedCharge[]) => { if (active) setItems([...next]); };
    listeners.add(update);
    read().then(update).catch(() => {});
    return () => {
      active = false;
      listeners.delete(update);
    };
  }, []);
  return items;
}
