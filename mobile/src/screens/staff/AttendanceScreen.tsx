import React, { useEffect, useState } from 'react';
import { Alert, FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import { Lock, Users } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { AttendanceCode, RosterStudent, TrainingSession } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Banner, Button, Empty, Loading, Row, useBottomInset } from '../../components/ui';
import { colors, font, radius, spacing } from '../../theme';
import { initials, shortDay } from '../../utils/format';

const OPTIONS: { code: AttendanceCode; short: string; label: string; bg: string; fg: string }[] = [
  { code: 1, short: 'P', label: 'Presente', bg: colors.successBg, fg: colors.success },
  { code: 3, short: 'T', label: 'Tarde', bg: colors.warningBg, fg: colors.warning },
  { code: 2, short: 'F', label: 'Falta', bg: colors.dangerBg, fg: colors.danger },
  { code: 4, short: 'J', label: 'Justificada', bg: colors.infoBg, fg: colors.info },
];

/** Toma de asistencia de una sesión: un toque por alumno, "todos presentes" y cierre de la lista. */
export default function AttendanceScreen({ route, navigation }: any) {
  const s: TrainingSession = route.params.session;
  const { role } = useAuth();
  const bottomInset = useBottomInset();
  const [roster, setRoster] = useState<RosterStudent[]>([]);
  const [marks, setMarks] = useState<Record<string, AttendanceCode>>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [closed, setClosed] = useState(s.attendanceClosed);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    navigation.setOptions({ title: `${s.categoryName} · ${shortDay(s.date)}` });
    (async () => {
      try {
        const { data } = await api.get<RosterStudent[]>('/attendance/mobile/my-students', { params: { eventId: s.eventId, date: s.date } });
        const sorted = data.slice().sort((a, b) => `${a.lastName}${a.firstName}`.localeCompare(`${b.lastName}${b.firstName}`));
        setRoster(sorted);
        setMarks(Object.fromEntries(sorted.filter(r => r.status).map(r => [r.studentId, r.status as AttendanceCode])));
      } catch (e) {
        setError(errorMessage(e));
      } finally {
        setLoading(false);
      }
    })();
  }, [s, navigation]);

  const allPresent = () => setMarks(Object.fromEntries(roster.map(r => [r.studentId, marks[r.studentId] ?? 1])));
  const missing = roster.filter(r => !marks[r.studentId]).length;

  const save = async (andClose = false) => {
    if (missing > 0) {
      Alert.alert('Faltan alumnos', `Marca a los ${missing} alumno(s) que faltan antes de guardar.`);
      return;
    }
    setSaving(true);
    try {
      await api.post('/attendance/mobile/my-students', {
        date: s.date,
        eventId: s.eventId,
        records: roster.map(r => ({ studentId: r.studentId, status: marks[r.studentId] })),
      });
      if (andClose) {
        await api.post(`/attendance/events/${s.eventId}/close`);
        setClosed(true);
      }
      Alert.alert('Asistencia guardada', andClose ? 'La lista quedó cerrada.' : 'Puedes seguir editándola hasta cerrarla.');
      if (andClose) navigation.goBack();
    } catch (e) {
      Alert.alert('No se pudo guardar', errorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  const confirmClose = () => Alert.alert(
    'Cerrar asistencia',
    'Al cerrar ya no podrás modificarla (solo un administrador puede reabrirla). ¿Continuar?',
    [{ text: 'Cancelar', style: 'cancel' }, { text: 'Guardar y cerrar', style: 'destructive', onPress: () => save(true) }],
  );

  if (loading) return <Loading />;

  const present = Object.values(marks).filter(v => v === 1 || v === 3).length;

  return (
    <View style={{ flex: 1, backgroundColor: colors.bg }}>
      <View style={styles.summary}>
        <Text style={font.h3}>{s.title} · {s.startTime}</Text>
        <Text style={font.small}>{present} presentes · {roster.length - present - missing} ausentes · {missing} sin marcar</Text>
        {closed && <Banner tone="muted" icon={<Lock color={colors.textSecondary} size={16} />} text="Asistencia cerrada: solo un administrador puede reabrirla." />}
        {error && <Banner tone="danger" text={error} />}
        {!closed && roster.length > 0 && <Button compact variant="secondary" title="Marcar todos presentes" onPress={allPresent} />}
      </View>

      <FlatList
        data={roster}
        keyExtractor={r => r.studentId}
        contentContainerStyle={{ padding: spacing.lg, gap: spacing.sm, paddingBottom: 140 + bottomInset }}
        ListEmptyComponent={<Empty icon={<Users color={colors.textMuted} />} text="No hay alumnos en esta categoría." />}
        renderItem={({ item }) => (
          <View style={styles.row}>
            <View style={styles.avatar}><Text style={styles.avatarText}>{initials(`${item.firstName} ${item.lastName}`)}</Text></View>
            <Text style={[font.body, { flex: 1, color: colors.text }]} numberOfLines={2}>{item.lastName}, {item.firstName}</Text>
            <Row style={{ gap: 6 }}>
              {OPTIONS.map(o => {
                const active = marks[item.studentId] === o.code;
                return (
                  <Pressable key={o.code} disabled={closed} onPress={() => setMarks(m => ({ ...m, [item.studentId]: o.code }))}
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
          <Button title="Guardar" onPress={() => save(false)} loading={saving} style={{ flex: 1 }} />
          {(role === 'AcademyAdmin' || role === 'Staff') && (
            <Button title="Guardar y cerrar" variant="ghost" onPress={confirmClose} disabled={saving} style={{ flex: 1 }} />
          )}
        </View>
      )}
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
