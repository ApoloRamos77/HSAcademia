import { routeFor } from '../src/notifications/push';

describe('destino de cada notificación', () => {
  test('la familia va a la pestaña de su aviso', () => {
    expect(routeFor('arrival', true)).toEqual({ name: 'Tabs', params: { screen: 'Inicio' } });
    expect(routeFor('proof', true)).toEqual({ name: 'Tabs', params: { screen: 'Pagos' } });
    expect(routeFor('call', true)).toEqual({ name: 'Tabs', params: { screen: 'Agenda' } });
  });

  test('los comunicados abren Comunicados para todos', () => {
    expect(routeFor('announcement', true)).toEqual({ name: 'News' });
    expect(routeFor('announcement', false)).toEqual({ name: 'News' });
  });

  test('un aviso desconocido o sin pantalla no navega', () => {
    expect(routeFor('otro', true)).toBeNull();
    expect(routeFor(undefined, true)).toBeNull();
    expect(routeFor('arrival', false)).toBeNull();
  });
});
