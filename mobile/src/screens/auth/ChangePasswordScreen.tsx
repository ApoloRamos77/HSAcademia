import React, { useState } from 'react';
import { Text, View } from 'react-native';
import { api, errorMessage } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { Banner, Button, Input, Screen } from '../../components/ui';
import { font, spacing } from '../../theme';

/**
 * Cambio de contraseña. Es obligatorio cuando la cuenta tiene una contraseña temporal
 * (creada o reseteada por la academia); también accesible desde "Mi cuenta".
 */
export default function ChangePasswordScreen({ navigation, route }: any) {
  const forced = route?.params?.forced ?? false;
  const { markPasswordChanged, logout } = useAuth();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);

  const submit = async () => {
    setError(null);
    if (next.length < 8) return setError('La nueva contraseña debe tener al menos 8 caracteres.');
    if (!/[A-Za-zÁÉÍÓÚáéíóúÑñ]/.test(next) || !/\d/.test(next)) return setError('Combina letras y números.');
    if (next !== confirm) return setError('Las contraseñas no coinciden.');
    setSaving(true);
    try {
      await api.post('/auth/change-password', { currentPassword: current, newPassword: next });
      await markPasswordChanged();
      setDone(true);
      if (!forced) navigation.goBack();
    } catch (e) {
      setError(errorMessage(e, 'No se pudo cambiar la contraseña.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Screen edges={forced ? ['top', 'left', 'right'] : undefined}>
      <Text style={font.h1}>{forced ? 'Crea tu contraseña' : 'Cambiar contraseña'}</Text>
      {forced && (
        <Banner tone="info" text="Por seguridad, reemplaza la contraseña temporal que te entregó la academia por una propia." />
      )}
      <View style={{ marginTop: spacing.sm }}>
        <Input label={forced ? 'Contraseña temporal' : 'Contraseña actual'} value={current} onChangeText={setCurrent} secureTextEntry />
        <Input label="Nueva contraseña" value={next} onChangeText={setNext} secureTextEntry hint="Mínimo 8 caracteres, con letras y números." />
        <Input label="Repite la nueva contraseña" value={confirm} onChangeText={setConfirm} secureTextEntry onSubmitEditing={submit} />
        {error && <View style={{ marginBottom: spacing.md }}><Banner tone="danger" text={error} /></View>}
        {done && <View style={{ marginBottom: spacing.md }}><Banner tone="success" text="¡Contraseña actualizada!" /></View>}
        <Button title="Guardar contraseña" onPress={submit} loading={saving} />
        {forced && <Button title="Cerrar sesión" variant="ghost" onPress={logout} style={{ marginTop: spacing.md }} />}
      </View>
    </Screen>
  );
}
