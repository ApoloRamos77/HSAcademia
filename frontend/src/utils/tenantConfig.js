// src/utils/tenantConfig.js

// This simulates a response from an API for a specific tenant.
// In the future, this will be fetched dynamically (e.g. by subdomain).
export const mockTenants = {
  'adhsoft-sport': {
    id: 'adhsoft-sport',
    name: 'Academia ADHSOFT Sport',
    slogan: 'Formando campeones con valores y pasión por el deporte.',
    description: 'Únete a nuestra gran familia y desarrolla tu máximo potencial en la cancha. Entrenamiento de élite para jóvenes talentos.',
    logo: '/logo.png', // Fallback to current logo
    theme: {
      primaryColor: '#0033A0', // Blue
      secondaryColor: '#8DC63F', // Green
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
  },
  'demo-club': {
    id: 'demo-club',
    name: 'Club Atlético Elite',
    slogan: 'Tu camino hacia la profesionalidad empieza aquí.',
    description: 'Más de 10 años formando talentos. Contamos con entrenadores certificados y las mejores instalaciones.',
    logo: 'https://via.placeholder.com/150x50?text=CAE+Logo',
    theme: {
      primaryColor: '#1e293b', // Dark Slate
      secondaryColor: '#f59e0b', // Amber
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
  return mockTenants[tenantId] || null;
};
