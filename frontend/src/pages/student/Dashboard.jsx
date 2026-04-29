import React from 'react';
import AppLayout from '../../components/AppLayout';
import { useAuth } from '../../context/AuthContext';
import { User, Calendar, CreditCard, Shield } from 'lucide-react';

export default function StudentDashboard() {
  const { user } = useAuth();

  return (
    <AppLayout title="Portal del Alumno / Apoderado">
      <div className="fade-in space-y-6">
        <h1 className="text-2xl font-bold mb-2 text-white">¡Hola, {user?.fullName}!</h1>
        <p className="text-text-secondary mb-8">
          Bienvenido al portal web. Recuerda que la experiencia completa de clases, pagos y asistencia está disponible en la Aplicación Móvil.
        </p>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <div className="card text-center p-6 bg-card/40 border border-primary/20">
            <div className="flex justify-center mb-4 text-primary-400">
              <User size={48} />
            </div>
            <h3 className="font-bold text-lg text-white mb-2">Mi Perfil</h3>
            <p className="text-sm text-text-muted">Visualiza tu información personal y deportiva.</p>
          </div>

          <div className="card text-center p-6 bg-card/40 border border-success/20">
            <div className="flex justify-center mb-4 text-success">
              <Calendar size={48} />
            </div>
            <h3 className="font-bold text-lg text-white mb-2">Mis Horarios</h3>
            <p className="text-sm text-text-muted">Consulta tus próximas sesiones de entrenamiento.</p>
          </div>

          <div className="card text-center p-6 bg-card/40 border border-warning/20">
            <div className="flex justify-center mb-4 text-warning">
              <CreditCard size={48} />
            </div>
            <h3 className="font-bold text-lg text-white mb-2">Pagos</h3>
            <p className="text-sm text-text-muted">Revisa el estado de tus mensualidades.</p>
          </div>
        </div>

        <div className="bg-primary/10 border border-primary/30 p-6 rounded-xl text-center mt-8">
          <Shield className="mx-auto text-primary-400 mb-2" size={32} />
          <h4 className="font-bold text-white">Acceso Web Limitado</h4>
          <p className="text-sm text-text-secondary mt-2">
            Por el momento, el acceso web para alumnos y apoderados está en desarrollo. Por favor, utiliza la app móvil de HSAcademia para disfrutar de todas las funcionalidades.
          </p>
        </div>
      </div>
    </AppLayout>
  );
}
