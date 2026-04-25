import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Toaster } from 'react-hot-toast';
import { AuthProvider } from './context/AuthContext';
import ProtectedRoute from './components/ProtectedRoute';
import Landing from './pages/public/Landing';
import LoginPage from './pages/LoginPage';
import RegisterPage from './pages/RegisterPage';
import SuperAdminDashboard from './pages/superadmin/Dashboard';
import SolicitudesPage from './pages/superadmin/Solicitudes';
import AcademiasPage from './pages/superadmin/Academias';
import UsuariosPage from './pages/superadmin/Usuarios';
import Dashboard from './pages/academy/Dashboard';
import Sedes from './pages/academy/Sedes';
import Categorias from './pages/academy/Categorias';
import Roles from './pages/academy/Roles';
import Usuarios from './pages/academy/Usuarios';
import Alumnos from './pages/academy/Alumnos';
import Asistencia from './pages/academy/Asistencia';
import AsistenciaMetricas from './pages/academy/AsistenciaMetricas';
import Calendario from './pages/academy/Calendario';
import Tienda from './pages/academy/Tienda';
import Finanzas from './pages/academy/Finanzas';
import ChangePasswordPage from './pages/ChangePasswordPage';
import SessionTimeout from './components/SessionTimeout';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <SessionTimeout timeout={60000} />
        <Toaster
          position="top-right"
          toastOptions={{
            style: { background: '#1a1a2e', color: '#f1f5f9', border: '1px solid #2d2d50' },
            success: { iconTheme: { primary: '#10b981', secondary: '#fff' } },
            error: { iconTheme: { primary: '#ef4444', secondary: '#fff' } },
          }}
        />
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/registro" element={<RegisterPage />} />
          <Route path="/cambiar-password" element={<ChangePasswordPage />} />

          {/* Super Admin */}
          <Route path="/super-admin/dashboard" element={
            <ProtectedRoute roles={['SuperAdmin']}><SuperAdminDashboard /></ProtectedRoute>
          } />
          <Route path="/super-admin/solicitudes" element={
            <ProtectedRoute roles={['SuperAdmin']}><SolicitudesPage /></ProtectedRoute>
          } />
          <Route path="/super-admin/academias" element={
            <ProtectedRoute roles={['SuperAdmin']}><AcademiasPage /></ProtectedRoute>
          } />
          <Route path="/super-admin/usuarios" element={
            <ProtectedRoute roles={['SuperAdmin']}><UsuariosPage /></ProtectedRoute>
          } />

          {/* Academy Admin */}
          <Route path="/dashboard" element={
            <ProtectedRoute roles={['AcademyAdmin', 'Staff']}>
              <Dashboard />
            </ProtectedRoute>
          } />
          <Route path="/academy/sedes" element={
            <ProtectedRoute roles={['AcademyAdmin', 'Staff']}>
              <Sedes />
            </ProtectedRoute>
          } />
          <Route path="/academy/categorias" element={
            <ProtectedRoute roles={['AcademyAdmin', 'Staff']}>
              <Categorias />
            </ProtectedRoute>
          } />
          <Route path="/academy/roles" element={
            <ProtectedRoute roles={['AcademyAdmin', 'Staff']}>
              <Roles />
            </ProtectedRoute>
          } />
          <Route path="/academy/usuarios" element={
            <ProtectedRoute roles={['AcademyAdmin', 'Staff']}>
              <Usuarios />
            </ProtectedRoute>
          } />
          <Route path="/academy/alumnos" element={
            <ProtectedRoute roles={['AcademyAdmin', 'Staff']}>
              <Alumnos />
            </ProtectedRoute>
          } />
          <Route path="/academy/asistencia" element={
            <ProtectedRoute roles={['AcademyAdmin', 'Staff']}>
              <Asistencia />
            </ProtectedRoute>
          } />
          <Route path="/academy/asistencia-metricas" element={
            <ProtectedRoute roles={['AcademyAdmin', 'Staff']}>
              <AsistenciaMetricas />
            </ProtectedRoute>
          } />
          <Route path="/academy/calendario" element={
            <ProtectedRoute roles={['AcademyAdmin', 'Staff']}>
              <Calendario />
            </ProtectedRoute>
          } />
          <Route path="/academy/tienda" element={
            <ProtectedRoute roles={['AcademyAdmin', 'Staff']}>
              <Tienda />
            </ProtectedRoute>
          } />
          <Route path="/academy/finanzas" element={
            <ProtectedRoute roles={['AcademyAdmin', 'Staff']}>
              <Finanzas />
            </ProtectedRoute>
          } />

          <Route path="/" element={<Landing />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
