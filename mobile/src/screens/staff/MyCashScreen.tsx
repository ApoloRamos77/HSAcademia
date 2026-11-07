import React, { useState } from 'react';
import { Alert, Text, View } from 'react-native';
import { AlertTriangle, Banknote, CheckCircle2, Clock, Send } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { CashHandover, MyCash } from '../../api/types';
import { Badge, Banner, Button, Card, Divider, Empty, Input, Loading, Row, Screen, SectionTitle } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, spacing } from '../../theme';
import { instantDate, money } from '../../utils/format';

const METHOD: Record<string, string> = { Cash: 'Efectivo', Yape: 'Yape', Plin: 'Plin', BankTransfer: 'Transferencia', Card: 'Tarjeta', Other: 'Otro' };
const num = (v: string) => { const n = Number(v.replace(',', '.')); return Number.isFinite(n) ? n : NaN; };
const time = (iso: string) => { const d = new Date(iso); return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`; };

function DiffBadge({ value }: { value?: number | null }) {
  if (value == null) return null;
  if (Math.abs(value) < 0.01) return <Badge tone="success" text="Cuadra" />;
  return <Badge tone={value < 0 ? 'danger' : 'warning'} text={`${value < 0 ? 'Falta' : 'Sobra'} ${money(Math.abs(value))}`} />;
}

/**
 * Mi caja: lo que el coordinador cobró y aún no rindió. Al terminar la jornada declara el efectivo
 * que entrega; finanzas lo recibe y cuenta en la web. Los pagos digitales se informan para conciliar.
 */
export default function MyCashScreen() {
  const { data, loading, refreshing, refresh } = useApi(() => api.get<MyCash>('/cash/mine').then(r => r.data), []);
  const [declared, setDeclared] = useState<string | null>(null);
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (loading && !data) return <Loading />;
  if (!data) return <Screen onRefresh={refresh} refreshing={refreshing}><Empty text="No se pudo cargar tu caja." /></Screen>;

  const value = declared ?? data.pendingCash.toFixed(2);
  const diff = num(value) - data.pendingCash;
  const hasDiff = Number.isFinite(diff) && Math.abs(diff) >= 0.01;

  const submit = () => {
    const amount = num(value);
    if (!Number.isFinite(amount) || amount < 0) { setError('Ingresa el efectivo que entregas.'); return; }
    if (hasDiff && !notes.trim()) { setError('El efectivo no coincide con lo cobrado: explica la diferencia en la nota.'); return; }
    setError(null);
    Alert.alert('Rendir caja', `Entregas ${money(amount)} en efectivo por ${data.pendingReceipts} recibo(s).`, [
      { text: 'Cancelar', style: 'cancel' },
      {
        text: 'Rendir', onPress: async () => {
          setSaving(true);
          try {
            await api.post('/cash/handovers', { declaredCash: amount, notes: notes.trim() || null });
            setDeclared(null);
            setNotes('');
            Alert.alert('Caja rendida', 'Entrega el efectivo a finanzas. Verás aquí cuando lo reciban.');
            refresh();
          } catch (e) {
            setError(errorMessage(e, 'No se pudo rendir la caja.'));
          } finally {
            setSaving(false);
          }
        },
      },
    ]);
  };

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <Card>
        <Row><Banknote color={colors.primaryDark} size={20} /><Text style={font.h3}>Por rendir</Text></Row>
        <Text style={[font.h1, { color: data.pendingCash > 0 ? colors.warning : colors.text, marginTop: 4 }]}>{money(data.pendingCash)}</Text>
        <Text style={font.small}>en efectivo · {data.pendingReceipts} recibo(s){data.pendingDigital > 0 ? ` · digital ${money(data.pendingDigital)}` : ''}</Text>
        {data.byMethod.length > 0 && (
          <Row style={{ flexWrap: 'wrap', marginTop: spacing.sm }}>
            {data.byMethod.map(m => <Badge key={m.method} tone={m.method === 'Cash' ? 'warning' : 'info'} text={`${METHOD[m.method] ?? m.method}: ${money(m.total)}`} />)}
          </Row>
        )}
      </Card>

      {data.pendingReceipts > 0 ? (
        <>
          <SectionTitle title="Rendir caja" />
          <Card>
            <Input label="Efectivo que entregas (S/)" value={value} onChangeText={setDeclared} keyboardType="decimal-pad" />
            {hasDiff && (
              <Banner tone="warning" icon={<AlertTriangle color={colors.warning} size={18} />}
                text={`${diff < 0 ? 'Faltan' : 'Sobran'} ${money(Math.abs(diff))} respecto a lo cobrado. Explica el motivo.`} />
            )}
            <View style={{ marginTop: spacing.sm }}>
              <Input label={hasDiff ? 'Nota *' : 'Nota (opcional)'} value={notes} onChangeText={setNotes} maxLength={300} />
            </View>
          </Card>
          {error && <Banner tone="danger" text={error} />}
          <Button title="Rendir caja" icon={<Send color={colors.white} size={18} />} loading={saving} onPress={submit} />

          <SectionTitle title="Recibos por rendir" />
          <Card>
            {data.receipts.map((r, i) => (
              <View key={r.id ?? r.number}>
                {i > 0 && <Divider />}
                <Row style={{ justifyContent: 'space-between' }}>
                  <View style={{ flex: 1 }}>
                    <Text style={[font.body, { color: colors.text, fontWeight: '600' }]}>N° {r.number} · {r.payerName}</Text>
                    <Text style={font.tiny}>{instantDate(r.issuedAt)} {time(r.issuedAt)} · {METHOD[r.method] ?? r.method}</Text>
                  </View>
                  <Text style={[font.h3, { color: colors.text }]}>{money(r.total)}</Text>
                </Row>
              </View>
            ))}
          </Card>
        </>
      ) : (
        <Banner tone="success" icon={<CheckCircle2 color={colors.success} size={18} />} text="No tienes cobros pendientes de rendir." />
      )}

      {data.recentHandovers.length > 0 && (
        <>
          <SectionTitle title="Mis rendiciones" />
          {data.recentHandovers.map((h: CashHandover) => (
            <Card key={h.id}>
              <Row style={{ justifyContent: 'space-between' }}>
                <Text style={font.h3}>{instantDate(h.declaredAt)} {time(h.declaredAt)}</Text>
                {h.status === 'Received'
                  ? <Badge tone="success" text="Recibida" />
                  : <Row style={{ gap: 4 }}><Clock color={colors.warning} size={14} /><Badge tone="warning" text="Por recibir" /></Row>}
              </Row>
              <Text style={[font.small, { marginTop: 4 }]}>{h.receiptsCount} recibo(s) · esperado {money(h.expectedCash)} · declarado {money(h.declaredCash)}</Text>
              <Row style={{ marginTop: 6, flexWrap: 'wrap' }}>
                <DiffBadge value={h.declaredDifference} />
                {h.receivedCash != null && <Text style={font.small}>Recibido {money(h.receivedCash)} por {h.receivedByName}</Text>}
                {h.receivedCash != null && <DiffBadge value={h.receivedDifference} />}
              </Row>
              {!!h.receivedNotes && <Text style={[font.tiny, { marginTop: 4 }]}>Finanzas: “{h.receivedNotes}”</Text>}
            </Card>
          ))}
        </>
      )}
    </Screen>
  );
}
