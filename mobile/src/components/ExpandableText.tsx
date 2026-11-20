import React, { useState } from 'react';
import { Pressable, Text, View, type TextStyle } from 'react-native';
import { colors } from '../theme';

/**
 * Texto largo cortado a `lines` líneas con "Ver más" / "Ver menos".
 * Una copia invisible del texto completo cuenta sus líneas (Android recorta el conteo cuando hay numberOfLines),
 * así el botón solo aparece cuando de verdad no cabe.
 */
export default function ExpandableText({ text, lines = 3, style }: { text: string; lines?: number; style?: TextStyle | TextStyle[] }) {
  const [open, setOpen] = useState(false);
  const [total, setTotal] = useState(0);
  const overflows = total > lines;

  return (
    <View>
      <Text style={[style, { position: 'absolute', opacity: 0, left: 0, right: 0 }]} accessible={false} importantForAccessibility="no-hide-descendants"
        onTextLayout={e => setTotal(e.nativeEvent.lines.length)}>{text}</Text>
      <Text style={style} numberOfLines={open ? undefined : lines}>{text}</Text>
      {overflows && (
        <Pressable onPress={() => setOpen(o => !o)} hitSlop={8} accessibilityRole="button" accessibilityState={{ expanded: open }}
          style={{ alignSelf: 'flex-start', paddingVertical: 6, minHeight: 36, justifyContent: 'center' }}>
          <Text style={{ color: colors.primaryDark, fontWeight: '700' }}>{open ? 'Ver menos' : 'Ver más'}</Text>
        </Pressable>
      )}
    </View>
  );
}
