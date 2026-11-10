/**
 * Recordatorios de pago locales (hasta la app 1.0.11).
 * Desde la 1.0.12 los avisos de pago los envía el servidor como notificación push, también cuando la app no se abre.
 * Aquí solo se cancelan las alarmas que haya dejado programadas una versión anterior, para no avisar dos veces.
 */
import { NativeModules } from 'react-native';

const native = NativeModules.ClubioReminders as { cancelAll(): Promise<void> } | undefined;

/** Cancela los recordatorios locales: al iniciar la app y al cerrar sesión. Nunca lanza error. */
export async function clearPaymentReminders(): Promise<void> {
  try { await native?.cancelAll(); } catch { /* sin efecto */ }
}
