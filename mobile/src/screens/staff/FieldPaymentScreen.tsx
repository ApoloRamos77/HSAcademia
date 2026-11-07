import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Pressable, Share, Switch, Text, View } from 'react-native';
import { CheckCircle2, Minus, Plus, Receipt as ReceiptIcon, Share2, ShoppingBag, Trash2, Users, Wallet } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { Debt, FamilyAccount, Product, Receipt, StudentSummary } from '../../api/types';
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
const newRequestId = () => `app-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
const num = (v: string) => { const n = Number(v.replace(',', '.')); return Number.isFinite(n) ? n : 0; };

type DebtSel = { on: boolean; amount: string };
type Line = { key: number; product: Product; studentId: string | null; quantity: number; isGift: boolean };

/**
 * Cobro en campo con UN solo recibo para toda la familia: mensualidades de los hermanos (mismo apoderado),
 * abonos parciales y productos (uniformes). Cada intento lleva una clave única: si la señal se corta y se
 * reintenta, el servidor devuelve el mismo recibo en lugar de cobrar dos veces.
 */
export default function FieldPaymentScreen({ route, navigation }: any) {
  const student: StudentSummary = route.params.student;
  const { academy, can } = useAuth();
  const canDebts = can('manage_finances');
  const canProducts = canDebts || can('manage_store');
  useEffect(() => { navigation.setOptions({ title: 'Cobrar' }); }, [navigation]);

  const { data, loading, refreshing, refresh } = useApi(async () => {
    const [family, products] = await Promise.all([
      api.get<FamilyAccount>(`/receipts/family/${student.id}`).then(r => r.data),
      canProducts ? api.get<Product[]>('/store/products').then(r => r.data.filter(p => p.isActive !== false)).catch(() => [] as Product[]) : Promise.resolve([] as Product[]),
    ]);
    return { family, products };
  }, [student.id]);

  const [sel, setSel] = useState<Record<string, DebtSel>>({});
  const [lines, setLines] = useState<Line[]>([]);
  const [payerName, setPayerName] = useState('');
  const [method, setMethod] = useState<Method>('Cash');
  const [operation, setOperation] = useState('');
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState<Receipt | null>(null);
  const requestId = useRef(newRequestId());
  const lineKey = useRef(0);

  // Al cargar: marca las deudas del alumno elegido; las de los hermanos quedan disponibles.
  useEffect(() => {
    if (!data) return;
    setPayerName(p => p || data.family.payerName);
    setSel(prev => {
      const next: Record<string, DebtSel> = {};
      data.family.pendingDebts.forEach(d => {
        next[d.id] = prev[d.id] ?? { on: d.studentId === student.id, amount: d.amountPending.toFixed(2) };
      });
      return next;
    });
  }, [data, student.id]);

  const debts = useMemo(() => data?.family.pendingDebts ?? [], [data]);
  const family = data?.family.students ?? [];
  const chosen = debts.filter(d => sel[d.id]?.on);
  const productLines = lines.map(l => ({ ...l, total: l.isGift ? 0 : l.product.price * l.quantity }));
  const total = chosen.reduce((a, d) => a + num(sel[d.id].amount), 0) + productLines.reduce((a, l) => a + l.total, 0);
  const itemCount = chosen.length + lines.length;
  const needsOperation = method !== 'Cash';

  const toggle = (d: Debt) => setSel(p => ({ ...p, [d.id]: { ...p[d.id], on: !p[d.id]?.on } }));
  const setAmount = (d: Debt, v: string) => setSel(p => ({ ...p, [d.id]: { ...p[d.id], amount: v.replace(',', '.') } }));
  const addProduct = (product: Product) => {
    const already = lines.filter(l => l.product.id === product.id).reduce((a, l) => a + l.quantity, 0);
    if (already >= product.stock) { Alert.alert('Sin stock', `Solo hay ${product.stock} unidad(es) de ${product.name}.`); return; }
    setLines(p => [...p, { key: ++lineKey.current, product, studentId: student.id, quantity: 1, isGift: false }]);
  };
  const changeQty = (key: number, delta: number) => setLines(p => p.map(l => {
    if (l.key !== key) return l;
    const others = p.filter(x => x.product.id === l.product.id && x.key !== key).reduce((a, x) => a + x.quantity, 0);
    const q = Math.max(1, Math.min(l.quantity + delta, l.product.stock - others));
    return { ...l, quantity: q };
  }));

  const validate = (): string | null => {
    if (itemCount === 0) return 'Elige al menos una deuda o un producto.';
    for (const d of chosen) {
      const v = num(sel[d.id].amount);
      if (v <= 0) return `Ingresa el monto de ${d.description} (${d.studentName}).`;
      if (v > d.amountPending + 0.001) return `${d.description} (${d.studentName}): el monto supera lo pendiente (${money(d.amountPending)}).`;
    }
    if (needsOperation && !operation.trim()) return 'Ingresa el número de operación del pago digital.';
    return null;
  };

  const confirm = () => {
    const problem = validate();
    if (problem) { setError(problem); return; }
    setError(null);
    const detail = [
      ...chosen.map(d => `• ${d.description} — ${d.studentName}: ${money(num(sel[d.id].amount))}`),
      ...productLines.map(l => `• ${l.quantity}× ${l.product.name}${l.isGift ? ' (obsequio)' : ''}: ${money(l.total)}`),
    ].join('\n');
    Alert.alert('Confirmar cobro', `${detail}\n\nTotal ${money(total)} en ${methodLabel(method)}`, [
      { text: 'Cancelar', style: 'cancel' },
      { text: 'Registrar', onPress: submit },
    ]);
  };

  const submit = async () => {
    setSaving(true);
    try {
      const { data: receipt } = await api.post<Receipt>('/receipts', {
        clientRequestId: requestId.current,
        guardianId: data?.family.guardianId ?? null,
        payerName: payerName.trim() || null,
        method,
        operationNumber: operation.trim() || null,
        notes: [notes.trim(), 'Cobro en campo (app)'].filter(Boolean).join(' · '),
        channel: 'app',
        debts: chosen.map(d => ({ paymentRecordId: d.id, amount: num(sel[d.id].amount), discount: 0 })),
        products: lines.map(l => ({ productId: l.product.id, studentId: l.studentId, quantity: l.quantity, discount: 0, isGift: l.isGift })),
      });
      setDone(receipt);
      requestId.current = newRequestId(); // el próximo cobro es un intento nuevo
      setLines([]);
      setSel({});
      setOperation('');
      setNotes('');
      refresh();
    } catch (e) {
      // Si no hubo respuesta (sin señal), reintentar con la MISMA clave es seguro: no se cobra dos veces.
      setError(errorMessage(e, 'No se pudo registrar el cobro. Puedes reintentar sin riesgo de cobrar dos veces.'));
    } finally {
      setSaving(false);
    }
  };

  const shareReceipt = () => {
    if (!done) return;
    Share.share({
      message:
        `${academy?.name ?? 'Academia'} — Recibo N° ${done.number}\n` +
        `Pagador: ${done.payerName}\n\n` +
        done.lines.map(l => `• ${l.quantity > 1 ? `${l.quantity}× ` : ''}${l.description}${l.studentName ? ` — ${l.studentName}` : ''}: ${money(l.total)}`).join('\n') +
        `\n\nTotal pagado: ${money(done.total)} (${methodLabel(done.method)}${done.operationNumber ? ` · Op. ${done.operationNumber}` : ''})\n` +
        `Fecha: ${new Date(done.issuedAt).toLocaleDateString('es-PE')}\n\nGracias por su pago.`,
    });
  };

  if (loading && !data) return <Loading />;

  if (done) {
    return (
      <Screen>
        <Card style={{ alignItems: 'center', gap: spacing.sm }}>
          <CheckCircle2 color={colors.success} size={48} />
          <Text style={font.h2}>Cobro registrado</Text>
          <Badge tone="info" text={`Recibo N° ${done.number}`} />
          <Text style={[font.h1, { color: colors.accentDark }]}>{money(done.total)}</Text>
          <Text style={font.small}>{done.payerName} · {methodLabel(done.method)}</Text>
        </Card>
        <Card>
          {done.lines.map((l, i) => (
            <Row key={i} style={{ justifyContent: 'space-between', paddingVertical: 4 }}>
              <Text style={[font.body, { flex: 1 }]}>{l.quantity > 1 ? `${l.quantity}× ` : ''}{l.description}{l.studentName ? ` — ${l.studentName}` : ''}</Text>
              <Text style={[font.body, { fontWeight: '700', color: colors.text }]}>{money(l.total)}</Text>
            </Row>
          ))}
        </Card>
        <Button title="Compartir recibo" icon={<Share2 color={colors.white} size={18} />} onPress={shareReceipt} />
        {debts.length > 0 && <Button title="Cobrar algo más" variant="secondary" onPress={() => setDone(null)} />}
        <Button title="Volver al alumno" variant="ghost" onPress={() => navigation.goBack()} />
      </Screen>
    );
  }

  return (
    <Screen refreshing={refreshing} onRefresh={refresh}>
      <Card>
        <Row>
          <Users color={colors.primaryDark} size={18} />
          <Text style={[font.h3, { flex: 1 }]}>{family.map(s => s.name).join(' · ') || `${student.firstName} ${student.lastName}`}</Text>
        </Row>
        <Text style={[font.small, { marginTop: 4 }]}>Pendiente total de la familia</Text>
        <Text style={[font.h1, { color: (data?.family.totalPending ?? 0) > 0 ? colors.danger : colors.success }]}>{money(data?.family.totalPending)}</Text>
      </Card>

      {canDebts && (
        <>
          <SectionTitle title="Deudas" />
          {debts.length === 0 ? (
            <Empty icon={<CheckCircle2 color={colors.success} />} text="La familia está al día." />
          ) : debts.map(d => {
            const s = sel[d.id];
            const on = !!s?.on;
            return (
              <Pressable key={d.id} onPress={() => toggle(d)} accessibilityRole="checkbox" accessibilityState={{ checked: on }}
                style={[{ backgroundColor: colors.card, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, padding: spacing.md },
                  on && { borderColor: colors.primaryDark, borderWidth: 2 }]}>
                <Row style={{ justifyContent: 'space-between' }}>
                  <View style={{ flex: 1 }}>
                    <Text style={font.h3}>{d.studentName}</Text>
                    <Text style={font.small}>{d.description} · vence {shortDay(d.dueDate)}</Text>
                  </View>
                  <View style={{ alignItems: 'flex-end', gap: 4 }}>
                    <Text style={[font.h3, { color: colors.text }]}>{money(d.amountPending)}</Text>
                    <Badge tone={d.status === 'Vencido' ? 'danger' : 'warning'} text={d.status === 'Vencido' ? `Vencido ${d.daysOverdue}d` : 'En curso'} />
                  </View>
                </Row>
                {on && (
                  <View style={{ marginTop: spacing.sm }}>
                    <Input label="Monto a cobrar (S/)" value={s.amount} onChangeText={v => setAmount(d, v)} keyboardType="decimal-pad"
                      hint={num(s.amount) < d.amountPending ? 'Pago parcial: el saldo queda pendiente.' : undefined} />
                  </View>
                )}
              </Pressable>
            );
          })}
        </>
      )}

      {canProducts && (data?.products.length ?? 0) > 0 && (
        <>
          <SectionTitle title="Productos" />
          <Card>
            <Row style={{ flexWrap: 'wrap' }}>
              {data!.products.map(p => (
                <Pressable key={p.id} onPress={() => addProduct(p)} disabled={p.stock <= 0}
                  style={{ paddingHorizontal: 12, paddingVertical: 8, borderRadius: radius.pill, borderWidth: 1, borderColor: colors.border, opacity: p.stock <= 0 ? 0.4 : 1 }}>
                  <Text style={{ fontWeight: '600', color: colors.textSecondary }}>+ {p.name} · {money(p.price)}</Text>
                </Pressable>
              ))}
            </Row>
            {lines.map(l => (
              <View key={l.key} style={{ marginTop: spacing.md, paddingTop: spacing.md, borderTopWidth: 1, borderTopColor: colors.border, gap: spacing.sm }}>
                <Row style={{ justifyContent: 'space-between' }}>
                  <Row style={{ flex: 1 }}><ShoppingBag color={colors.primaryDark} size={16} /><Text style={[font.h3, { flexShrink: 1 }]}>{l.product.name}</Text></Row>
                  <Pressable onPress={() => setLines(p => p.filter(x => x.key !== l.key))} hitSlop={10}><Trash2 color={colors.danger} size={18} /></Pressable>
                </Row>
                {family.length > 1 && (
                  <Chips options={family.map(s => ({ value: s.studentId, label: s.name.split(' ')[0] }))} value={l.studentId ?? ''}
                    onChange={v => setLines(p => p.map(x => x.key === l.key ? { ...x, studentId: v } : x))} />
                )}
                <Row style={{ justifyContent: 'space-between' }}>
                  <Row>
                    <Pressable onPress={() => changeQty(l.key, -1)} style={qtyBtn} hitSlop={6}><Minus size={16} color={colors.primaryDark} /></Pressable>
                    <Text style={[font.h3, { minWidth: 24, textAlign: 'center' }]}>{l.quantity}</Text>
                    <Pressable onPress={() => changeQty(l.key, 1)} style={qtyBtn} hitSlop={6}><Plus size={16} color={colors.primaryDark} /></Pressable>
                  </Row>
                  <Row>
                    <Text style={font.small}>Obsequio</Text>
                    <Switch value={l.isGift} onValueChange={v => setLines(p => p.map(x => x.key === l.key ? { ...x, isGift: v } : x))} trackColor={{ true: colors.primary }} />
                  </Row>
                  <Text style={[font.h3, { color: colors.text }]}>{money(l.isGift ? 0 : l.product.price * l.quantity)}</Text>
                </Row>
              </View>
            ))}
          </Card>
        </>
      )}

      {itemCount > 0 && (
        <>
          <SectionTitle title="Pago" />
          <Card>
            <Input label="Pagador" value={payerName} onChangeText={setPayerName} autoCapitalize="words" />
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
              text="Recibes efectivo: entrégalo en caja al terminar la jornada. El cobro queda a tu nombre en el registro." />
          )}
        </>
      )}

      {error && <Banner tone="danger" text={error} />}
      <Button title={itemCount ? `Cobrar ${money(total)} · ${itemCount} ítem(s)` : 'Elige qué cobrar'} variant="success" disabled={!itemCount}
        icon={<ReceiptIcon color={colors.white} size={18} />} loading={saving} onPress={confirm} />
    </Screen>
  );
}

const qtyBtn = { width: 34, height: 34, borderRadius: radius.pill, backgroundColor: colors.primaryLight, alignItems: 'center' as const, justifyContent: 'center' as const };
