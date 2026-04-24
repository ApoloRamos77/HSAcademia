import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { DollarSign, Settings, Play, AlertTriangle, CheckCircle, Search, Calendar, FileText } from 'lucide-react';
import toast from 'react-hot-toast';

export default function Finanzas() {
  const [activeTab, setActiveTab] = useState('debts'); // 'debts' | 'config'
  
  const [config, setConfig] = useState({ defaultPaymentDay: 5 });
  const [debts, setDebts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [generating, setGenerating] = useState(false);

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      const [cRes, dRes] = await Promise.all([
        api.get('/finances/config'),
        api.get('/finances/debts/pending')
      ]);
      setConfig(cRes.data);
      setDebts(dRes.data);
    } catch (err) {
      toast.error('Error al cargar datos financieros.');
    } finally {
      setLoading(false);
    }
  };

  const handleConfigSubmit = async (e) => {
    e.preventDefault();
    try {
      await api.put('/finances/config', config);
      toast.success('Configuración guardada correctamente.');
    } catch (err) {
      toast.error('Error al guardar configuración.');
    }
  };

  const handleGenerateDebts = async () => {
    setGenerating(true);
    try {
      const res = await api.post('/finances/generate-debts');
      toast.success(res.data.message || 'Proceso completado.');
      fetchData();
    } catch (err) {
      toast.error('Error al ejecutar el motor de deudas.');
    } finally {
      setGenerating(false);
    }
  };

  const handleMarkAsPaid = async (id) => {
    if(!window.confirm('¿Confirmar el pago de este registro?')) return;
    try {
      await api.post(`/finances/debts/${id}/pay`);
      toast.success('Pago registrado con éxito.');
      fetchData();
    } catch (err) {
      toast.error('Error al registrar pago.');
    }
  };

  const overdueDebts = debts.filter(d => d.daysOverdue > 0);
  const upcomingDebts = debts.filter(d => d.daysOverdue <= 0);

  if (loading) return <AppLayout title="Finanzas"><div className="text-center p-8"><span className="spinner" style={{borderColor: 'var(--primary)', borderTopColor: 'transparent'}}></span></div></AppLayout>;

  return (
    <AppLayout title="Gestión Financiera y Cobranzas">
      <div className="fade-in">
        
        {/* Tabs Menu */}
        <div className="flex gap-4 mb-6 border-b border-border/50 pb-2">
          <button 
            className={`flex items-center gap-2 pb-2 px-1 border-b-2 transition-colors ${activeTab === 'debts' ? 'border-primary text-primary font-bold' : 'border-transparent text-text-muted hover:text-text-main'}`}
            onClick={() => setActiveTab('debts')}
          >
            <DollarSign size={18} /> Morosidad y Cobranzas
          </button>
          <button 
            className={`flex items-center gap-2 pb-2 px-1 border-b-2 transition-colors ${activeTab === 'config' ? 'border-primary text-primary font-bold' : 'border-transparent text-text-muted hover:text-text-main'}`}
            onClick={() => setActiveTab('config')}
          >
            <Settings size={18} /> Configuración de Pagos
          </button>
        </div>

        {/* DEBTS TAB */}
        {activeTab === 'debts' && (
          <div className="fade-in">
            {/* Stats */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
              <div className="card p-4 flex items-center gap-4 bg-danger/10 border-danger/30">
                <div className="p-3 bg-danger/20 rounded-full text-danger"><AlertTriangle size={24}/></div>
                <div>
                  <h4 className="text-danger font-bold text-lg">Alumnos en Mora</h4>
                  <p className="text-sm text-text-secondary">{overdueDebts.length} registros vencidos</p>
                </div>
              </div>
              <div className="card p-4 flex items-center gap-4 bg-warning/10 border-warning/30">
                <div className="p-3 bg-warning/20 rounded-full text-warning"><Calendar size={24}/></div>
                <div>
                  <h4 className="text-warning font-bold text-lg">Próximos a Vencer</h4>
                  <p className="text-sm text-text-secondary">{upcomingDebts.length} registros pendientes</p>
                </div>
              </div>
            </div>

            <div className="card">
              <div className="card-header border-b border-border/50 pb-4 mb-4">
                <h3 className="card-title">Cuentas por Cobrar</h3>
                <div className="relative">
                  <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
                  <input type="text" placeholder="Buscar alumno..." className="form-control pl-9 py-1 text-sm w-64" />
                </div>
              </div>

              <div className="table-container">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Estado</th>
                      <th>Alumno</th>
                      <th>Categoría</th>
                      <th>Concepto</th>
                      <th>Vencimiento</th>
                      <th>Monto</th>
                      <th className="text-right">Acción Rápida</th>
                    </tr>
                  </thead>
                  <tbody>
                    {debts.map(d => (
                      <tr key={d.id} className={d.daysOverdue > 0 ? 'bg-danger/5' : ''}>
                        <td>
                          {d.daysOverdue > 0 ? (
                            <span className="badge badge-danger flex items-center gap-1 w-max">
                              <AlertTriangle size={12}/> Mora: {d.daysOverdue} días
                            </span>
                          ) : (
                            <span className="badge badge-warning flex items-center gap-1 w-max">
                              Pendiente
                            </span>
                          )}
                        </td>
                        <td className="font-medium text-white">{d.studentName}</td>
                        <td className="text-sm">{d.categoryName}</td>
                        <td>
                          <div className="flex items-center gap-2">
                            <FileText size={14} className="text-primary-400"/> {d.description}
                          </div>
                        </td>
                        <td className="text-sm">{new Date(d.dueDate).toLocaleDateString()}</td>
                        <td className="font-bold text-success">${d.amount.toFixed(2)}</td>
                        <td className="text-right">
                          <button onClick={() => handleMarkAsPaid(d.id)} className="btn btn-sm btn-success flex items-center gap-1 ml-auto">
                            <CheckCircle size={14}/> Cobrar
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {debts.length === 0 && <div className="empty-state"><CheckCircle size={40} className="text-success"/><p>No hay cuentas por cobrar pendientes.</p></div>}
            </div>
          </div>
        )}

        {/* CONFIG TAB */}
        {activeTab === 'config' && (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 fade-in">
            <div className="card">
              <div className="card-header border-b border-border/50 pb-4 mb-4">
                <h3 className="card-title">Configuración del Ciclo</h3>
              </div>
              <form onSubmit={handleConfigSubmit}>
                <div className="form-group">
                  <label className="form-label">Día de Cobro Mensual Estándar</label>
                  <p className="text-sm text-text-muted mb-2">Las mensualidades vencerán automáticamente este día de cada mes.</p>
                  <select className="form-control" value={config.defaultPaymentDay} onChange={e => setConfig({...config, defaultPaymentDay: parseInt(e.target.value)})}>
                    {[...Array(28).keys()].map(i => (
                      <option key={i+1} value={i+1}>Día {i+1}</option>
                    ))}
                  </select>
                </div>
                <button type="submit" className="btn btn-primary mt-2">Guardar Configuración</button>
              </form>
            </div>

            <div className="card border-primary/30">
              <div className="card-header border-b border-primary/20 pb-4 mb-4">
                <h3 className="card-title text-primary-400">Motor de Generación</h3>
              </div>
              <p className="text-sm text-text-secondary mb-4">
                El motor verifica todos los alumnos activos (excepto Invitados y Becados 100%) y genera la deuda correspondiente a la mensualidad del mes actual según el monto de su categoría o su tarifa preferencial.
              </p>
              
              <div className="bg-bg-dark p-4 rounded border border-border mb-4">
                <ul className="text-sm text-text-muted list-disc pl-4 space-y-1">
                  <li>Respeta exoneraciones (Becados, Invitados).</li>
                  <li>No duplica cargos (verifica si ya se generó en este mes).</li>
                  <li>Aplica montos preferenciales si existen.</li>
                </ul>
              </div>

              <button 
                onClick={handleGenerateDebts} 
                disabled={generating}
                className="btn btn-primary w-full flex justify-center items-center gap-2"
              >
                {generating ? <span className="spinner w-4 h-4"></span> : <Play size={16} />}
                {generating ? 'Generando...' : 'Ejecutar Motor Manualmente'}
              </button>
            </div>
          </div>
        )}

      </div>
    </AppLayout>
  );
}
