import React from 'react';
import { NavigationContainer, DefaultTheme } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import {
  CalendarDays, ClipboardCheck, CreditCard, Home, Inbox, LayoutDashboard, Menu, QrCode, TrendingUp, Users,
} from 'lucide-react-native';
import { useAuth } from '../auth/AuthContext';
import { Loading } from '../components/ui';
import { colors } from '../theme';

import AcademySelectScreen from '../screens/auth/AcademySelectScreen';
import LoginScreen from '../screens/auth/LoginScreen';
import ChangePasswordScreen from '../screens/auth/ChangePasswordScreen';
import { FamilyProvider } from '../screens/family/FamilyContext';
import FamilyHomeScreen from '../screens/family/FamilyHomeScreen';
import PaymentsScreen from '../screens/family/PaymentsScreen';
import SendProofScreen from '../screens/family/SendProofScreen';
import AgendaScreen from '../screens/family/AgendaScreen';
import ProgressScreen from '../screens/family/ProgressScreen';
import CardScreen from '../screens/family/CardScreen';
import CoachTodayScreen from '../screens/staff/CoachTodayScreen';
import AttendanceScreen from '../screens/staff/AttendanceScreen';
import ScanScreen from '../screens/staff/ScanScreen';
import StudentsScreen from '../screens/staff/StudentsScreen';
import StudentDetailScreen from '../screens/staff/StudentDetailScreen';
import StudentFormScreen from '../screens/staff/StudentFormScreen';
import FieldPaymentScreen from '../screens/staff/FieldPaymentScreen';
import MyCashScreen from '../screens/staff/MyCashScreen';
import EvaluationScreen from '../screens/staff/EvaluationScreen';
import StaffAgendaScreen from '../screens/staff/StaffAgendaScreen';
import MatchScreen from '../screens/staff/MatchScreen';
import AdminHomeScreen from '../screens/admin/AdminHomeScreen';
import ProofsReviewScreen from '../screens/admin/ProofsReviewScreen';
import NewsScreen from '../screens/shared/NewsScreen';
import MyPaymentsScreen from '../screens/shared/MyPaymentsScreen';
import AccountScreen from '../screens/shared/AccountScreen';

const Stack = createNativeStackNavigator();
const Tab = createBottomTabNavigator();

const navTheme = { ...DefaultTheme, colors: { ...DefaultTheme.colors, primary: colors.primaryDark, background: colors.bg, card: colors.card, text: colors.text, border: colors.border } };

const header = {
  headerStyle: { backgroundColor: colors.card },
  headerTintColor: colors.primaryDark,
  headerTitleStyle: { fontWeight: '700' as const, color: colors.text },
  headerShadowVisible: false,
};

/**
 * Opciones de las pestañas. La altura suma el espacio de la barra de navegación del sistema
 * (botones ◀ ● ■ o barra de gestos): desde Android 15 la app se dibuja debajo de ella.
 */
function useTabOptions() {
  const insets = useSafeAreaInsets();
  const bottom = Math.max(insets.bottom, 8);
  return {
    ...header,
    tabBarActiveTintColor: colors.primaryDark,
    tabBarInactiveTintColor: colors.textMuted,
    tabBarLabelStyle: { fontSize: 12, fontWeight: '600' as const },
    tabBarStyle: { height: 56 + bottom, paddingBottom: bottom, paddingTop: 6, borderTopColor: colors.border },
  };
}

const icon = (Icon: React.ComponentType<{ color: string; size: number }>) =>
  ({ color, size }: { color: string; size: number }) => <Icon color={color} size={size} />;

// ── Familia (apoderado / alumno) ─────────────────────────────────
function FamilyTabs() {
  const tabOptions = useTabOptions();
  return (
    <Tab.Navigator screenOptions={tabOptions}>
      <Tab.Screen name="Inicio" component={FamilyHomeScreen} options={{ tabBarIcon: icon(Home), title: 'Inicio' }} />
      <Tab.Screen name="Pagos" component={PaymentsScreen} options={{ tabBarIcon: icon(CreditCard) }} />
      <Tab.Screen name="Agenda" component={AgendaScreen} options={{ tabBarIcon: icon(CalendarDays) }} />
      <Tab.Screen name="Progreso" component={ProgressScreen} options={{ tabBarIcon: icon(TrendingUp) }} />
      <Tab.Screen name="Carné" component={CardScreen} options={{ tabBarIcon: icon(QrCode) }} />
    </Tab.Navigator>
  );
}

