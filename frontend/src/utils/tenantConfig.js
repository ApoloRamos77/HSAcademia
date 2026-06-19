// src/utils/tenantConfig.js

export const defaultAcademy = {
  id: 'default',
  name: 'ADHSOFT SPORT',
  badge: 'BIENVENIDO A LA ÉLITE',
  slogan: 'ACADEMIA DEPORTIVA ADHSOFT SPORT',
  description: 'Formando campeones con valores y pasión por el deporte. Únete a nuestra gran familia y desarrolla tu máximo potencial en la cancha.',
  logo: '/logo.png',
  theme: {
    primary: '#1E3A5F',       // Background / Main Dark Blue
    secondary: '#8DC63F',     // Action Button / Glow Green
    accent: '#264A7F',        // Secondary buttons / Highlights
    textLight: '#FFFFFF',     // Text on dark backgrounds
    textDark: '#334155'       // Text on light backgrounds
  },
  contact: {
    phone: '+51 941 883 990',
    email: 'nvelazco@helpersoft.com.pe',
    address: 'Colegio El Divino Maestro - Puente Piedra',
    whatsapp: '51941883990',
  },
  categories: [
    { id: 1, title: 'Categoría Sub-15', desc: 'Constancia, disciplina y táctica avanzada para los más grandes.' },
    { id: 2, title: 'Categoría Sub-13', desc: 'Esfuerzo y dedicación, mejorando habilidades técnicas individuales.' },
    { id: 3, title: 'Baby Fútbol', desc: 'Talento y disciplina desde los primeros pasos. Diversión asegurada.' }
  ],
  socialLinks: {
    facebook: '#',
    instagram: '#'
  }
};

export const mockTenants = {
  'demo-club': {
    id: 'demo-club',
    name: 'Club Atlético Elite',
    badge: 'FORJANDO EL FUTURO',
    slogan: 'CLUB ATLÉTICO ELITE',
    description: 'Más de 10 años formando talentos. Contamos con entrenadores certificados y las mejores instalaciones.',
    logo: 'https://via.placeholder.com/150x50?text=CAE+Logo',
    theme: {
      primary: '#1e293b',       // Dark Slate
      secondary: '#f59e0b',     // Amber
      accent: '#334155',        // Slightly lighter slate
      textLight: '#f8fafc',
      textDark: '#0f172a'
    },
    contact: {
      phone: '+51 987 654 321',
      email: 'info@clubelite.com',
      address: 'Av. Las Palmeras 123, Los Olivos',
      whatsapp: '51987654321',
    },
    categories: [
      { id: 1, title: 'Equipo Competitivo', desc: 'Para jugadores experimentados buscando alto rendimiento.' },
      { id: 2, title: 'Academia Formativa', desc: 'Aprende los fundamentos técnicos y tácticos.' }
    ],
    socialLinks: {
      facebook: '#',
      instagram: '#'
    }
  }
};

export const getTenantData = (tenantId) => {
  // If the tenant ID is found in our mocks, return it.
  if (tenantId && mockTenants[tenantId]) {
    return mockTenants[tenantId];
  }
  // Fallback: return the default ADHSOFT SPORT template for any unrecognized GUID.
  return defaultAcademy;
};
