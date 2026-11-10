import { createContext, useCallback, useContext, useState, useEffect } from 'react';
import api from '../api/axios';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(() => {
    const u = localStorage.getItem('user');
    return u ? JSON.parse(u) : null;
  });

  // Trae los permisos vigentes: un cambio de roles aplica sin volver a iniciar sesión.
  const refresh = useCallback(async () => {
    const stored = JSON.parse(localStorage.getItem('user') || 'null');
    if (!stored || stored.requirePasswordChange) return;
    try {
      const { data } = await api.get('/auth/me');
      const next = { ...stored, ...data, token: stored.token };
      localStorage.setItem('user', JSON.stringify(next));
      setUser(next);
    } catch { /* sin conexión: se conservan los permisos conocidos */ }
  }, []);

  useEffect(() => {
    refresh();
    window.addEventListener('focus', refresh);
    return () => window.removeEventListener('focus', refresh);
  }, [refresh]);

  const isAdmin = user?.role === 'AcademyAdmin' || user?.role === 'SuperAdmin';
  const can = (...permissions) => isAdmin || permissions.some(p => user?.permissions?.includes(p));
  const academyWide = isAdmin || user?.academyWide !== false;

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
    <AuthContext.Provider value={{ user, login, logout, can, academyWide }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
