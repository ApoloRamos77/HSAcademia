import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Alert, FlatList, KeyboardAvoidingView, Modal, Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { CloudOff, Lock, QrCode, Users } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { AttendanceCode, RosterStudent, TrainingSession } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import QrScanner, { type ScanResult } from '../../components/QrScanner';
import { Banner, Button, Chips, Empty, Input, Loading, Row, useBottomInset } from '../../components/ui';
import { enqueue, fetchRoster, isOfflineError, pendingFor, readRoster, saveAndSend, saveRoster, type QueuedRecord } from '../../offline/attendanceOffline';
import { chargeSession as sendCharge, discardCharge, newChargeId, usePendingCharges, type ChargeMethod } from '../../offline/sessionCharges';
import { colors, font, radius, spacing } from '../../theme';
import { clockTime, initials, markingBlock, money, shortDay } from '../../utils/format';

const CHARGE_METHODS: { value: ChargeMethod; label: string }[] = [
  { value: 'Cash', label: 'Efectivo' },
  { value: 'Yape', label: 'Yape' },
  { value: 'Plin', label: 'Plin' },
];
type Quote = { amount: number; note?: string | null; estimated?: boolean; paid?: boolean };

const OPTIONS: { code: AttendanceCode; short: string; label: string; bg: string; fg: string }[] = [
  { code: 1, short: 'P', label: 'Presente', bg: colors.successBg, fg: colors.success },
  { code: 3, short: 'T', label: 'Tarde', bg: colors.warningBg, fg: colors.warning },
  { code: 2, short: 'F', label: 'Falta', bg: colors.dangerBg, fg: colors.danger },
  { code: 4, short: 'J', label: 'Justificada', bg: colors.infoBg, fg: colors.info },
];

type Marks = Record<string, AttendanceCode>;

const normalize = (text: string) => text.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
const sortRoster = (list: RosterStudent[]) => list.slice().sort((a, b) => `${a.lastName}${a.firstName}`.localeCompare(`${b.lastName}${b.firstName}`));
const marksOf = (list: RosterStudent[]): Marks => Object.fromEntries(list.filter(r => r.status).map(r => [r.studentId, r.status as AttendanceCode]));

/**
 * Lista de la sesión, a cargo del entrenador: un toque por alumno y QR para quien traiga su carné.
 * Funciona sin señal con la lista guardada: las marcas quedan en el celular y se envían solas.
 * Al cerrar, quienes sigan sin marcar quedan con falta.
 */
