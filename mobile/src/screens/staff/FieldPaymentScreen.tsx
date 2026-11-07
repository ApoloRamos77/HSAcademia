import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Alert, Pressable, Share, Switch, Text, View } from 'react-native';
import { Banknote, CheckCircle2, Minus, Plus, Receipt as ReceiptIcon, Share2, ShoppingBag, Tag, Trash2, Users, Wallet } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { Debt, DiscountRule, FamilyAccount, Product, Receipt, StudentSummary } from '../../api/types';
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

type DebtSel = { on: boolean; partial: boolean; amount: string };
type Line = { key: number; product: Product; studentId: string | null; quantity: number; isGift: boolean };

/**
 * Cobro en campo con UN solo recibo para toda la familia: mensualidades de los hermanos (mismo apoderado),
 * abonos parciales y productos. Los descuentos (pronto pago, promoción) los calcula el servidor en la
 * vista previa: lo que se muestra es exactamente lo que se cobra. Cada intento lleva una clave única:
 * si la señal se corta y se reintenta, el servidor devuelve el mismo recibo en lugar de cobrar dos veces.
 */
export default function FieldPaymentScreen({ route, navigation }: any) {
  const student: StudentSummary = route.params.student;
  const { academy, can } = useAuth();
  const canDebts = can('manage_finances');
  const canProducts = canDebts || can('manage_store');
  useEffect(() => { navigation.setOptions({ title: 'Cobrar' }); }, [navigation]);

  const { data, loading, refreshing, refresh } = useApi(async () => {
    const [family, products, promotions] = await Promise.all([
      api.get<FamilyAccount>(`/receipts/family/${student.id}`).then(r => r.data),
      canProducts ? api.get<Product[]>('/store/products').then(r => r.data.filter(p => p.isActive !== false)).catch(() => [] as Product[]) : Promise.resolve([] as Product[]),
      api.get<DiscountRule[]>('/discounts', { params: { active: true } })
        .then(r => r.data.filter(d => d.kind === 'Promotion' && d.isCurrentlyValid)).catch(() => [] as DiscountRule[]),
    ]);
    return { family, products, promotions };
  }, [student.id]);

  const [sel, setSel] = useState<Record<string, DebtSel>>({});
  const [lines, setLines] = useState<Line[]>([]);
  const [promotionId, setPromotionId] = useState('');
  const [payerName, setPayerName] = useState('');
  const [method, setMethod] = useState<Method>('Cash');
  const [operation, setOperation] = useState('');
  const [notes, setNotes] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState<Receipt | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [previewing, setPreviewing] = useState(false);
  const [done, setDone] = useState<Receipt | null>(null);
  const requestId = useRef(newRequestId());
  const lineKey = useRef(0);
  const previewSeq = useRef(0);

  // Al cargar: marca las deudas del alumno elegido; las de los hermanos quedan disponibles.
  useEffect(() => {
    if (!data) return;
    setPayerName(p => p || data.family.payerName);
    setSel(prev => {
      const next: Record<string, DebtSel> = {};
      data.family.pendingDebts.forEach(d => {
        next[d.id] = prev[d.id] ?? { on: d.studentId === student.id, partial: false, amount: '' };
      });
      return next;
    });
  }, [data, student.id]);

  const debts = useMemo(() => data?.family.pendingDebts ?? [], [data]);
  const family = data?.family.students ?? [];
  const chosen = debts.filter(d => sel[d.id]?.on);
  const itemCount = chosen.length + lines.length;
  const needsOperation = method !== 'Cash';

  const body = useMemo(() => ({
    guardianId: data?.family.guardianId ?? null,
    payerName: payerName.trim() || null,
    promotionId: promotionId || null,
    method,
    operationNumber: operation.trim() || null,
    notes: [notes.trim(), 'Cobro en campo (app)'].filter(Boolean).join(' · '),
    channel: 'app',
    debts: chosen.map(d => ({ paymentRecordId: d.id, amount: sel[d.id].partial ? num(sel[d.id].amount) : null, discount: 0 })),
    products: lines.map(l => ({ productId: l.product.id, studentId: l.studentId, quantity: l.quantity, discount: 0, isGift: l.isGift })),
  }), [data, payerName, promotionId, method, operation, notes, chosen, sel, lines]);

  // Vista previa del servidor (con espera para no consultar en cada tecla).
  const bodyKey = JSON.stringify([body.debts, body.products, body.promotionId]);
  useEffect(() => {
    if (itemCount === 0) { setPreview(null); setPreviewError(null); return; }
    const seq = ++previewSeq.current;
    setPreviewing(true);
    const t = setTimeout(async () => {
      try {
        const { data: pv } = await api.post<Receipt>('/receipts/preview', body);
        if (seq === previewSeq.current) { setPreview(pv); setPreviewError(null); }
      } catch (e) {
        if (seq === previewSeq.current) { setPreview(null); setPreviewError(errorMessage(e, 'No se pudo calcular el cobro.')); }
      } finally {
        if (seq === previewSeq.current) setPreviewing(false);
      }
    }, 400);
    return () => clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [bodyKey, itemCount]);

  const lineOf = (debtId: string) => preview?.lines.find(l => l.paymentRecordId === debtId);
  const toggle = (d: Debt) => setSel(p => ({ ...p, [d.id]: { ...p[d.id], on: !p[d.id]?.on } }));
  const setPartial = (d: Debt, partial: boolean) => setSel(p => ({ ...p, [d.id]: { ...p[d.id], partial, amount: '' } }));
  const setAmount = (d: Debt, v: string) => setSel(p => ({ ...p, [d.id]: { ...p[d.id], amount: v.replace(',', '.') } }));
  const addProduct = (product: Product) => {
    const already = lines.filter(l => l.product.id === product.id).reduce((a, l) => a + l.quantity, 0);
    if (already >= product.stock) { Alert.alert('Sin stock', `Solo hay ${product.stock} unidad(es) de ${product.name}.`); return; }
    setLines(p => [...p, { key: ++lineKey.current, product, studentId: student.id, quantity: 1, isGift: false }]);
  };
  const changeQty = (key: number, delta: number) => setLines(p => p.map(l => {
    if (l.key !== key) return l;
    const others = p.filter(x => x.product.id === l.product.id && x.key !== key).reduce((a, x) => a + x.quantity, 0);
    return { ...l, quantity: Math.max(1, Math.min(l.quantity + delta, l.product.stock - others)) };
  }));

  const validate = (): string | null => {
    if (itemCount === 0) return 'Elige al menos una deuda o un producto.';
    for (const d of chosen) {
      if (!sel[d.id].partial) continue;
      const v = num(sel[d.id].amount);
      if (v <= 0) return `Ingresa el abono de ${d.description} (${d.studentName}).`;
      if (v > d.amountPending + 0.001) return `${d.description} (${d.studentName}): el abono supera lo pendiente (${money(d.amountPending)}).`;
    }
    if (needsOperation && !operation.trim()) return 'Ingresa el número de operación del pago digital.';
    if (previewError) return previewError;
    if (!preview || previewing) return 'Espera a que se calcule el total.';
    return null;
  };

  const confirm = () => {
    const problem = validate();
    if (problem) { setError(problem); return; }
    setError(null);
    const detail = preview!.lines.map(l =>
      `• ${l.quantity > 1 ? `${l.quantity}× ` : ''}${l.description}${l.studentName ? ` — ${l.studentName}` : ''}: ${money(l.total)}${l.discount > 0 ? ` (${l.discountLabel ?? 'desc.'} -${money(l.discount)})` : ''}`).join('\n');
    Alert.alert('Confirmar cobro', `${detail}\n\nTotal ${money(preview!.total)} en ${methodLabel(method)}`, [
      { text: 'Cancelar', style: 'cancel' },
      { text: 'Registrar', onPress: submit },
    ]);
  };

  const submit = async () => {
    setSaving(true);
    try {
      const { data: receipt } = await api.post<Receipt>('/receipts', { ...body, clientRequestId: requestId.current });
      setDone(receipt);
      requestId.current = newRequestId(); // el próximo cobro es un intento nuevo
      setLines([]);
      setSel({});
      setPromotionId('');
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
        done.lines.map(l => `• ${l.quantity > 1 ? `${l.quantity}× ` : ''}${l.description}${l.studentName ? ` — ${l.studentName}` : ''}: ${money(l.total)}` +
          (l.discount > 0 ? ` (${l.discountLabel ?? 'descuento'} -${money(l.discount)})` : '')).join('\n') +
        (done.discountTotal > 0 ? `\n\nAhorro total: ${money(done.discountTotal)}` : '') +
        `\nTotal pagado: ${money(done.total)} (${methodLabel(done.method)}${done.operationNumber ? ` · Op. ${done.operationNumber}` : ''})\n` +
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
          {done.discountTotal > 0 && <Badge tone="success" text={`Descuentos ${money(done.discountTotal)}`} />}
        </Card>
        <Card>
          {done.lines.map((l, i) => (
            <View key={i} style={{ paddingVertical: 4 }}>
              <Row style={{ justifyContent: 'space-between' }}>
                <Text style={[font.body, { flex: 1 }]}>{l.quantity > 1 ? `${l.quantity}× ` : ''}{l.description}{l.studentName ? ` — ${l.studentName}` : ''}</Text>
                <Text style={[font.body, { fontWeight: '700', color: colors.text }]}>{money(l.total)}</Text>
              </Row>
              {l.discount > 0 && <Text style={[font.tiny, { color: colors.success }]}>{l.discountLabel}: -{money(l.discount)}</Text>}
            </View>
          ))}
        </Card>
        <Button title="Compartir recibo" icon={<Share2 color={colors.white} size={18} />} onPress={shareReceipt} />
        {done.lines.length > 0 && <Button title="Ver mi caja" variant="secondary" icon={<Banknote color={colors.primaryDark} size={18} />} onPress={() => navigation.navigate('MyCash')} />}
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
            const pv = on ? lineOf(d.id) : undefined;
            return (
              <Pressable key={d.id} onPress={() => toggle(d)} accessibilityRole="checkbox" accessibilityState={{ checked: on }}
                style={[{ backgroundColor: colors.card, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border, padding: spacing.md },
                  on && { borderColor: colors.primaryDark, borderWidth: 2 }]}>
                <Row style={{ justifyContent: 'space-between' }}>
                  <View style={{ flex: 1 }}>
                    <Text style={font.h3}>{d.studentName}</Text>
                    <Text style={font.small}>{d.description} · vence {shortDay(d.dueDate)}</Text>
                    {!!d.discountDetail && <Text style={[font.tiny, { color: colors.success }]}>{d.discountDetail}</Text>}
                  </View>
                  <View style={{ alignItems: 'flex-end', gap: 4 }}>
                    <Text style={[font.h3, { color: colors.text }]}>{money(d.amountPending)}</Text>
                    <Badge tone={d.status === 'Vencido' ? 'danger' : 'warning'} text={d.status === 'Vencido' ? `Vencido ${d.daysOverdue}d` : 'En curso'} />
                  </View>
                </Row>
                {on && (
                  <View style={{ marginTop: spacing.sm, gap: spacing.sm }}>
                    {!!pv?.discountLabel && pv.discount > 0 && (
                      <Row><Tag color={colors.success} size={14} /><Text style={[font.small, { color: colors.success, flex: 1 }]}>{pv.discountLabel}: -{money(pv.discount)}</Text></Row>
                    )}
                    <Row style={{ justifyContent: 'space-between' }}>
                      <Chips options={[{ value: 'full', label: 'Completa' }, { value: 'partial', label: 'Abono parcial' }]}
                        value={s.partial ? 'partial' : 'full'} onChange={v => setPartial(d, v === 'partial')} />
                      {!s.partial && <Text style={[font.h3, { color: colors.accentDark }]}>{pv ? money(pv.total) : '…'}</Text>}
                    </Row>
                    {s.partial && (
                      <Input label="Abono (S/)" value={s.amount} onChangeText={v => setAmount(d, v)} keyboardType="decimal-pad"
                        hint="Los abonos parciales no reciben pronto pago ni promociones." />
                    )}
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
            {lines.map(l => {
              const pv = preview?.lines.find(x => x.type === 'Product' && x.productId === l.product.id && (x.studentId ?? null) === l.studentId);
              return (
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
                    <Text style={[font.h3, { color: colors.text }]}>{pv ? money(pv.total) : money(l.isGift ? 0 : l.product.price * l.quantity)}</Text>
                  </Row>
                  {!!pv?.discountLabel && pv.discount > 0 && !l.isGift && <Text style={[font.tiny, { color: colors.success }]}>{pv.discountLabel}: -{money(pv.discount)}</Text>}
                </View>
              );
            })}
          </Card>
        </>
      )}

      {itemCount > 0 && (
        <>
          {(data?.promotions.length ?? 0) > 0 && (
            <>
              <SectionTitle title="Promoción" />
              <Card>
                <Chips options={[{ value: '', label: 'Ninguna' }, ...data!.promotions.map(p => ({ value: p.id, label: p.label }))]}
                  value={promotionId} onChange={setPromotionId} />
                <Text style={[font.tiny, { marginTop: 6 }]}>Pronto pago se aplica solo. Los descuentos no se acumulan: se aplica el mayor.</Text>
              </Card>
            </>
          )}

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

          {preview && (
            <Card>
              <Row style={{ justifyContent: 'space-between' }}><Text style={font.body}>Subtotal</Text><Text style={font.body}>{money(preview.subtotal)}</Text></Row>
              {preview.discountTotal > 0 && (
                <Row style={{ justifyContent: 'space-between' }}><Text style={[font.body, { color: colors.success }]}>Descuentos</Text><Text style={[font.body, { color: colors.success }]}>-{money(preview.discountTotal)}</Text></Row>
              )}
              <Row style={{ justifyContent: 'space-between', marginTop: 4 }}><Text style={font.h3}>Total</Text><Text style={[font.h2, { color: colors.accentDark }]}>{money(preview.total)}</Text></Row>
            </Card>
          )}
          {method === 'Cash' && (
            <Banner tone="warning" icon={<Wallet color={colors.warning} size={18} />}
              text="Recibes efectivo: queda en «Mi caja» hasta que lo rindas a finanzas." />
          )}
        </>
      )}

      {previewError && <Banner tone="danger" text={previewError} />}
      {error && error !== previewError && <Banner tone="danger" text={error} />}
      <Button title={!itemCount ? 'Elige qué cobrar' : previewing || !preview ? 'Calculando…' : `Cobrar ${money(preview.total)} · ${itemCount} ítem(s)`}
        variant="success" disabled={!itemCount || !preview || previewing || !!previewError}
        icon={<ReceiptIcon color={colors.white} size={18} />} loading={saving} onPress={confirm} />
    </Screen>
  );
}

const qtyBtn = { width: 34, height: 34, borderRadius: radius.pill, backgroundColor: colors.primaryLight, alignItems: 'center' as const, justifyContent: 'center' as const };
