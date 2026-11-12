import AsyncStorage from '@react-native-async-storage/async-storage';
import { api } from '../src/api/client';
import { chargeSession, discardCharge, newChargeId, setChargesOwner, syncCharges, type QueuedCharge } from '../src/offline/sessionCharges';

jest.mock('@react-native-async-storage/async-storage', () => require('@react-native-async-storage/async-storage/jest/async-storage-mock'));
jest.mock('../src/api/client', () => ({
  api: { post: jest.fn(), get: jest.fn() },
  errorMessage: (e: any) => e?.response?.data?.message ?? 'error',
}));

const post = api.post as jest.Mock;
const noSignal = new Error('Network Error');
const rejected = (status: number, message = 'rechazado') => Object.assign(new Error('x'), { response: { status, data: { message } } });
const charge = (over: Partial<QueuedCharge> = {}): QueuedCharge => ({
  id: newChargeId(), studentId: 's1', studentName: 'Luis Pérez', eventId: 'e1', sessionTitle: 'Entrenamiento', date: '2026-11-12',
  method: 'Cash', amount: 15, chargedAt: '2026-11-12T18:00:00Z', ...over,
});
const queue = async () => JSON.parse((await AsyncStorage.getItem('clubio.charges.coach-1.queue')) ?? '[]') as QueuedCharge[];

beforeEach(async () => {
  post.mockReset();
  await AsyncStorage.clear();
  setChargesOwner('coach-1');
});

describe('cobro de sesión sin señal', () => {
  test('con señal cobra, devuelve el recibo y no deja nada pendiente', async () => {
    post.mockResolvedValue({ data: { paid: true, receipt: { number: '000123', total: 15 } } });
    const c = charge();
    const result = await chargeSession(c);
    expect(result).toEqual({ state: 'sent', response: { paid: true, receipt: { number: '000123', total: 15 } } });
    expect(post).toHaveBeenCalledWith('/finances/sessions/charge-and-pay', expect.objectContaining({ studentId: 's1', eventId: 'e1', method: 'Cash', clientRequestId: c.id, chargedAt: c.chargedAt }));
    expect(await queue()).toEqual([]);
  });

  test('sin señal queda en el celular y se envía después con la misma clave', async () => {
    post.mockRejectedValueOnce(noSignal);
    const c = charge({ method: 'Yape', operationNumber: 'OP-1' });
    expect(await chargeSession(c)).toEqual({ state: 'queued' });
    expect((await queue()).map(x => x.id)).toEqual([c.id]);
    post.mockResolvedValue({ data: { paid: true } });
    await syncCharges();
    expect(post).toHaveBeenLastCalledWith('/finances/sessions/charge-and-pay', expect.objectContaining({ clientRequestId: c.id, operationNumber: 'OP-1' }));
    expect(await queue()).toEqual([]);
  });

  test('rechazado con señal (ej. tope del mes): avisa y no queda pendiente', async () => {
    post.mockRejectedValueOnce(rejected(400, 'Ya alcanzó el tope del mes'));
    expect(await chargeSession(charge())).toEqual({ state: 'rejected', message: 'Ya alcanzó el tope del mes' });
    expect(await queue()).toEqual([]);
  });

  test('si el servidor lo rechaza al enviarlo después, queda marcado con el motivo y se puede descartar', async () => {
    post.mockRejectedValueOnce(noSignal);
    const c = charge();
    await chargeSession(c);
    post.mockRejectedValueOnce(rejected(400, 'Pasaron más de 7 días'));
    await syncCharges();
    expect((await queue())[0].error).toBe('Pasaron más de 7 días');
    post.mockClear();
    await syncCharges();
    expect(post).not.toHaveBeenCalled(); // no se reintenta solo
    await discardCharge(c.id);
    expect(await queue()).toEqual([]);
  });

  test('un error del servidor (500) se reintenta más tarde', async () => {
    post.mockRejectedValueOnce(noSignal);
    const c = charge();
    await chargeSession(c);
    post.mockRejectedValueOnce(rejected(503));
    await syncCharges();
    expect((await queue())[0].error).toBeUndefined();
  });

  test('cada usuario tiene su propia cola', async () => {
    post.mockRejectedValueOnce(noSignal);
    await chargeSession(charge());
    setChargesOwner('coach-2');
    post.mockResolvedValue({ data: { paid: true } });
    await syncCharges();
    expect(post).toHaveBeenCalledTimes(1); // solo el intento original de coach-1
  });
});
