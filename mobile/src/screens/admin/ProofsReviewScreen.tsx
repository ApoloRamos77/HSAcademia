import React, { useState } from 'react';
import { Alert, Image, Linking, Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { CheckCircle2, FileText, Inbox, X } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { PaymentProof, ProofStatus } from '../../api/types';
import { Badge, Banner, Button, Card, Chips, Empty, Input, Loading, Row, Screen } from '../../components/ui';
import { FILES_URL } from '../../config';
import { useApi } from '../../hooks/useApi';
import { colors, font, radius, spacing } from '../../theme';
import { instantDate, money, shortDay } from '../../utils/format';

const METHOD: Record<string, string> = { Yape: 'Yape', Plin: 'Plin', BankTransfer: 'Transferencia', Card: 'Tarjeta', Other: 'Otro', Cash: 'Efectivo' };
const STATUS: Record<ProofStatus, { label: string; tone: 'warning' | 'success' | 'danger' }> = {
  Pending: { label: 'Por validar', tone: 'warning' }, Approved: { label: 'Aprobado', tone: 'success' }, Rejected: { label: 'Rechazado', tone: 'danger' },
};
const fileUrl = (p: string) => (/^https?:/i.test(p) ? p : `${FILES_URL}${p}`);

/** Validación de comprobantes enviados por las familias: aprobar registra el abono con su recibo. */
export default function ProofsReviewScreen() {
  const [status, setStatus] = useState<ProofStatus>('Pending');
  const [selected, setSelected] = useState<PaymentProof | null>(null);
  const { data, loading, refreshing, refresh, reload } = useApi(
    () => api.get<PaymentProof[]>('/finances/proofs', { params: { status } }).then(r => r.data), [status]);

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <Chips<ProofStatus> options={[{ value: 'Pending', label: 'Por validar' }, { value: 'Approved', label: 'Aprobados' }, { value: 'Rejected', label: 'Rechazados' }]}
        value={status} onChange={setStatus} />
      {loading && !data ? <Loading /> : (data ?? []).length === 0 ? (
        <Card><Empty icon={<Inbox color={colors.textMuted} />} text={status === 'Pending' ? '¡Todo al día! No hay comprobantes por validar.' : 'Sin comprobantes.'} /></Card>
      ) : data!.map(p => (
        <Card key={p.id} onPress={() => setSelected(p)}>
          <Row style={{ justifyContent: 'space-between', alignItems: 'flex-start' }}>
            <View style={{ flex: 1 }}>
              <Text style={font.h3}>{p.studentName}</Text>
              <Text style={font.small}>{p.debtDescription}</Text>
              <Text style={font.tiny}>{METHOD[p.method] ?? p.method}{p.operationNumber ? ` · Op. ${p.operationNumber}` : ''} · enviado {instantDate(p.createdAt)}</Text>
            </View>
            <View style={{ alignItems: 'flex-end', gap: 6 }}>
              <Text style={{ fontSize: 18, fontWeight: '700', color: colors.text }}>{money(p.amount)}</Text>
              <Badge tone={STATUS[p.status].tone} text={STATUS[p.status].label} />
            </View>
          </Row>
        </Card>
      ))}
      {selected && <ReviewModal proof={selected} onClose={() => setSelected(null)} onDone={() => { setSelected(null); reload(); }} />}
    </Screen>
  );
}

