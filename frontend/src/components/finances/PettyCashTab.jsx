import React, { useState, useEffect } from 'react';
import api from '../../api/axios';
import {
  Archive, Plus, TrendingDown, TrendingUp, ChevronDown, ChevronUp, X, DollarSign
} from 'lucide-react';
import toast from 'react-hot-toast';

const MONTHS = [
  'Enero','Febrero','Marzo','Abril','Mayo','Junio',
  'Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'
];

export default function PettyCashTab() {
  const now = new Date();
  const [month, setMonth] = useState(now.getMonth() + 1);
  const [year, setYear] = useState(now.getFullYear());
  const [boxes, setBoxes] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expandedId, setExpandedId] = useState(null);

  // Modals
  const [modal, setModal] = useState(null); // 'newBox' | 'transaction'
  const [selectedBox, setSelectedBox] = useState(null);
  const [newBoxForm, setNewBoxForm] = useState({ headquarterId: '', month, year, assignedAmount: '' });
  const [txForm, setTxForm] = useState({ type: 2, amount: '', concept: '', date: new Date().toISOString().split('T')[0] });

  useEffect(() => { fetchData(); }, [month, year]);

  const fetchData = async () => {
    try {
      setLoading(true);
      const { data } = await api.get(`/finances-premium/petty-cash?month=${month}&year=${year}`);
      setBoxes(data);
    } catch { toast.error('Error al cargar caja chica'); }
    finally { setLoading(false); }
  };

  const handleCreateBox = async (e) => {
    e.preventDefault();
    try {
      await api.post('/finances-premium/petty-cash', {
        ...newBoxForm,
        month: parseInt(newBoxForm.month),
        year: parseInt(newBoxForm.year),
        assignedAmount: parseFloat(newBoxForm.assignedAmount),
        headquarterId: newBoxForm.headquarterId || null
      });
      toast.success('Caja Chica creada');
      setModal(null);
      fetchData();
    } catch (err) { toast.error(err.response?.data?.message || 'Error al crear caja chica'); }
  };

  const handleAddTransaction = async (e) => {
    e.preventDefault();
    try {
      await api.post('/finances-premium/petty-cash/transaction', {
        pettyCashId: selectedBox.id,
        type: parseInt(txForm.type),
        amount: parseFloat(txForm.amount),
        concept: txForm.concept,
        date: txForm.date
      });
      toast.success('Movimiento registrado');
      setModal(null);
      fetchData();
    } catch (err) { toast.error(err.response?.data?.message || 'Error al registrar movimiento'); }
  };

  const openTx = (box) => {
    setSelectedBox(box);
    setTxForm({ type: 2, amount: '', concept: '', date: new Date().toISOString().split('T')[0] });
    setModal('transaction');
  };

  const usedPct = (box) => {
    if (box.assignedAmount === 0) return 0;
    return Math.min(100, Math.round(((box.assignedAmount - box.currentBalance) / box.assignedAmount) * 100));
  };

  return (
    <div className="fade-in space-y-6">
      {/* Period selector + New button */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-card/40 backdrop-blur-md border border-border/50 p-4 rounded-xl">
        <h3 className="text-xl font-bold text-white flex items-center gap-2">
          <Archive className="text-primary" size={20}/> Caja Chica
        </h3>
        <div className="flex flex-wrap items-center gap-3">
          <select className="form-control text-sm py-2 w-auto" value={month} onChange={e => setMonth(+e.target.value)}>
            {MONTHS.map((m, i) => <option key={i+1} value={i+1}>{m}</option>)}
          </select>
          <select className="form-control text-sm py-2 w-auto" value={year} onChange={e => setYear(+e.target.value)}>
            {[2024,2025,2026,2027].map(y => <option key={y} value={y}>{y}</option>)}
          </select>
          <button onClick={() => { setNewBoxForm({ headquarterId:'', month, year, assignedAmount:'' }); setModal('newBox'); }}
            className="btn btn-primary flex items-center gap-2">
            <Plus size={16}/> Nueva Caja
          </button>
        </div>
      </div>

      {loading ? (
        <div className="text-center py-12 text-text-muted">Cargando...</div>
      ) : boxes.length === 0 ? (
        <div className="card p-12 text-center">
          <Archive size={48} className="mx-auto text-text-muted mb-4 opacity-30"/>
          <p className="text-text-muted">No hay cajas chicas para este período.</p>
          <p className="text-xs text-text-muted mt-1">Crea una nueva para comenzar a registrar movimientos.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-5">
          {boxes.map(box => {
            const pct = usedPct(box);
            const isExpanded = expandedId === box.id;
            return (
              <div key={box.id} className="card overflow-hidden">
                {/* Header */}
                <div className="p-5 border-b border-border/50">
                  <div className="flex justify-between items-start gap-4">
                    <div>
                      <h4 className="font-bold text-white text-lg">{box.headquarterName || 'General'}</h4>
                      <p className="text-xs text-text-muted mt-0.5">{MONTHS[box.month-1]} {box.year}</p>
                    </div>
                    <button onClick={() => openTx(box)} className="btn btn-primary btn-sm flex items-center gap-1 shrink-0">
                      <Plus size={14}/> Movimiento
                    </button>
                  </div>

                  {/* Balance */}
                  <div className="grid grid-cols-2 gap-3 mt-4">
                    <div className="bg-bg-dark rounded-lg p-3 border border-border/50">
                      <p className="text-xs text-text-muted">Asignado</p>
                      <p className="text-lg font-bold text-white">S/ {box.assignedAmount.toFixed(2)}</p>
                    </div>
                    <div className={`rounded-lg p-3 border ${box.currentBalance < box.assignedAmount * 0.2 ? 'bg-danger/10 border-danger/30' : 'bg-success/10 border-success/30'}`}>
                      <p className="text-xs text-text-muted">Saldo</p>
                      <p className={`text-lg font-bold ${box.currentBalance < box.assignedAmount * 0.2 ? 'text-danger' : 'text-success'}`}>
                        S/ {box.currentBalance.toFixed(2)}
                      </p>
                    </div>
                  </div>

                  {/* Progress bar */}
                  <div className="mt-3">
                    <div className="flex justify-between text-xs text-text-muted mb-1">
                      <span>Utilizado</span>
                      <span>{pct}%</span>
                    </div>
                    <div className="w-full bg-bg-dark rounded-full h-2 overflow-hidden">
                      <div
                        className={`h-2 rounded-full transition-all ${pct > 80 ? 'bg-danger' : pct > 50 ? 'bg-warning' : 'bg-success'}`}
                        style={{ width: `${pct}%` }}
                      />
                    </div>
                  </div>
                </div>

                {/* Transactions toggle */}
                <button
                  onClick={() => setExpandedId(isExpanded ? null : box.id)}
                  className="w-full flex items-center justify-between px-5 py-3 text-sm text-text-muted hover:text-text-main hover:bg-white/5 transition-colors"
                >
                  <span>Ver movimientos ({box.transactions?.length || 0})</span>
                  {isExpanded ? <ChevronUp size={16}/> : <ChevronDown size={16}/>}
                </button>

                {isExpanded && (
                  <div className="px-5 pb-5 space-y-2">
                    {box.transactions?.length === 0 ? (
                      <p className="text-xs text-text-muted text-center py-4">Sin movimientos aún.</p>
                    ) : (
                      box.transactions.map(t => (
                        <div key={t.id} className="flex items-center justify-between bg-bg-dark rounded-lg px-3 py-2.5 border border-border/50 text-sm">
                          <div className="flex items-center gap-3">
                            {t.type === 2
                              ? <TrendingDown size={16} className="text-danger shrink-0"/>
                              : <TrendingUp size={16} className="text-success shrink-0"/>
                            }
                            <div>
                              <p className="font-medium text-white">{t.concept}</p>
                              <p className="text-xs text-text-muted">{new Date(t.date).toLocaleDateString('es-PE')}</p>
                            </div>
                          </div>
                          <span className={`font-bold ${t.type === 2 ? 'text-danger' : 'text-success'}`}>
                            {t.type === 2 ? '-' : '+'}S/ {t.amount.toFixed(2)}
                          </span>
                        </div>
                      ))
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}

      {/* Modal: Nueva Caja */}
      {modal === 'newBox' && (
        <div className="modal-overlay" onClick={() => setModal(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 440 }}>
            <div className="flex justify-between items-center mb-5 border-b border-border pb-3">
              <h3 className="modal-title m-0 flex items-center gap-2"><Archive size={18} className="text-primary"/> Nueva Caja Chica</h3>
              <button onClick={() => setModal(null)} className="btn btn-ghost btn-sm"><X size={16}/></button>
            </div>
            <form onSubmit={handleCreateBox}>
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Mes</label>
                  <select className="form-control" value={newBoxForm.month} onChange={e => setNewBoxForm({...newBoxForm, month: e.target.value})}>
                    {MONTHS.map((m, i) => <option key={i+1} value={i+1}>{m}</option>)}
                  </select>
                </div>
                <div className="form-group">
                  <label className="form-label">Año</label>
                  <select className="form-control" value={newBoxForm.year} onChange={e => setNewBoxForm({...newBoxForm, year: e.target.value})}>
                    {[2024,2025,2026,2027].map(y => <option key={y} value={y}>{y}</option>)}
                  </select>
                </div>
              </div>
              <div className="form-group">
                <label className="form-label">Monto Asignado (S/)</label>
                <input required type="number" step="0.01" min="1" className="form-control"
                  placeholder="Ej. 500.00"
                  value={newBoxForm.assignedAmount} onChange={e => setNewBoxForm({...newBoxForm, assignedAmount: e.target.value})}/>
              </div>
              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={() => setModal(null)} className="btn btn-ghost text-danger">Cancelar</button>
                <button type="submit" className="btn btn-primary flex items-center gap-2"><Archive size={15}/> Crear Caja</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Nuevo Movimiento */}
      {modal === 'transaction' && selectedBox && (
        <div className="modal-overlay" onClick={() => setModal(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 440 }}>
            <div className="flex justify-between items-center mb-5 border-b border-border pb-3">
              <h3 className="modal-title m-0 flex items-center gap-2"><DollarSign size={18} className="text-primary"/> Registrar Movimiento</h3>
              <button onClick={() => setModal(null)} className="btn btn-ghost btn-sm"><X size={16}/></button>
            </div>
            <p className="text-sm text-text-secondary mb-4">
              Caja: <strong className="text-white">{selectedBox.headquarterName || 'General'}</strong> — Saldo: <strong className="text-success">S/ {selectedBox.currentBalance.toFixed(2)}</strong>
            </p>
            <form onSubmit={handleAddTransaction}>
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Tipo</label>
                  <select className="form-control" value={txForm.type} onChange={e => setTxForm({...txForm, type: e.target.value})}>
                    <option value={2}>🔻 Egreso (Gasto)</option>
                    <option value={1}>🔼 Ingreso (Reposición)</option>
                  </select>
                </div>
                <div className="form-group">
                  <label className="form-label">Monto (S/)</label>
                  <input required type="number" step="0.01" min="0.01" className="form-control"
                    placeholder="0.00"
                    value={txForm.amount} onChange={e => setTxForm({...txForm, amount: e.target.value})}/>
                </div>
              </div>
              <div className="form-group">
                <label className="form-label">Concepto / Descripción</label>
                <input required type="text" className="form-control" placeholder="Ej. Compra de útiles"
                  value={txForm.concept} onChange={e => setTxForm({...txForm, concept: e.target.value})}/>
              </div>
              <div className="form-group">
                <label className="form-label">Fecha</label>
                <input required type="date" className="form-control"
                  value={txForm.date} onChange={e => setTxForm({...txForm, date: e.target.value})}/>
              </div>
              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={() => setModal(null)} className="btn btn-ghost text-danger">Cancelar</button>
                <button type="submit" className="btn btn-primary flex items-center gap-2"><Plus size={15}/> Registrar</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
