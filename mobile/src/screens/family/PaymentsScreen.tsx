import React from 'react';
import { Text, View } from 'react-native';
import { CheckCircle2, Clock, Inbox, Upload } from 'lucide-react-native';
import { api } from '../../api/client';
import type { Debt, PaymentProof, SessionSummary } from '../../api/types';
import { Badge, Banner, Button, Card, Divider, Empty, Loading, Row, Screen, SectionTitle } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, spacing } from '../../theme';
import { longDay, money, shortDay } from '../../utils/format';
import { useFamily } from './FamilyContext';
import StudentPicker from './StudentPicker';

const PROOF: Record<string, { label: string; tone: 'warning' | 'success' | 'danger' }> = {
  Pending: { label: 'En validación', tone: 'warning' },
  Approved: { label: 'Aprobado', tone: 'success' },
  Rejected: { label: 'Rechazado', tone: 'danger' },
};

export default function PaymentsScreen({ navigation }: any) {
  const { student } = useFamily();
  const { data, loading, refreshing, refresh } = useApi(async () => {
    const [debts, proofs, sessions] = await Promise.all([
      api.get<Debt[]>('/finances/my-debts').then(r => r.data),
      api.get<PaymentProof[]>('/finances/my-proofs').then(r => r.data).catch(() => [] as PaymentProof[]),
      // Servidores anteriores no tienen el resumen de sesiones.
      api.get<SessionSummary[]>('/finances/sessions/my-summary').then(r => r.data).catch(() => [] as SessionSummary[]),
    ]);
    return { debts, proofs, sessions };
  }, []);

  if (loading && !data) return <Loading />;
  const sid = student?.id;
  const debts = (data?.debts ?? []).filter(d => d.studentId === sid);
  const pending = debts.filter(d => !d.isPaid);
  const paid = debts.filter(d => d.isPaid).slice(-6).reverse();
  const proofs = (data?.proofs ?? []).filter(p => p.studentId === sid);
  const session = (data?.sessions ?? []).find(x => x.studentId === sid);
  const inReview = new Set(proofs.filter(p => p.status === 'Pending').map(p => p.paymentRecordId));

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <StudentPicker />
      <Banner tone="info" text="Paga por Yape, Plin o transferencia hasta el día de vencimiento y envía la foto del comprobante. La academia lo valida y registra tu pago con su recibo." />

      {session && (
        <>
          <SectionTitle title="Sesiones de este mes" />
          <Card>
            <Text style={font.h3}>
              {session.attended} {session.attended === 1 ? 'sesión' : 'sesiones'}
              {session.plan === 'Package' && session.packageSessions ? ` de ${session.packageSessions} del paquete` : ''}
            </Text>
            <Text style={font.small}>
              {session.plan === 'Package'
                ? (session.accrued > 0 ? `Sesiones extra: ${money(session.accrued)}` : 'Aún dentro del paquete.')
                : `Acumulado: ${money(session.accrued)}${session.paidInField > 0 ? ` · pagado en campo ${money(session.paidInField)}` : ''}`}
            </Text>
            {session.capReached && <Text style={[font.tiny, { color: colors.success, marginTop: 4 }]}>Llegaste al tope del mes: las demás sesiones no se cobran.</Text>}
            <Text style={[font.tiny, { marginTop: 4 }]}>Se liquida a inicio del próximo mes. Cuentan las sesiones con asistencia (Presente o Tarde).</Text>
          </Card>
        </>
      )}

      <SectionTitle title="Por pagar" />
      {pending.length === 0 ? (
        <Card><Empty icon={<CheckCircle2 color={colors.success} size={36} />} text="¡Todo al día! No hay pagos pendientes." /></Card>
      ) : pending.map(d => (
        <Card key={d.id} style={d.status === 'Vencido' ? { borderLeftWidth: 4, borderLeftColor: colors.danger } : undefined}>
          <Row style={{ justifyContent: 'space-between', alignItems: 'flex-start' }}>
            <View style={{ flex: 1 }}>
              <Text style={font.h3}>{d.description}</Text>
              <Row style={{ marginTop: 4 }}><Clock color={colors.textMuted} size={14} /><Text style={font.small}>Vence el {longDay(d.dueDate)}</Text></Row>
              {d.amountPaid > 0 && <Text style={font.tiny}>Abonado {money(d.amountPaid)} de {money(d.amount)}</Text>}
            </View>
            <View style={{ alignItems: 'flex-end', gap: 6 }}>
              <Text style={{ fontSize: 20, fontWeight: '700', color: colors.text }}>{money(d.amountPending)}</Text>
              <Badge tone={d.requiresRegularization || (d.status === 'Vencido' && !d.regularizedAt) ? 'danger' : 'warning'}
                text={d.requiresRegularization ? `Vencido ${d.daysOverdue} d` : d.regularizedAt ? 'Regularizado' : d.status === 'Vencido' ? `Vencido ${d.daysOverdue} d` : 'Pendiente'} />
            </View>
          </Row>
          {!!d.regularizedAt && (
            <Text style={[font.tiny, { marginTop: 6 }]}>Regularizado {d.lateFeeAmount ? `con mora de ${money(d.lateFeeAmount)}` : 'sin mora'}{d.regularizationNote ? `: ${d.regularizationNote}` : ''}</Text>
          )}
          <View style={{ marginTop: spacing.md }}>
            {d.requiresRegularization && !inReview.has(d.id) && (
              <View style={{ marginBottom: spacing.sm }}>
                <Banner tone="danger" text={`Tu pago venció el ${longDay(d.dueDate)}. Puede aplicarse una mora: comunícate con administración o envía tu comprobante y lo evaluarán.`} />
              </View>
            )}
            {inReview.has(d.id) ? (
              <Banner tone="warning" text="Tu comprobante está en validación." />
            ) : (
              <Button title="Enviar comprobante" icon={<Upload color={colors.white} size={18} />} onPress={() => navigation.navigate('SendProof', { debt: d })} />
            )}
          </View>
        </Card>
      ))}

      <SectionTitle title="Mis comprobantes" />
      {proofs.length === 0 ? (
        <Card><Empty icon={<Inbox color={colors.textMuted} />} text="Aún no enviaste comprobantes." /></Card>
      ) : (
        <Card>
          {proofs.slice(0, 10).map((p, i) => (
            <View key={p.id}>
              {i > 0 && <Divider />}
              <Row style={{ justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <View style={{ flex: 1 }}>
                  <Text style={[font.body, { color: colors.text, fontWeight: '600' }]}>{p.debtDescription}</Text>
                  <Text style={font.small}>Pagado el {shortDay(p.paidOn)} · {money(p.amount)}</Text>
                  {p.receiptNumber && <Text style={font.tiny}>Recibo N° {p.receiptNumber}</Text>}
                  {p.rejectionReason && <Text style={[font.small, { color: colors.danger }]}>Motivo: {p.rejectionReason}</Text>}
                </View>
                <Badge tone={PROOF[p.status].tone} text={PROOF[p.status].label} />
              </Row>
            </View>
          ))}
        </Card>
      )}

      {paid.length > 0 && (
        <>
          <SectionTitle title="Pagos recientes" />
          <Card>
            {paid.map((d, i) => (
              <View key={d.id}>
                {i > 0 && <Divider />}
                <Row style={{ justifyContent: 'space-between' }}>
                  <Text style={font.body}>{d.description}</Text>
                  <Text style={[font.body, { color: colors.success, fontWeight: '600' }]}>{money(d.amount)}</Text>
                </Row>
              </View>
            ))}
          </Card>
        </>
      )}
    </Screen>
  );
}
