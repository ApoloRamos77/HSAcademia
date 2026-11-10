import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { AppState } from 'react-native';
import * as Keychain from 'react-native-keychain';
import { api, setAuthToken, setUnauthorizedHandler } from '../api/client';
import type { LoginResponse, PublicAcademy, Role } from '../api/types';
import { unregisterDevice } from '../notifications/push';
import { clearPaymentReminders } from '../utils/reminders';

/**
 * Sesión multi-academia.
 * - El token (JWT) se guarda cifrado en el Keystore de Android (react-native-keychain).
 * - La academia elegida se recuerda para el próximo inicio de sesión.
 * - El backend emite el token con el tenant (academyId): todas las llamadas quedan aisladas por academia.
 */
const SESSION_SERVICE = 'hsacademia.session';
const ACADEMY_SERVICE = 'hsacademia.academy';

export interface Session extends LoginResponse {}

interface AuthState {
  ready: boolean;
  session: Session | null;
  academy: PublicAcademy | null;
  selectAcademy: (a: PublicAcademy | null) => Promise<void>;
  login: (emailOrPhone: string, password: string) => Promise<Session>;
  logout: () => Promise<void>;
  markPasswordChanged: () => Promise<void>;
  refreshSession: () => Promise<void>;
  can: (...permissions: string[]) => boolean;
  role: Role | null;
}

const AuthContext = createContext<AuthState | null>(null);

async function readJson<T>(service: string): Promise<T | null> {
  try {
    const item = await Keychain.getGenericPassword({ service });
    return item ? (JSON.parse(item.password) as T) : null;
  } catch {
    return null;
  }
}

async function writeJson(service: string, value: unknown) {
  await Keychain.setGenericPassword('hsacademia', JSON.stringify(value), {
    service,
    accessible: Keychain.ACCESSIBLE.WHEN_UNLOCKED_THIS_DEVICE_ONLY,
  });
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [ready, setReady] = useState(false);
  const [session, setSession] = useState<Session | null>(null);
  const [academy, setAcademy] = useState<PublicAcademy | null>(null);
  const sessionRef = useRef<Session | null>(null);
  sessionRef.current = session;

  // Permisos y datos vigentes: un cambio de roles llega sin volver a iniciar sesión.
  const refreshSession = useCallback(async () => {
    const current = sessionRef.current;
    if (!current || current.requirePasswordChange) return;
    try {
      const { data } = await api.get<LoginResponse>('/auth/me');
      const updated = { ...current, ...data, token: current.token };
      setSession(updated);
      await writeJson(SESSION_SERVICE, updated);
    } catch { /* sin señal: se conservan los permisos conocidos */ }
  }, []);

  useEffect(() => {
    const subscription = AppState.addEventListener('change', state => { if (state === 'active') refreshSession(); });
    return () => subscription.remove();
  }, [refreshSession]);

  // Sesión vencida o revocada: el servidor ya no aceptaría ninguna llamada.
  const clearSession = useCallback(async () => {
    clearPaymentReminders();
    setAuthToken(null);
    setSession(null);
    await Keychain.resetGenericPassword({ service: SESSION_SERVICE }).catch(() => {});
  }, []);

  const logout = useCallback(async () => {
    await unregisterDevice();
    await clearSession();
  }, [clearSession]);

  useEffect(() => {
    setUnauthorizedHandler(() => { clearSession(); });
    (async () => {
      const [s, a] = await Promise.all([readJson<Session>(SESSION_SERVICE), readJson<PublicAcademy>(ACADEMY_SERVICE)]);
      if (a) setAcademy(a);
      if (s?.token && !isExpired(s.token)) {
        setAuthToken(s.token);
        sessionRef.current = s;
        setSession(s);
        refreshSession();
      }
      setReady(true);
    })();
    return () => setUnauthorizedHandler(null);
  }, [clearSession, refreshSession]);

  const selectAcademy = useCallback(async (a: PublicAcademy | null) => {
    setAcademy(a);
    if (a) await writeJson(ACADEMY_SERVICE, a);
    else await Keychain.resetGenericPassword({ service: ACADEMY_SERVICE }).catch(() => {});
  }, []);

  const login = useCallback(async (emailOrPhone: string, password: string) => {
    const { data } = await api.post<LoginResponse>('/auth/login', {
      emailOrPhone: emailOrPhone.trim(),
      password,
      academyId: academy?.tenantId ?? academy?.id ?? null,
    });
    if (data.role === 'SuperAdmin') {
      throw Object.assign(new Error('superadmin'), {
        response: { data: { message: 'La cuenta de plataforma se administra desde la web.' } },
      });
    }
    setAuthToken(data.token);
    setSession(data);
    await writeJson(SESSION_SERVICE, data);
    return data;
  }, [academy]);

  const markPasswordChanged = useCallback(async () => {
    if (!session) return;
    const updated = { ...session, requirePasswordChange: false };
    setSession(updated);
    await writeJson(SESSION_SERVICE, updated);
  }, [session]);

  const value = useMemo<AuthState>(() => ({
    ready,
    session,
    academy,
    selectAcademy,
    login,
    logout,
    markPasswordChanged,
    role: session?.role ?? null,
    refreshSession,
    can: (...permissions: string[]) => session?.role === 'AcademyAdmin' || permissions.some(p => session?.permissions?.includes(p)),
  }), [ready, session, academy, selectAcademy, login, logout, markPasswordChanged, refreshSession]);

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de AuthProvider');
  return ctx;
}

/** Lee la expiración del JWT (sin validar firma: eso lo hace el servidor). */
function isExpired(token: string): boolean {
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const atobFn = (globalThis as any).atob as ((s: string) => string) | undefined;
    const json = JSON.parse(atobFn ? atobFn(payload) : decodeBase64(payload));
    return typeof json.exp === 'number' && json.exp * 1000 < Date.now();
  } catch {
    return false;
  }
}

function decodeBase64(input: string): string {
  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/=';
  let str = input.replace(/=+$/, '');
  let output = '';
  for (let bc = 0, bs = 0, buffer, i = 0; (buffer = str.charAt(i++)); ) {
    const idx = chars.indexOf(buffer);
    if (idx === -1) continue;
    bs = bc % 4 ? bs * 64 + idx : idx;
    if (bc++ % 4) output += String.fromCharCode(255 & (bs >> ((-2 * bc) & 6)));
  }
  return output;
}
