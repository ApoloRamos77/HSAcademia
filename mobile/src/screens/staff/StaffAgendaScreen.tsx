import React, { useState } from 'react';
import { Text, View } from 'react-native';
import { CalendarDays, ChevronRight } from 'lucide-react-native';
import { api } from '../../api/client';
import type { MobileEvent } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Badge, Card, Chips, Empty, Input, Loading, Row, Screen } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, spacing } from '../../theme';
import { longDay, todayISO } from '../../utils/format';
import { EVENT_TYPE_LABEL, EventIcon, MATCH_STATUS, matchBadgeText, matchExtra } from '../family/AgendaScreen';

type View_ = 'next' | 'past';
type Kind = 'all' | 'matches';

const norm = (s?: string | null) => (s ?? '').normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

/**
 * Agenda del personal: entrenamientos, partidos (con registro de resultado) y cumpleaños.
 * Empieza por hoy y lo próximo; lo pasado (hasta 6 meses) se ve con el filtro "Pasados" y se busca por nombre.
 */
export default function StaffAgendaScreen({ navigation }: any) {
  const { can } = useAuth();
  const [view, setView] = useState<View_>('next');
  const [kind, setKind] = useState<Kind>('all');
  const [q, setQ] = useState('');
  const { data, loading, refreshing, refresh } = useApi(() => api.get<MobileEvent[]>('/calendar/mobile/events').then(r => r.data), []);
  if (loading && !data) return <Loading />;

  const today = todayISO();
  const key = (e: MobileEvent) => e.date + (e.startTime ?? '');
  const query = norm(q.trim());
  const events = view === 'next'
    ? (data ?? []).filter(e => e.date >= today).sort((a, b) => key(a).localeCompare(key(b)))
    : (data ?? [])
      .filter(e => e.date < today && e.type !== 'Birthday')
      .filter(e => kind === 'all' || e.type === 'Tournament')
      .filter(e => !query || [e.title, e.category, e.opponent, e.tournament, e.location, e.round].some(v => norm(v).includes(query)))
      .sort((a, b) => key(b).localeCompare(key(a))); // lo más reciente primero
  const byDay = events.reduce<Record<string, MobileEvent[]>>((acc, e) => { (acc[e.date] ||= []).push(e); return acc; }, {});
  const days = Object.keys(byDay).slice(0, 30);

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <Chips<View_> options={[{ value: 'next', label: 'Próximos' }, { value: 'past', label: 'Pasados' }]} value={view} onChange={setView} />
      {view === 'past' && (
        <>
          <Input placeholder="Buscar por nombre, categoría, rival o torneo" value={q} onChangeText={setQ} autoCorrect={false} />
          <Chips<Kind> options={[{ value: 'all', label: 'Todos' }, { value: 'matches', label: 'Solo partidos' }]} value={kind} onChange={setKind} />
        </>
      )}
      {days.length === 0 ? (
        <Card><Empty icon={<CalendarDays color={colors.textMuted} />} text={view === 'next' ? 'No hay actividades programadas.' : 'No hay actividades pasadas con ese filtro.'} /></Card>
      ) : days.map(day => (
        <View key={day} style={{ gap: spacing.sm }}>
          <Text style={[font.small, { fontWeight: '700', textTransform: 'capitalize', color: day === today ? colors.primaryDark : colors.textSecondary }]}>
            {day === today ? 'Hoy · ' : ''}{longDay(day)}
          </Text>
          {byDay[day].map(e => {
            const isMatch = e.type === 'Tournament';
            const canRecord = isMatch && can('events.manage');
            const hasResult = e.goalsFor != null && e.goalsAgainst != null;
            return (
              <Card key={e.id} style={{ padding: spacing.md }} onPress={canRecord ? () => navigation.navigate('Match', { eventId: e.id, title: e.title }) : undefined}>
                <Row>
                  <EventIcon type={e.type} />
                  <View style={{ flex: 1 }}>
                    <Text style={font.h3}>{e.title}</Text>
                    <Text style={font.small}>{EVENT_TYPE_LABEL[e.type]}{e.startTime && !e.allDay ? ` · ${e.startTime}` : ''}{e.category ? ` · ${e.category}` : ''}</Text>
                    {matchExtra(e.round, e.callTime) ? <Text style={[font.small, { fontWeight: '600' }]}>{matchExtra(e.round, e.callTime)}</Text> : null}
                    {e.location ? <Text style={font.tiny}>{e.location}</Text> : null}
                    {e.matchStatus && MATCH_STATUS[e.matchStatus] ? <Text style={font.tiny}>{matchBadgeText(e.matchStatus, e.goalsFor, e.goalsAgainst)}</Text> : null}
                  </View>
                  {canRecord && <Badge tone={day < today && !hasResult ? 'warning' : 'info'} text={day < today && !hasResult ? 'Registrar resultado' : 'Resultado'} />}
                  {canRecord && <ChevronRight color={colors.textMuted} />}
                </Row>
              </Card>
            );
          })}
        </View>
      ))}
      {Object.keys(byDay).length > days.length && (
        <Text style={[font.tiny, { textAlign: 'center' }]}>{view === 'past' ? 'Se muestran los 30 días más recientes: usa el buscador para encontrar otros.' : 'Se muestran los próximos 30 días con actividades.'}</Text>
      )}
    </Screen>
  );
}
