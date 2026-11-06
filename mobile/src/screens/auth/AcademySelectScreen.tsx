import React, { useEffect, useState } from 'react';
import { FlatList, Image, Pressable, StyleSheet, Text, View } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Building2, ChevronRight, Search } from 'lucide-react-native';
import { api, errorMessage } from '../../api/client';
import type { PublicAcademy } from '../../api/types';
import { useAuth } from '../../auth/AuthContext';
import { Empty, Input, Loading } from '../../components/ui';
import { colors, font, radius, spacing } from '../../theme';

/** Paso 1 del acceso multi-academia: elegir la academia (se recuerda para la próxima vez). */
export default function AcademySelectScreen({ navigation }: any) {
  const { selectAcademy } = useAuth();
  const [search, setSearch] = useState('');
  const [items, setItems] = useState<PublicAcademy[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const t = setTimeout(async () => {
      setLoading(true);
      try {
        const { data } = await api.get<PublicAcademy[]>('/auth/academies', { params: { search: search || undefined } });
        setItems(data);
        setError(null);
      } catch (e) {
        setError(errorMessage(e));
      } finally {
        setLoading(false);
      }
    }, 350);
    return () => clearTimeout(t);
  }, [search]);

  const choose = async (a: PublicAcademy) => {
    await selectAcademy(a);
    navigation.navigate('Login');
  };

  return (
    <SafeAreaView style={styles.screen}>
      <View style={styles.header}>
        <Image source={require('../../assets/logo.png')} style={styles.logo} resizeMode="contain" />
        <Text style={font.h1}>¡Bienvenido!</Text>
        <Text style={[font.body, { textAlign: 'center' }]}>Busca y elige tu academia para ingresar.</Text>
      </View>
      <View style={{ paddingHorizontal: spacing.lg }}>
        <Input placeholder="Nombre de la academia" value={search} onChangeText={setSearch} autoCorrect={false} returnKeyType="search" />
      </View>
      {loading ? <Loading /> : error ? <Empty text={error} /> : (
        <FlatList
          data={items}
          keyExtractor={a => a.id}
          contentContainerStyle={{ padding: spacing.lg, gap: spacing.sm }}
          ListEmptyComponent={<Empty icon={<Search color={colors.textMuted} />} text="No encontramos academias con ese nombre." />}
          renderItem={({ item }) => (
            <Pressable onPress={() => choose(item)} style={({ pressed }) => [styles.item, pressed && { opacity: 0.8 }]} accessibilityRole="button">
              <View style={styles.icon}><Building2 color={colors.primaryDark} size={22} /></View>
              <View style={{ flex: 1 }}>
                <Text style={font.h3}>{item.name}</Text>
                <Text style={font.small}>{[item.sport, item.city].filter(Boolean).join(' · ') || 'Academia deportiva'}</Text>
              </View>
              <ChevronRight color={colors.textMuted} />
            </Pressable>
          )}
        />
      )}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.bg },
  header: { alignItems: 'center', padding: spacing.xl, gap: spacing.sm },
  logo: { width: 96, height: 96 },
  item: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, backgroundColor: colors.card, padding: spacing.lg, borderRadius: radius.lg, borderWidth: 1, borderColor: colors.border },
  icon: { width: 44, height: 44, borderRadius: radius.md, backgroundColor: colors.primaryLight, alignItems: 'center', justifyContent: 'center' },
});
