import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Alert, FlatList, Modal, Pressable, StyleSheet, Text, View } from 'react-native';
import { Lock, QrCode, Users } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { AttendanceCode, RosterStudent, TrainingSession } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import QrScanner, { type ScanResult } from '../../components/QrScanner';
import { Banner, Button, Empty, Input, Loading, Row, useBottomInset } from '../../components/ui';
import { colors, font, radius, spacing } from '../../theme';
import { initials, shortDay } from '../../utils/format';

const time = (iso: string) => { const d = new Date(iso); return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`; };

const OPTIONS: { code: AttendanceCode; short: string; label: string; bg: string; fg: string }[] = [
  { code: 1, short: 'P', label: 'Presente', bg: colors.successBg, fg: colors.success },
  { code: 3, short: 'T', label: 'Tarde', bg: colors.warningBg, fg: colors.warning },
  { code: 2, short: 'F', label: 'Falta', bg: colors.dangerBg, fg: colors.danger },
  { code: 4, short: 'J', label: 'Justificada', bg: colors.infoBg, fg: colors.info },
];

type Marks = Record<string, AttendanceCode>;
type RosterRecord = { studentId: string; status: AttendanceCode };

const normalize = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
const marksOf = (list: RosterStudent[]): Marks => Object.fromEntries(list.filter(r => r.status).map(r => [r.studentId, r.status as AttendanceCode]));

/**
 * Lista de la sesión, a cargo del entrenador: un toque por alumno y QR para quien traiga su carné.
 * Se puede guardar a medias. Al cerrar, quienes sigan sin marcar quedan con falta.
 */
export default function AttendanceScreen({ route, navigation }: any) {
  const s: TrainingSession = route.params.session;
  const { role } = useAuth();
  const bottomInset = useBottomInset();
  const [roster, setRoster] = useState<RosterStudent[]>([]);
  const [marks, setMarks] = useState<Marks>({});
  const [query, setQuery] = useState('');
  const [scanning, setScanning] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [closed, setClosed] = useState(s.attendanceClosed);
  const [error, setError] = useState<string | null>(null);

  const fetchRoster = useCallback(async () => {
    const { data } = await api.get<RosterStudent[]>('/attendance/mobile/my-students', { params: { eventId: s.eventId, date: s.date } });
    return data.slice().sort((a, b) => `${a.lastName}${a.firstName}`.localeCompare(`${b.lastName}${b.firstName}`));
  }, [s.eventId, s.date]);

  useEffect(() => {
    navigation.setOptions({ title: `${s.categoryName} · ${shortDay(s.date)}` });
    (async () => {
      try {
        const sorted = await fetchRoster();
        setRoster(sorted);
        setMarks(marksOf(sorted));
      } catch (e) {
        setError(errorMessage(e));
      } finally {
        setLoading(false);
      }
    })();
  }, [s, navigation, fetchRoster]);

  const visible = useMemo(() => {
    const term = normalize(query.trim());
    return term ? roster.filter(r => normalize(`${r.firstName} ${r.lastName}`).includes(term)) : roster;
  }, [roster, query]);

  const mark = (studentId: string, code: AttendanceCode) => {
    setMarks(m => ({ ...m, [studentId]: code }));
    setRoster(list => list.map(r => (r.studentId === studentId ? { ...r, markedVia: null, markedByName: null, markedAt: null } : r)));
  };

  const onScanned = useCallback((r: ScanResult) => {
    setMarks(m => ({ ...m, [r.studentId]: 1 }));
    setRoster(list => list.map(x => (x.studentId === r.studentId ? { ...x, markedVia: 'QR', markedAt: r.markedAt } : x)));
  }, []);

  const save = async (records: RosterRecord[], andClose: boolean) => {
    if (records.length === 0) {
      Alert.alert('Sin marcas', 'Marca al menos a un alumno antes de guardar.');
      return;
    }
    setSaving(true);
    try {
      await api.post('/attendance/mobile/my-students', { date: s.date, eventId: s.eventId, records });
      if (andClose) {
        await api.post(`/attendance/events/${s.eventId}/close`);
        setClosed(true);
      }
      Alert.alert('Asistencia guardada', andClose ? 'La lista quedó cerrada.' : 'Puedes seguir marcando hasta cerrarla.');
      if (andClose) navigation.goBack();
    } catch (e) {
      Alert.alert('No se pudo guardar', errorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  const saveProgress = () => save(roster.filter(r => marks[r.studentId]).map(r => ({ studentId: r.studentId, status: marks[r.studentId] })), false);

  // Se vuelve a leer la lista antes de cerrar: otro celular pudo haber registrado carnés mientras tanto.
  const confirmClose = async () => {
    setSaving(true);
    let latest: RosterStudent[];
    try {
      latest = await fetchRoster();
    } catch (e) {
      Alert.alert('No se pudo cerrar', errorMessage(e));
      return;
    } finally {
      setSaving(false);
    }
    const merged: Marks = { ...marksOf(latest), ...marks };
    setRoster(latest);
    setMarks(merged);

    const final = latest.map(r => ({ studentId: r.studentId, status: merged[r.studentId] ?? (2 as AttendanceCode) }));
    const total = (...codes: AttendanceCode[]) => final.filter(r => codes.includes(r.status)).length;
    const unmarked = latest.filter(r => !merged[r.studentId]).length;
    const justified = total(4);
    Alert.alert(
      'Cerrar asistencia',
      `${total(1, 3)} presentes · ${total(2)} faltas${justified ? ` · ${justified} justificadas` : ''}\n` +
      `${unmarked ? `Los ${unmarked} sin marcar quedarán con falta.\n` : ''}` +
      'Después de cerrar, solo un administrador podrá reabrirla.',
      [{ text: 'Cancelar', style: 'cancel' }, { text: 'Guardar y cerrar', style: 'destructive', onPress: () => save(final, true) }],
    );
  };

  if (loading) return <Loading />;

  const count = (...codes: AttendanceCode[]) => roster.filter(r => marks[r.studentId] && codes.includes(marks[r.studentId])).length;
  const missing = roster.filter(r => !marks[r.studentId]).length;

  return (
    <View style={{ flex: 1, backgroundColor: colors.bg }}>
      <View style={styles.summary}>
        <Text style={font.h3}>{s.title} · {s.startTime}</Text>
        <Text style={font.small}>{count(1, 3)} presentes · {count(2)} faltas · {count(4)} justificadas · {missing} sin marcar</Text>
        {closed && <Banner tone="muted" icon={<Lock color={colors.textSecondary} size={16} />} text="Asistencia cerrada: solo un administrador puede reabrirla." />}
        {error && <Banner tone="danger" text={error} />}
        {!closed && roster.length > 0 && (
          <Button compact variant="secondary" title="Escanear carnés QR" icon={<QrCode color={colors.primaryDark} size={16} />} onPress={() => setScanning(true)} />
        )}
        {roster.length > 0 && <Input placeholder="Buscar alumno" value={query} onChangeText={setQuery} autoCorrect={false} style={{ marginBottom: -spacing.md }} />}
      </View>

      <FlatList
        data={visible}
        keyExtractor={r => r.studentId}
        keyboardShouldPersistTaps="handled"
        contentContainerStyle={{ padding: spacing.lg, gap: spacing.sm, paddingBottom: 140 + bottomInset }}
        ListEmptyComponent={<Empty icon={<Users color={colors.textMuted} />} text={roster.length ? 'Ningún alumno coincide con la búsqueda.' : 'No hay alumnos en esta categoría.'} />}
        renderItem={({ item }) => (
          <View style={styles.row}>
            <View style={styles.avatar}><Text style={styles.avatarText}>{initials(`${item.firstName} ${item.lastName}`)}</Text></View>
            <View style={{ flex: 1 }}>
              <Text style={[font.body, { color: colors.text }]} numberOfLines={2}>{item.lastName}, {item.firstName}</Text>
              {!!item.markedVia && (
                <Text style={font.tiny} numberOfLines={1}>
                  {item.markedVia}{item.markedAt ? ` · ${time(item.markedAt)}` : ''}{item.markedByName ? ` · ${item.markedByName}` : ''}
                </Text>
              )}
            </View>
            <Row style={{ gap: 6 }}>
              {OPTIONS.map(o => {
                const active = marks[item.studentId] === o.code;
                return (
                  <Pressable key={o.code} disabled={closed} onPress={() => mark(item.studentId, o.code)}
                    accessibilityRole="radio" accessibilityState={{ selected: active }} accessibilityLabel={`${o.label} ${item.firstName}`}
                    style={[styles.opt, active && { backgroundColor: o.bg, borderColor: o.fg }]} hitSlop={4}>
                    <Text style={[styles.optText, active && { color: o.fg }]}>{o.short}</Text>
                  </Pressable>
                );
              })}
            </Row>
          </View>
        )}
      />

      {!closed && roster.length > 0 && (
        <View style={[styles.footer, { paddingBottom: spacing.lg + bottomInset }]}>
          <Button title="Guardar" onPress={saveProgress} loading={saving} style={{ flex: 1 }} />
          {(role === 'AcademyAdmin' || role === 'Staff') && (
            <Button title="Guardar y cerrar" variant="ghost" onPress={confirmClose} disabled={saving} style={{ flex: 1 }} />
          )}
        </View>
      )}

      <Modal visible={scanning} animationType="slide" onRequestClose={() => setScanning(false)}>
        {scanning && <QrScanner eventId={s.eventId} onScanned={onScanned} onClose={() => setScanning(false)} />}
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  summary: { padding: spacing.lg, gap: spacing.sm, backgroundColor: colors.card, borderBottomWidth: 1, borderBottomColor: colors.border },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, backgroundColor: colors.card, padding: spacing.md, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border },
  avatar: { width: 36, height: 36, borderRadius: radius.pill, backgroundColor: colors.primaryLight, alignItems: 'center', justifyContent: 'center' },
  avatarText: { color: colors.primaryDark, fontWeight: '700' },
  opt: { width: 36, height: 36, borderRadius: radius.sm, borderWidth: 1, borderColor: colors.border, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.card },
  optText: { fontWeight: '700', color: colors.textMuted },
  footer: { position: 'absolute', left: 0, right: 0, bottom: 0, flexDirection: 'row', gap: spacing.sm, padding: spacing.lg, backgroundColor: colors.card, borderTopWidth: 1, borderTopColor: colors.border },
});
