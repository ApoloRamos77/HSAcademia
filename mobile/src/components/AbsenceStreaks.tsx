import React from 'react';
import { Linking, Pressable, Text, View } from 'react-native';
import { Phone } from 'lucide-react-native';
import type { AbsenceStreak } from '../api/types';
import { colors, font } from '../theme';
import { Card, Divider, Row, SectionTitle } from './ui';

/** Alumnos con varias faltas seguidas, con acceso directo para llamar a la familia. */
export default function AbsenceStreaks({ items }: { items: AbsenceStreak[] }) {
  if (items.length === 0) return null;
  return (
    <>
      <SectionTitle title="Faltas seguidas: llamar a la familia" />
      <Card>
        {items.map((s, i) => (
          <View key={s.studentId}>
            {i > 0 && <Divider />}
            <Row style={{ justifyContent: 'space-between' }}>
              <View style={{ flex: 1 }}>
                <Text style={[font.body, { color: colors.text, fontWeight: '600' }]}>{s.name}</Text>
                <Text style={font.tiny}>{s.category} · {s.absences} faltas seguidas</Text>
              </View>
              {!!s.contactPhone && (
                <Pressable onPress={() => Linking.openURL(`tel:${s.contactPhone}`)} hitSlop={10} accessibilityRole="button" accessibilityLabel={`Llamar a la familia de ${s.name}`}>
                  <Phone color={colors.primaryDark} size={20} />
                </Pressable>
              )}
            </Row>
          </View>
        ))}
      </Card>
    </>
  );
}
