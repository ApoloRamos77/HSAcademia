import React, { useMemo, useState } from 'react';
import { FlatList, Pressable, StyleSheet, Text, View } from 'react-native';
import { ChevronRight, Search, Users } from 'lucide-react-native';
import { api } from '../../api/client';
import type { StudentSummary } from '../../api/types';
import { Chips, Empty, Input, Loading } from '../../components/ui';
import { useApi } from '../../hooks/useApi';
import { colors, font, radius, spacing } from '../../theme';
import { initials } from '../../utils/format';

/** Alumnos del entrenador (el backend ya filtra por sus categorías asignadas). */
export default function StudentsScreen({ navigation }: any) {
  const [q, setQ] = useState('');
  const [cat, setCat] = useState('all');
  const { data, loading, refreshing, refresh } = useApi(() => api.get<StudentSummary[]>('/students').then(r => r.data), []);

  const active = (data ?? []).filter(s => s.isActive);
  const categories = useMemo(() => {
    const names = Array.from(new Set(active.map(s => s.categoryName))).sort();
    return [{ value: 'all', label: `Todas (${active.length})` }, ...names.map(n => ({ value: n, label: n }))];
  }, [active]);

  const term = q.trim().toLowerCase();
  const list = active
    .filter(s => cat === 'all' || s.categoryName === cat)
    .filter(s => !term || `${s.firstName} ${s.lastName} ${s.documentNumber ?? ''}`.toLowerCase().includes(term))
    .sort((a, b) => `${a.lastName}${a.firstName}`.localeCompare(`${b.lastName}${b.firstName}`));

  if (loading && !data) return <Loading />;

  return (
    <View style={{ flex: 1, backgroundColor: colors.bg }}>
      <View style={{ padding: spacing.lg, paddingBottom: 0 }}>
        <Input placeholder="Buscar por nombre o DNI" value={q} onChangeText={setQ} autoCorrect={false} />
        <Chips options={categories} value={cat} onChange={setCat} />
      </View>
      <FlatList
        data={list}
        keyExtractor={s => s.id}
        onRefresh={refresh}
        refreshing={refreshing}
        contentContainerStyle={{ padding: spacing.lg, gap: spacing.sm }}
        ListEmptyComponent={<Empty icon={term ? <Search color={colors.textMuted} /> : <Users color={colors.textMuted} />}
          text={term ? 'Sin resultados.' : 'No tienes alumnos asignados.'} />}
        renderItem={({ item }) => (
          <Pressable onPress={() => navigation.navigate('StudentDetail', { student: item })} style={({ pressed }) => [styles.row, pressed && { opacity: 0.8 }]}>
            <View style={styles.avatar}><Text style={styles.avatarText}>{initials(`${item.firstName} ${item.lastName}`)}</Text></View>
            <View style={{ flex: 1 }}>
              <Text style={font.h3}>{item.lastName}, {item.firstName}</Text>
              <Text style={font.small}>{item.categoryName} · {item.age} años · {item.headquarterName}</Text>
            </View>
            <ChevronRight color={colors.textMuted} />
          </Pressable>
        )}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, backgroundColor: colors.card, padding: spacing.md, borderRadius: radius.md, borderWidth: 1, borderColor: colors.border },
  avatar: { width: 42, height: 42, borderRadius: radius.pill, backgroundColor: colors.primaryLight, alignItems: 'center', justifyContent: 'center' },
  avatarText: { color: colors.primaryDark, fontWeight: '700' },
});
