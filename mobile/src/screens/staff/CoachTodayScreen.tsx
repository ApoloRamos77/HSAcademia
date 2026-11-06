import React from 'react';
import { Text, View } from 'react-native';
import { CheckCircle2, ChevronRight, ClipboardCheck, Lock, QrCode } from 'lucide-react-native';
import { api } from '../../api/client';
import type { TrainingSession } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Badge, Button, Card, Empty, Loading, Row, Screen, SectionTitle } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, spacing } from '../../theme';
import { firstName, greeting, longDay, shortDay, todayISO } from '../../utils/format';

/** Panel del entrenador: entrenamientos de hoy y de los próximos días, con el estado de la asistencia. */
export default function CoachTodayScreen({ navigation }: any) {
  const { session } = useAuth();
  const { data, loading, refreshing, refresh } = useApi(async () => {
    const res = await api.get<TrainingSession[]>('/attendance/mobile/my-training-history', { params: { months: 2 } });
    return res.data;
  }, []);

  if (loading && !data) return <Loading />;
  const today = todayISO();
  const all = (data ?? []).slice().sort((a, b) => (a.date + a.startTime).localeCompare(b.date + b.startTime));
  const todays = all.filter(s => s.date === today);
  const upcoming = all.filter(s => s.date > today).slice(0, 6);
  const pendingPast = all.filter(s => s.date < today && !s.attendanceTaken).slice(-5).reverse();

  const open = (s: TrainingSession) => navigation.navigate('Attendance', { session: s });

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <View>
        <Text style={font.h1}>{greeting()}, {firstName(session?.fullName)}</Text>
        <Text style={[font.small, { textTransform: 'capitalize' }]}>{longDay(today)}</Text>
      </View>

      <Button title="Escanear carné QR" icon={<QrCode color={colors.white} size={20} />} onPress={() => navigation.navigate('Scan')} />

      <SectionTitle title="Hoy" />
      {todays.length === 0 ? (
        <Card><Empty icon={<CheckCircle2 color={colors.textMuted} />} text="No tienes entrenamientos programados para hoy." /></Card>
      ) : todays.map(s => <SessionCard key={s.eventId} s={s} onPress={() => open(s)} highlight />)}

      {pendingPast.length > 0 && (
        <>
          <SectionTitle title="Asistencias pendientes" />
          {pendingPast.map(s => <SessionCard key={s.eventId} s={s} onPress={() => open(s)} />)}
        </>
      )}

      <SectionTitle title="Próximos días" />
      {upcoming.length === 0 ? (
        <Card><Empty text="Sin entrenamientos programados." /></Card>
      ) : upcoming.map(s => <SessionCard key={s.eventId} s={s} onPress={() => open(s)} />)}
    </Screen>
  );
}

function SessionCard({ s, onPress, highlight }: { s: TrainingSession; onPress: () => void; highlight?: boolean }) {
  const status = s.attendanceClosed
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
