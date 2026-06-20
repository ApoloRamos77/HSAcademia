import React, { useState, useEffect } from 'react';
import AppLayout from '../../components/AppLayout';
import { useAuth } from '../../context/AuthContext';
import api from '../../api/axios';
import { 
  User, Calendar, CreditCard, Shield, Clock, AlertTriangle, CheckCircle, Info, Users
} from 'lucide-react';
import toast from 'react-hot-toast';

// Utilidad para parsear fechas de forma segura (soporta ISO y DD/MM/YYYY)
const safeFormatDate = (dateStr) => {
  if (!dateStr) return 'Fecha desconocida';
  try {
    let d;
    if (dateStr.includes('/')) {
      const [day, month, year] = dateStr.split('/');
      d = new Date(`${year}-${month}-${day}T12:00:00`);
    } else {
      d = new Date(dateStr);
    }
    if (isNaN(d.getTime())) return 'Fecha inválida';
    return d.toLocaleDateString('es-PE', { weekday: 'long', day: 'numeric', month: 'long' });
  } catch (e) {
    return 'Fecha inválida';
  }
};

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
      // If just student
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
      <div className="fade-in space-y-6 max-w-5xl mx-auto pb-10">
        
        {/* Cabecera y Selector */}
        <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-6 bg-white p-6 rounded-2xl shadow-sm border border-border/50">
          <div>
            <h1 className="text-2xl font-bold mb-2 text-primary-dark flex items-center gap-2">
              <User className="text-primary" size={28} /> ¡Hola, {user?.fullName}!
            </h1>
            <p className="text-text-secondary text-sm">
              Bienvenido a tu portal. Aquí puedes consultar tu estado de cuenta y asistencias recientes.
            </p>
          </div>

          {/* Student Selector for Guardians */}
          {isGuardian && students.length > 0 && (
            <div className="flex flex-col items-start md:items-end gap-2 w-full md:w-auto">
              <span className="text-xs font-semibold text-text-muted uppercase tracking-wider">Viendo información de:</span>
              <div className="tabs m-0 p-1 bg-bg-surface rounded-xl border border-border shadow-inner flex flex-wrap gap-1">
                {students.map(s => (
                  <button
                    key={s.id}
                    onClick={() => setSelectedStudentId(s.id)}
                    className={`tab-pill px-4 py-2 ${selectedStudentId === s.id ? 'active shadow-sm' : ''}`}
                    style={{ fontSize: '14px' }}
                  >
                    {s.firstName} {s.lastName}
                  </button>
                ))}
              </div>
            </div>
          )}
        </div>

        {loading ? (
          <div className="text-center py-20 text-text-muted">
            <span className="spinner mx-auto block mb-4 border-t-primary" style={{ width: '40px', height: '40px' }}></span>
            <p className="font-medium text-primary">Cargando información del portal...</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
            
            {/* ── Finanzas ── */}
            <div className="card h-full flex flex-col shadow-sm border-border/60 hover:shadow-md transition-shadow">
              <div className="card-header border-b border-border/50 pb-4 mb-4 bg-primary/5 rounded-t-2xl">
                <h3 className="card-title flex items-center gap-2 text-primary-dark">
                  <CreditCard className="text-primary" size={22}/> 
                  Estado de Cuenta
                </h3>
              </div>

              <div className="px-6 pb-6 flex-1">
                {pendingDebts.length > 0 ? (
                  <div className="space-y-4">
                    {overdueCount > 0 && (
                      <div className="bg-danger/10 border-l-4 border-danger text-danger p-4 rounded-r-xl flex items-start gap-3 shadow-sm">
                        <AlertTriangle className="shrink-0 mt-0.5" size={20}/>
                        <div>
                          <p className="font-bold">Tienes {overdueCount} mensualidad(es) vencida(s).</p>
                          <p className="text-sm mt-1 opacity-90">Por favor, acércate a la academia para regularizar tus pagos.</p>
                        </div>
                      </div>
                    )}

                    {pendingDebts.map(d => (
                      <div key={d.id} className="bg-white border border-border rounded-xl p-4 flex justify-between items-center shadow-sm hover:border-primary/30 transition-colors">
                        <div>
                          <h4 className="font-bold text-text-primary">{d.description}</h4>
                          <p className="text-sm text-text-muted mt-1 flex items-center gap-1">
                            <Clock size={14}/> Vence: {safeFormatDate(d.dueDate)}
                          </p>
                        </div>
                        <div className="text-right">
                          <p className="font-extrabold text-xl text-text-primary">S/. {d.amountPending.toFixed(2)}</p>
                          <span className={`badge mt-2 ${d.status === 'Vencido' ? 'badge-danger' : 'badge-warning'}`}>
                            {d.status}
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div className="text-center py-10 flex flex-col items-center justify-center h-full">
                    <div className="bg-success/10 w-20 h-20 rounded-full flex items-center justify-center mx-auto mb-4 shadow-inner">
                      <CheckCircle className="text-success" size={40}/>
                    </div>
                    <p className="text-success font-extrabold text-lg">¡Todo al día!</p>
                    <p className="text-text-secondary mt-2">No tienes mensualidades pendientes de pago.</p>
                  </div>
                )}
              </div>
            </div>

            {/* ── Asistencia ── */}
            <div className="card h-full flex flex-col shadow-sm border-border/60 hover:shadow-md transition-shadow">
              <div className="card-header border-b border-border/50 pb-4 mb-4 bg-success/5 rounded-t-2xl">
                <h3 className="card-title flex items-center gap-2 text-success-dark">
                  <Calendar className="text-success" size={22}/> 
                  Últimas Asistencias
                </h3>
              </div>

              <div className="px-6 pb-6 flex-1">
                {history.length > 0 ? (
                  <div className="space-y-3">
                    {history.slice(0, 5).map(h => {
                      const isPresent = h.status === 'Present';
                      const isJustified = h.status === 'Justified';
                      const color = isPresent ? 'text-success' : isJustified ? 'text-warning' : 'text-danger';
                      const bgColor = isPresent ? 'bg-success/10' : isJustified ? 'bg-warning/10' : 'bg-danger/10';
                      const label = isPresent ? 'Asistió' : isJustified ? 'Justificado' : 'Faltó';

                      return (
                        <div key={h.id} className="bg-white border border-border rounded-xl p-3.5 flex justify-between items-center shadow-sm hover:border-border/80 transition-colors">
                          <div className="flex items-center gap-4">
                            <div className={`p-2.5 rounded-xl ${bgColor} ${color} shadow-inner`}>
                              <Calendar size={20}/>
                            </div>
                            <div>
                              <p className="font-semibold text-text-primary capitalize">{safeFormatDate(h.date)}</p>
                              <p className="text-xs text-text-muted mt-1 uppercase tracking-wide">{h.categoryName}</p>
                            </div>
                          </div>
                          <span className={`badge ${bgColor} ${color} border-none font-bold px-3 py-1.5`}>{label}</span>
                        </div>
                      );
                    })}
                    {history.length > 5 && (
                      <p className="text-center text-xs text-text-muted mt-4 uppercase tracking-widest font-semibold">
                        Mostrando los últimos 5 registros
                      </p>
                    )}
                  </div>
                ) : (
                  <div className="text-center py-10 flex flex-col items-center justify-center h-full">
                    <Calendar className="mx-auto text-text-muted opacity-30 mb-4" size={50}/>
                    <p className="text-text-muted font-medium">No hay registros recientes de asistencia.</p>
                  </div>
                )}
              </div>
            </div>

          </div>
        )}

        <div className="bg-gradient-to-r from-primary/10 to-primary/5 border border-primary/20 p-5 rounded-2xl flex items-start gap-4 mt-8 shadow-sm">
          <Info className="text-primary shrink-0 mt-0.5" size={24} />
          <div>
            <h4 className="font-bold text-primary-dark">Experiencia Completa en la App Móvil</h4>
            <p className="text-sm text-text-secondary mt-1.5 leading-relaxed">
              Para pagar tus mensualidades online con tarjeta, justificar inasistencias en tiempo real y comprar en la tienda de la academia, por favor descarga y utiliza la aplicación móvil oficial.
            </p>
          </div>
        </div>
      </div>
    </AppLayout>
  );
}
