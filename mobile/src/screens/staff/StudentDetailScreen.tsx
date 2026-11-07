import React, { useEffect } from 'react';
import { Linking, Text, View } from 'react-native';
import { HeartPulse, MessageCircle, Phone, Star, Trophy } from 'lucide-react-native';
import { api } from '../../api/client';
import type { Evaluation, MedicalRecord, SportsSummary, StudentSummary } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Badge, Banner, Button, Card, Divider, Loading, Row, Screen, SectionTitle, Stat } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, spacing } from '../../theme';
import { shortDay } from '../../utils/format';

const digits = (s?: string | null) => (s ?? '').replace(/\D/g, '');
const waNumber = (s?: string | null) => { const d = digits(s); return d.length === 9 ? `51${d}` : d; };

/** Ficha del alumno para el entrenador: contacto de emergencia, salud, evaluaciones y partidos. */
export default function StudentDetailScreen({ route, navigation }: any) {
  const student: StudentSummary = route.params.student;
  const { can } = useAuth();
  useEffect(() => { navigation.setOptions({ title: `${student.firstName} ${student.lastName}` }); }, [navigation, student]);

  const { data, loading, refreshing, refresh } = useApi(async () => {
    const [medical, evaluations, sports] = await Promise.all([
      api.get<MedicalRecord>(`/students/${student.id}/medical-record`).then(r => r.data).catch(() => null),
      api.get<Evaluation[]>(`/sports/students/${student.id}/evaluations`).then(r => r.data).catch(() => [] as Evaluation[]),
      api.get<SportsSummary>(`/sports/students/${student.id}/summary`).then(r => r.data).catch(() => null),
    ]);
    return { medical, evaluations, sports };
  }, [student.id]);

  if (loading && !data) return <Loading />;
  const m = data?.medical;
  const hasHealthAlert = !!(m?.allergies || m?.medicalConditions);
  const emergencyPhone = m?.emergencyContactPhone || student.guardianPhone || student.phone;

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <View>
        <Text style={font.h2}>{student.firstName} {student.lastName}</Text>
        <Text style={font.small}>{student.categoryName} · {student.age} años · Sede {student.headquarterName}</Text>
        {student.documentNumber ? <Text style={font.tiny}>DNI {student.documentNumber}</Text> : null}
      </View>

      {hasHealthAlert && (
        <Banner tone="danger" icon={<HeartPulse color={colors.danger} size={20} />}
          text={[m?.allergies && `Alergias: ${m.allergies}`, m?.medicalConditions && `Condición: ${m.medicalConditions}`].filter(Boolean).join(' · ')} />
      )}

      <SectionTitle title="Contacto" />
      <Card>
        <Text style={font.small}>Apoderado</Text>
        <Text style={font.h3}>{student.guardianName || '—'}</Text>
        {!!student.guardianPhone && (
          <Row style={{ marginTop: spacing.sm }}>
            <Button compact title="Llamar" variant="secondary" icon={<Phone color={colors.primaryDark} size={16} />} style={{ flex: 1 }}
              onPress={() => Linking.openURL(`tel:${digits(student.guardianPhone)}`)} />
            <Button compact title="WhatsApp" variant="secondary" icon={<MessageCircle color={colors.primaryDark} size={16} />} style={{ flex: 1 }}
              onPress={() => Linking.openURL(`https://wa.me/${waNumber(student.guardianPhone)}`)} />
          </Row>
        )}
        <Divider />
        <Text style={font.small}>Contacto de emergencia</Text>
        <Text style={font.h3}>{m?.emergencyContactName || (m?.emergencyContactPhone ? 'Sin nombre' : 'No registrado')}</Text>
        {!!emergencyPhone && (
          <Button compact title={`Emergencia: ${emergencyPhone}`} variant="danger" icon={<Phone color={colors.danger} size={16} />}
            style={{ marginTop: spacing.sm }} onPress={() => Linking.openURL(`tel:${digits(emergencyPhone)}`)} />
        )}
      </Card>

      {(m?.weightKg || m?.heightCm) && (
        <View style={{ flexDirection: 'row', gap: spacing.sm }}>
          {m?.weightKg ? <Stat label="Peso" value={`${m.weightKg} kg`} /> : null}
          {m?.heightCm ? <Stat label="Talla" value={`${m.heightCm} cm`} /> : null}
          {m?.bmi ? <Stat label="IMC" value={Number(m.bmi).toFixed(1)} /> : null}
        </View>
      )}

      <SectionTitle title="Evaluaciones"
        action={can('view_students') ? <Button compact title="Evaluar" icon={<Star color={colors.white} size={16} />}
          onPress={() => navigation.navigate('Evaluation', { student })} /> : undefined} />
      {(data?.evaluations ?? []).length === 0 ? (
        <Card><Text style={font.small}>Sin evaluaciones registradas.</Text></Card>
      ) : (
        <Card>
          {data!.evaluations.slice(0, 4).map((e, i) => (
            <View key={e.id}>
              {i > 0 && <Divider />}
              <Row style={{ justifyContent: 'space-between' }}>
                <View style={{ flex: 1 }}>
                  <Text style={[font.body, { color: colors.text, fontWeight: '600' }]}>{e.period}</Text>
                  <Text style={font.tiny}>T {e.technical} · Tá {e.tactical} · F {e.physical} · A {e.attitude} · {shortDay(e.evaluationDate)}</Text>
                </View>
                <Badge tone="info" text={`${e.average}`} />
              </Row>
            </View>
          ))}
        </Card>
      )}

      {data?.sports && data.sports.matches > 0 && (
        <>
          <SectionTitle title="Partidos" />
          <Card>
            <Row>
              <Trophy color={colors.warning} />
              <Text style={font.body}>
                {data.sports.matches} partidos · {data.sports.minutes} min · {data.sports.goals} goles · {data.sports.assists} asist.
                {data.sports.averageRating != null ? ` · nota ${data.sports.averageRating}` : ''}
              </Text>
            </Row>
          </Card>
        </>
      )}
    </Screen>
  );
}
