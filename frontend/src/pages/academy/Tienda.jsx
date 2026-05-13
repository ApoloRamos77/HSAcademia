import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { Package, ShoppingCart, PlusCircle, DollarSign, Tag, TrendingUp, AlertCircle, ShoppingBag, BarChart3, Download, Trash2, X, AlertTriangle } from 'lucide-react';
import toast from 'react-hot-toast';
import { generateReceiptPDF } from '../../utils/pdfGenerator';

export default function Tienda() {
  const [activeTab, setActiveTab] = useState('dashboard'); // 'dashboard' | 'inventory' | 'sales'
  
  const [products, setProducts] = useState([]);
  const [sales, setSales] = useState([]);
  const [students, setStudents] = useState([]);
  const [pendingDebts, setPendingDebts] = useState([]);
  const [loading, setLoading] = useState(true);

  // Confirm void dialog
  const [confirmVoid, setConfirmVoid] = useState(null); // null | sale object

  const [showProductModal, setShowProductModal] = useState(false);
  const [editingProductId, setEditingProductId] = useState(null);
  
  const initialProduct = { name: '', description: '', productCategory: '', costPrice: '', price: '', stock: '' };
  const [productForm, setProductForm] = useState(initialProduct);

  const [showSaleModal, setShowSaleModal] = useState(false);
  const todayISO = () => new Date().toISOString().split('T')[0];
  const initialSale = { productId: '', studentId: '', quantity: '1', isGift: false, customDiscount: '', paymentMethod: 'Cash', operationNumber: '', saleDate: todayISO(), paymentRecordId: '', monthlyAmountPaid: '' };
  const [saleForm, setSaleForm] = useState(initialSale);

  const today = new Date();
  const [filterYear,  setFilterYear]  = useState(today.getFullYear());
  const [filterMonth, setFilterMonth] = useState(today.getMonth() + 1);

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      const [pRes, sRes, stuRes, debtsRes] = await Promise.all([
        api.get('/store/products'),
        api.get('/store/sales'),
        api.get('/students'),
        api.get('/finances/debts/pending')
      ]);
      setProducts(pRes.data);
      setSales(sRes.data);
      setStudents(stuRes.data);
      setPendingDebts(debtsRes.data || []);
    } catch (err) {
      toast.error('Error al cargar la tienda.');
    } finally {
      setLoading(false);
    }
  };

  const handleProductSubmit = async (e) => {
    e.preventDefault();
    try {
      const payload = { ...productForm, costPrice: parseFloat(productForm.costPrice || 0), price: parseFloat(productForm.price), stock: parseInt(productForm.stock, 10) };
      
      if (editingProductId) {
        await api.put(`/store/products/${editingProductId}`, payload);
        toast.success('Producto actualizado');
      } else {
        await api.post('/store/products', payload);
        toast.success('Producto creado');
      }
      setShowProductModal(false);
      fetchData();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al guardar producto.');
    }
  };

  const handleSaleSubmit = async (e) => {
    e.preventDefault();
    try {
      const payload = { 
        productId: saleForm.productId, 
        studentId: saleForm.studentId || null, 
        quantity: parseInt(saleForm.quantity, 10),
        isGift: saleForm.isGift,
        paymentMethod: saleForm.isGift ? null : saleForm.paymentMethod,
        operationNumber: saleForm.operationNumber || null,
        saleDate: saleForm.saleDate ? new Date(saleForm.saleDate + 'T12:00:00').toISOString() : null,
        paymentRecordId: saleForm.paymentRecordId || null,
        monthlyAmountPaid: saleForm.monthlyAmountPaid ? parseFloat(saleForm.monthlyAmountPaid) : null,
        customDiscount: saleForm.customDiscount ? parseFloat(saleForm.customDiscount) : null
      };
      const res = await api.post('/store/sales', payload);
      toast.success(saleForm.isGift ? '🎁 Obsequio registrado' : 'Venta registrada con éxito');
      setShowSaleModal(false);
      
      const product = products.find(p => p.id === saleForm.productId);
      const student = students.find(s => s.id === saleForm.studentId);
      const total = product && !saleForm.isGift ? (product.price * parseInt(saleForm.quantity, 10)).toFixed(2) : '0.00';
      
      const items = [];
      const discount = saleForm.customDiscount ? parseFloat(saleForm.customDiscount) : 0;
      if (!saleForm.isGift && product) {
        items.push({
          quantity: parseInt(saleForm.quantity, 10),
          description: `Venta de: ${product.name}${discount > 0 ? ' (Incluye Descuento)' : ''}`,
          total: parseFloat(total) - discount
        });
      } else if (saleForm.isGift && product) {
        items.push({
          quantity: parseInt(saleForm.quantity, 10),
          description: `Venta de: ${product.name} (Obsequio)`,
          total: 0
        });
      }
      
      if (res.data?.combinedMonthlyDescription) {
        items.push({
          quantity: 1,
          description: res.data.combinedMonthlyDescription,
          total: parseFloat(res.data.combinedMonthlyAmount || 0)
        });
      }
      
      const combinedTotal = items.reduce((acc, curr) => acc + curr.total, 0);

      generateReceiptPDF({
        receiptNumber: res.data?.receiptNumber,
        customerName: student ? `${student.firstName} ${student.lastName}` : 'Público General',
        items: items,
        total: combinedTotal,
        notes: saleForm.isGift && items.length === 1 ? 'Obsequio / Entrega de Producto' : 'Venta de Tienda'
      });
      
      fetchData();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al registrar venta. Verifique el stock.');
    }
  };

  const selectedProductForSale = products.find(p => p.id === saleForm.productId);
  const totalSaleEstimation = selectedProductForSale && !saleForm.isGift ? (selectedProductForSale.price * parseInt(saleForm.quantity || 0)).toFixed(2) : '0.00';

  const regenerateSaleReceipt = (s) => {
    const items = [];
    items.push({
      quantity: s.quantity,
      description: `Venta de: ${s.productName}${s.isGift ? ' (Obsequio)' : ''}${s.discountAmount > 0 && !s.isGift ? ' (Incluye Descuento)' : ''}`,
      total: s.isGift ? 0 : parseFloat(s.totalPrice)
    });
    
    if (s.combinedMonthlyDescription) {
      items.push({
        quantity: 1,
        description: s.combinedMonthlyDescription,
        total: parseFloat(s.combinedMonthlyAmount || 0)
      });
    }

    const combinedTotal = items.reduce((acc, curr) => acc + curr.total, 0);

    generateReceiptPDF({
      receiptNumber: s.receiptNumber,
      customerName: s.studentName || 'Público General',
      items: items,
      total: combinedTotal,
      notes: s.isGift && items.length === 1 ? 'Obsequio / Entrega de Producto' : 'Copia de Recibo - Venta de Tienda'
    });
  };

  const handleVoidSale = async (sale) => {
    setConfirmVoid(sale);
  };

  const confirmVoidSale = async () => {
    if (!confirmVoid) return;
    try {
      await api.delete(`/store/sales/${confirmVoid.id}`);
      toast.success('Venta anulada correctamente.');
      setConfirmVoid(null);
      fetchData();
    } catch (err) {
      toast.error('Error al anular venta: ' + (err.response?.data?.message || err.message));
      setConfirmVoid(null);
    }
  };

  const isCurrentMonth = (dateString) => {
    const d = new Date(dateString);
    const now = new Date();
    return d.getMonth() === now.getMonth() && d.getFullYear() === now.getFullYear();
  };

  // Dashboard Metrics
  const totalRevenue = sales.reduce((acc, curr) => acc + curr.totalPrice, 0);
  const totalItemsSold = sales.reduce((acc, curr) => acc + curr.quantity, 0);
  
  // Best selling product
  const filteredSales = sales.filter(s => {
    if (activeTab !== 'sales') return true;
    const d = new Date(s.saleDate);
    return d.getMonth() + 1 === filterMonth && d.getFullYear() === filterYear;
  });

  const productSalesMap = {};
  filteredSales.forEach(s => {
    if (!productSalesMap[s.productName]) productSalesMap[s.productName] = 0;
    productSalesMap[s.productName] += s.quantity;
  });
  let bestSellingProduct = "N/A";
  let maxSold = 0;
  Object.keys(productSalesMap).forEach(key => {
    if (productSalesMap[key] > maxSold) {
      maxSold = productSalesMap[key];
      bestSellingProduct = key;
    }
  });

  if (loading) return <AppLayout title="Tienda"><div className="text-center p-8"><span className="spinner" style={{borderColor: 'var(--primary)', borderTopColor: 'transparent'}}></span></div></AppLayout>;

  return (
    <AppLayout title="Tienda y Ventas">
      <div className="fade-in">
        
        {/* Tabs Menu */}
        <div className="flex gap-4 mb-6 border-b border-border/50 pb-2">
          <button 
            className={`flex items-center gap-2 pb-2 px-1 border-b-2 transition-colors ${activeTab === 'dashboard' ? 'border-primary text-primary font-bold' : 'border-transparent text-text-muted hover:text-text-main'}`}
            onClick={() => setActiveTab('dashboard')}
          >
            <BarChart3 size={18} /> Dashboard
          </button>
          <button 
            className={`flex items-center gap-2 pb-2 px-1 border-b-2 transition-colors ${activeTab === 'inventory' ? 'border-primary text-primary font-bold' : 'border-transparent text-text-muted hover:text-text-main'}`}
            onClick={() => setActiveTab('inventory')}
          >
            <Package size={18} /> Inventario Base
          </button>
          <button 
            className={`flex items-center gap-2 pb-2 px-1 border-b-2 transition-colors ${activeTab === 'sales' ? 'border-primary text-primary font-bold' : 'border-transparent text-text-muted hover:text-text-main'}`}
            onClick={() => setActiveTab('sales')}
          >
            <ShoppingCart size={18} /> Historial de Ventas
          </button>
        </div>

        {/* DASHBOARD TAB */}
        {activeTab === 'dashboard' && (
          <div className="fade-in">
            <h3 className="text-lg font-bold mb-4 flex items-center gap-2"><BarChart3 className="text-primary-400" /> Resumen de Ventas</h3>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
              <div className="card p-4 flex items-center gap-4 bg-primary/10 border-primary/30">
                <div className="p-3 bg-primary/20 rounded-full text-primary"><DollarSign size={22}/></div>
                <div><h4 className="text-primary font-bold">Ingresos Totales</h4><p className="text-sm text-text-secondary">S/. {totalRevenue.toFixed(2)}</p></div>
              </div>
              <div className="card p-4 flex items-center gap-4 bg-success/10 border-success/30">
                <div className="p-3 bg-success/20 rounded-full text-success"><ShoppingCart size={22}/></div>
                <div><h4 className="text-success font-bold">Artículos Vendidos</h4><p className="text-sm text-text-secondary">{totalItemsSold} unidades</p></div>
              </div>
              <div className="card p-4 flex items-center gap-4 bg-warning/10 border-warning/30">
                <div className="p-3 bg-warning/20 rounded-full text-warning"><TrendingUp size={22}/></div>
                <div><h4 className="text-warning font-bold">Más Vendido</h4><p className="text-sm text-text-secondary">{bestSellingProduct}</p></div>
              </div>
            </div>
            
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="card p-5">
                <h4 className="font-bold mb-4 text-text-muted uppercase text-xs tracking-wider">Top Productos</h4>
                {Object.keys(productSalesMap).length === 0 ? (
                  <p className="text-sm text-text-muted">No hay datos suficientes.</p>
                ) : (
                  <ul className="space-y-3">
                    {Object.entries(productSalesMap).sort((a,b)=>b[1]-a[1]).slice(0,5).map(([name, qty], idx) => (
                      <li key={idx} className="flex justify-between items-center text-sm border-b border-border/50 pb-2">
                        <span className="font-medium">{name}</span>
                        <span className="badge badge-success">{qty} vendidos</span>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
              
              <div className="card p-5 bg-gradient-to-br from-bg-dark to-bg-surface border-primary/20">
                <div className="flex flex-col items-center justify-center h-full text-center">
                  <BarChart3 size={40} className="text-primary/40 mb-3" />
                  <h4 className="font-bold text-lg mb-1">Centro de Ventas</h4>
                  <p className="text-sm text-text-muted mb-4">Gestiona el inventario y lleva el control de todos los ingresos extra de la academia.</p>
                  <button onClick={() => setActiveTab('sales')} className="btn btn-primary btn-sm">Ver Transacciones</button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* INVENTORY TAB */}
        {activeTab === 'inventory' && (
          <div className="card fade-in">
            <div className="card-header">
              <div>
                <h3 className="card-title flex items-center gap-2"><Package className="text-primary-400" size={20} /> Productos e Inventario</h3>
                <p className="text-muted mt-1">Registra artículos (uniformes, balones) con stock y precio.</p>
              </div>
              <button onClick={() => { setProductForm(initialProduct); setEditingProductId(null); setShowProductModal(true); }} className="btn btn-primary">
                <PlusCircle size={16} /> Agregar Producto
              </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {products.map(p => (
                <div key={p.id} className="card p-4" style={{ background: 'var(--bg-surface)' }}>
                  <div className="flex justify-between items-start mb-2">
                    <h3 className="font-bold text-lg text-primary-100">{p.name}</h3>
                    <span className={`badge ${p.stock > 5 ? 'badge-success' : p.stock > 0 ? 'badge-warning' : 'badge-danger'}`}>
                      {p.stock} en stock
                    </span>
                  </div>
                  <div className="text-sm text-text-muted mb-4 h-10 overflow-hidden">{p.description}</div>
                  
                  <div className="flex flex-col gap-2 text-sm">
                    <div className="flex justify-between border-b border-border/50 pb-1">
                      <span className="text-text-secondary">Categoría:</span>
                      <span className="font-medium text-white flex items-center gap-1"><Tag size={12} className="text-primary-400"/>{p.productCategory}</span>
                    </div>
                    <div className="flex justify-between border-b border-border/50 pb-1">
                      <span className="text-text-secondary">Costo de Compra:</span>
                      <span className="font-medium text-warning">S/. {(p.costPrice || 0).toFixed(2)}</span>
                    </div>
                    <div className="flex justify-between border-b border-border/50 pb-1">
                      <span className="text-text-secondary">Precio de Venta:</span>
                      <span className="font-bold text-success">S/. {p.price.toFixed(2)}</span>
                    </div>
                    {p.costPrice > 0 && (
                      <div className="flex justify-between pb-1">
                        <span className="text-text-secondary">Margen:</span>
                        <span className={`font-bold flex items-center gap-1 ${(p.price - p.costPrice) > 0 ? 'text-emerald-400' : 'text-red-400'}`}>
                          S/. {(p.price - p.costPrice).toFixed(2)} ({p.price > 0 ? (((p.price - p.costPrice) / p.price) * 100).toFixed(0) : 0}%)
                        </span>
                      </div>
                    )}
                  </div>

                  <div className="mt-4 flex gap-2">
                    <button className="btn btn-outline btn-sm flex-1" onClick={() => {
                      setSaleForm({ ...initialSale, productId: p.id });
                      setShowSaleModal(true);
                    }}>Vender</button>
                    <button className="btn btn-ghost btn-sm text-primary" onClick={() => {
                      setProductForm({ name: p.name, description: p.description, productCategory: p.productCategory, costPrice: p.costPrice || '', price: p.price, stock: p.stock });
                      setEditingProductId(p.id);
                      setShowProductModal(true);
                    }}>Editar</button>
                  </div>
                </div>
              ))}
            </div>
            {products.length === 0 && <div className="empty-state"><Package size={40}/><p>No hay productos en inventario</p></div>}
          </div>
        )}

        {/* SALES TAB */}
        {activeTab === 'sales' && (
          <div className="card fade-in">
            <div className="card-header">
              <div>
                <h3 className="card-title flex items-center gap-2"><TrendingUp className="text-success" size={20} /> Ventas Realizadas</h3>
                <p className="text-muted mt-1">Historial de productos vendidos a los alumnos o público general.</p>
              </div>
              <div className="flex items-center gap-3 flex-wrap">
                <select
                  className="form-control text-sm py-2 w-auto"
                  value={filterMonth}
                  onChange={e => setFilterMonth(parseInt(e.target.value))}
                >
                  {['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre']
                    .map((m, i) => <option key={i+1} value={i+1}>{m}</option>)}
                </select>
                <select
                  className="form-control text-sm py-2 w-auto"
                  value={filterYear}
                  onChange={e => setFilterYear(parseInt(e.target.value))}
                >
                  {[2024, 2025, 2026, 2027].map(y => <option key={y} value={y}>{y}</option>)}
                </select>
                <button onClick={() => { setSaleForm(initialSale); setShowSaleModal(true); }} className="btn btn-success">
                  <ShoppingBag size={16} /> Registrar Venta
                </button>
              </div>
            </div>

            <div className="table-container">
              <table className="table">
                <thead>
                  <tr>
                    <th>Fecha</th>
                    <th>Producto</th>
                    <th>Comprador (Alumno)</th>
                    <th>Cantidad</th>
                    <th>Total</th>
                    <th className="text-right">Recibo</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredSales.map(s => (
                    <tr key={s.id}>
                      <td>{new Date(s.saleDate).toLocaleDateString()} {new Date(s.saleDate).toLocaleTimeString()}</td>
                      <td><div className="font-medium text-primary-100">{s.productName}</div><div className="text-xs text-text-muted">P.U: S/. {s.unitPrice.toFixed(2)} {s.discountAmount > 0 && !s.isGift && <span className="text-warning ml-1">(Desc: S/. {s.discountAmount.toFixed(2)})</span>}</div></td>
                      <td>{s.studentName}</td>
                      <td>{s.quantity}</td>
                      <td>
                        {s.isGift ? (
                          <span className="badge badge-warning text-xs">🎁 Obsequio</span>
                        ) : (
                          <span className="font-bold text-success">S/. {s.totalPrice.toFixed(2)}</span>
                        )}
                      </td>
                      <td className="text-right">
                        <div className="flex items-center justify-end gap-2">
                          <button onClick={() => regenerateSaleReceipt(s)} className="btn btn-ghost btn-sm text-primary flex items-center gap-1" title="Volver a descargar recibo">
                            <Download size={14} /> PDF
                          </button>
                          {isCurrentMonth(s.saleDate) && (
                            <button onClick={() => handleVoidSale(s)} className="btn btn-ghost btn-sm text-danger flex items-center gap-1" title="Anular Venta">
                              <Trash2 size={14} /> Anular
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {filteredSales.length === 0 && <div className="empty-state"><ShoppingCart size={40}/><p>Aún no hay ventas registradas en este período</p></div>}
          </div>
        )}

        {/* PRODUCT MODAL */}
        {showProductModal && (
          <div className="modal-overlay" onClick={() => setShowProductModal(false)}>
            <div className="modal" onClick={e => e.stopPropagation()}>
              <h3 className="modal-title">{editingProductId ? 'Editar Producto' : 'Nuevo Producto'}</h3>
              <form onSubmit={handleProductSubmit}>
                <div className="form-group">
                  <label className="form-label">Nombre del Producto *</label>
                  <input required type="text" className="form-control" value={productForm.name} onChange={e => setProductForm({...productForm, name: e.target.value})} placeholder="Ej. Camiseta Oficial 2026"/>
                </div>
                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Categoría *</label>
                    <input required type="text" className="form-control" value={productForm.productCategory} onChange={e => setProductForm({...productForm, productCategory: e.target.value})} placeholder="Ej. Uniformes, Balones" list="categories" />
                    <datalist id="categories"><option value="Uniforme"/><option value="Balón"/><option value="Suplemento"/><option value="Accesorio"/></datalist>
                  </div>
                  <div className="form-group">
                    <label className="form-label">Stock Inicial *</label>
                    <input required type="number" min="0" className="form-control" value={productForm.stock} onChange={e => setProductForm({...productForm, stock: e.target.value})} />
                  </div>
                </div>
                <div className="form-row">
                  <div className="form-group">
                    <label className="form-label">Costo de Compra (S/.)</label>
                    <div className="relative">
                      <span className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted font-bold">S/.</span>
                      <input required type="number" step="0.01" min="0" className="form-control pl-12 text-warning font-bold" value={productForm.costPrice} onChange={e => setProductForm({...productForm, costPrice: e.target.value})} placeholder="0.00" />
                    </div>
                  </div>
                  <div className="form-group">
                    <label className="form-label">Precio de Venta (S/.) *</label>
                    <div className="relative">
                      <span className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted font-bold">S/.</span>
                      <input required type="number" step="0.01" min="0" className="form-control pl-12 text-success font-bold" value={productForm.price} onChange={e => setProductForm({...productForm, price: e.target.value})} />
                    </div>
                  </div>
                </div>
                <div className="form-group">
                  <label className="form-label">Descripción</label>
                  <textarea className="form-control" rows="2" value={productForm.description} onChange={e => setProductForm({...productForm, description: e.target.value})}></textarea>
                </div>
                <div className="modal-footer">
                  <button type="button" onClick={() => setShowProductModal(false)} className="btn btn-ghost text-danger">Cancelar</button>
                  <button type="submit" className="btn btn-primary">{editingProductId ? 'Guardar Cambios' : 'Crear Producto'}</button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* SALE MODAL */}
        {showSaleModal && (
          <div className="modal-overlay" onClick={() => setShowSaleModal(false)}>
            <div className="modal" onClick={e => e.stopPropagation()}>
              <h3 className="modal-title flex items-center gap-2"><ShoppingBag className="text-success" /> Registrar Venta</h3>
              <form onSubmit={handleSaleSubmit}>
                <div className="form-group">
                  <label className="form-label">Producto a Vender *</label>
                  <select required className="form-control" value={saleForm.productId} onChange={e => setSaleForm({...saleForm, productId: e.target.value})}>
                    <option value="">-- Seleccione un Producto --</option>
                    {products.filter(p => p.stock > 0).map(p => (
                      <option key={p.id} value={p.id}>{p.name} (Stock: {p.stock} | Precio: S/. {p.price.toFixed(2)})</option>
                    ))}
                  </select>
                </div>
                
                <div className="form-group">
                  <label className="form-label flex justify-between">
                    <span>Comprador (Opcional)</span>
                    <span className="text-xs text-text-muted font-normal">Si se deja vacío, es "Público General"</span>
                  </label>
                  <select className="form-control" value={saleForm.studentId} onChange={e => setSaleForm({...saleForm, studentId: e.target.value})}>
                    <option value="">Público General</option>
                    {students.map(s => <option key={s.id} value={s.id}>{s.firstName} {s.lastName} (Apoderado: {s.guardianName})</option>)}
                  </select>
                </div>

                {saleForm.studentId && pendingDebts.filter(d => d.studentId === saleForm.studentId).length > 0 && (
                  <div className="form-row">
                    <div className="form-group flex-1">
                      <label className="form-label flex justify-between">
                        <span className="text-primary-100">Vincular Mensualidad (Opcional)</span>
                        <span className="text-xs text-text-muted">Añadir al mismo recibo</span>
                      </label>
                      <select 
                        className="form-control" 
                        value={saleForm.paymentRecordId || ''} 
                        onChange={e => {
                          const selectedRecordId = e.target.value;
                          const selectedRecord = pendingDebts.find(d => d.id === selectedRecordId);
                          setSaleForm({
                            ...saleForm, 
                            paymentRecordId: selectedRecordId,
                            monthlyAmountPaid: selectedRecord ? (selectedRecord.amount - selectedRecord.amountPaid).toFixed(2) : ''
                          });
                        }}
                      >
                        <option value="">-- No vincular --</option>
                        {pendingDebts.filter(d => d.studentId === saleForm.studentId).map(d => (
                          <option key={d.id} value={d.id}>{d.description} (Pendiente: S/. {(d.amount - d.amountPaid).toFixed(2)})</option>
                        ))}
                      </select>
                    </div>
                    {saleForm.paymentRecordId && (
                      <div className="form-group w-32">
                        <label className="form-label">Monto a pagar</label>
                        <input 
                          type="number" 
                          step="0.01"
                          min="0.01"
                          max={pendingDebts.find(d => d.id === saleForm.paymentRecordId) ? pendingDebts.find(d => d.id === saleForm.paymentRecordId).amount - pendingDebts.find(d => d.id === saleForm.paymentRecordId).amountPaid : undefined}
                          className="form-control text-success font-bold" 
                          value={saleForm.monthlyAmountPaid} 
                          onChange={e => setSaleForm({...saleForm, monthlyAmountPaid: e.target.value})} 
                        />
                      </div>
                    )}
                  </div>
                )}

                <div className="form-group mb-4 flex items-center justify-between bg-bg-surface p-3 rounded-lg border border-border">
                  <div>
                    <label className="form-label mb-0 text-warning flex items-center gap-2">
                      Marcar como Obsequio / Entrega gratuita
                    </label>
                    <p className="text-xs text-text-muted mt-1">
                      El monto cobrado será 0, pero se registrará la cantidad entregada para fines de inventario y costo.
                    </p>
                  </div>
                  <label className="relative inline-flex items-center cursor-pointer">
                    <input 
                      type="checkbox" 
                      className="sr-only peer"
                      checked={saleForm.isGift} 
                      onChange={e => setSaleForm({...saleForm, isGift: e.target.checked})} 
                    />
                    <div className="w-11 h-6 bg-border peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-warning"></div>
                  </label>
                </div>

                <div className="form-row items-end">
                  <div className="form-group mb-0">
                    <label className="form-label">Fecha de Venta *</label>
                    <input
                      type="date"
                      required
                      className="form-control"
                      value={saleForm.saleDate}
                      onChange={e => setSaleForm({...saleForm, saleDate: e.target.value})}
                    />
                  </div>
                  <div className="form-group mb-0">
                    <label className="form-label">Cantidad *</label>
                    <input required type="number" min="1" max={selectedProductForSale?.stock || 1} className="form-control" value={saleForm.quantity} onChange={e => setSaleForm({...saleForm, quantity: e.target.value})} />
                  </div>
                  <div className="form-group mb-0">
                    <label className="form-label">Desc. (S/.)</label>
                    <input type="number" step="0.01" min="0" className="form-control" value={saleForm.customDiscount} onChange={e => setSaleForm({...saleForm, customDiscount: e.target.value})} disabled={saleForm.isGift} placeholder="0.00" />
                  </div>
                </div>
                
                <div className="form-group mt-4 flex-1">
                  <div className="bg-bg-dark border border-border rounded-lg p-3 flex justify-between items-center">
                    <p className="text-text-muted text-sm uppercase tracking-wide">Total a Cobrar</p>
                    <div className="text-right">
                      {!saleForm.isGift && (
                        <div className={`text-2xl font-bold text-success`}>
                          S/. {Math.max(0, ((parseFloat(selectedProductForSale?.price || 0) * parseInt(saleForm.quantity || 0)) - (parseFloat(saleForm.customDiscount) || 0)) + (parseFloat(saleForm.monthlyAmountPaid) || 0)).toFixed(2)}
                        </div>
                      )}
                      {saleForm.isGift && (
                        <div className="text-2xl font-bold text-success">
                          S/. {(parseFloat(saleForm.monthlyAmountPaid) || 0).toFixed(2)}
                        </div>
                      )}
                      {saleForm.paymentRecordId && (
                        <div className="text-xs text-text-muted mt-1">
                          (Producto: S/. {saleForm.isGift ? '0.00' : Math.max(0, (parseFloat(selectedProductForSale?.price || 0) * parseInt(saleForm.quantity || 0)) - (parseFloat(saleForm.customDiscount) || 0)).toFixed(2)} + Mensualidad: S/. {(parseFloat(saleForm.monthlyAmountPaid) || 0).toFixed(2)})
                        </div>
                      )}
                    </div>
                  </div>
                </div>

                {!selectedProductForSale && saleForm.productId && (
                  <p className="text-danger text-sm mt-3 flex items-center gap-1"><AlertCircle size={14}/> Producto sin stock disponible.</p>
                )}

                {!saleForm.isGift && (
                  <>
                    <div className="form-group">
                      <label className="form-label">Método de Pago *</label>
                      <div className="flex flex-wrap gap-2 mt-1">
                        {[{v:'Cash',l:'💵 Efectivo'},{v:'Yape',l:'📱 Yape'},{v:'Plin',l:'📱 Plin'},{v:'BankTransfer',l:'🏦 Transferencia'},{v:'Card',l:'💳 Tarjeta'}].map(m => (
                          <button type="button" key={m.v}
                            className={`px-3 py-1.5 rounded-lg border text-sm font-medium transition-all ${saleForm.paymentMethod===m.v ? 'bg-primary text-white border-primary' : 'border-border text-text-muted hover:border-primary'}`}
                            onClick={() => setSaleForm({...saleForm, paymentMethod: m.v})}>
                            {m.l}
                          </button>
                        ))}
                      </div>
                    </div>
                    {saleForm.paymentMethod !== 'Cash' && (
                      <div className="form-group">
                        <label className="form-label">N° Operación <span className="text-text-muted font-normal">(Opcional)</span></label>
                        <input type="text" className="form-control" value={saleForm.operationNumber} onChange={e => setSaleForm({...saleForm, operationNumber: e.target.value})} placeholder="Ej. 123456789" />
                      </div>
                    )}
                  </>
                )}

                <div className="modal-footer mt-6">
                  <button type="button" onClick={() => setShowSaleModal(false)} className="btn btn-ghost text-danger">Cancelar</button>
                  <button type="submit" className="btn btn-success" disabled={!selectedProductForSale}>Confirmar Venta</button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>

      {/* ── MODAL: CONFIRMAR ANULACIÓN ── */}
      {confirmVoid && (
        <div className="modal-overlay" onClick={() => setConfirmVoid(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 420 }}>
            {/* Header */}
            <div className="flex justify-between items-center mb-5 border-b border-border pb-3">
              <h3 className="modal-title m-0 flex items-center gap-2 text-danger">
                <AlertTriangle size={20} /> Anular Venta
              </h3>
              <button onClick={() => setConfirmVoid(null)} className="btn btn-ghost btn-sm">
                <X size={16} />
              </button>
            </div>

            {/* Body */}
            <div className="flex flex-col items-center text-center gap-4 py-2">
              <div className="p-4 bg-danger/15 rounded-full">
                <Trash2 size={32} className="text-danger" />
              </div>
              <div>
                <p className="text-text-main font-medium mb-1">¿Estás seguro de que deseas anular esta venta?</p>
                <p className="text-sm text-text-muted">
                  Producto: <span className="font-bold text-white">&ldquo;{confirmVoid.productName}&rdquo;</span>
                </p>
                <p className="text-xs text-text-muted mt-2">
                  Se restaurará el stock automáticamente. Esta acción no se puede deshacer.
                </p>
              </div>
            </div>

            {/* Footer */}
            <div className="modal-footer mt-6 flex justify-end gap-3">
              <button
                type="button"
                onClick={() => setConfirmVoid(null)}
                className="btn btn-ghost"
              >
                Cancelar
              </button>
              <button
                type="button"
                onClick={confirmVoidSale}
                className="btn btn-danger flex items-center gap-2"
              >
                <Trash2 size={15} /> Sí, anular venta
              </button>
            </div>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
