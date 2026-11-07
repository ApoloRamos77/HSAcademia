import { createContext, useContext, useState, useEffect } from 'react';
import api from '../api/axios';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => {
    const u = localStorage.getItem('user');
    return u ? JSON.parse(u) : null;
  });

  const login = async (emailOrPhone, password, role) => {
    const res = await api.post('/auth/login', { emailOrPhone, password, role });
    const data = res.data;
    localStorage.setItem('token', data.token);
    localStorage.setItem('user', JSON.stringify(data));
    setUser(data);
    // Perfil público de la academia (nombre, dirección, teléfono) para los recibos PDF.
    if (data.academyId) {
      api.get(`/public/academies/${data.academyId}`)
        .then(r => localStorage.setItem('academyProfile', JSON.stringify({
          name: r.data.name, address: r.data.contact?.address || '', phone: r.data.contact?.phone || '',
        })))
        .catch(() => {});
    }
    return data;
  };

  const logout = () => {
    localStorage.clear();
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
