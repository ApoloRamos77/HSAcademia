import AsyncStorage from '@react-native-async-storage/async-storage';
import { api } from '../src/api/client';
import { discardPending, enqueue, isOfflineError, pendingFor, saveAndSend, setOfflineOwner } from '../src/offline/attendanceOffline';

jest.mock('@react-native-async-storage/async-storage', () => require('@react-native-async-storage/async-storage/jest/async-storage-mock'));
jest.mock('../src/api/client', () => ({
  api: { post: jest.fn(), get: jest.fn() },
  errorMessage: (e: any) => e?.response?.data?.message ?? 'error',
}));

const post = api.post as jest.Mock;
const base = { eventId: 'e1', date: '2026-11-10', title: 'Entrenamiento', close: false };
const noSignal = Object.assign(new Error('Network Error'), {});
const rejected = (status: number, message = 'rechazado') => Object.assign(new Error('x'), { response: { status, data: { message } } });

beforeEach(async () => {
  post.mockReset();
  await AsyncStorage.clear();
  setOfflineOwner('coach-1');
});

describe('asistencia sin conexión', () => {
  test('con señal envía y vacía la cola', async () => {
    post.mockResolvedValue({});
    const result = await saveAndSend({ ...base, records: { a: { status: 1 } } });
    expect(result).toEqual({ state: 'sent' });
    expect(post).toHaveBeenCalledWith('/attendance/mobile/my-students', expect.objectContaining({ eventId: 'e1', records: [{ studentId: 'a', status: 1 }] }));
    expect(await pendingFor('e1')).toBeNull();
  });

  test('sin señal conserva la marca y la envía después', async () => {
    post.mockRejectedValueOnce(noSignal);
    expect(await saveAndSend({ ...base, records: { a: { status: 1, via: 'QR', markedAt: '2026-11-10T18:00:00Z' } } })).toEqual({ state: 'queued' });
    expect((await pendingFor('e1'))?.records.a.via).toBe('QR');
    post.mockResolvedValue({});
    expect(await saveAndSend({ ...base, records: { b: { status: 2 } } })).toEqual({ state: 'sent' });
    const sent = post.mock.calls[post.mock.calls.length - 1][1].records;
    expect(sent).toEqual(expect.arrayContaining([expect.objectContaining({ studentId: 'a', via: 'QR' }), { studentId: 'b', status: 2 }]));
  });

  test('la falta automática no pisa una marca real', async () => {
    await enqueue({ ...base, records: { a: { status: 1 } } });
    await enqueue({ ...base, records: { a: { status: 2, ifUnmarked: true }, b: { status: 2, ifUnmarked: true } } });
    const item = await pendingFor('e1');
    expect(item?.records.a).toEqual({ status: 1 });
    expect(item?.records.b.ifUnmarked).toBe(true);
  });

  test('una marca real reemplaza a la falta automática', async () => {
    await enqueue({ ...base, records: { a: { status: 2, ifUnmarked: true } } });
    await enqueue({ ...base, records: { a: { status: 3 } } });
    expect((await pendingFor('e1'))?.records.a).toEqual({ status: 3 });
  });

  test('guarda y cierra; una sesión ya cerrada cuenta como enviada', async () => {
    post.mockResolvedValueOnce({}).mockRejectedValueOnce(rejected(409));
    expect(await saveAndSend({ ...base, close: true, records: { a: { status: 1 } } })).toEqual({ state: 'sent' });
    expect(post).toHaveBeenLastCalledWith('/attendance/events/e1/close');
  });

  test('cerrar sin cambios no reenvía marcas', async () => {
    post.mockResolvedValue({});
    await saveAndSend({ ...base, close: true, records: {} });
    expect(post).toHaveBeenCalledTimes(1);
    expect(post).toHaveBeenCalledWith('/attendance/events/e1/close');
  });

  test('un rechazo del servidor queda visible y no bloquea las demás', async () => {
    post.mockRejectedValueOnce(rejected(400, 'Lista cerrada'));
    expect(await saveAndSend({ ...base, records: { a: { status: 1 } } })).toEqual({ state: 'rejected', message: 'Lista cerrada' });
    post.mockResolvedValue({});
    expect(await saveAndSend({ ...base, eventId: 'e2', records: { b: { status: 1 } } })).toEqual({ state: 'sent' });
    expect((await pendingFor('e1'))?.error).toBe('Lista cerrada');
    await discardPending('e1');
    expect(await pendingFor('e1')).toBeNull();
  });

  test('errores temporales se reintentan', async () => {
    post.mockRejectedValueOnce(rejected(503));
    expect(await saveAndSend({ ...base, records: { a: { status: 1 } } })).toEqual({ state: 'queued' });
  });

  test('la cola es de cada usuario', async () => {
    await enqueue({ ...base, records: { a: { status: 1 } } });
    setOfflineOwner('coach-2');
    expect(await pendingFor('e1')).toBeNull();
    setOfflineOwner('coach-1');
    expect(await pendingFor('e1')).not.toBeNull();
  });

  test('distingue falta de señal de una respuesta del servidor', () => {
    expect(isOfflineError(noSignal)).toBe(true);
    expect(isOfflineError(rejected(400))).toBe(false);
  });
});
