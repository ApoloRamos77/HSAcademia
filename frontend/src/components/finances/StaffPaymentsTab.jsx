import React, { useState, useEffect } from 'react';
import api from '../../api/axios';
import { Users, Plus, CheckCircle, Clock, X, DollarSign } from 'lucide-react';
import toast from 'react-hot-toast';

const MONTHS = [
  'Enero','Febrero','Marzo','Abril','Mayo','Junio',
  'Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'
];

export default function StaffPaymentsTab() {
  const now = new Date();
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [year, setYear] = useState(now.getFullYear());
  const [payments, setPayments] = useState([]);
  const [staffList, setStaffList] = useState([]);
  const [loading, setLoading] = useState(true);
  const [modal, setModal] = useState(false);
  const [form, setForm] = useState({
    staffId: '', periodMonth: now.getMonth() + 1, periodYear: now.getFullYear(),
    baseAmount: '', bonuses: '0', deductions: '0', notes: ''
  });

  useEffect(() => { fetchAll(); }, [month, year]);

  const fetchAll = async () => {
    try {
      setLoading(true);
      const [pRes, sRes] = await Promise.all([
        api.get(`/finances-premium/staff-payments?month=${month}&year=${year}`),
        api.get('/academy-config/users')
      ]);
      setPayments(pRes.data);
      // Only Staff and AcademyAdmin
      setStaffList(sRes.data.filter(u => u.systemRole === 'Staff' || u.systemRole === 'AcademyAdmin'));
    } catch { toast.error('Error al cargar nómina'); }
    finally { setLoading(false); }
  };

  const handleCreate = async (e) => {
    e.preventDefault();
    try {
      await api.post('/finances-premium/staff-payments', {
        staffId: form.staffId,
        periodMonth: parseInt(form.periodMonth),
        periodYear: parseInt(form.periodYear),
        baseAmount: parseFloat(form.baseAmount),
        bonuses: parseFloat(form.bonuses) || 0,
        deductions: parseFloat(form.deductions) || 0,
        notes: form.notes || null
      });
      toast.success('Pago de nómina registrado');
      setModal(false);
      fetchAll();
    } catch (err) { toast.error(err.response?.data?.message || 'Error al registrar pago'); }
  };

  const handleMarkPaid = async (id) => {
    try {
      await api.patch(`/finances-premium/staff-payments/${id}/mark-paid`);
      toast.success('Marcado como pagado');
      fetchAll();
    } catch (err) { toast.error(err.response?.data?.message || 'Error'); }
  };

  const totalPending = payments.filter(p => p.status === 0).reduce((s, p) => s + p.totalPaid, 0);
  const totalPaid = payments.filter(p => p.status === 1).reduce((s, p) => s + p.totalPaid, 0);
  const net = (parseFloat(form.baseAmount) || 0) + (parseFloat(form.bonuses) || 0) - (parseFloat(form.deductions) || 0);

  return (
    <div className="fade-in space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-card/40 backdrop-blur-md border border-border/50 p-4 rounded-xl">
        <h3 className="text-xl font-bold text-white flex items-center gap-2">
          <Users className="text-primary" size={20}/> Nómina del Personal
        </h3>
        <div className="flex flex-wrap items-center gap-3">
          <select className="form-control text-sm py-2 w-auto" value={month} onChange={e => setMonth(+e.target.value)}>
            {MONTHS.map((m, i) => <option key={i+1} value={i+1}>{m}</option>)}
          </select>
          <select className="form-control text-sm py-2 w-auto" value={year} onChange={e => setYear(+e.target.value)}>
            {[2024,2025,2026,2027].map(y => <option key={y} value={y}>{y}</option>)}
          </select>
          <button onClick={() => setModal(true)} className="btn btn-primary flex items-center gap-2">
            <Plus size={16}/> Registrar Pago
          </button>
        </div>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div className="card p-5 flex items-center gap-4 bg-warning/10 border-warning/30">
          <div className="p-3 bg-warning/20 rounded-full text-warning"><Clock size={22}/></div>
          <div>
            <p className="text-warning font-bold text-lg">S/ {totalPending.toFixed(2)}</p>
            <p className="text-sm text-text-secondary">{payments.filter(p => p.status === 0).length} pendientes de pago</p>
          </div>
        </div>
        <div className="card p-5 flex items-center gap-4 bg-success/10 border-success/30">
          <div className="p-3 bg-success/20 rounded-full text-success"><CheckCircle size={22}/></div>
          <div>
            <p className="text-success font-bold text-lg">S/ {totalPaid.toFixed(2)}</p>
            <p className="text-sm text-text-secondary">{payments.filter(p => p.status === 1).length} pagados este período</p>
          </div>
        </div>
      </div>

      {/* Table */}
      <div className="card overflow-hidden">
        <div className="table-container">
          <table className="table">
            <thead>
              <tr>
                <th>Personal</th>
                <th>Base</th>
                <th>Bonos</th>
                <th>Deducciones</th>
                <th>Total Neto</th>
                <th>Estado</th>
                <th className="text-center">Acción</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr><td colSpan="7" className="text-center p-8 text-text-muted">Cargando...</td></tr>
              ) : payments.length === 0 ? (
                <tr><td colSpan="7" className="text-center p-8 text-text-muted">No hay registros de nómina para este período.</td></tr>
              ) : (
                payments.map(p => (
                  <tr key={p.id} className="hover:bg-white/5 transition-colors">
                    <td>
                      <p className="font-bold text-white">{p.staffName}</p>
                      {p.notes && <p className="text-xs text-text-muted mt-0.5 italic">{p.notes}</p>}
                    </td>
                    <td className="text-text-main">S/ {p.baseAmount.toFixed(2)}</td>
                    <td className="text-success">+ S/ {p.bonuses.toFixed(2)}</td>
                    <td className="text-danger">- S/ {p.deductions.toFixed(2)}</td>
                    <td className="font-bold text-white">S/ {p.totalPaid.toFixed(2)}</td>
                    <td>
                      {p.status === 1
                        ? <span className="badge badge-success flex items-center gap-1 w-max"><CheckCircle size={11}/> Pagado</span>
                        : <span className="badge badge-warning flex items-center gap-1 w-max"><Clock size={11}/> Pendiente</span>
                      }
                      {p.paidAt && <p className="text-xs text-text-muted mt-1">{new Date(p.paidAt).toLocaleDateString('es-PE')}</p>}
                    </td>
                    <td className="text-center">
                      {p.status === 0 && (
                        <button
                          onClick={() => handleMarkPaid(p.id)}
                          className="btn btn-sm btn-success flex items-center gap-1 mx-auto"
                        >
                          <CheckCircle size={13}/> Pagar
                        </button>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal Crear */}
      {modal && (
        <div className="modal-overlay" onClick={() => setModal(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 500 }}>
            <div className="flex justify-between items-center mb-5 border-b border-border pb-3">
              <h3 className="modal-title m-0 flex items-center gap-2"><DollarSign size={18} className="text-primary"/> Registrar Pago de Nómina</h3>
              <button onClick={() => setModal(false)} className="btn btn-ghost btn-sm"><X size={16}/></button>
            </div>
            <form onSubmit={handleCreate}>
              <div className="form-group">
                <label className="form-label">Personal *</label>
                <select required className="form-control" value={form.staffId} onChange={e => setForm({...form, staffId: e.target.value})}>
                  <option value="">-- Seleccione un miembro --</option>
                  {staffList.map(s => (
                    <option key={s.id} value={s.id}>{s.firstName} {s.lastName} ({s.systemRole === 'AcademyAdmin' ? 'Admin' : 'Staff'})</option>
                  ))}
                </select>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Mes</label>
                  <select className="form-control" value={form.periodMonth} onChange={e => setForm({...form, periodMonth: e.target.value})}>
                    {MONTHS.map((m, i) => <option key={i+1} value={i+1}>{m}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label className="form-label">Año</label>
                  <select className="form-control" value={form.periodYear} onChange={e => setForm({...form, periodYear: e.target.value})}>
                    {[2024,2025,2026,2027].map(y => <option key={y} value={y}>{y}</option>)}
                  </select>
                </div>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Sueldo Base (S/) *</label>
                  <input required type="number" step="0.01" min="0" className="form-control" placeholder="0.00"
                    value={form.baseAmount} onChange={e => setForm({...form, baseAmount: e.target.value})}/>
                </div>
                <div className="form-group">
                  <label className="form-label">Bonificaciones (S/)</label>
                  <input type="number" step="0.01" min="0" className="form-control" placeholder="0.00"
                    value={form.bonuses} onChange={e => setForm({...form, bonuses: e.target.value})}/>
                </div>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Deducciones (S/)</label>
                  <input type="number" step="0.01" min="0" className="form-control" placeholder="0.00"
                    value={form.deductions} onChange={e => setForm({...form, deductions: e.target.value})}/>
                </div>
                <div className="form-group">
                  <label className="form-label">Total Neto</label>
                  <div className={`form-control flex items-center font-bold ${net >= 0 ? 'text-success' : 'text-danger'}`}>
                    S/ {net.toFixed(2)}
                  </div>
                </div>
              </div>

              <div className="form-group">
                <label className="form-label">Nota (Opcional)</label>
                <input type="text" className="form-control" placeholder="Ej. Incluye horas extra"
                  value={form.notes} onChange={e => setForm({...form, notes: e.target.value})}/>
              </div>

              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={() => setModal(false)} className="btn btn-ghost text-danger">Cancelar</button>
                <button type="submit" className="btn btn-primary flex items-center gap-2"><DollarSign size={15}/> Guardar</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
