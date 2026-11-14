import React, { useState } from 'react';
import { Alert, Text, View } from 'react-native';
import { Cake, CalendarDays, Dumbbell, MapPin, Trophy } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { MobileEvent, MyCall } from '../../api/types';
import { Badge, Button, Card, Empty, Loading, Row, Screen, SectionTitle } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, spacing } from '../../theme';
import { eventLabel, longDay, todayISO } from '../../utils/format';
import { useFamily } from './FamilyContext';
import StudentPicker from './StudentPicker';

export function EventIcon({ type, size = 18 }: { type: string; size?: number }) {
  if (type === 'Tournament') return <Trophy color={colors.warning} size={size} />;
  if (type === 'Birthday') return <Cake color={colors.accentDark} size={size} />;
  return <Dumbbell color={colors.primaryDark} size={size} />;
}

export const EVENT_TYPE_LABEL: Record<string, string> = { Training: 'Entrenamiento', Tournament: 'Partido', Birthday: 'Cumpleaños', Other: 'Evento' };

/** Estados de partido que la familia debe notar (programado no se muestra). */
export const MATCH_STATUS: Record<number, { text: string; tone: 'success' | 'warning' | 'danger' | 'muted' }> = {
  2: { text: 'Jugado', tone: 'muted' },
  3: { text: 'Postergado', tone: 'warning' },
  4: { text: 'Suspendido', tone: 'danger' },
  5: { text: 'W.O. a favor', tone: 'success' },
  6: { text: 'W.O. en contra', tone: 'danger' },
};

/** "Fecha 3 · citación 08:15" para la tarjeta de un partido. */
export function matchExtra(round?: string | null, callTime?: string | null) {
  const parts = [round, callTime ? `citación ${callTime}` : null].filter(Boolean);
  return parts.length ? parts.join(' · ') : null;
}

const timeOfIso = (iso?: string | null) => (iso ? iso.slice(11, 16) : null);

export default function AgendaScreen() {
  const { student } = useFamily();
  const [busy, setBusy] = useState<string | null>(null);
  const { data, loading, refreshing, refresh, setData } = useApi(async () => {
    if (!student) return null;
    const [events, calls] = await Promise.all([
      api.get<MobileEvent[]>('/calendar/mobile/events', { params: { categoryId: student.categoryId, studentId: student.id } }).then(r => r.data),
      api.get<MyCall[]>('/calendar/mobile/my-calls').then(r => r.data).catch(() => [] as MyCall[]),
    ]);
    return { events, calls };
  }, [student?.id]);

  const answer = async (call: MyCall, isConfirmed: boolean) => {
    setBusy(call.callId);
    try {
      await api.put(`/calendar/mobile/calls/${call.callId}/confirm`, { isConfirmed });
      setData(d => d && ({ ...d, calls: d.calls.map(c => (c.callId === call.callId ? { ...c, isConfirmed } : c)) }));
    } catch (e) {
      Alert.alert('No se pudo responder', errorMessage(e));
    } finally {
      setBusy(null);
    }
  };

  if (loading && !data) return <Loading />;
  const today = todayISO();
  const calls = (data?.calls ?? []).filter(c => c.studentId === student?.id);
  const upcoming = (data?.events ?? []).filter(e => e.date >= today).sort((a, b) => (a.date + (a.startTime ?? '')).localeCompare(b.date + (b.startTime ?? '')));
  const byDay = upcoming.reduce<Record<string, MobileEvent[]>>((acc, e) => { (acc[e.date] ||= []).push(e); return acc; }, {});
  const days = Object.keys(byDay).slice(0, 21);

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <StudentPicker />

      {calls.length > 0 && (
        <>
          <SectionTitle title="Convocatorias" />
          {calls.map(c => (
            <Card key={c.callId}>
              <Row style={{ justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <View style={{ flex: 1 }}>
                  <Text style={font.h3}>{c.title}{c.opponent && !c.title.includes(' vs ') ? ` vs. ${c.opponent}` : ''}</Text>
                  <Text style={font.small}>{eventLabel(c.startTime)}{c.location ? ` · ${c.location}` : ''}</Text>
                  {matchExtra(c.round, timeOfIso(c.callTime)) && <Text style={[font.small, { fontWeight: '600' }]}>{matchExtra(c.round, timeOfIso(c.callTime))}</Text>}
                </View>
                {c.matchStatus && MATCH_STATUS[c.matchStatus] && <Badge tone={MATCH_STATUS[c.matchStatus].tone} text={MATCH_STATUS[c.matchStatus].text} />}
                {c.isConfirmed === true && <Badge tone="success" text="Asistirá" />}
                {c.isConfirmed === false && <Badge tone="danger" text="No asistirá" />}
                {c.isConfirmed === null && <Badge tone="warning" text="Por confirmar" />}
              </Row>
              <Row style={{ marginTop: spacing.md }}>
                <Button compact title="Asistirá" variant={c.isConfirmed === true ? 'success' : 'secondary'} style={{ flex: 1 }}
                  loading={busy === c.callId} onPress={() => answer(c, true)} />
                <Button compact title="No asistirá" variant={c.isConfirmed === false ? 'danger' : 'ghost'} style={{ flex: 1 }}
                  disabled={busy === c.callId} onPress={() => answer(c, false)} />
              </Row>
            </Card>
          ))}
        </>
      )}

      <SectionTitle title="Próximas actividades" />
      {days.length === 0 ? (
        <Card><Empty icon={<CalendarDays color={colors.textMuted} />} text="No hay actividades programadas." /></Card>
      ) : days.map(day => (
        <View key={day} style={{ gap: spacing.sm }}>
          <Text style={[font.small, { fontWeight: '700', textTransform: 'capitalize', color: day === today ? colors.primaryDark : colors.textSecondary }]}>
            {day === today ? 'Hoy · ' : ''}{longDay(day)}
          </Text>
          {byDay[day].map(e => (
            <Card key={e.id} style={{ padding: spacing.md }}>
              <Row>
                <EventIcon type={e.type} />
                <View style={{ flex: 1 }}>
                  <Text style={font.h3}>{e.title}</Text>
                  <Text style={font.small}>
                    {EVENT_TYPE_LABEL[e.type] ?? 'Evento'}{e.startTime && !e.allDay ? ` · ${e.startTime}${e.endTime ? `–${e.endTime}` : ''}` : ''}
                  </Text>
                  {matchExtra(e.round, e.callTime) && <Text style={[font.small, { fontWeight: '600' }]}>{matchExtra(e.round, e.callTime)}</Text>}
                  {e.location && <Row style={{ marginTop: 2 }}><MapPin color={colors.textMuted} size={12} /><Text style={font.tiny}>{e.location}</Text></Row>}
                </View>
                {e.matchStatus && MATCH_STATUS[e.matchStatus] && <Badge tone={MATCH_STATUS[e.matchStatus].tone} text={MATCH_STATUS[e.matchStatus].text} />}
              </Row>
            </Card>
          ))}
        </View>
      ))}
    </Screen>
  );
}
