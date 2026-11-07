import React, { useEffect, useState } from 'react';
import { Alert, Pressable, StyleSheet, Text, View } from 'react-native';
import { Minus, Plus } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { MatchResult } from '../../api/types';
import { Banner, Button, Card, Empty, Loading, Row, Screen, SectionTitle } from '../../components/ui';
import { colors, font, radius, spacing } from '../../theme';

type Player = MatchResult['players'][number];

function Stepper({ value, onChange, min = 0, max = 99, label }: { value: number; onChange: (v: number) => void; min?: number; max?: number; label: string }) {
  return (
    <View style={{ alignItems: 'center', gap: 2 }}>
      <Text style={font.tiny}>{label}</Text>
      <Row style={{ gap: 4 }}>
        <Pressable style={styles.step} onPress={() => onChange(Math.max(min, value - 1))} accessibilityLabel={`Restar ${label}`} hitSlop={4}><Minus size={16} color={colors.primaryDark} /></Pressable>
        <Text style={styles.stepValue}>{value}</Text>
        <Pressable style={styles.step} onPress={() => onChange(Math.min(max, value + 1))} accessibilityLabel={`Sumar ${label}`} hitSlop={4}><Plus size={16} color={colors.primaryDark} /></Pressable>
      </Row>
    </View>
  );
}

/** Resultado del partido y estadística por jugador, cargados desde el celular al terminar el encuentro. */
export default function MatchScreen({ route, navigation }: any) {
  const { eventId } = route.params;
  const [data, setData] = useState<MatchResult | null>(null);
  const [gf, setGf] = useState(0);
  const [ga, setGa] = useState(0);
  const [players, setPlayers] = useState<Player[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    navigation.setOptions({ title: route.params.title ?? 'Partido' });
    api.get<MatchResult>(`/sports/matches/${eventId}`).then(r => {
      setData(r.data);
      setGf(r.data.goalsFor ?? 0);
      setGa(r.data.goalsAgainst ?? 0);
      setPlayers(r.data.players);
    }).catch(e => setError(errorMessage(e)));
  }, [eventId, navigation, route.params.title]);

  const update = (id: string, patch: Partial<Player>) => setPlayers(ps => ps.map(p => (p.studentId === id ? { ...p, ...patch } : p)));

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      await api.put(`/sports/matches/${eventId}`, {
        goalsFor: gf, goalsAgainst: ga,
        players: players.filter(p => p.played).map(p => ({
          studentId: p.studentId, isStarter: p.isStarter, minutes: p.minutes, goals: p.goals, assists: p.assists,
          yellowCards: p.yellowCards, redCards: p.redCards, rating: p.rating ?? null,
        })),
      });
      Alert.alert('Resultado guardado', 'Las estadísticas ya se ven en la ficha de cada jugador.');
      navigation.goBack();
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  if (!data && !error) return <Loading />;

  return (
    <Screen>
      {error && <Banner tone="danger" text={error} />}
      {data && (
        <>
          <Card>
            <Text style={[font.small, { textAlign: 'center' }]}>{data.opponent ? `vs. ${data.opponent}` : 'Marcador'}</Text>
            <Row style={{ justifyContent: 'space-around', marginTop: spacing.sm }}>
              <Stepper label="A favor" value={gf} onChange={setGf} />
              <Text style={{ fontSize: 28, fontWeight: '700', color: colors.text }}>{gf} - {ga}</Text>
              <Stepper label="En contra" value={ga} onChange={setGa} />
            </Row>
          </Card>

          <SectionTitle title="Jugadores" />
          {players.length === 0 ? <Card><Empty text="No hay jugadores convocados." /></Card> : players.map(p => (
            <Card key={p.studentId} style={{ padding: spacing.md, opacity: p.played ? 1 : 0.65 }}>
              <Pressable onPress={() => update(p.studentId, { played: !p.played, minutes: !p.played && p.minutes === 0 ? 60 : p.minutes })}>
                <Row style={{ justifyContent: 'space-between' }}>
                  <View style={{ flex: 1 }}>
                    <Text style={font.h3}>{p.studentName}</Text>
                    <Text style={font.tiny}>{p.category}{p.called ? ' · convocado' : ''}</Text>
                  </View>
                  <View style={[styles.toggle, p.played && { backgroundColor: colors.accentDark, borderColor: colors.accentDark }]}>
                    <Text style={[styles.toggleText, p.played && { color: colors.white }]}>{p.played ? 'Jugó' : 'No jugó'}</Text>
                  </View>
                </Row>
              </Pressable>
              {p.played && (
                <View style={{ marginTop: spacing.md, gap: spacing.sm }}>
                  <Row style={{ justifyContent: 'space-between' }}>
                    <Stepper label="Min" value={p.minutes} max={200} onChange={v => update(p.studentId, { minutes: v })} />
                    <Stepper label="Goles" value={p.goals} onChange={v => update(p.studentId, { goals: v })} />
                    <Stepper label="Asist." value={p.assists} onChange={v => update(p.studentId, { assists: v })} />
                  </Row>
                  <Row style={{ justifyContent: 'space-between' }}>
                    <Pressable onPress={() => update(p.studentId, { isStarter: !p.isStarter })} style={[styles.toggle, p.isStarter && { backgroundColor: colors.primaryDark, borderColor: colors.primaryDark }]}>
                      <Text style={[styles.toggleText, p.isStarter && { color: colors.white }]}>Titular</Text>
                    </Pressable>
                    <Stepper label="TA" value={p.yellowCards} max={2} onChange={v => update(p.studentId, { yellowCards: v })} />
                    <Stepper label="TR" value={p.redCards} max={1} onChange={v => update(p.studentId, { redCards: v })} />
                    <Stepper label="Nota" value={p.rating ?? 0} max={10} onChange={v => update(p.studentId, { rating: v === 0 ? null : v })} />
                  </Row>
                </View>
              )}
            </Card>
          ))}
          <Button title="Guardar resultado" onPress={save} loading={saving} />
        </>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  step: { width: 32, height: 32, borderRadius: radius.sm, borderWidth: 1, borderColor: colors.border, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.primaryLight },
  stepValue: { minWidth: 24, textAlign: 'center', fontSize: 16, fontWeight: '700', color: colors.text },
  toggle: { paddingHorizontal: 12, paddingVertical: 6, borderRadius: radius.pill, borderWidth: 1, borderColor: colors.border, alignSelf: 'center' },
  toggleText: { fontSize: 13, fontWeight: '700', color: colors.textSecondary },
});
