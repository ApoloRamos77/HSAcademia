import { buildReminders, reminderId } from '../src/utils/reminders';
import type { Debt } from '../src/api/types';

jest.mock('react-native', () => ({ NativeModules: {}, PermissionsAndroid: {}, Platform: { OS: 'android', Version: 34 } }));

const debt = (over: Partial<Debt>): Debt => ({
  id: 'd1', studentId: 's1', studentName: 'Luis Pérez', description: 'Mensualidad Diciembre 2026',
  amount: 100, amountPaid: 0, amountPending: 100, dueDate: '2026-12-05T00:00:00Z', isPaid: false,
  status: 'En Curso', daysOverdue: 0, installments: [], type: 1, ...over,
});

describe('recordatorios de pago', () => {
  const now = new Date(2026, 10, 7, 12, 0).getTime(); // 7 nov 2026, mediodía

  test('mensualidad por vencer: inicio de pago, víspera, día de vencimiento y día siguiente a las 9:00', () => {
    const r = buildReminders([debt({})], now);
    expect(r.map(x => new Date(x.at).getDate())).toEqual([1, 4, 5, 6]);
    expect(r.every(x => new Date(x.at).getHours() === 9)).toBe(true);
    expect(r[1].title).toMatch(/mañana/);
    expect(r[3].body).toMatch(/administración/);
  });

  test('omite fechas pasadas y deudas pagadas', () => {
    const r = buildReminders([debt({ dueDate: '2026-11-08T00:00:00Z' }), debt({ id: 'd2', isPaid: true })], now);
    // 8 nov: el aviso de inicio (4 nov) ya pasó → víspera (7 nov 9:00) también pasó → quedan el día y el siguiente
    expect(r.map(x => new Date(x.at).getDate())).toEqual([8, 9]);
  });

  test('vencida sin regularizar: avisos de acercarse a administración, máximo 3', () => {
    const r = buildReminders([debt({ requiresRegularization: true, status: 'Vencido', dueDate: '2026-11-05T00:00:00Z' })], now);
    expect(r).toHaveLength(3);
    expect(r[0].title).toBe('Pago vencido');
    expect(r[0].body).toMatch(/5 de noviembre/);
  });

  test('con varios hijos se indica de quién es la deuda', () => {
    const r = buildReminders([debt({}), debt({ id: 'd2', studentId: 's2', studentName: 'Tomás Pérez' })], now);
    expect(r.some(x => x.body.includes('de Tomás'))).toBe(true);
  });

  test('ids estables y distintos por aviso', () => {
    expect(reminderId('a:open')).toBe(reminderId('a:open'));
    expect(reminderId('a:open')).not.toBe(reminderId('a:today'));
    expect(reminderId('x')).toBeGreaterThan(0);
  });
});