function FamilyStack() {
  return (
    <FamilyProvider>
      <Stack.Navigator screenOptions={header}>
        <Stack.Screen name="Tabs" component={FamilyTabs} options={{ headerShown: false }} />
        <Stack.Screen name="SendProof" component={SendProofScreen} options={{ title: 'Enviar comprobante' }} />
        <Stack.Screen name="News" component={NewsScreen} options={{ title: 'Comunicados' }} />
        <Stack.Screen name="Account" component={AccountScreen} options={{ title: 'Mi cuenta' }} />
        <Stack.Screen name="ChangePassword" component={ChangePasswordScreen} options={{ title: 'Cambiar contraseña' }} />
      </Stack.Navigator>
    </FamilyProvider>
  );
}

// ── Personal (entrenador) y administrador ────────────────────────
function StaffTabs() {
  const { role, can } = useAuth();
  const isAdmin = role === 'AcademyAdmin';
  const tabOptions = useTabOptions();
  return (
    <Tab.Navigator screenOptions={tabOptions}>
      {isAdmin && <Tab.Screen name="Resumen" component={AdminHomeScreen} options={{ tabBarIcon: icon(LayoutDashboard) }} />}
      <Tab.Screen name="Hoy" component={CoachTodayScreen} options={{ tabBarIcon: icon(ClipboardCheck), title: isAdmin ? 'Asistencia' : 'Hoy' }} />
      {isAdmin
        ? <Tab.Screen name="Comprobantes" component={ProofsReviewScreen} options={{ tabBarIcon: icon(Inbox) }} />
        : <Tab.Screen name="Escanear" component={ScanScreen} options={{ tabBarIcon: icon(QrCode), title: 'Escanear QR' }} />}
      {can('view_students') && <Tab.Screen name="Alumnos" component={StudentsScreen} options={{ tabBarIcon: icon(Users) }} />}
      {!isAdmin && <Tab.Screen name="Agenda" component={StaffAgendaScreen} options={{ tabBarIcon: icon(CalendarDays) }} />}
      <Tab.Screen name="Más" component={AccountScreen} options={{ tabBarIcon: icon(Menu), title: 'Más' }} />
    </Tab.Navigator>
  );
}

function StaffStack() {
  return (
    <Stack.Navigator screenOptions={header}>
      <Stack.Screen name="Tabs" component={StaffTabs} options={{ headerShown: false }} />
      <Stack.Screen name="Attendance" component={AttendanceScreen} options={{ title: 'Asistencia' }} />
      <Stack.Screen name="Scan" component={ScanScreen} options={{ title: 'Escanear carné' }} />
      <Stack.Screen name="StudentDetail" component={StudentDetailScreen} options={{ title: 'Alumno' }} />
      <Stack.Screen name="StudentForm" component={StudentFormScreen} options={{ title: 'Alumno' }} />
      <Stack.Screen name="FieldPayment" component={FieldPaymentScreen} options={{ title: 'Cobrar' }} />
      <Stack.Screen name="MyCash" component={MyCashScreen} options={{ title: 'Mi caja' }} />
      <Stack.Screen name="Evaluation" component={EvaluationScreen} options={{ title: 'Evaluación deportiva' }} />
      <Stack.Screen name="Agenda" component={StaffAgendaScreen} options={{ title: 'Agenda y partidos' }} />
      <Stack.Screen name="Match" component={MatchScreen} options={{ title: 'Partido' }} />
      <Stack.Screen name="News" component={NewsScreen} options={{ title: 'Comunicados' }} />
      <Stack.Screen name="MyPayments" component={MyPaymentsScreen} options={{ title: 'Mis pagos' }} />
      <Stack.Screen name="ChangePassword" component={ChangePasswordScreen} options={{ title: 'Cambiar contraseña' }} />
    </Stack.Navigator>
  );
}

// ── Raíz ─────────────────────────────────────────────────────────
export default function RootNavigator() {
  const { ready, session, academy } = useAuth();
  if (!ready) return <Loading />;

  return (
    <NavigationContainer theme={navTheme}>
      {!session ? (
        <Stack.Navigator screenOptions={{ headerShown: false }} initialRouteName={academy ? 'Login' : 'AcademySelect'}>
          <Stack.Screen name="AcademySelect" component={AcademySelectScreen} />
          <Stack.Screen name="Login" component={LoginScreen} />
        </Stack.Navigator>
      ) : session.requirePasswordChange ? (
        <Stack.Navigator screenOptions={{ headerShown: false }}>
          <Stack.Screen name="ForcedPassword" component={ChangePasswordScreen} initialParams={{ forced: true }} />
        </Stack.Navigator>
      ) : session.role === 'Guardian' || session.role === 'Student' ? (
        <FamilyStack />
      ) : (
        <StaffStack />
      )}
    </NavigationContainer>
  );
}
