import React from 'react';
import { Pressable, Text, View } from 'react-native';
import { CalendarCheck, ChevronRight, Star, Trophy } from 'lucide-react-native';
import { api } from '../../api/client';
import type { AttendanceMonth, Evaluation, SportsSummary } from '../../api/types';
import { Badge, Card, Empty, Loading, Row, Screen, SectionTitle, Stat } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, radius, spacing } from '../../theme';
import { longDay, shortDay } from '../../utils/format';
import { useFamily } from './FamilyContext';
import StudentPicker from './StudentPicker';
import { ATT, MatchRow, capitalize } from './HistoryScreens';
const DIMENSIONS: [keyof Evaluation, string][] = [['technical', 'Técnica'], ['tactical', 'Táctica'], ['physical', 'Física'], ['attitude', 'Actitud']];

/** Enlace "Ver historial…" al pie de una tarjeta. */
function MoreLink({ label, onPress }: { label: string; onPress: () => void }) {
  return (
    <Pressable onPress={onPress} accessibilityRole="button" hitSlop={6}
      style={({ pressed }) => [{ flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingTop: spacing.md, marginTop: spacing.sm, borderTopWidth: 1, borderTopColor: colors.border, minHeight: 44 }, pressed && { opacity: 0.7 }]}>
      <Text style={{ color: colors.primaryDark, fontWeight: '700' }}>{label}</Text>
      <ChevronRight color={colors.primaryDark} size={18} />
    </Pressable>
  );
}

/**
 * Progreso: solo lo último (asistencia, evaluación y partido) para que cargue rápido.
 * El historial completo se abre en otra pantalla.
 */
