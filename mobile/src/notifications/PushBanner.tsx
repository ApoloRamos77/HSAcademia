import React, { useEffect } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Bell } from 'lucide-react-native';
import { colors, font, radius, shadow, spacing } from '../theme';
import type { PushNotice } from './push';

const VISIBLE_MS = 6000;

/** Aviso recibido con la app abierta: el sistema no lo muestra en ese caso. */
export default function PushBanner({ notice, onPress, onHide }: { notice: PushNotice | null; onPress: () => void; onHide: () => void }) {
  const insets = useSafeAreaInsets();

  useEffect(() => {
    if (!notice) return;
    const timer = setTimeout(onHide, VISIBLE_MS);
    return () => clearTimeout(timer);
  }, [notice, onHide]);

  if (!notice) return null;
  return (
    <Pressable onPress={onPress} accessibilityRole="alert" style={[styles.banner, { top: insets.top + spacing.sm }]}>
      <Bell color={colors.primaryDark} size={20} />
      <View style={{ flex: 1 }}>
        <Text style={font.h3} numberOfLines={1}>{notice.title}</Text>
        {!!notice.body && <Text style={font.small} numberOfLines={2}>{notice.body}</Text>}
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  banner: {
    position: 'absolute', left: spacing.md, right: spacing.md, flexDirection: 'row', alignItems: 'center', gap: spacing.md,
    padding: spacing.md, borderRadius: radius.md, backgroundColor: colors.card, ...shadow,
  },
});
