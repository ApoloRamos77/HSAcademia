import React, { useEffect, useState } from 'react';
import { Alert, Pressable, Share, Text, View } from 'react-native';
import { CheckCircle2, Receipt, Share2, Wallet } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { Debt, StudentSummary } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Badge, Banner, Button, Card, Chips, Empty, Input, Loading, Row, Screen, SectionTitle } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, radius, spacing } from '../../theme';
import { money, shortDay } from '../../utils/format';

type Method = 'Cash' | 'Yape' | 'Plin' | 'BankTransfer';
const METHODS: { value: Method; label: string }[] = [
  { value: 'Cash', label: 'Efectivo' },
  { value: 'Yape', label: 'Yape' },
  { value: 'Plin', label: 'Plin' },
  { value: 'BankTransfer', label: 'Transferencia' },
];
const methodLabel = (m: string) => METHODS.find(x => x.value === m)?.label ?? m;

/**
 * Cobro en campo: el coordinador registra el pago (total o parcial) de una deuda del alumno
 * y comparte el comprobante con el apoderado. El backend controla concurrencia y periodos cerrados.
 */
export default function FieldPaymentScreen({ route, navigation }: any) {
  const student: StudentSummary = route.params.student;
  const { academy } = useAuth();
  useEffect(() => { navigation.setOptions({ title: `Cobrar · ${student.firstName}` }); }, [navigation, student]);

  const { data, loading, refreshing, refresh } = useApi(
    () => api.get<Debt[]>('/finances/debts/pending', { params: { studentId: student.id } }).then(r => r.data.filter(d => d.studentId === student.id)),
    [student.id],
  );

  const [selected, setSelected] = useState<Debt | null>(null);
  const [amount, setAmount] = useState('');
  const [method, setMethod] = useState<Method>('Cash');
  const [operation, setOperation] = useState('');
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<{ debt: Debt; paid: number; receipt?: string | null; method: Method } | null>(null);

  const pick = (d: Debt) => {
    setSelected(d);
    setAmount(d.amountPending.toFixed(2));
    setError(null);
  };

  const total = (data ?? []).reduce((a, d) => a + d.amountPending, 0);
  const needsOperation = method !== 'Cash';

  const pay = () => {
    if (!selected) return;
    const value = Number(amount.replace(',', '.'));
    if (!value || Number.isNaN(value) || value <= 0) { setError('Ingresa un monto válido.'); return; }
    if (value > selected.amountPending + 0.001) { setError(`El monto supera lo pendiente (${money(selected.amountPending)}).`); return; }
    if (needsOperation && !operation.trim()) { setError('Ingresa el número de operación del pago digital.'); return; }
    setError(null);
    Alert.alert(
      'Confirmar cobro',
      `${money(value)} en ${methodLabel(method)}\n${selected.description}\n${student.firstName} ${student.lastName}`,
      [
        { text: 'Cancelar', style: 'cancel' },
        { text: 'Registrar', onPress: () => submit(value) },
      ],
    );
  };

  const submit = async (value: number) => {
    if (!selected) return;
    setSaving(true);
    try {
      const { data: updated } = await api.post<Debt>('/finances/debts/pay', {
        paymentRecordId: selected.id,
        amountPaid: value,
        method,
        operationNumber: operation.trim() || null,
        notes: [notes.trim(), 'Cobro en campo (app)'].filter(Boolean).join(' · '),
      });
      const last = [...(updated.installments ?? [])].sort((a, b) => b.paidAt.localeCompare(a.paidAt))[0];
      setDone({ debt: updated, paid: value, receipt: last?.receiptNumber, method });
      setSelected(null);
      setOperation('');
      setNotes('');
      refresh();
    } catch (e) {
      setError(errorMessage(e, 'No se pudo registrar el pago.'));
    } finally {
      setSaving(false);
    }
  };

  const shareReceipt = () => {
    if (!done) return;
    const pending = done.debt.amount - done.debt.amountPaid;
    Share.share({
      message:
        `${academy?.name ?? 'Academia'} — Constancia de pago\n` +
        (done.receipt ? `Recibo N° ${done.receipt}\n` : '') +
        `Alumno: ${student.firstName} ${student.lastName}\n` +
        `Concepto: ${done.debt.description}\n` +
        `Monto pagado: ${money(done.paid)} (${methodLabel(done.method)})\n` +
        (pending > 0.001 ? `Saldo pendiente: ${money(pending)}\n` : 'Deuda cancelada ✅\n') +
        `Fecha: ${new Date().toLocaleDateString('es-PE')}\n\nGracias por su pago.`,
    });
  };

  if (loading && !data) return <Loading />;

  if (done) {
    const pending = done.debt.amount - done.debt.amountPaid;
    return (
      <Screen>
        <Card style={{ alignItems: 'center', gap: spacing.sm }}>
          <CheckCircle2 color={colors.success} size={48} />
          <Text style={font.h2}>Pago registrado</Text>
          <Text style={[font.h1, { color: colors.accentDark }]}>{money(done.paid)}</Text>
          <Text style={font.body}>{done.debt.description} · {methodLabel(done.method)}</Text>
          {!!done.receipt && <Badge tone="info" text={`Recibo N° ${done.receipt}`} />}
          {pending > 0.001 ? <Text style={font.small}>Saldo pendiente: {money(pending)}</Text> : <Text style={font.small}>Deuda cancelada</Text>}
        </Card>
        <Button title="Compartir constancia" icon={<Share2 color={colors.white} size={18} />} onPress={shareReceipt} />
        {(data ?? []).length > 0
          ? <Button title="Cobrar otra deuda" variant="secondary" onPress={() => setDone(null)} />
          : null}
        <Button title="Volver al alumno" variant="ghost" onPress={() => navigation.goBack()} />
      </Screen>
    );
  }

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <Card>
        <Text style={font.small}>Total pendiente</Text>
        <Text style={[font.h1, { color: total > 0 ? colors.danger : colors.success }]}>{money(total)}</Text>
        <Text style={font.small}>{student.firstName} {student.lastName} · {student.categoryName}</Text>
      </Card>

      <SectionTitle title="Deudas pendientes" />
      {(data ?? []).length === 0 ? (
        <Empty icon={<CheckCircle2 color={colors.success} />} text="El alumno está al día. No tiene deudas pendientes." />
      ) : (
        (data ?? []).map(d => {
          const active = selected?.id === d.id;
          return (
            <Pressable key={d.id} onPress={() => pick(d)} accessibilityRole="radio" accessibilityState={{ selected: active }}
              style={[{ backgroundColor: colors.card, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, padding: spacing.md },
                active && { borderColor: colors.primaryDark, borderWidth: 2 }]}>
              <Row style={{ justifyContent: 'space-between' }}>
                <View style={{ flex: 1 }}>
                  <Text style={font.h3}>{d.description}</Text>
                  <Text style={font.small}>Vence {shortDay(d.dueDate)}{d.amountPaid > 0 ? ` · pagado ${money(d.amountPaid)}` : ''}</Text>
                </View>
                <View style={{ alignItems: 'flex-end', gap: 4 }}>
                  <Text style={[font.h3, { color: colors.text }]}>{money(d.amountPending)}</Text>
                  <Badge tone={d.status === 'Vencido' ? 'danger' : 'warning'} text={d.status === 'Vencido' ? `Vencido ${d.daysOverdue}d` : 'En curso'} />
                </View>
              </Row>
            </Pressable>
          );
        })
      )}

      {selected && (
        <>
          <SectionTitle title="Registrar cobro" />
          <Card>
            <Input label="Monto (S/)" value={amount} onChangeText={v => setAmount(v.replace(',', '.'))} keyboardType="decimal-pad"
              hint={`Pendiente: ${money(selected.amountPending)}. Puedes registrar un pago parcial.`} />
            <Text style={[font.small, { fontWeight: '600', color: colors.textSecondary, marginBottom: 6 }]}>Medio de pago</Text>
            <Chips options={METHODS} value={method} onChange={setMethod} />
            {needsOperation && (
              <View style={{ marginTop: spacing.md }}>
                <Input label="N° de operación *" value={operation} onChangeText={setOperation} keyboardType="number-pad" maxLength={30} />
              </View>
            )}
            <View style={{ marginTop: needsOperation ? 0 : spacing.md }}>
              <Input label="Nota (opcional)" value={notes} onChangeText={setNotes} maxLength={200} />
            </View>
          </Card>
          {method === 'Cash' && (
            <Banner tone="warning" icon={<Wallet color={colors.warning} size={18} />}
              text="Recibes efectivo: entrégalo en caja al terminar la jornada. El pago queda a tu nombre en el registro de auditoría." />
          )}
          {error && <Banner tone="danger" text={error} />}
          <Button title={`Registrar ${money(Number(amount) || 0)}`} variant="success" icon={<Receipt color={colors.white} size={18} />} loading={saving} onPress={pay} />
        </>
      )}
    </Screen>
  );
}
