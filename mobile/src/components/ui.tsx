import React from 'react';
import {
  ActivityIndicator, Pressable, RefreshControl, ScrollView, StyleSheet, Text, TextInput, TextInputProps, View, ViewStyle,
} from 'react-native';
import { SafeAreaView, useSafeAreaInsets } from 'react-native-safe-area-context';
import { BottomTabBarHeightContext } from '@react-navigation/bottom-tabs';
import { colors, font, radius, shadow, spacing } from '../theme';

/** Contenedor de pantalla con scroll y "jalar para actualizar". */
/**
 * Espacio inferior que ocupa la barra de navegación del sistema (◀ ● ■ o la barra de gestos).
 * Desde Android 15 las apps se dibujan "de borde a borde", también debajo de esa barra.
 * Dentro de las pestañas devuelve 0: la barra de pestañas ya reserva ese espacio.
 */
export function useBottomInset(): number {
  const insets = useSafeAreaInsets();
  const inTabs = React.useContext(BottomTabBarHeightContext) !== undefined;
  return inTabs ? 0 : insets.bottom;
}

export function Screen({ children, refreshing, onRefresh, padded = true, scroll = true, edges }: {
  children: React.ReactNode; refreshing?: boolean; onRefresh?: () => void; padded?: boolean; scroll?: boolean;
  edges?: ('top' | 'bottom' | 'left' | 'right')[];
}) {
  const bottomInset = useBottomInset();
  const content = padded ? <View style={styles.padded}>{children}</View> : children;
  return (
    <SafeAreaView style={styles.screen} edges={edges ?? ['left', 'right']}>
      {scroll ? (
        <ScrollView
          contentContainerStyle={{ paddingBottom: spacing.xxl + bottomInset }}
          keyboardShouldPersistTaps="handled"
          refreshControl={onRefresh ? <RefreshControl refreshing={!!refreshing} onRefresh={onRefresh} colors={[colors.primary]} /> : undefined}
        >
          {content}
        </ScrollView>
      ) : content}
    </SafeAreaView>
  );
}

export function Card({ children, style, onPress }: { children: React.ReactNode; style?: ViewStyle; onPress?: () => void }) {
  if (onPress) {
    return (
      <Pressable onPress={onPress} style={({ pressed }) => [styles.card, style, pressed && { opacity: 0.85 }]} android_ripple={{ color: colors.surface }}>
        {children}
      </Pressable>
    );
  }
  return <View style={[styles.card, style]}>{children}</View>;
}

export function SectionTitle({ title, action }: { title: string; action?: React.ReactNode }) {
  return (
    <View style={styles.sectionRow}>
      <Text style={styles.section}>{title}</Text>
      {action}
    </View>
  );
}

type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost' | 'success';
export function Button({ title, onPress, variant = 'primary', loading, disabled, icon, style, compact }: {
  title: string; onPress?: () => void; variant?: ButtonVariant; loading?: boolean; disabled?: boolean;
  icon?: React.ReactNode; style?: ViewStyle; compact?: boolean;
}) {
  const v = buttonVariants[variant];
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityState={{ disabled: disabled || loading }}
      onPress={onPress}
      disabled={disabled || loading}
      android_ripple={{ color: 'rgba(255,255,255,0.25)' }}
      style={({ pressed }) => [
        styles.button, compact && styles.buttonCompact, { backgroundColor: v.bg, borderColor: v.border },
        (disabled || loading) && { opacity: 0.55 }, pressed && { opacity: 0.85 }, style,
      ]}
    >
      {loading ? <ActivityIndicator color={v.fg} /> : (
        <View style={styles.buttonInner}>
          {icon}
          <Text style={[styles.buttonText, compact && { fontSize: 14 }, { color: v.fg }]}>{title}</Text>
        </View>
      )}
    </Pressable>
  );
}

const buttonVariants: Record<ButtonVariant, { bg: string; fg: string; border: string }> = {
  primary: { bg: colors.primaryDark, fg: colors.white, border: colors.primaryDark },
  success: { bg: colors.accentDark, fg: colors.white, border: colors.accentDark },
  secondary: { bg: colors.primaryLight, fg: colors.primaryDark, border: colors.primaryLight },
  danger: { bg: colors.dangerBg, fg: colors.danger, border: colors.dangerBg },
  ghost: { bg: 'transparent', fg: colors.primaryDark, border: colors.border },
};

export function Input({ label, hint, error, style, ...props }: TextInputProps & { label?: string; hint?: string; error?: string }) {
  return (
    <View style={{ marginBottom: spacing.md }}>
      {label && <Text style={styles.label}>{label}</Text>}
      <TextInput
        placeholderTextColor={colors.textMuted}
        style={[styles.input, !!error && { borderColor: colors.danger }, props.multiline && { minHeight: 90, textAlignVertical: 'top' }, style]}
        {...props}
      />
      {!!error && <Text style={[font.small, { color: colors.danger, marginTop: 4 }]}>{error}</Text>}
      {!!hint && !error && <Text style={[font.tiny, { marginTop: 4 }]}>{hint}</Text>}
    </View>
  );
}

