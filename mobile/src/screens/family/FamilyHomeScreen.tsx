import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { CalendarDays, CheckCircle2, CreditCard, Megaphone, Pin, QrCode, Trophy, ChevronRight, UserCircle2 } from 'lucide-react-native';
import { api } from '../../api/client';
import type { Announcement, AttendanceMonth, Debt, MyCall } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Badge, Banner, Card, Empty, Loading, Row, Screen, SectionTitle } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, radius, spacing } from '../../theme';
import { firstName, greeting, instantDate, money } from '../../utils/format';
import { useFamily } from './FamilyContext';
import StudentPicker from './StudentPicker';

interface NextEvent { title: string; date: string; location: string; type: string }

export default function FamilyHomeScreen({ navigation }: any) {
  const { session } = useAuth();
  const { student, loading: loadingFamily } = useFamily();

  const { data, loading, refreshing, refresh } = useApi(async () => {
    if (!student) return null;
    const [debts, calls, history, news, next] = await Promise.all([
      api.get<Debt[]>('/finances/my-debts').then(r => r.data),
      api.get<MyCall[]>('/calendar/mobile/my-calls').then(r => r.data).catch(() => [] as MyCall[]),
      api.get<AttendanceMonth[]>('/attendance/my-history', { params: { studentId: student.id, months: 1 } }).then(r => r.data).catch(() => []),
      api.get<Announcement[]>('/announcement').then(r => r.data).catch(() => [] as Announcement[]),
      api.get<NextEvent>('/calendar/mobile/next', { params: { categoryId: student.categoryId } }).then(r => (r.status === 204 ? null : r.data)).catch(() => null),
    ]);
    return { debts, calls, history, news, next };
  }, [student?.id]);

  if (loadingFamily || (loading && !data)) return <Loading text="Cargando…" />;
  if (!student) {
    return <Screen><Empty icon={<UserCircle2 color={colors.textMuted} size={40} />} text="Tu cuenta aún no tiene alumnos vinculados. Comunícate con la academia." /></Screen>;
  }

  const pending = (data?.debts ?? []).filter(d => d.studentId === student.id && !d.isPaid);
  const totalPending = pending.reduce((s, d) => s + d.amountPending, 0);
  const overdue = pending.filter(d => d.status === 'Vencido');
  const toRegularize = pending.filter(d => d.requiresRegularization);
  const callsToAnswer = (data?.calls ?? []).filter(c => c.studentId === student.id && c.isConfirmed === null);
  const month = data?.history?.[0];
  const attended = month ? month.present : 0;
  const counted = month ? month.present + month.absent : 0;
  const rate = counted ? Math.round((100 * attended) / counted) : null;

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <View>
        <Text style={font.h1}>{greeting()}, {firstName(session?.fullName)}</Text>
        <Text style={font.small}>{student.firstName} {student.lastName} · {student.categoryName} · Sede {student.headquarterName}</Text>
      </View>
      <StudentPicker />

      {/* Estado de cuenta */}
      <Card onPress={() => navigation.navigate('Pagos')} style={pending.length ? (overdue.length ? styles.cardDanger : undefined) : styles.cardOk}>
        <Row style={{ justifyContent: 'space-between' }}>
          <Row>
            {pending.length ? <CreditCard color={overdue.length ? colors.danger : colors.primaryDark} /> : <CheckCircle2 color={colors.success} />}
            <Text style={font.h3}>{pending.length ? 'Pagos pendientes' : '¡Todo al día!'}</Text>
          </Row>
          <ChevronRight color={colors.textMuted} />
        </Row>
        {pending.length > 0 ? (
          <>
            <Text style={styles.big}>{money(totalPending)}</Text>
            <Text style={font.small}>
              {toRegularize.length
                ? `${toRegularize.length} pago(s) vencido(s): acércate a administración para regularizar.`
                : `${overdue.length ? `${overdue.length} vencida(s). ` : ''}Toca para pagar con Yape, Plin o transferencia y enviar tu comprobante.`}
            </Text>
          </>
        ) : <Text style={font.small}>No hay mensualidades pendientes.</Text>}
      </Card>

      {/* Convocatorias por responder */}
      {callsToAnswer.length > 0 && (
        <Pressable onPress={() => navigation.navigate('Agenda')}>
          <Banner tone="warning" icon={<Trophy color={colors.warning} size={20} />}
            text={`${student.firstName} tiene ${callsToAnswer.length} convocatoria(s) por confirmar. Toca para responder.`} />
        </Pressable>
      )}

      {/* Próximo evento */}
      <Card onPress={() => navigation.navigate('Agenda')}>
        <Row><CalendarDays color={colors.primaryDark} /><Text style={font.h3}>Próxima actividad</Text></Row>
        {data?.next ? (
          <View style={{ marginTop: spacing.sm }}>
            <Text style={font.h2}>{data.next.title}</Text>
            <Text style={font.body}>{data.next.date}{data.next.location ? ` · ${data.next.location}` : ''}</Text>
          </View>
        ) : <Text style={[font.small, { marginTop: spacing.sm }]}>No hay actividades programadas.</Text>}
      </Card>

      {/* Accesos rápidos */}
      <View style={styles.grid}>
        <Quick icon={<QrCode color={colors.primaryDark} />} label="Carné QR" onPress={() => navigation.navigate('Carné')} />
        <Quick icon={<Trophy color={colors.primaryDark} />} label="Progreso" onPress={() => navigation.navigate('Progreso')} />
        <Quick icon={<CalendarDays color={colors.primaryDark} />} label="Asistencia"
          extra={rate != null ? `${rate}% este mes` : undefined} onPress={() => navigation.navigate('Progreso')} />
        <Quick icon={<UserCircle2 color={colors.primaryDark} />} label="Mi cuenta" onPress={() => navigation.navigate('Account')} />
      </View>

      {/* Comunicados */}
      <SectionTitle title="Comunicados" action={(data?.news?.length ?? 0) > 3 ? (
        <Text style={styles.link} onPress={() => navigation.navigate('News')}>Ver todos</Text>) : undefined} />
      {(data?.news ?? []).length === 0 ? (
        <Card><Empty icon={<Megaphone color={colors.textMuted} />} text="Sin comunicados recientes." /></Card>
      ) : (data?.news ?? []).slice(0, 3).map(n => (
        <Card key={n.id}>
          <Row>
            {n.isPinned && <Pin color={colors.primaryDark} size={16} />}
            <Text style={[font.h3, { flex: 1 }]}>{n.title}</Text>
          </Row>
          <Text style={[font.body, { marginTop: 4 }]} numberOfLines={4}>{n.content}</Text>
          <Text style={[font.tiny, { marginTop: 6 }]}>{instantDate(n.datePosted)}</Text>
        </Card>
      ))}
      {overdue.length > 0 && <Badge tone="danger" text="Tienes pagos vencidos" />}
    </Screen>
  );
}

function Quick({ icon, label, extra, onPress }: { icon: React.ReactNode; label: string; extra?: string; onPress: () => void }) {
  return (
    <Pressable onPress={onPress} style={({ pressed }) => [styles.quick, pressed && { opacity: 0.8 }]} accessibilityRole="button">
      <View style={styles.quickIcon}>{icon}</View>
      <Text style={[font.small, { color: colors.text, fontWeight: '600' }]}>{label}</Text>
      {extra && <Text style={font.tiny}>{extra}</Text>}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  big: { fontSize: 28, fontWeight: '700', color: colors.text, marginTop: spacing.sm },
  cardDanger: { borderColor: colors.danger, borderLeftWidth: 4 },
  cardOk: { borderColor: colors.successBg },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  quick: { width: '48%', flexGrow: 1, backgroundColor: colors.card, borderRadius: radius.lg, padding: spacing.md, borderWidth: 1, borderColor: colors.border, gap: 6 },
  quickIcon: { width: 40, height: 40, borderRadius: radius.md, backgroundColor: colors.primaryLight, alignItems: 'center', justifyContent: 'center' },
  link: { ...font.small, color: colors.primaryDark, fontWeight: '600' },
});

