import React from 'react';
import { Image, StyleSheet, Text, View } from 'react-native';
import QRCode from 'react-native-qrcode-svg';
import { useAuth } from '../../auth/AuthContext';
import { Banner, Empty, Screen } from '../../components/ui';
import { colors, font, radius, shadow, spacing } from '../../theme';
import { useFamily } from './FamilyContext';
import StudentPicker from './StudentPicker';

/**
 * Carné digital del alumno. El QR lo escanea el entrenador para marcar asistencia.
 * Formato compatible con el backend: {"u": <Student.Id>, "a": <tenant>} (el campo "u" es el que usa /attendance/scan).
 */
export function buildStudentQr(studentId: string, academyId?: string | null) {
  return JSON.stringify({ u: studentId, a: academyId ?? undefined });
}

export default function CardScreen() {
  const { session } = useAuth();
  const { student } = useFamily();
  if (!student) return <Screen><Empty text="No hay alumnos vinculados." /></Screen>;

  return (
    <Screen>
      <StudentPicker />
      <View style={styles.card}>
        <View style={styles.header}>
          <Image source={require('../../assets/logo.png')} style={styles.logo} resizeMode="contain" />
          <View style={{ flex: 1 }}>
            <Text style={styles.academy} numberOfLines={1}>{session?.academyName ?? 'Academia'}</Text>
            <Text style={styles.headerSub}>Carné de alumno</Text>
          </View>
        </View>
        <View style={styles.body}>
          <Text style={styles.name}>{student.firstName} {student.lastName}</Text>
          <Text style={font.body}>{student.categoryName} · Sede {student.headquarterName}</Text>
          {student.documentNumber ? <Text style={font.small}>DNI {student.documentNumber}</Text> : null}
          <View style={styles.qr} accessible accessibilityLabel={`Código QR de asistencia de ${student.firstName}`}>
            <QRCode value={buildStudentQr(student.id, session?.academyId)} size={220} color={colors.text} backgroundColor={colors.white} />
          </View>
          <Text style={[font.small, { textAlign: 'center' }]}>Muestra este código al entrenador para registrar tu asistencia.</Text>
        </View>
      </View>
      <Banner tone="info" text="Consejo: sube el brillo de la pantalla para que el código se lea más rápido." />
    </Screen>
  );
}

const styles = StyleSheet.create({
  card: { backgroundColor: colors.card, borderRadius: radius.lg, overflow: 'hidden', borderWidth: 1, borderColor: colors.border, ...shadow },
  header: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, backgroundColor: colors.primaryDark, padding: spacing.lg },
  logo: { width: 44, height: 44, backgroundColor: colors.white, borderRadius: radius.pill },
  academy: { color: colors.white, fontSize: 16, fontWeight: '700' },
  headerSub: { color: colors.primaryLight, fontSize: 12 },
  body: { alignItems: 'center', padding: spacing.xl, gap: 4 },
  name: { fontSize: 22, fontWeight: '700', color: colors.text, textAlign: 'center' },
  qr: { marginVertical: spacing.lg, padding: spacing.md, backgroundColor: colors.white, borderRadius: radius.md },
});