function ReviewModal({ proof, onClose, onDone }: { proof: PaymentProof; onClose: () => void; onDone: () => void }) {
  const [amount, setAmount] = useState(Math.min(proof.amount, proof.debtPending).toFixed(2));
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState<'approve' | 'reject' | null>(null);
  const [error, setError] = useState<string | null>(null);
  const isPdf = /\.pdf$/i.test(proof.voucherUrl);
  const pending = proof.status === 'Pending';

  const approve = async () => {
    setBusy('approve');
    setError(null);
    try {
      const { data } = await api.post<PaymentProof>(`/finances/proofs/${proof.id}/approve`, { amount: parseFloat(amount.replace(',', '.')) });
      Alert.alert('Pago registrado', `Recibo N° ${data.receiptNumber ?? '—'}`);
      onDone();
    } catch (e) { setError(errorMessage(e)); } finally { setBusy(null); }
  };
  const reject = async () => {
    if (!reason.trim()) { setError('Escribe el motivo del rechazo: la familia lo verá en su app.'); return; }
    setBusy('reject');
    setError(null);
    try {
      await api.post(`/finances/proofs/${proof.id}/reject`, { reason: reason.trim() });
      onDone();
    } catch (e) { setError(errorMessage(e)); } finally { setBusy(null); }
  };

  return (
    <Modal visible animationType="slide" onRequestClose={onClose}>
      <View style={styles.modalHead}>
        <Text style={[font.h2, { flex: 1 }]}>{proof.studentName}</Text>
        <Pressable onPress={onClose} hitSlop={12} accessibilityLabel="Cerrar"><X color={colors.text} /></Pressable>
      </View>
      <ScrollView contentContainerStyle={{ padding: spacing.lg, gap: spacing.md }} keyboardShouldPersistTaps="handled">
        {isPdf ? (
          <Button variant="secondary" title="Abrir PDF del comprobante" icon={<FileText color={colors.primaryDark} size={18} />} onPress={() => Linking.openURL(fileUrl(proof.voucherUrl))} />
        ) : (
          <Pressable onPress={() => Linking.openURL(fileUrl(proof.voucherUrl))}>
            <Image source={{ uri: fileUrl(proof.voucherUrl) }} style={styles.voucher} resizeMode="contain" accessibilityLabel="Comprobante de pago" />
          </Pressable>
        )}
        <Card>
          <Text style={font.body}>{proof.debtDescription}</Text>
          <Text style={font.small}>Saldo de la deuda: {money(proof.debtPending)}</Text>
          <Text style={font.small}>Declarado: {money(proof.amount)} · {METHOD[proof.method] ?? proof.method}</Text>
          <Text style={font.small}>N° operación: {proof.operationNumber || '—'} · pagado el {shortDay(proof.paidOn)}</Text>
          <Text style={font.small}>Enviado por {proof.submittedByName}</Text>
          {proof.notes ? <Text style={[font.small, { marginTop: 4 }]}>Nota: {proof.notes}</Text> : null}
          {proof.rejectionReason ? <Text style={[font.small, { color: colors.danger }]}>Motivo de rechazo: {proof.rejectionReason}</Text> : null}
          {proof.receiptNumber ? <Text style={font.small}>Recibo N° {proof.receiptNumber}</Text> : null}
        </Card>
        {pending && (
          <>
            <Input label="Monto a registrar (S/)" value={amount} onChangeText={setAmount} keyboardType="decimal-pad" hint="Ajústalo si lo recibido es distinto a lo declarado." />
            {error && <Banner tone="danger" text={error} />}
            <Button title="Aprobar y registrar pago" variant="success" icon={<CheckCircle2 color={colors.white} size={18} />} onPress={approve} loading={busy === 'approve'} disabled={!!busy} />
            <Input label="Motivo (si rechazas)" value={reason} onChangeText={setReason} placeholder="Ej. La operación no figura en el banco" maxLength={500} />
            <Button title="Rechazar" variant="danger" onPress={reject} loading={busy === 'reject'} disabled={!!busy} />
          </>
        )}
      </ScrollView>
    </Modal>
  );
}

const styles = StyleSheet.create({
  modalHead: { flexDirection: 'row', alignItems: 'center', padding: spacing.lg, borderBottomWidth: 1, borderBottomColor: colors.border, backgroundColor: colors.card },
  voucher: { width: '100%', height: 380, borderRadius: radius.lg, backgroundColor: colors.surface },
});
