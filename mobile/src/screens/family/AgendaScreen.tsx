import React, { useState } from 'react';
import { Alert, Text, View } from 'react-native';
import { Cake, CalendarDays, Dumbbell, MapPin, Trophy } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { MobileEvent, MyCall, MyTournament } from '../../api/types';
import { Badge, Banner, Button, Card, Empty, Loading, Row, Screen, SectionTitle } from '../../components/ui';
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
    const [events, calls, tournaments] = await Promise.all([
      api.get<MobileEvent[]>('/calendar/mobile/events', { params: { categoryId: student.categoryId, studentId: student.id } }).then(r => r.data),
      api.get<MyCall[]>('/calendar/mobile/my-calls').then(r => r.data).catch(() => [] as MyCall[]),
      api.get<MyTournament[]>('/tournaments/mobile/mine', { params: { studentId: student.id } }).then(r => r.data).catch(() => [] as MyTournament[]),
    ]);
    return { events, calls, tournaments };
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

  const authorize = (t: MyTournament) => {
    Alert.alert(
      `Autorizar a ${t.studentName}`,
      `${t.authorizationText ?? 'Autorizo su participación en el torneo.'}\n\nTorneo: ${t.name}`,
      [
        { text: 'Cancelar', style: 'cancel' },
        {
          text: 'Acepto',
          onPress: async () => {
            setBusy(t.tournamentId);
            try {
              await api.post(`/tournaments/mobile/${t.tournamentId}/authorize`, { studentId: t.studentId });
              setData(d => d && ({ ...d, tournaments: d.tournaments.map(x => (x.tournamentId === t.tournamentId && x.studentId === t.studentId ? { ...x, authorized: true } : x)) }));
            } catch (e) {
              Alert.alert('No se pudo autorizar', errorMessage(e));
            } finally {
              setBusy(null);
            }
          },
        },
      ],
    );
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

      {(data?.tournaments ?? []).length > 0 && (
        <>
          <SectionTitle title="Torneos" />
          {(data?.tournaments ?? []).map(t => (
            <Card key={`${t.tournamentId}-${t.studentId}`}>
              <Row style={{ justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <View style={{ flex: 1 }}>
                  <Text style={font.h3}>{t.name}</Text>
                  <Text style={font.small}>{t.teamName}{t.shirtNumber != null ? ` · camiseta ${t.shirtNumber}` : ''}</Text>
                  {t.record.played > 0 && (
                    <Text style={[font.small, { fontWeight: '600' }]}>
                      {t.record.played} PJ · {t.record.won}G {t.record.drawn}E {t.record.lost}P · {t.record.points} pts
                    </Text>
                  )}
                  {t.lastResults[0] && (
                    <Text style={font.tiny}>
                      Último: {t.lastResults[0].round ? `${t.lastResults[0].round} · ` : ''}{t.lastResults[0].goalsFor}-{t.lastResults[0].goalsAgainst}
                      {t.lastResults[0].opponent ? ` vs ${t.lastResults[0].opponent}` : ''}
                    </Text>
                  )}
                  {t.nextMatch && <Text style={font.tiny}>Próximo: {t.nextMatch}</Text>}
                </View>
                <Trophy color={colors.warning} size={18} />
              </Row>
              {t.suspendedFor && <View style={{ marginTop: spacing.sm }}><Banner tone="danger" text={`Suspendido · ${t.suspendedFor}`} /></View>}
              {t.missingDocuments.length > 0 && (
                <View style={{ marginTop: spacing.sm }}><Banner tone="warning" text={`Falta entregar: ${t.missingDocuments.join(', ')}`} /></View>
              )}
              {t.requiresAuthorization && !t.authorized && (
                <View style={{ marginTop: spacing.sm, gap: spacing.sm }}>
                  <Banner tone="info" text="La academia pide tu autorización para este torneo." />
                  <Button compact title="Leer y autorizar" loading={busy === t.tournamentId} onPress={() => authorize(t)} />
                </View>
              )}
              {t.requiresAuthorization && t.authorized && <View style={{ marginTop: spacing.sm }}><Badge tone="success" text="Autorizado" /></View>}
            </Card>
          ))}
        </>
      )}

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
