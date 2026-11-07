import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Linking, PermissionsAndroid, StyleSheet, Text, Vibration, View } from 'react-native';
import { Camera, CameraType } from 'react-native-camera-kit';
import { useIsFocused } from '@react-navigation/native';
import { CheckCircle2, XCircle } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import { Button, Loading, useBottomInset } from '../../components/ui';
import { colors, font, radius, spacing } from '../../theme';

const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i;

/** Extrae el Student.Id del QR del carné ({"u": id}) o de un GUID suelto (carnés antiguos). */
function parseStudentId(raw: string): string | null {
  try {
    const obj = JSON.parse(raw);
    if (obj && typeof obj.u === 'string' && GUID.test(obj.u)) return obj.u;
  } catch { /* no es JSON */ }
  return raw.match(GUID)?.[0] ?? null;
}

type Result = { ok: boolean; title: string; detail?: string };

/** Escáner de carnés: registra "Presente" del día. Pensado para la puerta de la cancha: uno tras otro. */
export default function ScanScreen() {
  const focused = useIsFocused();
  const bottomInset = useBottomInset();
  const [permission, setPermission] = useState<'checking' | 'granted' | 'denied'>('checking');
  const [result, setResult] = useState<Result | null>(null);
  const [count, setCount] = useState(0);
  const busy = useRef(false);
  const lastCode = useRef<{ code: string; at: number } | null>(null);

  useEffect(() => {
    (async () => {
      const r = await PermissionsAndroid.request(PermissionsAndroid.PERMISSIONS.CAMERA, {
        title: 'Permiso de cámara',
        message: 'La cámara se usa para leer el carné QR de los alumnos y registrar su asistencia.',
        buttonPositive: 'Permitir',
        buttonNegative: 'Ahora no',
      });
      setPermission(r === PermissionsAndroid.RESULTS.GRANTED ? 'granted' : 'denied');
    })();
  }, []);

  const onRead = useCallback(async (event: { nativeEvent: { codeStringValue: string } }) => {
    const code = event.nativeEvent.codeStringValue;
    const now = Date.now();
    if (busy.current) return;
    if (lastCode.current && lastCode.current.code === code && now - lastCode.current.at < 4000) return; // evita lecturas repetidas
    lastCode.current = { code, at: now };

    const studentId = parseStudentId(code);
    if (!studentId) {
      setResult({ ok: false, title: 'Código no válido', detail: 'Este QR no es un carné de la academia.' });
      Vibration.vibrate(300);
      return;
    }
    busy.current = true;
    try {
      const { data } = await api.post('/attendance/scan', { studentUserId: studentId, scannedAt: new Date().toISOString() });
      setResult({ ok: true, title: data.studentName, detail: `${data.category} · Asistencia registrada` });
      setCount(c => c + 1);
      Vibration.vibrate(80);
    } catch (e: any) {
      const already = e?.response?.status === 409;
      setResult({ ok: false, title: already ? 'Ya registrado' : 'No se pudo registrar', detail: errorMessage(e) });
      Vibration.vibrate(already ? 150 : 400);
    } finally {
      busy.current = false;
    }
  }, []);

  if (permission === 'checking') return <Loading />;
  if (permission === 'denied') {
    return (
      <View style={styles.center}>
        <Text style={[font.h2, { textAlign: 'center' }]}>Se necesita la cámara</Text>
        <Text style={[font.body, { textAlign: 'center', marginVertical: spacing.md }]}>
          Activa el permiso de cámara en los ajustes de Android para escanear carnés.
        </Text>
        <Button title="Abrir ajustes" onPress={() => Linking.openSettings()} />
      </View>
    );
  }

  return (
    <View style={{ flex: 1, backgroundColor: '#000' }}>
      {focused && (
        <Camera
          style={StyleSheet.absoluteFill}
          cameraType={CameraType.Back}
          scanBarcode
          onReadCode={onRead}
          showFrame
          frameColor={colors.primary}
          laserColor={colors.accent}
        />
      )}
      <View style={styles.top}>
        <Text style={styles.topText}>Apunta al código QR del carné</Text>
        <Text style={styles.topSub}>{count} registrado(s) en esta sesión</Text>
      </View>
      {result && (
        <View style={[styles.result, { bottom: spacing.xl + bottomInset, backgroundColor: result.ok ? colors.successBg : colors.dangerBg }]}>
          {result.ok ? <CheckCircle2 color={colors.success} size={28} /> : <XCircle color={colors.danger} size={28} />}
          <View style={{ flex: 1 }}>
            <Text style={[font.h3, { color: result.ok ? colors.success : colors.danger }]}>{result.title}</Text>
            {result.detail && <Text style={font.small}>{result.detail}</Text>}
          </View>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  center: { flex: 1, justifyContent: 'center', padding: spacing.xl, backgroundColor: colors.bg },
  top: { position: 'absolute', top: spacing.lg, left: spacing.lg, right: spacing.lg, backgroundColor: 'rgba(0,0,0,0.55)', borderRadius: radius.md, padding: spacing.md },
  topText: { color: colors.white, fontSize: 16, fontWeight: '600', textAlign: 'center' },
  topSub: { color: colors.primaryLight, fontSize: 12, textAlign: 'center', marginTop: 2 },
  result: { position: 'absolute', bottom: spacing.xl, left: spacing.lg, right: spacing.lg, flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg, borderRadius: radius.lg },
});
