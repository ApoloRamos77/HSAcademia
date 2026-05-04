import { Navigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export default function ProtectedRoute({ children, roles, permissions }) {
  const { user } = useAuth();
  if (!user) return <Navigate to="/login" replace />;
  if (roles && !roles.includes(user.role)) return <Navigate to="/login" replace />;

  if (permissions && user.role === 'Staff') {
    const userPerms = user.permissions || [];
    const hasPerm = permissions.some(p => userPerms.includes(p));
    if (!hasPerm) return <Navigate to="/dashboard" replace />;
  }

  return children;
}
