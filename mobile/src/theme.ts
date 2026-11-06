/** Tokens de diseño: los mismos colores de marca que la web (celeste, azul y verde). */
export const colors = {
  primary: '#3EB1E5',
  primaryDark: '#24588C',
  primaryLight: '#E0F4FF',
  accent: '#76B858',
  accentDark: '#4E8A33',

  bg: '#F2F9FC',
  card: '#FFFFFF',
  surface: '#E8F2F7',
  border: '#D6E8F2',

  text: '#1E293B',
  textSecondary: '#334155',
  textMuted: '#64748B',
  white: '#FFFFFF',

  success: '#2F7D32',
  successBg: '#DCEFD2',
  warning: '#93590B',
  warningBg: '#FDF0D2',
  danger: '#B42318',
  dangerBg: '#FDE2E2',
  info: '#1D5AA6',
  infoBg: '#E3EEFB',
};

export const spacing = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32 };
export const radius = { sm: 8, md: 12, lg: 16, pill: 999 };

export const font = {
  h1: { fontSize: 24, fontWeight: '700' as const, color: colors.text },
  h2: { fontSize: 18, fontWeight: '700' as const, color: colors.text },
  h3: { fontSize: 16, fontWeight: '600' as const, color: colors.text },
  body: { fontSize: 15, color: colors.textSecondary },
  small: { fontSize: 13, color: colors.textMuted },
  tiny: { fontSize: 11, color: colors.textMuted },
};

export const shadow = {
  shadowColor: '#24588C',
  shadowOpacity: 0.08,
  shadowRadius: 12,
  shadowOffset: { width: 0, height: 4 },
  elevation: 2,
};
