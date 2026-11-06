import React from 'react';
import { Text, View } from 'react-native';
import { CalendarCheck, Star, Trophy } from 'lucide-react-native';
import { api } from '../../api/client';
import type { AttendanceMonth, Evaluation, SportsSummary } from '../../api/types';
import { Badge, Card, Divider, Empty, Loading, Row, Screen, SectionTitle, Stat } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, radius, spacing } from '../../theme';
import { longDay, shortDay } from '../../utils/format';
import { useFamily } from './FamilyContext';
import StudentPicker from './StudentPicker';

const ATT: Record<string, { label: string; tone: 'success' | 'warning' | 'info' | 'danger' | 'muted' }> = {
  Presente: { label: 'Asistió', tone: 'success' },
  Tardanza: { label: 'Tardanza', tone: 'warning' },
  Justificado: { label: 'Justificado', tone: 'info' },
  Ausente: { label: 'Faltó', tone: 'danger' },
};
const DIMENSIONS: [keyof Evaluation, string][] = [['technical', 'Técnica'], ['tactical', 'Táctica'], ['physical', 'Física'], ['attitude', 'Actitud']];

export default function ProgressScreen() {
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
          {records.slice(0, 8).map((r, i) => (
            <View key={`${r.date}-${i}`}>
              {i > 0 && <Divider />}
              <Row style={{ justifyContent: 'space-between' }}>
                <View style={{ flex: 1 }}>
                  <Text style={[font.body, { textTransform: 'capitalize' }]}>{longDay(r.date)}</Text>
                  {r.notes ? <Text style={font.tiny} numberOfLines={1}>{r.notes}</Text> : null}
                </View>
                <Badge tone={ATT[r.status]?.tone ?? 'muted'} text={ATT[r.status]?.label ?? r.status} />
              </Row>
            </View>
          ))}
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
          <Card>
            {sports.recentMatches.slice(0, 5).map((m, i) => (
              <View key={m.eventId}>
                {i > 0 && <Divider />}
                <Row style={{ justifyContent: 'space-between' }}>
                  <View style={{ flex: 1 }}>
                    <Text style={[font.body, { color: colors.text, fontWeight: '600' }]}>{m.title}{m.opponent ? ` vs. ${m.opponent}` : ''}</Text>
                    <Text style={font.tiny}>{m.minutes}′ · {m.goals} gol(es) · {m.assists} asist.{m.rating ? ` · nota ${m.rating}` : ''}</Text>
                  </View>
                  {m.score && <Badge tone="muted" text={m.score} />}
                </Row>
              </View>
            ))}
          </Card>
        </>
      )}
    </Screen>
  );
}
