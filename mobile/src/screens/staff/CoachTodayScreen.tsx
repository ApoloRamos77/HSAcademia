import React from 'react';
import { Text, View } from 'react-native';
import { CheckCircle2, ChevronRight, ClipboardCheck, CloudOff, Lock, QrCode, TriangleAlert, UserCheck } from 'lucide-react-native';
import { api } from '../../api/client';
import type { Dashboard, TrainingSession } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import AbsenceStreaks from '../../components/AbsenceStreaks';
import { Badge, Banner, Button, Card, Empty, Loading, Row, Screen, SectionTitle } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { discardPending, fetchStaffDay, isOfflineError, preloadRosters, readSessions, saveSessions, syncQueue, usePendingAttendance } from '../../offline/attendanceOffline';
import { colors, font, spacing } from '../../theme';
import { clockTime, firstName, greeting, longDay, shortDay, todayISO } from '../../utils/format';

/** Panel del entrenador: entrenamientos de hoy y de los próximos días, con el estado de la asistencia. */
export default function CoachTodayScreen({ navigation }: any) {
  const { session, role, can } = useAuth();
  const canMarkStaff = can('manage_staff_attendance');
  const pending = usePendingAttendance();
  const { data, loading, refreshing, refresh } = useApi(async () => {
    syncQueue();
    try {
      const res = await api.get<TrainingSession[]>('/attendance/mobile/my-training-history', { params: { months: 2 } });
      await saveSessions(res.data);
      preloadRosters(res.data.filter(s => s.date === todayISO()));
      if (canMarkStaff) fetchStaffDay(todayISO()).catch(() => {});
      const dashboard = role === 'Staff' ? await api.get<Dashboard>('/dashboard').then(r => r.data).catch(() => null) : null;
      return { sessions: res.data, offlineSince: null as number | null, streaks: dashboard?.attendance.absenceStreaks ?? [] };
    } catch (e) {
      const cached = isOfflineError(e) ? await readSessions() : null;
      if (!cached) throw e;
      return { sessions: cached.value, offlineSince: cached.cachedAt, streaks: [] };
    }
  }, []);

  if (loading && !data) return <Loading />;
  const today = todayISO();
  const all = (data?.sessions ?? []).slice().sort((a, b) => (a.date + a.startTime).localeCompare(b.date + b.startTime));
  const todays = all.filter(s => s.date === today);
  const upcoming = all.filter(s => s.date > today).slice(0, 6);
  const pendingPast = all.filter(s => s.date < today && !s.attendanceTaken).slice(-5).reverse();
  const waiting = pending.filter(p => !p.error);
  const rejected = pending.filter(p => p.error);
  const isWaiting = (s: TrainingSession) => waiting.some(p => p.eventId === s.eventId);

  const open = (s: TrainingSession) => navigation.navigate('Attendance', { session: s });

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <View>
        <Text style={font.h1}>{greeting()}, {firstName(session?.fullName)}</Text>
        <Text style={[font.small, { textTransform: 'capitalize' }]}>{longDay(today)}</Text>
      </View>

      {data?.offlineSince != null && (
        <Banner tone="warning" icon={<CloudOff color={colors.warning} size={18} />}
          text={`Sin señal: datos guardados a las ${clockTime(new Date(data.offlineSince).toISOString())}. Puedes tomar asistencia de las listas de hoy.`} />
      )}
      {waiting.length > 0 && (
        <Banner tone="info" icon={<CloudOff color={colors.info} size={18} />}
          text={`${waiting.length} lista(s) guardada(s) en este celular. Se enviarán solas cuando vuelva la señal.`} />
      )}
      {rejected.map(p => (
        <Card key={p.eventId}>
          <Row><TriangleAlert color={colors.danger} size={18} /><Text style={[font.h3, { flex: 1 }]}>No se pudo enviar: {p.title}</Text></Row>
          <Text style={[font.small, { marginVertical: spacing.sm }]}>{p.error}</Text>
          <Button compact variant="ghost" title="Descartar estas marcas" onPress={() => discardPending(p.eventId)} />
        </Card>
      ))}

      <Button title="Escanear carné QR" icon={<QrCode color={colors.white} size={20} />} onPress={() => navigation.navigate('Scan')} />
      {canMarkStaff && (
        <Button title="Asistencia de entrenadores" variant="secondary" icon={<UserCheck color={colors.primaryDark} size={20} />} onPress={() => navigation.navigate('StaffAttendance')} />
      )}

      <AbsenceStreaks items={data?.streaks ?? []} />

      <SectionTitle title="Hoy" />
      {todays.length === 0 ? (
        <Card><Empty icon={<CheckCircle2 color={colors.textMuted} />} text="No tienes entrenamientos programados para hoy." /></Card>
      ) : todays.map(s => <SessionCard key={s.eventId} s={s} waiting={isWaiting(s)} onPress={() => open(s)} highlight />)}

      {pendingPast.length > 0 && (
        <>
          <SectionTitle title="Asistencias pendientes" />
          {pendingPast.map(s => <SessionCard key={s.eventId} s={s} waiting={isWaiting(s)} onPress={() => open(s)} />)}
        </>
      )}

      <SectionTitle title="Próximos días" />
      {upcoming.length === 0 ? (
        <Card><Empty text="Sin entrenamientos programados." /></Card>
      ) : upcoming.map(s => <SessionCard key={s.eventId} s={s} waiting={isWaiting(s)} onPress={() => open(s)} />)}
    </Screen>
  );
}

function SessionCard({ s, onPress, highlight, waiting }: { s: TrainingSession; onPress: () => void; highlight?: boolean; waiting: boolean }) {
  const status = waiting
    ? <Badge tone="info" text="Por enviar" />
    : s.attendanceClosed
      ? <Badge tone="muted" text="Cerrada" />
      : s.attendanceTaken ? <Badge tone="success" text={`${s.presentCount}/${s.totalStudents} presentes`} /> : <Badge tone="warning" text="Sin tomar" />;
  return (
    <Card onPress={onPress} style={highlight ? { borderLeftWidth: 4, borderLeftColor: colors.primary } : undefined}>
      <Row style={{ justifyContent: 'space-between' }}>
        <View style={{ flex: 1, gap: 2 }}>
          <Text style={font.h3}>{s.title}</Text>
          <Text style={font.small}>{shortDay(s.date)} · {s.startTime} · {s.categoryName}</Text>
          <Text style={font.tiny}>{s.headquarterName}</Text>
          <View style={{ marginTop: spacing.xs }}>{status}</View>
        </View>
        {s.attendanceClosed ? <Lock color={colors.textMuted} size={18} /> : <ClipboardCheck color={colors.primaryDark} />}
        <ChevronRight color={colors.textMuted} />
      </Row>
    </Card>
  );
}
