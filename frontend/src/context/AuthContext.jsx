import { createContext, useCallback, useContext, useState, useEffect } from 'react';
import api from '../api/axios';
import { loadReceiptProfile } from '../utils/pdfGenerator';

const printsReceipts = (u) => u && (u.role === 'AcademyAdmin' || u.role === 'Staff') && !u.requirePasswordChange;

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
      if (printsReceipts(next)) loadReceiptProfile(api);
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
    // Encabezado único de los recibos PDF (no la dirección de una sede).
    if (printsReceipts(data)) loadReceiptProfile(api);
    return data;
  };

  const logout = () => {
    localStorage.clear();
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, login, logout, can, isAdmin, academyWide, refresh }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