export default function ProgressScreen({ navigation }: any) {
  const { student } = useFamily();
  const { data, loading, refreshing, refresh } = useApi(async () => {
    if (!student) return null;
    const [history, evaluations, sports] = await Promise.all([
      api.get<AttendanceMonth[]>('/attendance/my-history', { params: { studentId: student.id, months: 3 } }).then(r => r.data).catch(() => []),
      api.get<Evaluation[]>(`/mobile/students/${student.id}/evaluations`).then(r => r.data).catch(() => []),
      api.get<SportsSummary>(`/mobile/students/${student.id}/sports-summary`).then(r => r.data).catch(() => null),
    ]);
    return { history, evaluations, sports };
  }, [student?.id]);

  if (loading && !data) return <Loading />;
  const records = (data?.history ?? []).flatMap(m => m.records).filter(r => r.status !== 'Pendiente').sort((a, b) => b.date.localeCompare(a.date));
  const attended = records.filter(r => r.status === 'Presente' || r.status === 'Tardanza').length;
  const counted = records.filter(r => r.status !== 'Justificado').length;
  const rate = counted ? Math.round((100 * attended) / counted) : null;
  const latest = data?.evaluations?.[0];
  const sports = data?.sports;

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <StudentPicker />

      {/* Asistencia */}
      <SectionTitle title="Asistencia" />
      <View style={{ flexDirection: 'row', gap: spacing.sm }}>
        <Stat label="Últimos 3 meses" value={rate != null ? `${rate}%` : '—'} tone={rate != null && rate < 70 ? 'danger' : undefined}
          sub={rate != null && rate < 70 ? 'Por debajo del 70% recomendado' : `${attended} de ${counted} sesiones`} />
        {(data?.history ?? []).slice(0, 1).map(m => (
          <Stat key={m.yearMonth} label={`Este mes (${m.month.split(' ')[0]})`} value={`${m.present}/${m.present + m.absent}`} sub={`${m.justified} justificada(s)`} />
        ))}
      </View>
      {records.length === 0 ? (
        <Card><Empty icon={<CalendarCheck color={colors.textMuted} />} text="Aún no hay registros de asistencia." /></Card>
      ) : (
        <Card>
          <Text style={font.small}>Último registro</Text>
          <Row style={{ justifyContent: 'space-between', marginTop: 4 }}>
            <View style={{ flex: 1 }}>
              <Text style={[font.body, { color: colors.text, fontWeight: '600' }]}>{capitalize(longDay(records[0].date))}</Text>
              {records[0].notes ? <Text style={font.tiny} numberOfLines={1}>{records[0].notes}</Text> : null}
            </View>
            <Badge tone={ATT[records[0].status]?.tone ?? 'muted'} text={ATT[records[0].status]?.label ?? records[0].status} />
          </Row>
          <MoreLink label="Ver historial de asistencia" onPress={() => navigation.navigate('AttendanceHistory')} />
        </Card>
      )}

      {/* Evaluación deportiva */}
      <SectionTitle title="Evaluación del entrenador" />
      {!latest ? (
        <Card><Empty icon={<Star color={colors.textMuted} />} text="El entrenador aún no registró evaluaciones." /></Card>
      ) : (
        <Card>
          <Row style={{ justifyContent: 'space-between' }}>
            <Text style={font.h3}>{latest.period}</Text>
            <Badge tone="info" text={`Promedio ${latest.average} / 5`} />
          </Row>
          <View style={{ gap: spacing.sm, marginTop: spacing.md }}>
            {DIMENSIONS.map(([k, label]) => {
              const v = latest[k] as number;
              return (
                <View key={k}>
                  <Row style={{ justifyContent: 'space-between' }}>
                    <Text style={font.small}>{label}</Text>
                    <Text style={[font.small, { fontWeight: '700', color: colors.text }]}>{v}/5</Text>
                  </Row>
                  <View style={{ height: 8, backgroundColor: colors.infoBg, borderRadius: radius.pill, marginTop: 4 }}>
                    <View style={{ width: `${(v / 5) * 100}%`, height: '100%', backgroundColor: '#2a78d6', borderRadius: radius.pill }} />
                  </View>
                </View>
              );
            })}
          </View>
          {latest.strengths ? <Text style={[font.body, { marginTop: spacing.md }]}><Text style={{ fontWeight: '700' }}>Fortalezas: </Text>{latest.strengths}</Text> : null}
          {latest.toImprove ? <Text style={[font.body, { marginTop: 4 }]}><Text style={{ fontWeight: '700' }}>Por mejorar: </Text>{latest.toImprove}</Text> : null}
          {latest.goals ? <Text style={[font.body, { marginTop: 4 }]}><Text style={{ fontWeight: '700' }}>Objetivos: </Text>{latest.goals}</Text> : null}
          <Text style={[font.tiny, { marginTop: spacing.sm }]}>{shortDay(latest.evaluationDate)} · {latest.evaluatorName}</Text>
          {(data?.evaluations?.length ?? 0) > 1 && (
            <Text style={[font.tiny, { marginTop: 4 }]}>
              Evaluación anterior ({data!.evaluations[1].period}): promedio {data!.evaluations[1].average}
            </Text>
          )}
        </Card>
      )}

      {/* Partidos */}
      <SectionTitle title="Partidos" />
      {!sports || sports.matches === 0 ? (
        <Card><Empty icon={<Trophy color={colors.textMuted} />} text="Aún no hay estadísticas de partidos." /></Card>
      ) : (
        <>
          <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
            <Stat label="Partidos" value={String(sports.matches)} sub={`${sports.starts} de titular`} />
            <Stat label="Minutos" value={String(sports.minutes)} />
            <Stat label="Goles" value={String(sports.goals)} />
            <Stat label="Asistencias" value={String(sports.assists)} />
          </View>
          {sports.recentMatches[0] && (
            <Card>
              <Text style={font.small}>Último partido</Text>
              <View style={{ marginTop: 4 }}><MatchRow m={sports.recentMatches[0]} /></View>
              <MoreLink label="Ver todos los partidos" onPress={() => navigation.navigate('MatchesHistory')} />
            </Card>
          )}
        </>
      )}
    </Screen>
  );
}
