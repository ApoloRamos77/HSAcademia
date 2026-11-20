import React from 'react';
import { Text, View } from 'react-native';
import { CalendarCheck, Trophy } from 'lucide-react-native';
import { api } from '../../api/client';
import type { AttendanceMonth, SportsSummary } from '../../api/types';
import { Badge, Card, Divider, Empty, Loading, Row, Screen, SectionTitle, Stat } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, spacing } from '../../theme';
import { longDay } from '../../utils/format';
import { useFamily } from './FamilyContext';

export const ATT: Record<string, { label: string; tone: 'success' | 'warning' | 'info' | 'danger' | 'muted' }> = {
  Presente: { label: 'Asistió', tone: 'success' },
  Tardanza: { label: 'Tardanza', tone: 'warning' },
  Justificado: { label: 'Justificado', tone: 'info' },
  Ausente: { label: 'Faltó', tone: 'danger' },
};

/** Historial completo de asistencia (6 meses), por mes. Se abre desde Progreso. */
export function AttendanceHistoryScreen() {
  const { student } = useFamily();
  const { data, loading, refreshing, refresh } = useApi(async () => {
    if (!student) return [] as AttendanceMonth[];
    return api.get<AttendanceMonth[]>('/attendance/my-history', { params: { studentId: student.id, months: 6 } }).then(r => r.data).catch(() => []);
  }, [student?.id]);

  if (loading && !data) return <Loading />;
  const months = (data ?? []).filter(m => m.records.some(r => r.status !== 'Pendiente'));
  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      {months.length === 0 ? (
        <Card><Empty icon={<CalendarCheck color={colors.textMuted} />} text="Aún no hay registros de asistencia." /></Card>
      ) : months.map(m => {
        const records = m.records.filter(r => r.status !== 'Pendiente').sort((a, b) => b.date.localeCompare(a.date));
        return (
          <View key={m.yearMonth}>
            <SectionTitle title={`${m.month} · ${m.present}/${m.present + m.absent}`} />
            <Card>
              {records.map((r, i) => (
                <View key={`${r.date}-${i}`}>
                  {i > 0 && <Divider />}
                  <Row style={{ justifyContent: 'space-between' }}>
                    <View style={{ flex: 1 }}>
                      <Text style={font.body}>{capitalize(longDay(r.date))}</Text>
                      {r.notes ? <Text style={font.tiny} numberOfLines={2}>{r.notes}</Text> : null}
                    </View>
                    <Badge tone={ATT[r.status]?.tone ?? 'muted'} text={ATT[r.status]?.label ?? r.status} />
                  </Row>
                </View>
              ))}
            </Card>
          </View>
        );
      })}
    </Screen>
  );
}

/** Todos los partidos con estadísticas. Se abre desde Progreso. */
export function MatchesHistoryScreen() {
  const { student } = useFamily();
  const { data: sports, loading, refreshing, refresh } = useApi(async () => {
    if (!student) return null;
    return api.get<SportsSummary>(`/mobile/students/${student.id}/sports-summary`, { params: { take: 100 } }).then(r => r.data).catch(() => null);
  }, [student?.id]);

  if (loading && !sports) return <Loading />;
  if (!sports || sports.matches === 0) {
    return <Screen refreshing={refreshing} onRefresh={refresh}><Card><Empty icon={<Trophy color={colors.textMuted} />} text="Aún no hay estadísticas de partidos." /></Card></Screen>;
  }
  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
        <Stat label="Partidos" value={String(sports.matches)} sub={`${sports.starts} de titular`} />
        <Stat label="Minutos" value={String(sports.minutes)} />
        <Stat label="Goles" value={String(sports.goals)} />
        <Stat label="Asistencias" value={String(sports.assists)} />
      </View>
      <Card>
        {sports.recentMatches.map((m, i) => (
          <View key={m.eventId}>
            {i > 0 && <Divider />}
            <MatchRow m={m} />
          </View>
        ))}
      </Card>
    </Screen>
  );
}

export function MatchRow({ m }: { m: SportsSummary['recentMatches'][number] }) {
  return (
    <Row style={{ justifyContent: 'space-between' }}>
      <View style={{ flex: 1 }}>
        <Text style={[font.body, { color: colors.text, fontWeight: '600' }]}>{m.title}{m.opponent ? ` vs. ${m.opponent}` : ''}</Text>
        <Text style={font.tiny}>{m.minutes}′ · {m.goals} gol(es) · {m.assists} asist.{m.rating ? ` · nota ${m.rating}` : ''}</Text>
      </View>
      {m.score && <Badge tone="muted" text={m.score} />}
    </Row>
  );
}

export const capitalize = (s: string) => (s ? s[0].toUpperCase() + s.slice(1) : s);
