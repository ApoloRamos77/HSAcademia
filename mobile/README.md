# Clubio — App móvil

React Native **CLI** (sin Expo), compilada con **Android Studio**. Multi-academia: el usuario elige su
academia, inicia sesión y ve las funciones de su rol.

| Rol | Pestañas |
|---|---|
| **Apoderado / Alumno** | Inicio · Pagos (enviar comprobante) · Agenda (convocatorias) · Progreso (asistencia, evaluaciones, partidos) · Carné QR |
| **Entrenador (Staff)** | Hoy (tomar asistencia) · Escanear QR · Alumnos* · Agenda (resultados de partido*) · Más |
| **Administrador** | Resumen · Asistencia · Comprobantes (validar pagos) · Alumnos · Más |

\* Según los permisos del cargo en "Roles internos" (`view_students`, `manage_events`).

## Requisitos
- Node.js 22.11 o superior.
- Android Studio, con SDK Platform **37** y build-tools 37 (SDK Manager).
- JDK 17 (el que trae Android Studio sirve).

## Desarrollo
```bash
cd mobile
npm install
npm start                # Metro (déjalo abierto)
```
Luego, en **Android Studio**, abre la carpeta `mobile/android` y pulsa **Run ▶** con un emulador o un teléfono conectado.
También puedes usar `npm run android` en otra terminal.

**API en desarrollo:** `src/config.ts`. En el emulador, `http://10.0.2.2:5000/api` apunta a la API que corre en tu PC.
En un teléfono físico, usa la IP local de tu PC (por ejemplo, `http://192.168.1.50:5000/api`).
El build *release* usa siempre la API de producción.

## Versión de producción (Play Store o APK)
1. Genera la llave de firma **una sola vez** y guárdala fuera del repositorio, con respaldo:
   ```bash
   keytool -genkeypair -v -storetype PKCS12 -keystore hsacademia-upload.keystore -alias hsacademia -keyalg RSA -keysize 2048 -validity 10000
   ```
2. Agrega a `C:\Users\<usuario>\.gradle\gradle.properties`, que no está en el repositorio:
   ```properties
   HSA_UPLOAD_STORE_FILE=C:/ruta/hsacademia-upload.keystore
   HSA_UPLOAD_STORE_PASSWORD=********
   HSA_UPLOAD_KEY_ALIAS=hsacademia
   HSA_UPLOAD_KEY_PASSWORD=********
   ```
3. Antes de cada publicación, sube `versionCode` en `android/app/build.gradle`.
4. En Android Studio, usa **Build → Generate Signed App Bundle / APK** (o `cd android && ./gradlew bundleRelease`).
   - Para Play Store: `android/app/build/outputs/bundle/release/app-release.aab`.
   - Para instalar directo: `./gradlew assembleRelease`, que genera `app-release.apk`.

> ⚠️ Si pierdes la keystore no podrás publicar actualizaciones de la app en Play Store.

## Ícono de la app
En Android Studio: clic derecho en `app` → **New → Image Asset**, y elige `src/assets/logo.png`.
