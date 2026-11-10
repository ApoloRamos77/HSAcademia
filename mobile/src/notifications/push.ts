/**
 * Notificaciones push (Firebase Cloud Messaging).
 * El servidor envía cada aviso; aquí se registra el celular y se decide a qué pantalla lleva cada uno.
 */
import { PermissionsAndroid, Platform } from 'react-native';
import { getApp } from '@react-native-firebase/app';
import {
  getMessaging, getToken, getInitialNotification, onMessage, onNotificationOpenedApp, onTokenRefresh,
} from '@react-native-firebase/messaging';
import { createNavigationContainerRef } from '@react-navigation/native';
import { api } from '../api/client';

type RemoteMessage = Parameters<Parameters<typeof onMessage>[1]>[0];

export const navigationRef = createNavigationContainerRef<any>();

export interface PushNotice { title: string; body: string; type: string }

const messaging = () => getMessaging(getApp());
let registeredToken: string | null = null;
let pendingRoute: { name: string; params?: object } | null = null;

const familyTab = (screen: string) => ({ name: 'Tabs', params: { screen } });

/** Pantalla a la que lleva cada aviso. */
export function routeFor(type: string | undefined, isFamily: boolean): { name: string; params?: object } | null {
  if (type === 'announcement') return { name: 'News' };
  if (!isFamily) return null;
  if (type === 'arrival') return familyTab('Inicio');
  if (type === 'proof') return familyTab('Pagos');
  if (type === 'call') return familyTab('Agenda');
  return null;
}

/** Abre la pantalla del aviso; si la navegación aún no está lista, queda pendiente. */
export function openRoute(route: { name: string; params?: object } | null) {
  if (!route) return;
  if (navigationRef.isReady()) navigationRef.navigate(route.name, route.params);
  else pendingRoute = route;
}

export function flushPendingRoute() {
  const route = pendingRoute;
  pendingRoute = null;
  openRoute(route);
}

async function ensurePermission(): Promise<boolean> {
  if (Platform.OS !== 'android' || Platform.Version < 33) return true;
  const permission = PermissionsAndroid.PERMISSIONS.POST_NOTIFICATIONS;
  if (await PermissionsAndroid.check(permission)) return true;
  return (await PermissionsAndroid.request(permission)) === PermissionsAndroid.RESULTS.GRANTED;
}

async function sendToken(token: string) {
  registeredToken = token;
  await api.post('/devices', { token, platform: Platform.OS }).catch(() => {});
}

/** Registra el celular para la cuenta actual. Devuelve cómo dejar de escuchar cambios del identificador. */
export async function registerDevice(): Promise<() => void> {
  if (!(await ensurePermission())) return () => {};
  await sendToken(await getToken(messaging()));
  return onTokenRefresh(messaging(), sendToken);
}

/** Al cerrar sesión el celular deja de recibir avisos de esa cuenta. Nunca impide salir. */
export async function unregisterDevice() {
  const token = registeredToken;
  registeredToken = null;
  if (token) await api.post('/devices/unregister', { token }, { timeout: 3000 }).catch(() => {});
}

const toNotice = (m: RemoteMessage): PushNotice | null => m.notification
  ? { title: m.notification.title ?? 'Clubio', body: m.notification.body ?? '', type: String(m.data?.type ?? '') }
  : null;

/** Escucha los avisos mientras hay sesión. Con la app abierta se muestran dentro de ella. */
export function listenForNotices(isFamily: boolean, onForeground: (notice: PushNotice) => void): () => void {
  const open = (m: RemoteMessage | null) => { if (m) openRoute(routeFor(String(m.data?.type ?? ''), isFamily)); };
  const stopForeground = onMessage(messaging(), async m => { const notice = toNotice(m); if (notice) onForeground(notice); });
  const stopOpened = onNotificationOpenedApp(messaging(), open);
  getInitialNotification(messaging()).then(open).catch(() => {});
  return () => { stopForeground(); stopOpened(); };
}
