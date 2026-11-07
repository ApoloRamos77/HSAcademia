module.exports = {
  root: true,
  extends: '@react-native',
  rules: {
    // Ajustes puntuales de diseño en línea: el tema y los componentes base viven en src/theme.ts y src/components/ui.tsx.
    'react-native/no-inline-styles': 'off',
  },
  overrides: [
    {
      // Decodificador base64 del JWT: usa operaciones de bits a propósito.
      files: ['src/auth/AuthContext.tsx'],
      rules: { 'no-bitwise': 'off', 'no-div-regex': 'off' },
    },
  ],
};
