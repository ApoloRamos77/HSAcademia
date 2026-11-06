import React from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { Building2, ChevronRight, KeyRound, LogOut, Mail, Megaphone, QrCode, Wallet, CalendarDays } from 'lucide-react-native';
import { getVersion } from '../../utils/appInfo';
import { useAuth } from '../../auth/AuthContext';
import { Card, Row, Screen } from '../../components/ui';
import { colors, font, radius, spacing } from '../../theme';
import { initials } from '../../utils/format';

const ROLE_LABEL: Record<string, string> = { AcademyAdmin: 'Administrador', Staff: 'Personal / Entrenador', Guardian: 'Apoderado', Student: 'Alumno' };

/** Mi cuenta + menú "Más": accesos secundarios según el rol y cierre de sesión. */
export default function AccountScreen({ navigation }: any) {
  const { session, logout, role, selectAcademy } = useAuth();
  const isStaff = role === 'Staff' || role === 'AcademyAdmin';

  const items = [
    isStaff && { icon: <QrCode color={colors.primaryDark} />, label: 'Escanear carné', go: () => navigation.navigate('Scan') },
    role === 'AcademyAdmin' && { icon: <CalendarDays color={colors.primaryDark} />, label: 'Agenda y partidos', go: () => navigation.navigate('Agenda') },
    { icon: <Megaphone color={colors.primaryDark} />, label: 'Comunicados', go: () => navigation.navigate('News') },
    isStaff && { icon: <Wallet color={colors.primaryDark} />, label: 'Mis pagos', go: () => navigation.navigate('MyPayments') },
    { icon: <KeyRound color={colors.primaryDark} />, label: 'Cambiar contraseña', go: () => navigation.navigate('ChangePassword') },
  ].filter(Boolean) as { icon: React.ReactNode; label: string; go: () => void }[];

  const confirmLogout = () => Alert.alert('Cerrar sesión', '¿Quieres salir de tu cuenta?', [
    { text: 'Cancelar', style: 'cancel' },
    { text: 'Salir', style: 'destructive', onPress: logout },
  ]);

  const switchAcademy = () => Alert.alert('Cambiar de academia', 'Se cerrará tu sesión para que ingreses a otra academia.', [
    { text: 'Cancelar', style: 'cancel' },
    { text: 'Continuar', onPress: async () => { await logout(); await selectAcademy(null); } },
  ]);

  return (
    <Screen>
      <Card>
        <Row>
          <View style={styles.avatar}><Text style={styles.avatarText}>{initials(session?.fullName)}</Text></View>
          <View style={{ flex: 1 }}>
            <Text style={font.h2}>{session?.fullName}</Text>
            <Text style={font.small}>{ROLE_LABEL[role ?? ''] ?? role}</Text>
          </View>
        </Row>
        <View style={{ marginTop: spacing.md, gap: 6 }}>
          <Row><Building2 color={colors.textMuted} size={16} /><Text style={font.body}>{session?.academyName}</Text></Row>
          <Row><Mail color={colors.textMuted} size={16} /><Text style={font.body}>{session?.email}</Text></Row>
        </View>
      </Card>

      <Card style={{ padding: 0 }}>
        {items.map((it, i) => (
          <Pressable key={it.label} onPress={it.go} android_ripple={{ color: colors.surface }}
            style={[styles.item, i < items.length - 1 && styles.itemBorder]}>
            {it.icon}
            <Text style={[font.body, { flex: 1, color: colors.text }]}>{it.label}</Text>
            <ChevronRight color={colors.textMuted} />
          </Pressable>
        ))}
      </Card>

      <Card style={{ padding: 0 }}>
        <Pressable onPress={switchAcademy} style={[styles.item, styles.itemBorder]} android_ripple={{ color: colors.surface }}>
          <Building2 color={colors.textSecondary} />
          <Text style={[font.body, { flex: 1 }]}>Cambiar de academia</Text>
        </Pressable>
        <Pressable onPress={confirmLogout} style={styles.item} android_ripple={{ color: colors.dangerBg }}>
          <LogOut color={colors.danger} />
          <Text style={[font.body, { flex: 1, color: colors.danger, fontWeight: '600' }]}>Cerrar sesión</Text>
        </Pressable>
      </Card>

      <Text style={[font.tiny, { textAlign: 'center' }]}>Clubio · versión {getVersion()}</Text>
    </Screen>
  );
}

const styles = StyleSheet.create({
  avatar: { width: 56, height: 56, borderRadius: radius.pill, backgroundColor: colors.primaryDark, alignItems: 'center', justifyContent: 'center' },
  avatarText: { color: colors.white, fontSize: 20, fontWeight: '700' },
  item: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg },
  itemBorder: { borderBottomWidth: StyleSheet.hairlineWidth, borderBottomColor: colors.border },
});
