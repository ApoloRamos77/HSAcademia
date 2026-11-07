import React, { useState } from 'react';
import { Alert, Pressable, StyleSheet, Switch, Text } from 'react-native';
import { api, errorMessage } from '../../api/client';
import type { StudentSummary } from '../../api/types';
import { Banner, Button, Card, Input, Row, Screen } from '../../components/ui';
import { colors, font, radius, spacing } from '../../theme';

const DIMENSIONS = [
  { key: 'technical', label: 'Técnica', hint: 'Control, pase, conducción, definición' },
  { key: 'tactical', label: 'Táctica', hint: 'Posicionamiento, lectura de juego' },
  { key: 'physical', label: 'Física', hint: 'Resistencia, velocidad, coordinación' },
  { key: 'attitude', label: 'Actitud', hint: 'Disciplina, compañerismo, compromiso' },
] as const;
const SCALE = ['', 'En inicio', 'En proceso', 'Logrado', 'Destacado', 'Sobresaliente'];

const defaultPeriod = () => {
  const d = new Date();
  return `${d.getFullYear()} - Trimestre ${Math.floor(d.getMonth() / 3) + 1}`;
};

/** Evaluación deportiva rápida desde la cancha. */
export default function EvaluationScreen({ route, navigation }: any) {
  const student: StudentSummary = route.params.student;
  const [period, setPeriod] = useState(defaultPeriod());
  const [scores, setScores] = useState<Record<string, number>>({ technical: 3, tactical: 3, physical: 3, attitude: 3 });
  const [strengths, setStrengths] = useState('');
  const [toImprove, setToImprove] = useState('');
  const [goals, setGoals] = useState('');
  const [visible, setVisible] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const save = async () => {
    if (!period.trim()) return setError('Indica el periodo.');
    setSaving(true);
    setError(null);
    try {
      await api.post(`/sports/students/${student.id}/evaluations`, {
        period: period.trim(), ...scores, strengths, toImprove, goals, isVisibleToFamily: visible,
      });
      Alert.alert('Evaluación guardada', visible ? 'La familia ya puede verla en su app.' : 'Quedó como evaluación interna.');
      navigation.goBack();
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Screen>
      <Text style={font.h2}>{student.firstName} {student.lastName}</Text>
      <Input label="Periodo" value={period} onChangeText={setPeriod} maxLength={60} />
      {DIMENSIONS.map(d => (
        <Card key={d.key} style={{ padding: spacing.md }}>
          <Row style={{ justifyContent: 'space-between' }}>
            <Text style={font.h3}>{d.label}</Text>
            <Text style={font.small}>{SCALE[scores[d.key]]}</Text>
          </Row>
          <Text style={font.tiny}>{d.hint}</Text>
          <Row style={{ marginTop: spacing.sm, justifyContent: 'space-between' }}>
            {[1, 2, 3, 4, 5].map(v => {
              const active = scores[d.key] === v;
              return (
                <Pressable key={v} onPress={() => setScores(s => ({ ...s, [d.key]: v }))} accessibilityRole="radio"
                  accessibilityState={{ selected: active }} accessibilityLabel={`${d.label} ${v}`}
                  style={[styles.score, active && { backgroundColor: colors.primaryDark, borderColor: colors.primaryDark }]}>
                  <Text style={[styles.scoreText, active && { color: colors.white }]}>{v}</Text>
                </Pressable>
              );
            })}
          </Row>
        </Card>
      ))}
      <Input label="Fortalezas" value={strengths} onChangeText={setStrengths} multiline maxLength={1000} />
      <Input label="Por mejorar" value={toImprove} onChangeText={setToImprove} multiline maxLength={1000} />
      <Input label="Objetivos del siguiente periodo" value={goals} onChangeText={setGoals} multiline maxLength={1000} />
      <Row style={{ justifyContent: 'space-between' }}>
        <Text style={[font.body, { flex: 1 }]}>Visible para la familia</Text>
        <Switch value={visible} onValueChange={setVisible} trackColor={{ true: colors.primary }} />
      </Row>
      {error && <Banner tone="danger" text={error} />}
      <Button title="Guardar evaluación" onPress={save} loading={saving} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  score: { width: 52, height: 48, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.card },
  scoreText: { fontSize: 18, fontWeight: '700', color: colors.textSecondary },
});
