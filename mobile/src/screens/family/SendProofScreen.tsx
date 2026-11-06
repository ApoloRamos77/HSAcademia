import React, { useState } from 'react';
import { Alert, Image, Pressable, StyleSheet, Text, View } from 'react-native';
import { launchCamera, launchImageLibrary, type Asset } from 'react-native-image-picker';
import { Camera, ImageIcon, X } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { Debt } from '../../api/types';
import { Banner, Button, Chips, Input, Row, Screen } from '../../components/ui';
import { colors, font, radius, spacing } from '../../theme';
import { money, todayISO } from '../../utils/format';

type Method = 'Yape' | 'Plin' | 'BankTransfer' | 'Other';
const METHODS: { value: Method; label: string }[] = [
  { value: 'Yape', label: 'Yape' }, { value: 'Plin', label: 'Plin' },
  { value: 'BankTransfer', label: 'Transferencia' }, { value: 'Other', label: 'Otro' },
];

/** Envío de comprobante de pago: foto (cámara o galería), monto, medio y N° de operación. */
export default function SendProofScreen({ route, navigation }: any) {
  const debt: Debt = route.params.debt;
  const [photo, setPhoto] = useState<Asset | null>(null);
  const [amount, setAmount] = useState(debt.amountPending.toFixed(2));
  const [method, setMethod] = useState<Method>('Yape');
  const [operation, setOperation] = useState('');
  const [notes, setNotes] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const pick = async (source: 'camera' | 'gallery') => {
    const options = { mediaType: 'photo' as const, quality: 0.7 as const, maxWidth: 1600, maxHeight: 1600, includeBase64: false };
    const res = source === 'camera' ? await launchCamera({ ...options, saveToPhotos: false }) : await launchImageLibrary({ ...options, selectionLimit: 1 });
    if (res.errorCode) {
      Alert.alert('No se pudo abrir', res.errorCode === 'camera_unavailable' ? 'Cámara no disponible.' : 'Revisa los permisos de la app.');
      return;
    }
    if (res.assets?.[0]) setPhoto(res.assets[0]);
  };

  const submit = async () => {
    setError(null);
    const value = parseFloat(amount.replace(',', '.'));
    if (!photo?.uri) return setError('Adjunta la foto del comprobante.');
    if (!value || value <= 0) return setError('Ingresa el monto pagado.');
    if (value > debt.amountPending + 0.01) return setError(`El monto no puede superar el saldo (${money(debt.amountPending)}).`);
    if ((photo.fileSize ?? 0) > 5 * 1024 * 1024) return setError('La foto supera 5 MB. Toma una nueva con menor resolución.');

    const form = new FormData();
    form.append('file', { uri: photo.uri, type: photo.type ?? 'image/jpeg', name: photo.fileName ?? 'comprobante.jpg' } as any);
    form.append('amount', value.toFixed(2));
    form.append('method', method);
    form.append('operationNumber', operation.trim());
    form.append('paidOn', todayISO());
    if (notes.trim()) form.append('notes', notes.trim());

    setSending(true);
    try {
      await api.post(`/finances/my-debts/${debt.id}/proof`, form, { headers: { 'Content-Type': 'multipart/form-data' }, timeout: 60000 });
      Alert.alert('¡Comprobante enviado!', 'La academia lo validará y registrará tu pago. Te mostraremos el estado en "Mis comprobantes".');
      navigation.goBack();
    } catch (e) {
      setError(errorMessage(e, 'No se pudo enviar el comprobante.'));
    } finally {
      setSending(false);
    }
  };

  return (
    <Screen>
      <View>
        <Text style={font.h2}>{debt.description}</Text>
        <Text style={font.body}>Saldo pendiente: <Text style={{ fontWeight: '700' }}>{money(debt.amountPending)}</Text></Text>
      </View>

      {photo?.uri ? (
        <View>
          <Image source={{ uri: photo.uri }} style={styles.preview} resizeMode="contain" accessibilityLabel="Foto del comprobante" />
          <Pressable style={styles.remove} onPress={() => setPhoto(null)} accessibilityLabel="Quitar foto" hitSlop={10}>
            <X color={colors.white} size={18} />
          </Pressable>
        </View>
      ) : (
        <Row>
          <Button title="Tomar foto" variant="secondary" icon={<Camera color={colors.primaryDark} size={18} />} onPress={() => pick('camera')} style={{ flex: 1 }} />
          <Button title="Galería" variant="secondary" icon={<ImageIcon color={colors.primaryDark} size={18} />} onPress={() => pick('gallery')} style={{ flex: 1 }} />
        </Row>
      )}

      <View>
        <Text style={[font.small, { fontWeight: '600', color: colors.textSecondary, marginBottom: 6 }]}>Medio de pago</Text>
        <Chips options={METHODS} value={method} onChange={setMethod} />
      </View>
      <Input label="Monto pagado (S/)" value={amount} onChangeText={setAmount} keyboardType="decimal-pad" />
      <Input label="N° de operación" value={operation} onChangeText={setOperation} placeholder="Ayuda a validar más rápido" maxLength={100} />
      <Input label="Nota para la academia (opcional)" value={notes} onChangeText={setNotes} maxLength={500} multiline />

      {error && <Banner tone="danger" text={error} />}
      <Button title="Enviar comprobante" onPress={submit} loading={sending} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  preview: { width: '100%', height: 320, borderRadius: radius.lg, backgroundColor: colors.surface },
  remove: { position: 'absolute', top: spacing.sm, right: spacing.sm, backgroundColor: 'rgba(0,0,0,0.55)', borderRadius: radius.pill, padding: 6 },
});
