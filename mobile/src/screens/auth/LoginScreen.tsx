import React, { useState } from 'react';
import { Image, KeyboardAvoidingView, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Eye, EyeOff, RefreshCw } from 'lucide-react-native';
import { errorMessage } from '../../api/client';
import { useAuth } from '../../auth/AuthContext';
import { Banner, Button, Input } from '../../components/ui';
import { colors, font, radius, spacing } from '../../theme';

/** Paso 2: ingreso con correo, celular o DNI dentro de la academia elegida. */
export default function LoginScreen({ navigation }: any) {
  const { academy, login, selectAcademy } = useAuth();
  const [user, setUser] = useState('');
  const [password, setPassword] = useState('');
  const [show, setShow] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (!user.trim() || !password) { setError('Ingresa tu usuario y contraseña.'); return; }
    setLoading(true);
    setError(null);
    try {
      await login(user, password);
      // La navegación cambia sola según la sesión (RootNavigator).
    } catch (e) {
      setError(errorMessage(e, 'No se pudo iniciar sesión.'));
    } finally {
      setLoading(false);
    }
  };

  const changeAcademy = async () => {
    await selectAcademy(null);
    navigation.replace('AcademySelect');
  };

  return (
    <SafeAreaView style={styles.screen}>
      <KeyboardAvoidingView style={{ flex: 1 }} behavior="height">
        <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
          <Image source={require('../../assets/logo.png')} style={styles.logo} resizeMode="contain" />
          <Text style={font.h1}>Iniciar sesión</Text>

          <Pressable onPress={changeAcademy} style={styles.academy} accessibilityRole="button" accessibilityHint="Cambiar de academia">
            <View style={{ flex: 1 }}>
              <Text style={font.tiny}>ACADEMIA</Text>
              <Text style={font.h3} numberOfLines={1}>{academy?.name ?? 'Sin seleccionar'}</Text>
            </View>
            <RefreshCw color={colors.primaryDark} size={18} />
            <Text style={[font.small, { color: colors.primaryDark, fontWeight: '600' }]}>Cambiar</Text>
          </Pressable>

          <View style={{ width: '100%' }}>
            <Input label="Correo, celular o DNI" value={user} onChangeText={setUser} autoCapitalize="none" autoCorrect={false}
              keyboardType="email-address" textContentType="username" autoComplete="username" returnKeyType="next" />
            <View>
              <Input label="Contraseña" value={password} onChangeText={setPassword} secureTextEntry={!show}
                textContentType="password" autoComplete="password" returnKeyType="go" onSubmitEditing={submit} />
              <Pressable onPress={() => setShow(s => !s)} style={styles.eye} accessibilityLabel={show ? 'Ocultar contraseña' : 'Mostrar contraseña'} hitSlop={12}>
                {show ? <EyeOff color={colors.textMuted} size={20} /> : <Eye color={colors.textMuted} size={20} />}
              </Pressable>
            </View>
            {error && <View style={{ marginBottom: spacing.md }}><Banner tone="danger" text={error} /></View>}
            <Button title="Ingresar" onPress={submit} loading={loading} />
            <Text style={[font.small, { textAlign: 'center', marginTop: spacing.lg }]}>
              ¿Olvidaste tu contraseña? Pide a tu academia que te genere una contraseña temporal.
            </Text>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.bg },
  content: { padding: spacing.xl, alignItems: 'center', gap: spacing.lg },
  logo: { width: 110, height: 110 },
  academy: { width: '100%', flexDirection: 'row', alignItems: 'center', gap: spacing.sm, backgroundColor: colors.primaryLight, padding: spacing.md, borderRadius: radius.md },
  eye: { position: 'absolute', right: 14, top: 38 },
});
