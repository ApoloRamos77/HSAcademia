import React from 'react';
import { Pressable, Text, View } from 'react-native';
import { AlertTriangle, Cake, ChevronRight, Info, Inbox, UserPlus } from 'lucide-react-native';
import { api } from '../../api/client';
import type { Dashboard } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Badge, Banner, Button, Card, Divider, Loading, Row, Screen, SectionTitle, Stat } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, radius, spacing } from '../../theme';
import { eventLabel, firstName, greeting, longDay, money } from '../../utils/format';

/** Resumen del administrador: alumnos, cobranza, morosidad, asistencia, comprobantes por validar y agenda. */
export default function AdminHomeScreen({ navigation }: any) {
  const { session } = useAuth();
  const { data, loading, refreshing, refresh } = useApi(async () => {
    const [dash, proofs] = await Promise.all([
      api.get<Dashboard>('/dashboard').then(r => r.data),
      api.get<{ count: number }>('/finances/proofs/pending-count').then(r => r.data.count).catch(() => 0),
    ]);
    return { dash, proofs };
  }, []);

  if (loading && !data) return <Loading />;
  const d = data?.dash;
  const f = d?.finance;

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <View>
        <Text style={font.h1}>{greeting()}, {firstName(session?.fullName)}</Text>
        <Text style={font.small}>{session?.academyName}</Text>
      </View>

      <Button title="Inscribir alumno" icon={<UserPlus color={colors.white} size={18} />} onPress={() => navigation.navigate('StudentForm')} />

      {(data?.proofs ?? 0) > 0 && (
        <Pressable onPress={() => navigation.navigate('Comprobantes')}>
          <Banner tone="warning" icon={<Inbox color={colors.warning} size={20} />} text={`${data!.proofs} comprobante(s) de pago por validar. Toca para revisarlos.`} />
        </Pressable>
      )}

      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
        <Stat label="Alumnos activos" value={String(d?.students.active ?? 0)} sub={`+${d?.students.newThisMonth ?? 0} nuevos · −${d?.students.withdrawnThisMonth ?? 0} retiros`} />
        <Stat label="Asistencia 30 días" value={d?.attendance.rateLast30Days != null ? `${d.attendance.rateLast30Days}%` : '—'}
          tone={d?.attendance.rateLast30Days != null && d.attendance.rateLast30Days < 70 ? 'danger' : undefined} />
        {f && <Stat label="Cobrado este mes" value={money(f.collected)} sub={`Facturado ${money(f.billed)}`} />}
        {f && <Stat label="Deuda vencida" value={money(f.overdueAmount)} sub={`${f.overdueStudents} alumno(s)`} tone={f.overdueAmount > 0 ? 'danger' : undefined} />}
      </View>

      {f && (
        <Card>
          <Row style={{ justifyContent: 'space-between' }}>
            <Text style={font.h3}>Cobranza del mes</Text>
            <Text style={[font.h3, { color: f.collectionRate >= 80 ? colors.success : f.collectionRate >= 50 ? colors.warning : colors.danger }]}>{f.collectionRate}%</Text>
          </Row>
          <View style={{ height: 10, backgroundColor: colors.surface, borderRadius: radius.pill, marginTop: spacing.sm }}>
            <View style={{ width: `${Math.min(100, f.collectionRate)}%`, height: '100%', borderRadius: radius.pill,
              backgroundColor: f.collectionRate >= 80 ? colors.accent : f.collectionRate >= 50 ? '#F59E0B' : '#EF4444' }} />
          </View>
          <Text style={[font.tiny, { marginTop: 6 }]}>Pendiente del mes: {money(f.pending)}</Text>
        </Card>
      )}

      {(d?.alerts ?? []).length > 0 && (
        <>
          <SectionTitle title="Alertas" />
          {d!.alerts.map((a, i) => (
            <Banner key={i} tone={a.level === 'danger' ? 'danger' : a.level === 'warning' ? 'warning' : 'info'}
              icon={a.level === 'info' ? <Info color={colors.info} size={18} /> : <AlertTriangle color={a.level === 'danger' ? colors.danger : colors.warning} size={18} />}
              text={a.message} />
          ))}
        </>
      )}

      {f && f.topOverdue.length > 0 && (
        <>
          <SectionTitle title="Mayores deudas" />
          <Card>
            {f.topOverdue.map((o, i) => (
              <View key={o.studentId}>
                {i > 0 && <Divider />}
                <Row style={{ justifyContent: 'space-between' }}>
                  <View style={{ flex: 1 }}>
                    <Text style={[font.body, { color: colors.text, fontWeight: '600' }]}>{o.name}</Text>
                    <Text style={font.tiny}>{o.category} · {o.maxDaysOverdue} días de atraso</Text>
                  </View>
                  <Text style={[font.body, { color: colors.danger, fontWeight: '700' }]}>{money(o.amount)}</Text>
                </Row>
              </View>
            ))}
          </Card>
        </>
      )}

      <SectionTitle title="Próximos eventos" action={<Pressable onPress={() => navigation.navigate('Agenda')} hitSlop={8}><ChevronRight color={colors.textMuted} /></Pressable>} />
      <Card>
        {(d?.upcomingEvents ?? []).length === 0 ? <Text style={font.small}>Sin eventos programados.</Text> : d!.upcomingEvents.map((e, i) => (
          <View key={e.id}>
            {i > 0 && <Divider />}
            <Text style={[font.body, { color: colors.text, fontWeight: '600' }]}>{e.title}{e.opponent ? ` vs. ${e.opponent}` : ''}</Text>
            <Text style={font.tiny}>{eventLabel(e.startTime)} · {e.categories}</Text>
          </View>
        ))}
      </Card>

      {(d?.birthdays ?? []).length > 0 && (
        <>
          <SectionTitle title="Cumpleaños de la semana" />
          <Card>
            {d!.birthdays.map((b, i) => (
              <View key={b.studentId}>
                {i > 0 && <Divider />}
                <Row style={{ justifyContent: 'space-between' }}>
                  <Row style={{ flex: 1 }}>
                    <Cake color={colors.accentDark} size={18} />
                    <View style={{ flex: 1 }}>
                      <Text style={[font.body, { color: colors.text }]}>{b.name}</Text>
                      <Text style={[font.tiny, { textTransform: 'capitalize' }]}>{b.isToday ? 'Hoy' : longDay(b.date)} · {b.category}</Text>
                    </View>
                  </Row>
                  <Badge tone={b.isToday ? 'warning' : 'muted'} text={`${b.turnsAge} años`} />
                </Row>
              </View>
            ))}
          </Card>
        </>
      )}
    </Screen>
  );
}
