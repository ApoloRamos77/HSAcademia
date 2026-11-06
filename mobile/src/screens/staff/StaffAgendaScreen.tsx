import React from 'react';
import { Text, View } from 'react-native';
import { CalendarDays, ChevronRight } from 'lucide-react-native';
import { api } from '../../api/client';
import type { MobileEvent } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Badge, Card, Empty, Loading, Row, Screen } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, spacing } from '../../theme';
import { longDay, todayISO } from '../../utils/format';
import { EVENT_TYPE_LABEL, EventIcon } from '../family/AgendaScreen';

/** Agenda del personal: entrenamientos, partidos (con registro de resultado) y cumpleaños. */
export default function StaffAgendaScreen({ navigation }: any) {
  const { can } = useAuth();
  const { data, loading, refreshing, refresh } = useApi(() => api.get<MobileEvent[]>('/calendar/mobile/events').then(r => r.data), []);
  if (loading && !data) return <Loading />;

  const today = todayISO();
  const recentFrom = new Date(Date.now() - 14 * 86400000).toISOString().slice(0, 10);
  // Próximas + partidos de las últimas 2 semanas (para registrar su resultado)
  const events = (data ?? [])
    .filter(e => e.date >= today || (e.type === 'Tournament' && e.date >= recentFrom))
    .sort((a, b) => (a.date + (a.startTime ?? '')).localeCompare(b.date + (b.startTime ?? '')));
  const byDay = events.reduce<Record<string, MobileEvent[]>>((acc, e) => { (acc[e.date] ||= []).push(e); return acc; }, {});

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      {Object.keys(byDay).length === 0 ? (
        <Card><Empty icon={<CalendarDays color={colors.textMuted} />} text="No hay actividades programadas." /></Card>
      ) : Object.keys(byDay).slice(0, 30).map(day => (
        <View key={day} style={{ gap: spacing.sm }}>
          <Text style={[font.small, { fontWeight: '700', textTransform: 'capitalize', color: day === today ? colors.primaryDark : colors.textSecondary }]}>
            {day === today ? 'Hoy · ' : day < today ? 'Pasado · ' : ''}{longDay(day)}
          </Text>
          {byDay[day].map(e => {
            const isMatch = e.type === 'Tournament';
            const canRecord = isMatch && can('manage_events');
            return (
              <Card key={e.id} style={{ padding: spacing.md }} onPress={canRecord ? () => navigation.navigate('Match', { eventId: e.id, title: e.title }) : undefined}>
                <Row>
                  <EventIcon type={e.type} />
                  <View style={{ flex: 1 }}>
                    <Text style={font.h3}>{e.title}</Text>
                    <Text style={font.small}>{EVENT_TYPE_LABEL[e.type]}{e.startTime && !e.allDay ? ` · ${e.startTime}` : ''}{e.category ? ` · ${e.category}` : ''}</Text>
                    {e.location ? <Text style={font.tiny}>{e.location}</Text> : null}
                  </View>
                  {canRecord && <Badge tone={day < today ? 'warning' : 'info'} text={day < today ? 'Registrar resultado' : 'Resultado'} />}
                  {canRecord && <ChevronRight color={colors.textMuted} />}
                </Row>
              </Card>
            );
          })}
        </View>
      ))}
    </Screen>
  );
}
