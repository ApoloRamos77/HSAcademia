// Módulos nativos de Firebase: no existen en el entorno de pruebas.
jest.mock('@react-native-firebase/app', () => ({ getApp: jest.fn(() => ({})) }));
jest.mock('@react-native-firebase/messaging', () => ({
  getMessaging: jest.fn(() => ({})),
  getToken: jest.fn(() => Promise.resolve('token-de-prueba')),
  getInitialNotification: jest.fn(() => Promise.resolve(null)),
  onMessage: jest.fn(() => () => {}),
  onNotificationOpenedApp: jest.fn(() => () => {}),
  onTokenRefresh: jest.fn(() => () => {}),
}));
jest.mock('@react-navigation/native', () => ({ createNavigationContainerRef: jest.fn(() => ({ isReady: () => false })) }));