type Tone = 'success' | 'warning' | 'danger' | 'info' | 'muted';
const tones: Record<Tone, { bg: string; fg: string }> = {
  success: { bg: colors.successBg, fg: colors.success },
  warning: { bg: colors.warningBg, fg: colors.warning },
  danger: { bg: colors.dangerBg, fg: colors.danger },
  info: { bg: colors.infoBg, fg: colors.info },
  muted: { bg: colors.surface, fg: colors.textSecondary },
};
export function Badge({ text, tone = 'muted' }: { text: string; tone?: Tone }) {
  const t = tones[tone];
  return <View style={[styles.badge, { backgroundColor: t.bg }]}><Text style={[styles.badgeText, { color: t.fg }]}>{text}</Text></View>;
}

export function Banner({ text, tone = 'info', icon }: { text: string; tone?: Tone; icon?: React.ReactNode }) {
  const t = tones[tone];
  return (
    <View style={[styles.banner, { backgroundColor: t.bg }]}>
      {icon}
      <Text style={[font.body, { color: t.fg, flex: 1 }]}>{text}</Text>
    </View>
  );
}

export function Empty({ text, icon }: { text: string; icon?: React.ReactNode }) {
  return (
    <View style={styles.empty}>
      {icon}
      <Text style={[font.body, { textAlign: 'center', color: colors.textMuted, marginTop: spacing.sm }]}>{text}</Text>
    </View>
  );
}

export function Loading({ text }: { text?: string }) {
  return (
    <View style={styles.loading}>
      <ActivityIndicator size="large" color={colors.primary} />
      {text && <Text style={[font.small, { marginTop: spacing.md }]}>{text}</Text>}
    </View>
  );
}

export function Stat({ label, value, sub, tone }: { label: string; value: string; sub?: string; tone?: Tone }) {
  return (
    <View style={[styles.stat, tone === 'danger' && { borderLeftColor: colors.danger, borderLeftWidth: 4 }]}>
      <Text style={font.small}>{label}</Text>
      <Text style={styles.statValue} numberOfLines={1} adjustsFontSizeToFit>{value}</Text>
      {sub && <Text style={font.tiny} numberOfLines={2}>{sub}</Text>}
    </View>
  );
}

export function Row({ children, style }: { children: React.ReactNode; style?: ViewStyle }) {
  return <View style={[{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }, style]}>{children}</View>;
}

export function Divider() {
  return <View style={{ height: StyleSheet.hairlineWidth, backgroundColor: colors.border, marginVertical: spacing.sm }} />;
}

/** Selector horizontal (chips) para elegir alumno, filtros, etc. */
export function Chips<T extends string>({ options, value, onChange }: {
  options: { value: T; label: string }[]; value: T; onChange: (v: T) => void;
}) {
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: spacing.sm, paddingVertical: spacing.xs }}>
      {options.map(o => {
        const active = o.value === value;
        return (
          <Pressable key={o.value} onPress={() => onChange(o.value)} accessibilityRole="tab" accessibilityState={{ selected: active }}
            style={[styles.chip, active && { backgroundColor: colors.primaryDark, borderColor: colors.primaryDark }]}>
            <Text style={[styles.chipText, active && { color: colors.white }]}>{o.label}</Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.bg },
  padded: { padding: spacing.lg, gap: spacing.md },
  card: { backgroundColor: colors.card, borderRadius: radius.lg, padding: spacing.lg, borderWidth: 1, borderColor: colors.border, ...shadow },
  sectionRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: spacing.sm },
  section: { ...font.h2 },
  button: { minHeight: 50, borderRadius: radius.md, paddingHorizontal: spacing.lg, justifyContent: 'center', alignItems: 'center', borderWidth: 1 },
  buttonCompact: { minHeight: 40, paddingHorizontal: spacing.md },
  buttonInner: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  buttonText: { fontSize: 16, fontWeight: '600' },
  label: { ...font.small, color: colors.textSecondary, fontWeight: '600', marginBottom: 6 },
  input: { backgroundColor: colors.card, borderWidth: 1, borderColor: colors.border, borderRadius: radius.md, paddingHorizontal: spacing.md, paddingVertical: 12, fontSize: 16, color: colors.text },
  badge: { alignSelf: 'flex-start', paddingHorizontal: 10, paddingVertical: 4, borderRadius: radius.pill },
  badgeText: { fontSize: 12, fontWeight: '700' },
  banner: { flexDirection: 'row', gap: spacing.sm, alignItems: 'center', padding: spacing.md, borderRadius: radius.md },
  empty: { alignItems: 'center', padding: spacing.xl },
  loading: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: spacing.xxl, backgroundColor: colors.bg },
  stat: { flex: 1, minWidth: '45%', backgroundColor: colors.card, borderRadius: radius.lg, padding: spacing.md, borderWidth: 1, borderColor: colors.border, gap: 2 },
  statValue: { fontSize: 24, fontWeight: '700', color: colors.text },
  chip: { paddingHorizontal: 14, paddingVertical: 8, borderRadius: radius.pill, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card },
  chipText: { fontSize: 14, fontWeight: '600', color: colors.textSecondary },
});
