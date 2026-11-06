// Versión visible de la app. Mantener sincronizada con versionName en android/app/build.gradle.
const pkg = require('../../package.json');

export function getVersion(): string {
  return pkg.version ?? '1.0.0';
}