export default function AttendanceScreen({ route, navigation }: any) {
  const s: TrainingSession = route.params.session;
  const { role, can } = useAuth();
  const canCollect = can('collections.collect');
  const bottomInset = useBottomInset();
  const [roster, setRoster] = useState<RosterStudent[]>([]);
  const [marks, setMarks] = useState<Marks>({});
  const [query, setQuery] = useState('');
  const [scanning, setScanning] = useState(false);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [closed, setClosed] = useState(s.attendanceClosed);
  const [offlineSince, setOfflineSince] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  // Cobro de sesión en un paso (alumnos por sesión), también sin señal.
  const pendingCharges = usePendingCharges().filter(c => c.eventId === s.eventId);
  const [chargeFor, setChargeFor] = useState<RosterStudent | null>(null);
  const [quote, setQuote] = useState<Quote | null>(null);
  const [method, setMethod] = useState<ChargeMethod>('Cash');
  const [operation, setOperation] = useState('');
  const [charging, setCharging] = useState(false);
  // Lo que el servidor ya tiene: solo se envían las diferencias para no pisar marcas más recientes.
  const baseline = useRef<Marks>({});
  const current = useRef({ roster, marks });
  current.current = { roster, marks };

  const applyRoster = useCallback(async (list: RosterStudent[]) => {
    const sorted = sortRoster(list);
    const pending = await pendingFor(s.eventId);
    baseline.current = marksOf(sorted);
    const queued = Object.entries(pending?.records ?? {}).filter(([, r]) => !r.ifUnmarked);
    setRoster(sorted.map(r => {
      const record = pending?.records[r.studentId];
      return record && !record.ifUnmarked ? { ...r, markedVia: record.via ?? null, markedByName: null, markedAt: record.markedAt ?? null } : r;
    }));
    setMarks({ ...baseline.current, ...Object.fromEntries(queued.map(([id, r]) => [id, r.status])) });
    if (pending?.close) setClosed(true);
    return sorted;
  }, [s.eventId]);

  useEffect(() => {
    navigation.setOptions({ title: 'Tomar lista' });
    (async () => {
      try {
        const list = await fetchRoster(s.eventId, s.date);
        await saveRoster(s.eventId, list);
        await applyRoster(list);
      } catch (e) {
        const cached = isOfflineError(e) ? await readRoster(s.eventId) : null;
        if (cached) {
          await applyRoster(cached.value);
          setOfflineSince(cached.cachedAt);
        } else {
          setError(isOfflineError(e) ? 'Sin señal y esta lista aún no se guardó en el celular. Ábrela una vez con internet.' : errorMessage(e));
        }
      } finally {
        setLoading(false);
      }
    })();
  }, [s, navigation, applyRoster]);

  const visible = useMemo(() => {
    const term = normalize(query.trim());
    return term ? roster.filter(r => normalize(`${r.firstName} ${r.lastName}`).includes(term)) : roster;
  }, [roster, query]);

  const mark = (studentId: string, code: AttendanceCode) => {
    setMarks(m => ({ ...m, [studentId]: code }));
    setRoster(list => list.map(r => (r.studentId === studentId ? { ...r, markedVia: null, markedByName: null, markedAt: null } : r)));
  };

  const onScanned = useCallback((r: ScanResult) => {
    baseline.current = { ...baseline.current, [r.studentId]: 1 };
    setMarks(m => ({ ...m, [r.studentId]: 1 }));
    setRoster(list => list.map(x => (x.studentId === r.studentId ? { ...x, markedVia: 'QR', markedAt: r.markedAt } : x)));
  }, []);

  // Alumno por sesión: se cobra en un paso (queda Presente y sale el recibo). Sin señal queda en el celular.
  const openCharge = async (r: RosterStudent) => {
    setChargeFor(r);
    setMethod('Cash');
    setOperation('');
    setQuote(null);
    try {
      const { data } = await api.get<{ amount: number; note?: string | null; paid: boolean }>('/finances/sessions/quote', { params: { studentId: r.studentId, eventId: s.eventId } });
      setQuote({ amount: data.amount, note: data.note, paid: data.paid });
    } catch (e) {
      setQuote({ amount: r.sessionFee ?? 0, estimated: true, note: isOfflineError(e) ? 'Sin señal: monto estimado. El sistema lo confirma al enviar.' : errorMessage(e) });
    }
  };

  const confirmCharge = async () => {
    const r = chargeFor;
    if (!r) return;
    if (method !== 'Cash' && !operation.trim()) {
      Alert.alert('Falta el número de operación', `Para ${method} indica el número de operación.`);
      return;
    }
    setCharging(true);
    try {
      const result = await sendCharge({
        id: newChargeId(), studentId: r.studentId, studentName: `${r.firstName} ${r.lastName}`, eventId: s.eventId,
        sessionTitle: s.title, date: s.date, method, operationNumber: method === 'Cash' ? null : operation.trim(),
        amount: quote?.amount ?? r.sessionFee ?? 0, chargedAt: new Date().toISOString(),
      });
      if (result.state === 'rejected') {
        Alert.alert('No se pudo cobrar', result.message);
        return;
      }
      // Cobrar confirma que asistió: queda Presente (el servidor ya lo marcó si hubo señal).
      setMarks(m => ({ ...m, [r.studentId]: m[r.studentId] === 3 ? 3 : 1 }));
      if (result.state === 'sent') {
        baseline.current = { ...baseline.current, [r.studentId]: baseline.current[r.studentId] === 3 ? 3 : 1 };
        setRoster(list => list.map(x => (x.studentId === r.studentId ? { ...x, sessionCharged: true, sessionPaid: true } : x)));
        const rc = result.response.receipt;
        Alert.alert('Sesión cobrada', rc
          ? `Recibo N° ${rc.number} · ${money(rc.total)}`
          : result.response.coveredByCredit ? 'Se cubrió con su saldo a favor.' : 'La sesión ya estaba pagada.');
      } else {
        Alert.alert('Cobro guardado en este celular', 'No hay señal. Se enviará solo cuando vuelva el internet y saldrá el recibo. Aparecerá en Mi caja.');
      }
      setChargeFor(null);
    } finally {
      setCharging(false);
    }
  };

  const resolveOffline = useCallback(async (studentId: string) => {
    const student = current.current.roster.find(r => r.studentId === studentId);
    if (!student) return null;
    const status = current.current.marks[studentId];
    if (status === 1 || status === 3) return 'already' as const;
    const markedAt = new Date().toISOString();
    await enqueue({ eventId: s.eventId, date: s.date, title: s.title, close: false, records: { [studentId]: { status: 1, via: 'QR', markedAt } } });
    return { studentId, studentName: `${student.firstName} ${student.lastName}`, category: s.categoryName, markedAt };
  }, [s]);

  // La hora real de la marca permite aceptarla si se envía después del plazo por falta de señal.
  const changedRecords = (list: RosterStudent[], final: Marks, markedAt: string) =>
    Object.fromEntries(list.filter(r => final[r.studentId] && final[r.studentId] !== baseline.current[r.studentId])
      .map(r => [r.studentId, { status: final[r.studentId], markedAt } as QueuedRecord]));

  const save = async (records: Record<string, QueuedRecord>, final: Marks, andClose: boolean) => {
    setSaving(true);
    try {
      const result = await saveAndSend({ eventId: s.eventId, date: s.date, title: s.title, records, close: andClose });
      if (result.state === 'rejected') {
        Alert.alert('No se pudo guardar', result.message);
        return;
      }
      baseline.current = final;
      if (andClose) setClosed(true);
      if (result.state === 'queued') {
        setOfflineSince(t => t ?? Date.now());
        Alert.alert('Guardada en este celular', andClose
          ? 'No hay señal. La lista se enviará y se cerrará sola cuando vuelva el internet.'
          : 'No hay señal. Las marcas se enviarán solas cuando vuelva el internet.');
      } else {
        Alert.alert('Asistencia guardada', andClose ? 'La lista quedó cerrada.' : 'Puedes seguir marcando hasta cerrarla.');
      }
      if (andClose) navigation.goBack();
    } catch (e) {
      Alert.alert('No se pudo guardar', errorMessage(e));
    } finally {
      setSaving(false);
    }
  };

  const saveProgress = () => {
    if (Object.keys(marks).length === 0) {
      Alert.alert('Sin marcas', 'Marca al menos a un alumno antes de guardar.');
      return;
    }
    save(changedRecords(roster, marks, new Date().toISOString()), marks, false);
  };

  // Con señal se vuelve a leer la lista antes de cerrar: otro celular pudo haber registrado carnés.
  const confirmClose = async () => {
    setSaving(true);
    let latest = roster;
    let merged = marks;
    try {
      const list = await fetchRoster(s.eventId, s.date);
      const localMarks = marks;
      latest = await applyRoster(list);
      merged = { ...marksOf(latest), ...localMarks };
      setMarks(merged);
    } catch (e) {
      if (!isOfflineError(e)) {
        Alert.alert('No se pudo cerrar', errorMessage(e));
        return;
      }
    } finally {
      setSaving(false);
    }

    const unmarked = latest.filter(r => !merged[r.studentId]);
    const markedAt = new Date().toISOString();
    const records: Record<string, QueuedRecord> = {
      ...changedRecords(latest, merged, markedAt),
      ...Object.fromEntries(unmarked.map(r => [r.studentId, { status: 2 as AttendanceCode, ifUnmarked: true, markedAt }])),
    };
    const total = (...codes: AttendanceCode[]) => latest.filter(r => merged[r.studentId] && codes.includes(merged[r.studentId])).length;
    const justified = total(4);
    Alert.alert(
      'Cerrar asistencia',
      `${total(1, 3)} presentes · ${total(2) + unmarked.length} faltas${justified ? ` · ${justified} justificadas` : ''}\n` +
      `${unmarked.length ? `Los ${unmarked.length} sin marcar quedarán con falta.\n` : ''}` +
      'Después de cerrar, solo un administrador podrá reabrirla.',
      [{ text: 'Cancelar', style: 'cancel' }, { text: 'Guardar y cerrar', style: 'destructive', onPress: () => save(records, { ...merged, ...Object.fromEntries(unmarked.map(r => [r.studentId, 2 as AttendanceCode])) }, true) }],
    );
  };

  if (loading) return <Loading />;

  const count = (...codes: AttendanceCode[]) => roster.filter(r => marks[r.studentId] && codes.includes(marks[r.studentId])).length;
  const missing = roster.filter(r => !marks[r.studentId]).length;
  const blocked = closed ? null : markingBlock(s);
  const readOnly = closed || !!blocked;

  return (
    <View style={{ flex: 1, backgroundColor: colors.bg }}>
      <View style={styles.summary}>
        <Text style={font.h3}>{s.title} · {s.startTime}</Text>
        <Text style={font.small}>{s.categoryName} · {shortDay(s.date)}</Text>
        <Text style={font.small}>{count(1, 3)} presentes · {count(2)} faltas · {count(4)} justificadas · {missing} sin marcar</Text>
        {offlineSince != null && (
          <Banner tone="warning" icon={<CloudOff color={colors.warning} size={16} />}
            text={`Sin señal: lista guardada a las ${clockTime(new Date(offlineSince).toISOString())}. Tus marcas se enviarán solas.`} />
        )}
        {closed && <Banner tone="muted" icon={<Lock color={colors.textSecondary} size={16} />} text="Asistencia cerrada: solo un administrador puede reabrirla." />}
        {!!blocked && <Banner tone="muted" icon={<Lock color={colors.textSecondary} size={16} />} text={blocked} />}
        {error && <Banner tone="danger" text={error} />}
        {pendingCharges.filter(c => !c.error).length > 0 && (
          <Banner tone="info" icon={<CloudOff color={colors.info} size={16} />}
            text={`${pendingCharges.filter(c => !c.error).length} cobro(s) de sesión guardado(s) en este celular. Se enviarán solos.`} />
        )}
        {pendingCharges.filter(c => c.error).map(c => (
          <Pressable key={c.id} onPress={() => Alert.alert('Cobro no enviado', `${c.studentName} · ${money(c.amount)}\n${c.error}`, [
            { text: 'Mantener', style: 'cancel' },
            { text: 'Descartar', style: 'destructive', onPress: () => discardCharge(c.id) },
          ])}>
            <Banner tone="danger" text={`No se pudo enviar el cobro de ${c.studentName}: ${c.error}`} />
          </Pressable>
        ))}
        {!readOnly && roster.length > 0 && (
          <Button compact variant="secondary" title="Escanear carnés QR" icon={<QrCode color={colors.primaryDark} size={16} />} onPress={() => setScanning(true)} />
        )}
        {roster.length > 0 && <Input placeholder="Buscar alumno" value={query} onChangeText={setQuery} autoCorrect={false} style={{ marginBottom: -spacing.md }} />}
      </View>

      <FlatList
        data={visible}
        keyExtractor={r => r.studentId}
        keyboardShouldPersistTaps="handled"
        contentContainerStyle={{ padding: spacing.lg, gap: spacing.sm, paddingBottom: 140 + bottomInset }}
        ListEmptyComponent={<Empty icon={<Users color={colors.textMuted} />} text={roster.length ? 'Ningún alumno coincide con la búsqueda.' : 'No hay alumnos en esta categoría.'} />}
        // Nombre completo arriba y opciones a todo el ancho debajo: se lee igual en celulares angostos
        // o con letra grande, sin cortar el apellido.
        renderItem={({ item }) => (
          <View style={styles.row}>
            <Row style={{ gap: spacing.sm, alignItems: 'flex-start' }}>
              <View style={styles.avatar}><Text style={styles.avatarText} maxFontSizeMultiplier={1.2}>{initials(`${item.firstName} ${item.lastName}`)}</Text></View>
              <View style={{ flex: 1, minWidth: 0 }}>
                <Text style={styles.lastName}>{item.lastName}</Text>
                <Text style={styles.firstName}>{item.firstName}</Text>
                {item.billingPlan === 'PerSession' && item.sessionPaid && (
                  <View style={[styles.payTag, { backgroundColor: colors.successBg }]}><Text style={[styles.payTagText, { color: colors.success }]} maxFontSizeMultiplier={1.2}>💲 Sesión pagada</Text></View>
                )}
                {item.billingPlan === 'PerSession' && !item.sessionPaid && pendingCharges.some(c => c.studentId === item.studentId && !c.error) && (
                  <View style={[styles.payTag, { backgroundColor: colors.infoBg }]}><Text style={[styles.payTagText, { color: colors.info }]} maxFontSizeMultiplier={1.2}>💲 Cobro por enviar</Text></View>
                )}
                {item.billingPlan === 'PerSession' && item.sessionBillable !== false && canCollect && !item.sessionPaid && !pendingCharges.some(c => c.studentId === item.studentId && !c.error) && (
                  <Pressable onPress={() => openCharge(item)} accessibilityRole="button" accessibilityLabel={`Cobrar sesión a ${item.firstName}`} hitSlop={6} style={styles.payTag}>
                    <Text style={styles.payTagText} maxFontSizeMultiplier={1.2}>💲 Paga por sesión · Cobrar{item.sessionFee ? ` ${money(item.sessionFee)}` : ''}</Text>
                  </Pressable>
                )}
                {item.billingPlan === 'PerSession' && item.sessionBillable !== false && !canCollect && <Text style={font.tiny}>💲 Paga por sesión</Text>}
                {!!item.markedVia && (
                  <Text style={font.tiny} numberOfLines={1}>
                    {item.markedVia}{item.markedAt ? ` · ${clockTime(item.markedAt)}` : ''}{item.markedByName ? ` · ${item.markedByName}` : ''}
                  </Text>
                )}
              </View>
            </Row>
            <View style={styles.options} accessibilityRole="radiogroup">
              {OPTIONS.map(o => {
                const active = marks[item.studentId] === o.code;
                return (
                  <Pressable key={o.code} disabled={readOnly} onPress={() => mark(item.studentId, o.code)}
                    accessibilityRole="radio" accessibilityState={{ selected: active, disabled: readOnly }} accessibilityLabel={`${o.label} ${item.firstName} ${item.lastName}`}
                    style={[styles.opt, active && { backgroundColor: o.bg, borderColor: o.fg }, readOnly && !active && { opacity: 0.6 }]}>
                    <Text style={[styles.optText, active && { color: o.fg }]} maxFontSizeMultiplier={1.2}>{o.short}</Text>
                    <Text style={[styles.optLabel, active && { color: o.fg }]} numberOfLines={1} adjustsFontSizeToFit minimumFontScale={0.7} maxFontSizeMultiplier={1.1}>{o.label}</Text>
                  </Pressable>
                );
              })}
            </View>
          </View>
        )}
      />

      {!readOnly && roster.length > 0 && (
        <View style={[styles.footer, { paddingBottom: spacing.lg + bottomInset }]}>
          <Button title="Guardar" onPress={saveProgress} loading={saving} style={{ flex: 1 }} />
          {(role === 'AcademyAdmin' || role === 'Staff') && (
            <Button title="Guardar y cerrar" variant="ghost" onPress={confirmClose} disabled={saving} style={{ flex: 1 }} />
          )}
        </View>
      )}

      <Modal visible={!!chargeFor} transparent animationType="fade" onRequestClose={() => { if (!charging) setChargeFor(null); }}>
        <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.backdrop}>
          <View style={[styles.sheet, { paddingBottom: spacing.lg + bottomInset }]}>
            <Text style={font.h2}>Cobrar sesión</Text>
            <Text style={font.body}>{chargeFor?.firstName} {chargeFor?.lastName}</Text>
            <Text style={font.small}>{s.title} · {shortDay(s.date)}</Text>
            {quote == null ? <Loading /> : (
              <>
                <Text style={[font.h1, { marginTop: spacing.sm }]}>{money(quote.amount)}</Text>
                {!!quote.note && <Text style={font.small}>{quote.note}</Text>}
                {!quote.paid && quote.amount > 0 && (
                  <>
                    <Chips options={CHARGE_METHODS} value={method} onChange={setMethod} />
                    {method !== 'Cash' && (
                      <Input label="N° de operación" value={operation} onChangeText={setOperation} autoCapitalize="characters" placeholder={`Operación de ${method}`} />
                    )}
                  </>
                )}
                {!quote.paid && <Text style={font.tiny}>Al cobrar, el alumno queda Presente.</Text>}
              </>
            )}
            <Row style={{ gap: spacing.sm, marginTop: spacing.md }}>
              <Button title="Cancelar" variant="ghost" onPress={() => setChargeFor(null)} disabled={charging} style={{ flex: 1 }} />
              {!!quote && !quote.paid && (
                <Button title={quote.amount > 0 ? 'Cobrar' : 'Registrar'} onPress={confirmCharge} loading={charging} style={{ flex: 1 }} />
              )}
            </Row>
          </View>
        </KeyboardAvoidingView>
      </Modal>

      <Modal visible={scanning} animationType="slide" onRequestClose={() => setScanning(false)}>
        {scanning && <QrScanner eventId={s.eventId} onScanned={onScanned} resolveOffline={resolveOffline} onClose={() => setScanning(false)} />}
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  summary: { padding: spacing.lg, gap: spacing.sm, backgroundColor: colors.card, borderBottomWidth: 1, borderBottomColor: colors.border },
  row: { gap: spacing.sm, backgroundColor: colors.card, padding: spacing.md, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border },
  lastName: { ...font.body, color: colors.text, fontWeight: '700' },
  firstName: { ...font.body, color: colors.text },
  options: { flexDirection: 'row', gap: 6 },
  payTag: { alignSelf: 'flex-start', marginTop: 4, paddingHorizontal: 8, paddingVertical: 3, borderRadius: radius.pill, backgroundColor: colors.warningBg },
  payTagText: { fontSize: 12, fontWeight: '700', color: colors.warning },
  backdrop: { flex: 1, backgroundColor: 'rgba(15,23,42,0.45)', justifyContent: 'flex-end' },
  sheet: { backgroundColor: colors.card, padding: spacing.lg, gap: spacing.xs, borderTopLeftRadius: radius.lg, borderTopRightRadius: radius.lg },
  avatar: { width: 36, height: 36, borderRadius: radius.pill, backgroundColor: colors.primaryLight, alignItems: 'center', justifyContent: 'center' },
  avatarText: { color: colors.primaryDark, fontWeight: '700' },
  opt: { flex: 1, minWidth: 0, minHeight: 48, paddingVertical: 4, paddingHorizontal: 2, borderRadius: radius.sm, borderWidth: 1, borderColor: colors.border, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.card },
  optText: { fontWeight: '700', fontSize: 16, color: colors.textMuted },
  optLabel: { fontSize: 11, color: colors.textMuted },
  footer: { position: 'absolute', left: 0, right: 0, bottom: 0, flexDirection: 'row', gap: spacing.sm, padding: spacing.lg, backgroundColor: colors.card, borderTopWidth: 1, borderTopColor: colors.border },
});
