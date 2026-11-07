/**
 * Configuración de la app.
 *
 * - Release (APK/AAB firmado): usa SIEMPRE la API de producción.
 * - Debug: por defecto el emulador de Android Studio (10.0.2.2 = "localhost" de tu PC).
 *   Para probar en un teléfono físico por WiFi, cambia DEV_API_URL por la IP local de tu PC,
 *   p. ej. 'http://192.168.1.50:5000/api' (la API debe escuchar en 0.0.0.0, no solo localhost).
 */
const PROD_API_URL = 'https://softsport77-api.scuiaw.easypanel.host/api';
const DEV_API_URL = 'http://10.0.2.2:5000/api';

export const API_URL = __DEV__ ? DEV_API_URL : PROD_API_URL;

/** Origen de archivos servidos por la API (comprobantes): la URL sin "/api". */
export const FILES_URL = API_URL.replace(/\/api\/?$/, '');

export const APP_NAME = 'Clubio';
