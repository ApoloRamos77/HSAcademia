import React, { useState, useEffect } from 'react';
import AppLayout from '../../components/AppLayout';
import { useAuth } from '../../context/AuthContext';
import api from '../../api/axios';
import { 
  User, Calendar, CreditCard, Shield, Clock, AlertTriangle, CheckCircle, Info 
} from 'lucide-react';
import toast from 'react-hot-toast';

export default function StudentDashboard() {
  const { user } = useAuth();
  const isGuardian = user?.role === 'Guardian';
  
  const [students, setStudents] = useState([]);
  const [selectedStudentId, setSelectedStudentId] = useState(null);
  
  const [debts, setDebts] = useState([]);
  const [history, setHistory] = useState([]);
  const [loading, setLoading] = useState(true);

  // 1. Fetch Students if Guardian
  useEffect(() => {
    if (isGuardian) {
      api.get('/mobile/students/my-students')
        .then(res => {
          setStudents(res.data);
          if (res.data.length > 0) {
            setSelectedStudentId(res.data[0].id);
          } else {
            setLoading(false);
          }
        })
        .catch(() => {
          toast.error('Error al cargar alumnos');
          setLoading(false);
        });
    } else {
      // If just student, id is already implied in the token, but we trigger fetch
      setSelectedStudentId('self');
    }
  }, [isGuardian]);

  // 2. Fetch Data for Selected Student
  useEffect(() => {
    if (!selectedStudentId) return;

    setLoading(true);
    const params = selectedStudentId !== 'self' ? `?studentId=${selectedStudentId}` : '';

    Promise.all([
      api.get(`/finances/my-debts${params}`),
      api.get(`/attendance/my-history${params}`)
    ])
    .then(([debtsRes, histRes]) => {
      setDebts(debtsRes.data);
      setHistory(histRes.data);
    })
    .catch(() => {
      toast.error('Error al cargar datos del portal');
    })
    .finally(() => setLoading(false));
  }, [selectedStudentId]);

  const pendingDebts = debts.filter(d => !d.isPaid);
  const overdueCount = pendingDebts.filter(d => d.status === 'Vencido').length;

  return (
    <AppLayout title="Portal del Alumno / Apoderado">
      <div className="fade-in space-y-6 max-w-6xl mx-auto">
        <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
          <div>
            <h1 className="text-2xl font-bold mb-1 text-white flex items-center gap-2">
              <User className="text-primary-400" /> ¡Hola, {user?.fullName}!
            </h1>
            <p className="text-text-secondary">
              Bienvenido al portal web. Aquí puedes consultar el estado de tu cuenta.
            </p>
          </div>

          {/* Student Selector for Guardians */}
          {isGuardian && students.length > 0 && (
            <div className="flex items-center gap-2 bg-card/40 border border-border/50 p-2 rounded-xl backdrop-blur-sm">
              <span className="text-sm text-text-muted px-2">Viendo a:</span>
              <div className="flex gap-2">
                {students.map(s => (
                  <button
                    key={s.id}
                    onClick={() => setSelectedStudentId(s.id)}
                    className={`px-4 py-1.5 rounded-lg text-sm font-medium transition-all ${
                      selectedStudentId === s.id 
                        ? 'bg-primary text-white shadow-lg shadow-primary/20' 
                        : 'bg-bg-dark text-text-muted hover:text-white'
                    }`}
                  >
                    {s.firstName}
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>

        {loading ? (
          <div className="text-center py-16 text-text-muted">
            <span className="spinner mx-auto block mb-3 border-t-primary"></span>
            Cargando portal...
          </div>
        ) : (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            
            {/* ── Finanzas ── */}
            <div className="card border-border/50">
              <div className="card-header border-b border-border/50 pb-4 mb-4">
                <h3 className="card-title flex items-center gap-2">
                  <CreditCard className="text-primary-400" size={20}/> 
                  Estado de Cuenta
                </h3>
              </div>

              {pendingDebts.length > 0 ? (
                <div className="space-y-3">
                  {overdueCount > 0 && (
                    <div className="bg-danger/10 border border-danger/30 text-danger p-3 rounded-xl flex items-start gap-3 mb-4">
                      <AlertTriangle className="shrink-0" size={20}/>
                      <div>
                        <p className="font-bold text-sm">Tienes {overdueCount} mensualidad(es) vencida(s).</p>
                        <p className="text-xs opacity-80 mt-1">Acércate a la academia para regularizar tus pagos.</p>
                      </div>
                    </div>
                  )}

                  {pendingDebts.map(d => (
                    <div key={d.id} className="bg-bg-dark border border-border/50 rounded-xl p-4 flex justify-between items-center">
                      <div>
                        <h4 className="font-bold text-white text-sm">{d.description}</h4>
                        <p className="text-xs text-text-muted mt-1">Vence: {new Date(d.dueDate).toLocaleDateString('es-PE')}</p>
                      </div>
                      <div className="text-right">
                        <p className="font-bold text-lg text-white">S/ {d.amountPending.toFixed(2)}</p>
                        <span className={`badge mt-1 ${d.status === 'Vencido' ? 'badge-danger' : 'badge-warning'}`}>
                          {d.status}
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-center py-8">
                  <div className="bg-success/10 w-16 h-16 rounded-full flex items-center justify-center mx-auto mb-3">
                    <CheckCircle className="text-success" size={32}/>
                  </div>
                  <p className="text-success font-bold">¡Todo al día!</p>
                  <p className="text-sm text-text-muted mt-1">No tienes deudas pendientes.</p>
                </div>
              )}
            </div>

            {/* ── Asistencia ── */}
            <div className="card border-border/50">
              <div className="card-header border-b border-border/50 pb-4 mb-4">
                <h3 className="card-title flex items-center gap-2">
                  <Calendar className="text-success" size={20}/> 
                  Últimas Asistencias
                </h3>
              </div>

              {history.length > 0 ? (
                <div className="space-y-2">
                  {history.slice(0, 5).map(h => {
                    const isPresent = h.status === 'Present';
                    const isJustified = h.status === 'Justified';
                    const color = isPresent ? 'text-success' : isJustified ? 'text-warning' : 'text-danger';
                    const bgColor = isPresent ? 'bg-success/10' : isJustified ? 'bg-warning/10' : 'bg-danger/10';
                    const label = isPresent ? 'Asistió' : isJustified ? 'Justificado' : 'Faltó';

                    return (
                      <div key={h.id} className="bg-bg-dark border border-border/50 rounded-xl p-3 flex justify-between items-center">
                        <div className="flex items-center gap-3">
                          <div className={`p-2 rounded-lg ${bgColor} ${color}`}>
                            <Clock size={18}/>
                          </div>
                          <div>
                            <p className="font-medium text-sm text-white">{new Date(h.date).toLocaleDateString('es-PE', { weekday: 'long', day: 'numeric', month: 'long' })}</p>
                            <p className="text-xs text-text-muted mt-0.5">{h.categoryName}</p>
                          </div>
                        </div>
                        <span className={`font-bold text-sm ${color}`}>{label}</span>
                      </div>
                    );
                  })}
                  {history.length > 5 && (
                    <p className="text-center text-xs text-text-muted mt-4">Mostrando los últimos 5 registros.</p>
                  )}
                </div>
              ) : (
                <div className="text-center py-8">
                  <Calendar className="mx-auto text-text-muted opacity-40 mb-3" size={40}/>
                  <p className="text-text-muted text-sm">No hay registros recientes de asistencia.</p>
                </div>
              )}
            </div>

          </div>
        )}

        <div className="bg-primary/10 border border-primary/30 p-4 rounded-xl flex items-start gap-4 mt-8">
          <Info className="text-primary-400 shrink-0 mt-1" size={24} />
          <div>
            <h4 className="font-bold text-white text-sm">Experiencia Completa en Móvil</h4>
            <p className="text-sm text-text-secondary mt-1">
              Para pagar tus mensualidades online, justificar inasistencias y comprar en la tienda, por favor descarga y utiliza la aplicación móvil oficial de HSAcademia.
            </p>
          </div>
        </div>
      </div>
    </AppLayout>
  );
}
