import axios, { AxiosError } from 'axios';
import { API_URL } from '../config';

/** Cliente HTTP único. El token se inyecta desde AuthContext con setAuthToken(). */
export const api = axios.create({
  baseURL: API_URL,
  timeout: 20000,
  headers: { Accept: 'application/json' },
});

let token: string | null = null;
let onUnauthorized: (() => void) | null = null;

export function setAuthToken(value: string | null) {
  token = value;
}

/** Se llama cuando la API responde 401 (token vencido o revocado): cierra la sesión. */
export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler;
}

api.interceptors.request.use(config => {
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use(
  res => res,
  (err: AxiosError) => {
    const isLogin = err.config?.url?.includes('/auth/login');
    if (err.response?.status === 401 && !isLogin) onUnauthorized?.();
    return Promise.reject(err);
  },
);

/** Mensaje legible para el usuario a partir de un error de la API. */
export function errorMessage(err: unknown, fallback = 'Ocurrió un error. Inténtalo nuevamente.'): string {
  const e = err as AxiosError<{ message?: string; title?: string }>;
  if (e?.response?.data?.message) return e.response.data.message;
  if (e?.response?.status === 403) return 'No tienes permiso para realizar esta acción.';
  if (e?.response?.status === 429) return 'Demasiados intentos. Espera un minuto e inténtalo nuevamente.';
  if (e?.code === 'ECONNABORTED') return 'La conexión tardó demasiado. Revisa tu internet.';
  if (e?.message === 'Network Error') return 'Sin conexión con el servidor. Revisa tu internet.';
  return fallback;
}
