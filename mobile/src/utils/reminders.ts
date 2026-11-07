/**
 * Recordatorios de pago (notificaciones locales del teléfono).
 *
 * Política de la academia: la mensualidad se paga del 1 al día de vencimiento (ej. 5).
 * Por cada mensualidad pendiente se programan avisos a las 9:00:
 *   - 4 días antes del vencimiento (inicio del periodo de pago),
 *   - un día antes ("vence mañana"),
 *   - el día del vencimiento ("vence hoy"),
 *   - el día siguiente ("venció: acércate a administración").
 * Si ya está vencida sin regularizar: recordatorios al día siguiente y luego cada 4 días (máx. 3).
 * Las fechas pasadas se omiten. Cada vez que la app carga las deudas se reemplaza toda la programación.
 */
import { NativeModules, PermissionsAndroid, Platform } from 'react-native';
import type { Debt } from '../api/types';
import { parseDay } from './format';

export interface ReminderItem { id: number; title: string; body: string; at: number }

const HOUR = 9;
const DAY_MS = 86_400_000;
const MONTHS = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre'];

/** Id numérico estable por deuda y tipo de aviso (el sistema identifica cada alarma con un entero). */
export function reminderId(key: string): number {
  let h = 0;
  for (let i = 0; i < key.length; i++) h = (Math.imul(31, h) + key.charCodeAt(i)) % 2_147_483_647;
  return Math.abs(h) % 2_000_000_000 || 1;
}

function at(day: Date, offsetDays: number): number {
  const d = new Date(day.getFullYear(), day.getMonth(), day.getDate() + offsetDays, HOUR, 0, 0, 0);
  return d.getTime();
}

const dayLabel = (d: Date) => `${d.getDate()} de ${MONTHS[d.getMonth()]}`;

/** Calcula los recordatorios futuros para las deudas pendientes (función pura: se prueba sin teléfono). */
export function buildReminders(debts: Debt[], now: number = Date.now()): ReminderItem[] {
  const items: ReminderItem[] = [];
  const multipleKids = new Set(debts.map(d => d.studentId)).size > 1;

  for (const d of debts) {
    if (d.isPaid || d.amountPending <= 0) continue;
    const due = parseDay(d.dueDate);
    const who = multipleKids ? ` de ${d.studentName.split(' ')[0]}` : '';
    const what = `${d.description}${who}`;
    const amount = `S/ ${d.amountPending.toFixed(2)}`;
    const isMonthly = d.type === 1;

    if (d.requiresRegularization) {
      // Ya venció y debe regularizarse en administración.
      const base = new Date(now);
      [1, 5, 9].forEach((offset, i) => items.push({
        id: reminderId(`${d.id}:overdue:${i}`),
        title: 'Pago vencido',
        body: `${what} (${amount}) venció el ${dayLabel(due)}. Acércate a administración para regularizar tu pago.`,
        at: at(base, offset),
      }));
      continue;
    }

    if (isMonthly) {
      items.push(
        { id: reminderId(`${d.id}:open`), title: 'Ya puedes pagar tu mensualidad', body: `${what}: ${amount}. Tienes hasta el ${dayLabel(due)}.`, at: at(due, -4) },
        { id: reminderId(`${d.id}:tomorrow`), title: 'Tu mensualidad vence mañana', body: `${what}: ${amount}. Paga por Yape, Plin o transferencia y envía tu comprobante en la app.`, at: at(due, -1) },
        { id: reminderId(`${d.id}:today`), title: 'Hoy vence tu mensualidad', body: `${what}: ${amount}. Evita la mora pagando hoy.`, at: at(due, 0) },
        { id: reminderId(`${d.id}:late`), title: 'Tu pago venció', body: `${what} venció el ${dayLabel(due)}. Acércate a administración para regularizar tu pago.`, at: at(due, 1) },
      );
    } else {
      // Otras deudas (mora, productos): un aviso el día de vencimiento.
      items.push({ id: reminderId(`${d.id}:due`), title: 'Tienes un pago pendiente', body: `${what}: ${amount}.`, at: at(due, 0) });
    }
  }
  // Solo futuros; Android limita las alarmas por app: se priorizan las más próximas.
  return items.filter(i => i.at > now + 60_000).sort((a, b) => a.at - b.at).slice(0, 40);
}

const native = NativeModules.ClubioReminders as
  | { replaceAll(items: ReminderItem[]): Promise<number>; cancelAll(): Promise<void>; notificationsEnabled(): Promise<boolean> }
  | undefined;

let permissionAsked = false;

/** Pide el permiso de notificaciones (Android 13+) una vez por sesión. */
async function ensurePermission(): Promise<boolean> {
  if (Platform.OS !== 'android') return false;
  if (Platform.Version < 33) return true;
  const perm = 'android.permission.POST_NOTIFICATIONS' as any;
  if (await PermissionsAndroid.check(perm)) return true;
  if (permissionAsked) return false;
  permissionAsked = true;
  const res = await PermissionsAndroid.request(perm, {
    title: 'Recordatorios de pago',
    message: 'Te avisaremos cuando tu mensualidad esté por vencer o haya vencido.',
    buttonPositive: 'Permitir',
    buttonNegative: 'Ahora no',
  });
  return res === PermissionsAndroid.RESULTS.GRANTED;
}

/** Reprograma todos los recordatorios a partir de las deudas actuales. Nunca lanza error. */
export async function syncPaymentReminders(debts: Debt[]): Promise<void> {
  if (!native) return;
  try {
    const items = buildReminders(debts);
    if (items.length === 0) { await native.cancelAll(); return; }
    if (!(await ensurePermission())) return;
    await native.replaceAll(items);
  } catch {
    // Los recordatorios son un apoyo: si fallan, la app sigue funcionando.
  }
}

/** Al cerrar sesión (otro usuario puede usar el mismo teléfono). */
export async function clearPaymentReminders(): Promise<void> {
  try { await native?.cancelAll(); } catch { /* sin efecto */ }
}

export const __test = { DAY_MS };
