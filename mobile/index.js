/**
 * @format
 */

import { AppRegistry } from 'react-native';
import { getApp } from '@react-native-firebase/app';
import { getMessaging, setBackgroundMessageHandler } from '@react-native-firebase/messaging';
import App from './App';
import { name as appName } from './app.json';

// Con la app cerrada el sistema muestra la notificación: aquí no hay nada más que hacer.
setBackgroundMessageHandler(getMessaging(getApp()), async () => {});

AppRegistry.registerComponent(appName, () => App);
