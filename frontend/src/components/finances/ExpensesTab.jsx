import React, { useState, useEffect } from 'react';
import axios from '../../api/axios';
import { TrendingDown, Plus, FileText, Filter, Calendar, Tag, DollarSign, Trash2, X } from 'lucide-react';
import toast from 'react-hot-toast';

const EXPENSE_TYPES = {
  1: { label: 'Operativo', color: 'bg-blue-500/20 text-blue-400' },
  2: { label: 'Marketing', color: 'bg-purple-500/20 text-purple-400' },
  3: { label: 'Equipamiento', color: 'bg-orange-500/20 text-orange-400' },
  4: { label: 'Alquiler', color: 'bg-indigo-500/20 text-indigo-400' },
  5: { label: 'Otros', color: 'bg-gray-500/20 text-gray-400' },
  6: { label: 'Compra - Materiales', color: 'bg-emerald-500/20 text-emerald-400' },
  7: { label: 'Compra - Prod. para Venta', color: 'bg-teal-500/20 text-teal-400' },
  8: { label: 'Pagos/Servicios', color: 'bg-yellow-500/20 text-yellow-400' }
};

export default function ExpensesTab() {
  const [expenses, setExpenses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [formData, setFormData] = useState({
    type: 1,
    amount: '',
    date: new Date().toISOString().split('T')[0],
    description: '',
    supplier: '',
    voucherUrl: '',
    products: []
  });

  const [productForm, setProductForm] = useState({
    name: '', productCategory: '', quantity: '', unitCost: '', salePrice: '', description: ''
  });
  const [expandedId, setExpandedId] = useState(null);

  const currentMonth = new Date().getMonth() + 1;
  const currentYear = new Date().getFullYear();

  useEffect(() => {
    fetchExpenses();
  }, []);

  const fetchExpenses = async () => {
    try {
      setLoading(true);
      const { data } = await axios.get(`/finances-premium/expenses?month=${currentMonth}&year=${currentYear}`);
      setExpenses(data);
    } catch (err) {
      toast.error('Error al cargar los egresos');
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async (e) => {
    e.preventDefault();
    try {
      await axios.post('/finances-premium/expenses', {
        type: parseInt(formData.type),
        amount: parseFloat(formData.amount),
        date: formData.date,
        description: formData.description,
        supplier: formData.supplier || null,
        voucherUrl: formData.voucherUrl || null,
        products: formData.type == 7 ? formData.products : []
      });
      toast.success('Gasto registrado exitosamente');
      setIsModalOpen(false);
      setFormData({ type: 1, amount: '', date: new Date().toISOString().split('T')[0], description: '', supplier: '', voucherUrl: '', products: [] });
      setProductForm({ name: '', productCategory: '', quantity: '', unitCost: '', salePrice: '', description: '' });
      fetchExpenses();
    } catch (err) {
      toast.error('Error al registrar gasto');
    }
  };

  const handleDelete = async (id) => {
    if (!window.confirm('¿Eliminar este gasto?')) return;
    try {
      await axios.delete(`/finances-premium/expenses/${id}`);
      toast.success('Gasto eliminado');
      fetchExpenses();
    } catch (err) {
      toast.error('Error al eliminar gasto');
    }
  };

  const totalExpenses = expenses.reduce((sum, e) => sum + e.amount, 0);

  const handleAddProduct = () => {
    if (!productForm.name || !productForm.quantity || !productForm.unitCost || !productForm.salePrice) {
      toast.error('Complete los datos obligatorios del producto');
      return;
    }
    setFormData(prev => ({
      ...prev,
      products: [...prev.products, {
        name: productForm.name,
        productCategory: productForm.productCategory,
        description: productForm.description,
        quantity: parseInt(productForm.quantity),
        unitCost: parseFloat(productForm.unitCost),
        salePrice: parseFloat(productForm.salePrice),
        forSale: true
      }]
    }));
    setProductForm({ name: '', productCategory: '', quantity: '', unitCost: '', salePrice: '', description: '' });
  };

  const handleRemoveProduct = (index) => {
    setFormData(prev => ({
      ...prev,
      products: prev.products.filter((_, i) => i !== index)
    }));
  };

  return (
    <div className="animate-fade-in space-y-6">
      {/* Resumen Premium */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="bg-gradient-to-br from-red-500/10 to-rose-500/5 backdrop-blur-xl border border-red-500/20 p-6 rounded-2xl relative overflow-hidden group">
          <div className="absolute top-0 right-0 p-4 opacity-10 group-hover:opacity-20 transition-opacity">
            <TrendingDown size={64} />
          </div>
          <p className="text-red-400 font-medium mb-1">Egresos Totales (Mes)</p>
          <h2 className="text-4xl font-bold text-white tracking-tight">
            S/. {totalExpenses.toLocaleString('en-US', { minimumFractionDigits: 2 })}
          </h2>
          <p className="text-xs text-text-muted mt-2 flex items-center gap-1">
            <Calendar size={12} /> {new Date().toLocaleString('es-ES', { month: 'long', year: 'numeric' })}
          </p>
        </div>
        
        {/* Más tarjetas de resumen aquí si es necesario */}
      </div>

      {/* Header y Filtros */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 bg-card/40 backdrop-blur-md border border-border/50 p-4 rounded-xl">
        <h3 className="text-xl font-bold text-white flex items-center gap-2">
          <FileText className="text-primary" /> Historial de Egresos
        </h3>
        <button
          onClick={() => setIsModalOpen(true)}
          className="flex items-center gap-2 bg-primary hover:bg-primary/90 text-white px-5 py-2.5 rounded-xl font-medium transition-all transform hover:scale-105 shadow-lg shadow-primary/20"
        >
          <Plus size={18} /> Registrar Gasto
        </button>
      </div>

      {/* Tabla de Egresos */}
      <div className="bg-card/40 backdrop-blur-md border border-border/50 rounded-2xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-background/50 text-text-muted text-sm uppercase tracking-wider">
                <th className="p-4 font-medium">Fecha</th>
                <th className="p-4 font-medium">Descripción</th>
                <th className="p-4 font-medium">Categoría</th>
                <th className="p-4 font-medium text-right">Monto</th>
                <th className="p-4 font-medium text-center">Acciones</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/50">
              {loading ? (
                <tr><td colSpan="5" className="p-8 text-center text-text-muted">Cargando egresos...</td></tr>
              ) : expenses.length === 0 ? (
                <tr><td colSpan="5" className="p-8 text-center text-text-muted">No hay gastos registrados este mes.</td></tr>
              ) : (
                expenses.map((e) => (
                  <tr key={e.id} className="hover:bg-white/5 transition-colors group">
                    <td className="p-4 text-sm text-text-main whitespace-nowrap">
                      {new Date(e.date).toLocaleDateString('es-ES')}
                    </td>
                    <td className="p-4">
                      <p className="text-white font-medium">{e.description}</p>
                      {e.voucherUrl && (
                        <a href={e.voucherUrl} target="_blank" rel="noreferrer" className="text-xs text-primary hover:underline flex items-center gap-1 mt-1">
                          <FileText size={12} /> Ver Comprobante
                        </a>
                      )}
                    </td>
                    <td className="p-4">
                      <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium ${EXPENSE_TYPES[e.type]?.color || 'bg-gray-500/20 text-gray-400'}`}>
                        <Tag size={12} /> {EXPENSE_TYPES[e.type]?.label || 'Desconocido'}
                      </span>
                    </td>
                    <td className="p-4 text-right">
                      <span className="text-white font-bold bg-white/5 px-3 py-1.5 rounded-lg border border-white/10">
                        S/. {e.amount.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                      </span>
                    </td>
                    <td className="p-4 text-center">
                      <div className="flex items-center justify-center gap-2">
                        {e.products?.length > 0 && (
                          <button onClick={() => setExpandedId(expandedId === e.id ? null : e.id)} className="p-1.5 text-primary hover:bg-primary/10 rounded" title="Ver productos">
                            <Tag size={16} />
                          </button>
                        )}
                        <button 
                          onClick={() => handleDelete(e.id)}
                          className="p-1.5 text-text-muted hover:text-red-400 hover:bg-red-500/10 rounded transition-colors opacity-0 group-hover:opacity-100"
                          title="Eliminar gasto"
                        >
                          <Trash2 size={16} />
                        </button>
                      </div>
                    </td>
                  </tr>
                  {expandedId === e.id && e.products?.length > 0 && (
                    <tr className="bg-bg-dark/30">
                      <td colSpan="5" className="p-4 border-l-2 border-primary/50">
                        <p className="text-xs font-bold text-primary mb-2 uppercase tracking-wide">Productos Registrados en Inventario</p>
                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                          {e.products.map(p => (
                            <div key={p.productId} className="flex items-center justify-between bg-card/50 p-2 rounded border border-border/50 text-sm">
                              <div>
                                <p className="font-bold text-white">{p.name}</p>
                                <p className="text-xs text-text-muted">{p.quantity} und. x S/ {p.unitCost.toFixed(2)}</p>
                              </div>
                              <div className="text-right">
                                <p className="text-success font-medium">Venta: S/ {p.salePrice.toFixed(2)}</p>
                                {!p.forSale && <p className="text-xs text-danger">⚠️ Sin precio de venta</p>}
                              </div>
                            </div>
                          ))}
                        </div>
                      </td>
                    </tr>
                  )}
                </React.Fragment>
              ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal Crear */}
      {isModalOpen && (
        <div className="modal-overlay" onClick={() => setIsModalOpen(false)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 480 }}>
            <div className="flex justify-between items-center mb-5 border-b border-border pb-3">
              <h3 className="modal-title m-0 flex items-center gap-2">
                <DollarSign size={18} className="text-primary" /> Nuevo Egreso
              </h3>
              <button onClick={() => setIsModalOpen(false)} className="btn btn-ghost btn-sm">
                <X size={16} />
              </button>
            </div>
            
            <form onSubmit={handleCreate}>
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Categoría *</label>
                  <select 
                    value={formData.type}
                    onChange={e => setFormData({...formData, type: e.target.value})}
                    className="form-control font-bold text-primary-400"
                  >
                    {Object.entries(EXPENSE_TYPES).map(([val, {label}]) => (
                      <option key={val} value={val}>{label}</option>
                    ))}
                  </select>
                </div>
                <div className="form-group">
                  <label className="form-label">Fecha *</label>
                  <input 
                    type="date" required
                    value={formData.date}
                    onChange={e => setFormData({...formData, date: e.target.value})}
                    className="form-control"
                  />
                </div>
              </div>

              <div className="form-group">
                <label className="form-label">Descripción de la compra / gasto *</label>
                <input 
                  type="text" required
                  value={formData.description}
                  onChange={e => setFormData({...formData, description: e.target.value})}
                  className="form-control"
                  placeholder="Ej. Compra de polos para academia"
                />
              </div>

              {formData.type == 7 && (
                <div className="bg-bg-dark border border-primary/30 p-4 rounded-xl mb-4">
                  <h4 className="text-primary font-bold text-sm mb-3 flex items-center gap-2"><Tag size={16}/> Registro de Productos (Inventario)</h4>
                  
                  {formData.products.length > 0 && (
                    <div className="mb-3 space-y-2">
                      {formData.products.map((p, i) => (
                        <div key={i} className="flex justify-between items-center bg-card/60 p-2 rounded border border-border/50 text-sm">
                          <div>
                            <span className="font-bold text-white">{p.name}</span>
                            <span className="text-xs text-text-muted ml-2">({p.quantity} x S/{p.unitCost.toFixed(2)})</span>
                          </div>
                          <div className="flex items-center gap-3">
                            <span className="text-success font-medium">P.V: S/{p.salePrice.toFixed(2)}</span>
                            <button type="button" onClick={() => handleRemoveProduct(i)} className="text-danger"><X size={14}/></button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}

                  <div className="grid grid-cols-12 gap-2 mb-2">
                    <div className="col-span-12 sm:col-span-6">
                      <input type="text" placeholder="Nombre producto *" className="form-control text-sm py-1.5"
                        value={productForm.name} onChange={e => setProductForm({...productForm, name: e.target.value})}/>
                    </div>
                    <div className="col-span-12 sm:col-span-6">
                      <input type="text" placeholder="Categoría (Ej. Uniforme)" className="form-control text-sm py-1.5"
                        value={productForm.productCategory} onChange={e => setProductForm({...productForm, productCategory: e.target.value})}/>
                    </div>
                    <div className="col-span-4">
                      <input type="number" placeholder="Cant *" className="form-control text-sm py-1.5"
                        value={productForm.quantity} onChange={e => setProductForm({...productForm, quantity: e.target.value})}/>
                    </div>
                    <div className="col-span-4">
                      <input type="number" step="0.01" placeholder="Costo *" className="form-control text-sm py-1.5"
                        value={productForm.unitCost} onChange={e => setProductForm({...productForm, unitCost: e.target.value})}/>
                    </div>
                    <div className="col-span-4">
                      <input type="number" step="0.01" placeholder="Venta *" className="form-control text-sm py-1.5"
                        value={productForm.salePrice} onChange={e => setProductForm({...productForm, salePrice: e.target.value})}/>
                    </div>
                    <div className="col-span-12">
                      <input type="text" placeholder="Descripción adicional (Tallas, modelo...)" className="form-control text-sm py-1.5"
                        value={productForm.description} onChange={e => setProductForm({...productForm, description: e.target.value})}/>
                    </div>
                  </div>
                  <button type="button" onClick={handleAddProduct} className="btn btn-sm btn-ghost border border-primary/50 text-primary w-full py-1">
                    + Añadir a la lista
                  </button>
                  <p className="text-xs text-text-muted mt-2 text-center">Los productos se crearán en la tienda con su costo y precio de venta.</p>
                </div>
              )}
              
              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Monto Total Pagado (S/.) *</label>
                  <input 
                    type="number" step="0.01" required min="0.01"
                    value={formData.amount}
                    onChange={e => setFormData({...formData, amount: e.target.value})}
                    className="form-control"
                    placeholder="0.00"
                  />
                </div>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label className="form-label">Proveedor (Opcional)</label>
                  <input 
                    type="text"
                    value={formData.supplier}
                    onChange={e => setFormData({...formData, supplier: e.target.value})}
                    className="form-control"
                    placeholder="Nombre empresa/proveedor"
                  />
                </div>
                
                <div className="form-group">
                  <label className="form-label">Voucher URL (Opcional)</label>
                  <input 
                    type="url"
                    value={formData.voucherUrl}
                    onChange={e => setFormData({...formData, voucherUrl: e.target.value})}
                    className="form-control"
                    placeholder="https://..."
                  />
                </div>
              </div>
              
              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={() => setIsModalOpen(false)} className="btn btn-ghost text-danger">
                  Cancelar
                </button>
                <button type="submit" className="btn btn-primary flex items-center gap-2">
                  <DollarSign size={15} /> Guardar Gasto
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
