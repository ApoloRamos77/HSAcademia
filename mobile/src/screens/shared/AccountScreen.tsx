import React, { useState } from 'react';
import { ActivityIndicator, Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { Camera, Building2, ChevronRight, KeyRound, LogOut, Mail, Megaphone, QrCode, Wallet, CalendarDays, Banknote, UserRound } from 'lucide-react-native';
import { getVersion } from '../../utils/appInfo';
import { useAuth } from '../../auth/AuthContext';
import { Card, Row, Screen } from '../../components/ui';
import { colors, font, radius, spacing } from '../../theme';
import { initials } from '../../utils/format';
import { StudentAvatar, pickStudentPhoto, removeMyPhoto, uploadMyPhoto } from '../../components/StudentPhoto';
import { errorMessage } from '../../api/client';

const ROLE_LABEL: Record<string, string> = { AcademyAdmin: 'Administrador', Staff: 'Personal / Entrenador', Guardian: 'Apoderado', Student: 'Alumno' };

/** Mi cuenta + menú "Más": accesos secundarios según el rol y cierre de sesión. */
export default function AccountScreen({ navigation }: any) {
  const { session, logout, role, selectAcademy, can, refreshSession } = useAuth();
  const isTeam = role === 'AcademyAdmin' || role === 'Staff';
  const [photoBusy, setPhotoBusy] = useState(false);

  // El personal pone su propia foto: se ve en el equipo, la asistencia de entrenadores y el menú de la web.
  const run = async (task: () => Promise<unknown>, done: string) => {
    setPhotoBusy(true);
    try { await task(); await refreshSession(); Alert.alert('Foto', done); }
    catch (e) { Alert.alert('Foto', errorMessage(e, 'No se pudo guardar la foto. Inténtalo de nuevo con mejor señal.')); }
    finally { setPhotoBusy(false); }
  };
  const choose = async (source: 'camera' | 'library') => {
    try {
      const asset = await pickStudentPhoto(source);
      if (asset?.uri) await run(() => uploadMyPhoto(asset), 'Foto actualizada.');
    } catch (e: any) { Alert.alert('Foto', e?.message ?? 'No se pudo obtener la foto.'); }
  };
  const changePhoto = () => {
    if (!isTeam || photoBusy) return;
    Alert.alert('Tu foto', 'Se verá en el equipo y en la asistencia de entrenadores.', session?.avatarUrl ? [
      { text: 'Quitar', style: 'destructive', onPress: () => run(removeMyPhoto, 'Foto quitada.') },
      { text: 'Galería', onPress: () => choose('library') },
      { text: 'Tomar foto', onPress: () => choose('camera') },
    ] : [
      { text: 'Cancelar', style: 'cancel' },
      { text: 'Galería', onPress: () => choose('library') },
      { text: 'Tomar foto', onPress: () => choose('camera') },
    ], { cancelable: true });
  };
  const isStaff = role === 'Staff' || role === 'AcademyAdmin';

  const items = [
    { icon: <UserRound color={colors.primaryDark} />, label: 'Mi perfil', go: () => navigation.navigate('Profile') },
    isStaff && can('collections.collect', 'store.sell') && { icon: <Banknote color={colors.primaryDark} />, label: 'Mi caja (cobros por rendir)', go: () => navigation.navigate('MyCash') },
    isStaff && can('attendance.take') && { icon: <QrCode color={colors.primaryDark} />, label: 'Escanear carné', go: () => navigation.navigate('Scan') },
    role === 'AcademyAdmin' && { icon: <CalendarDays color={colors.primaryDark} />, label: 'Agenda y partidos', go: () => navigation.navigate('Agenda') },
    { icon: <Megaphone color={colors.primaryDark} />, label: 'Comunicados', go: () => navigation.navigate('News') },
    isStaff && { icon: <Wallet color={colors.primaryDark} />, label: 'Mis horas y pagos', go: () => navigation.navigate('MyPayments') },
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
          {isTeam ? (
            <Pressable onPress={changePhoto} disabled={photoBusy} hitSlop={6} accessibilityRole="button"
              accessibilityLabel={session?.avatarUrl ? 'Cambiar mi foto' : 'Agregar mi foto'}>
              {session?.avatarUrl
                ? <StudentAvatar photoUrl={session.avatarUrl} name={session.fullName} size={56} />
                : <View style={styles.avatar}><Text style={styles.avatarText}>{initials(session?.fullName)}</Text></View>}
              <View style={styles.photoBadge}>
                {photoBusy ? <ActivityIndicator size="small" color={colors.white} /> : <Camera color={colors.white} size={13} />}
              </View>
            </Pressable>
          ) : (
            <View style={styles.avatar}><Text style={styles.avatarText}>{initials(session?.fullName)}</Text></View>
          )}
          <View style={{ flex: 1 }}>
            <Text style={font.h2}>{session?.fullName}</Text>
            <Text style={font.small}>{session?.isOwner ? 'Propietario' : ROLE_LABEL[role ?? ''] ?? role}</Text>
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
  photoBadge: { position: 'absolute', right: -2, bottom: -2, width: 24, height: 24, borderRadius: radius.pill, backgroundColor: colors.accentDark, borderWidth: 2, borderColor: colors.card, alignItems: 'center', justifyContent: 'center' },
  avatarText: { color: colors.white, fontSize: 20, fontWeight: '700' },
  item: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg },
  itemBorder: { borderBottomWidth: StyleSheet.hairlineWidth, borderBottomColor: colors.border },
});
