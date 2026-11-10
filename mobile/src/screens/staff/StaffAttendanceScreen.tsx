import React, { useEffect, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { CheckCircle2, CloudOff, Lock } from 'lucide-react-native';
import type { AttendanceCode, StaffDay, StaffSession } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Badge, Banner, Card, Chips, Empty, Loading, Row, Screen } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { fetchStaffDay, isOfflineError, readStaffDay, saveStaffMark, staffQueueKey, usePendingAttendance, type QueuedRecord } from '../../offline/attendanceOffline';
import { colors, font, radius, spacing } from '../../theme';
import { clockTime, longDay, todayISO } from '../../utils/format';

const OPTIONS: { code: AttendanceCode; short: string; label: string; bg: string; fg: string }[] = [
  { code: 1, short: 'P', label: 'Presente', bg: colors.successBg, fg: colors.success },
  { code: 3, short: 'T', label: 'Tarde', bg: colors.warningBg, fg: colors.warning },
  { code: 2, short: 'F', label: 'Falta', bg: colors.dangerBg, fg: colors.danger },
  { code: 4, short: 'J', label: 'Justificada', bg: colors.infoBg, fg: colors.info },
];
const isReplaceable = (status?: AttendanceCode | null) => status === 2 || status === 4;
type Mark = Pick<QueuedRecord, 'staffId' | 'substituteStaffId'> & { status?: AttendanceCode | null };

/** Asistencia de los entrenadores de hoy. Cada toque se guarda; sin señal queda en el celular y se envía solo. */
export default function StaffAttendanceScreen() {
  const { session, role } = useAuth();
  const today = todayISO();
  const pending = usePendingAttendance();
  const [edits, setEdits] = useState<Record<string, Mark>>({});
  const { data, loading, refreshing, refresh, reload } = useApi(async () => {
    try {
      return { day: await fetchStaffDay(today), offlineSince: null as number | null };
    } catch (e) {
      const cached = isOfflineError(e) ? await readStaffDay(today) : null;
      if (!cached) throw e;
      return { day: cached.value as StaffDay, offlineSince: cached.cachedAt };
    }
  }, [today]);

  useEffect(() => { setEdits({}); }, [data]);

  if (loading && !data) return <Loading />;
  if (!data) {
    return <Screen refreshing={refreshing} onRefresh={refresh}><Banner tone="danger" text="No se pudieron cargar las sesiones. Abre esta pantalla una vez con internet." /></Screen>;
  }

  const isAdmin = role === 'AcademyAdmin';
  const trainers = data.day.trainers.filter(t => isAdmin || t.id !== session?.userId);

  const queuedOf = (s: StaffSession) => pending.find(p => p.eventId === staffQueueKey(s.eventId));
  const markOf = (s: StaffSession): Mark => edits[s.eventId] ?? queuedOf(s)?.records[s.eventId] ?? { status: s.status, staffId: s.staffId, substituteStaffId: s.substituteStaffId };

  const change = async (s: StaffSession, patch: Mark) => {
    const next = { ...markOf(s), ...patch };
    if (!isReplaceable(next.status)) next.substituteStaffId = null;
    if (!next.status) return;
    setEdits(e => ({ ...e, [s.eventId]: next }));
    const result = await saveStaffMark(s, { status: next.status, staffId: next.staffId ?? null, substituteStaffId: next.substituteStaffId ?? null, markedAt: new Date().toISOString() });
    if (result.state === 'rejected') {
      Alert.alert('No se pudo guardar', result.message);
      setEdits(e => { const rest = { ...e }; delete rest[s.eventId]; return rest; });
    } else if (result.state === 'sent') {
      reload();
    }
  };

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <Text style={[font.small, { textTransform: 'capitalize' }]}>{longDay(today)}</Text>
      {data.offlineSince != null && (
        <Banner tone="warning" icon={<CloudOff color={colors.warning} size={18} />}
          text={`Sin señal: datos guardados a las ${clockTime(new Date(data.offlineSince).toISOString())}. Tus marcas se enviarán solas.`} />
      )}
      {data.day.sessions.length === 0 && <Card><Empty icon={<CheckCircle2 color={colors.textMuted} />} text="No hay sesiones hoy en tus categorías." /></Card>}
      {data.day.sessions.map(s => {
        const mark = markOf(s);
        const queued = queuedOf(s);
        const blocked = !!s.blockedReason;
        const hasTrainer = !!(mark.staffId ?? s.staffId);
        return (
          <Card key={s.eventId}>
            <Row style={{ justifyContent: 'space-between' }}>
              <Text style={[font.h3, { flex: 1 }]}>{s.startTime} · {s.title}</Text>
              {queued && !queued.error && <Badge tone="info" text="Por enviar" />}
            </Row>
            <Text style={font.small}>{s.categoryName} · {s.headquarterName}</Text>
            <Text style={[font.body, { color: colors.text, marginTop: spacing.xs }]}>
              {s.staffName ?? trainers.find(t => t.id === mark.staffId)?.name ?? 'Sin entrenador asignado'}
            </Text>

            {blocked ? (
              <Row style={{ marginTop: spacing.sm }}><Lock color={colors.textMuted} size={16} /><Text style={[font.small, { flex: 1 }]}>{s.blockedReason}</Text></Row>
            ) : (
              <>
                {!s.staffId && (
                  <View style={{ marginTop: spacing.sm }}>
                    <Text style={styles.label}>¿Quién dictó?</Text>
                    <Chips options={trainers.map(t => ({ value: t.id, label: t.name }))} value={mark.staffId ?? ''}
                      onChange={id => change(s, { staffId: id, status: mark.status ?? 1 })} />
                  </View>
                )}
                <Row style={{ gap: spacing.sm, marginTop: spacing.sm }}>
                  {OPTIONS.map(o => {
                    const active = mark.status === o.code;
                    return (
                      <Pressable key={o.code} disabled={!hasTrainer} onPress={() => change(s, { status: o.code })}
                        accessibilityRole="radio" accessibilityState={{ selected: active }} accessibilityLabel={o.label}
                        style={[styles.opt, active && { backgroundColor: o.bg, borderColor: o.fg }, !hasTrainer && { opacity: 0.4 }]} hitSlop={4}>
                        <Text style={[styles.optText, active && { color: o.fg }]}>{o.short}</Text>
                      </Pressable>
                    );
                  })}
                </Row>
                {isReplaceable(mark.status) && (
                  <View style={{ marginTop: spacing.sm }}>
                    <Text style={styles.label}>¿Quién lo reemplazó?</Text>
                    <Chips options={[{ value: '', label: 'Sin reemplazo' }, ...trainers.filter(t => t.id !== (mark.staffId ?? s.staffId)).map(t => ({ value: t.id, label: t.name }))]}
                      value={mark.substituteStaffId ?? ''} onChange={id => change(s, { substituteStaffId: id || null })} />
                  </View>
                )}
              </>
            )}
            {!!s.markedByName && !queued && <Text style={[font.tiny, { marginTop: spacing.xs }]}>Marcó {s.markedByName}{s.markedAt ? ` · ${clockTime(s.markedAt)}` : ''}</Text>}
          </Card>
        );
      })}
    </Screen>
  );
}

const styles = StyleSheet.create({
  label: { ...font.small, fontWeight: '600', color: colors.textSecondary, marginBottom: 4 },
  opt: { flex: 1, height: 40, borderRadius: radius.sm, borderWidth: 1, borderColor: colors.border, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.card },
  optText: { fontWeight: '700', color: colors.textMuted },
});
