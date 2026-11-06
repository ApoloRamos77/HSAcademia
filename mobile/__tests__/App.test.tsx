/**
 * Pruebas de utilidades puras (no requieren módulos nativos).
 */
import { eventParts, initials, money, shortDay } from '../src/utils/format';

describe('format', () => {
  test('money usa soles con separador de miles y 2 decimales', () => {
    expect(money(1234.5)).toBe('S/ 1,234.50');
    expect(money(0)).toBe('S/ 0.00');
    expect(money(-80)).toBe('-S/ 80.00');
  });

  test('las horas de eventos se leen como "hora de pared" (partes UTC)', () => {
    // Las 18:00 de Lima se guardan como 18:00Z (misma convención que la web)
    expect(eventParts('2026-11-10T18:00:00Z')).toEqual({ day: '2026-11-10', time: '18:00' });
  });

  test('fechas calendario no cambian de día por la zona horaria', () => {
    expect(shortDay('2026-11-10')).toBe('mar 10 nov');
  });

  test('iniciales', () => {
    expect(initials('Rosa Quispe')).toBe('RQ');
    expect(initials('')).toBe('?');
  });
});
