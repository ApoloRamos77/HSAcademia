import React from 'react';
import { Text, View } from 'react-native';
import { CheckCircle2, Clock, Inbox, Upload } from 'lucide-react-native';
import { api } from '../../api/client';
import type { Debt, PaymentProof } from '../../api/types';
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
    const [debts, proofs] = await Promise.all([
      api.get<Debt[]>('/finances/my-debts').then(r => r.data),
      api.get<PaymentProof[]>('/finances/my-proofs').then(r => r.data).catch(() => [] as PaymentProof[]),
    ]);
    return { debts, proofs };
  }, []);

  if (loading && !data) return <Loading />;
  const sid = student?.id;
  const debts = (data?.debts ?? []).filter(d => d.studentId === sid);
  const pending = debts.filter(d => !d.isPaid);
  const paid = debts.filter(d => d.isPaid).slice(-6).reverse();
  const proofs = (data?.proofs ?? []).filter(p => p.studentId === sid);
  const inReview = new Set(proofs.filter(p => p.status === 'Pending').map(p => p.paymentRecordId));

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <StudentPicker />
      <Banner tone="info" text="Paga por Yape, Plin o transferencia y envía la foto del comprobante. La academia lo valida y registra tu pago con su recibo." />

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
              <Badge tone={d.status === 'Vencido' ? 'danger' : 'warning'} text={d.status === 'Vencido' ? `Vencido ${d.daysOverdue} d` : 'Pendiente'} />
            </View>
          </Row>
          <View style={{ marginTop: spacing.md }}>
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
