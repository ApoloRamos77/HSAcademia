import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { Package, ShoppingCart, PlusCircle, DollarSign, Tag, TrendingUp, AlertCircle, ShoppingBag } from 'lucide-react';
import toast from 'react-hot-toast';

export default function Tienda() {
  const [activeTab, setActiveTab] = useState('inventory'); // 'inventory' | 'sales'
  
  const [products, setProducts] = useState([]);
  const [sales, setSales] = useState([]);
  const [students, setStudents] = useState([]);
  const [loading, setLoading] = useState(true);

  const [showProductModal, setShowProductModal] = useState(false);
  const [editingProductId, setEditingProductId] = useState(null);
  
  const initialProduct = { name: '', description: '', productCategory: '', price: '', stock: '' };
  const [productForm, setProductForm] = useState(initialProduct);

  const [showSaleModal, setShowSaleModal] = useState(false);
  const initialSale = { productId: '', studentId: '', quantity: '1' };
  const [saleForm, setSaleForm] = useState(initialSale);

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      const [pRes, sRes, stuRes] = await Promise.all([
        api.get('/store/products'),
        api.get('/store/sales'),
        api.get('/students') // Fetch students to link to sale
      ]);
      setProducts(pRes.data);
      setSales(sRes.data);
      setStudents(stuRes.data);
    } catch (err) {
      toast.error('Error al cargar la tienda.');
    } finally {
      setLoading(false);
    }
  };

  const handleProductSubmit = async (e) => {
    e.preventDefault();
    try {
      const payload = { ...productForm, price: parseFloat(productForm.price), stock: parseInt(productForm.stock, 10) };
      
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
        quantity: parseInt(saleForm.quantity, 10) 
      };
      await api.post('/store/sales', payload);
      toast.success('Venta registrada con éxito');
      setShowSaleModal(false);
      fetchData(); // Reloads products (to show new stock) and sales list
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al registrar venta. Verifique el stock.');
    }
  };

  const selectedProductForSale = products.find(p => p.id === saleForm.productId);
  const totalSaleEstimation = selectedProductForSale ? (selectedProductForSale.price * parseInt(saleForm.quantity || 0)).toFixed(2) : '0.00';

  if (loading) return <AppLayout title="Tienda"><div className="text-center p-8"><span className="spinner" style={{borderColor: 'var(--primary)', borderTopColor: 'transparent'}}></span></div></AppLayout>;

  return (
    <AppLayout title="Tienda y Ventas">
      <div className="fade-in">
        
        {/* Tabs Menu */}
        <div className="flex gap-4 mb-6 border-b border-border/50 pb-2">
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
                      <span className="text-text-secondary">Precio Unitario:</span>
                      <span className="font-bold text-success flex items-center gap-1"><DollarSign size={14}/> {p.price.toFixed(2)}</span>
                    </div>
                  </div>

                  <div className="mt-4 flex gap-2">
                    <button className="btn btn-outline btn-sm flex-1" onClick={() => {
                      setSaleForm({ ...initialSale, productId: p.id });
                      setShowSaleModal(true);
                    }}>Vender</button>
                    <button className="btn btn-ghost btn-sm text-primary" onClick={() => {
                      setProductForm({ name: p.name, description: p.description, productCategory: p.productCategory, price: p.price, stock: p.stock });
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
              <button onClick={() => { setSaleForm(initialSale); setShowSaleModal(true); }} className="btn btn-success">
                <ShoppingBag size={16} /> Registrar Venta
              </button>
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
                  </tr>
                </thead>
                <tbody>
                  {sales.map(s => (
                    <tr key={s.id}>
                      <td>{new Date(s.saleDate).toLocaleDateString()} {new Date(s.saleDate).toLocaleTimeString()}</td>
                      <td><div className="font-medium text-primary-100">{s.productName}</div><div className="text-xs text-text-muted">P.U: ${s.unitPrice.toFixed(2)}</div></td>
                      <td>{s.studentName}</td>
                      <td>{s.quantity}</td>
                      <td className="font-bold text-success">${s.totalPrice.toFixed(2)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {sales.length === 0 && <div className="empty-state"><ShoppingCart size={40}/><p>Aún no hay ventas registradas</p></div>}
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
                <div className="form-group">
                  <label className="form-label">Precio Unitario ($) *</label>
                  <input required type="number" step="0.01" min="0" className="form-control text-success font-bold" value={productForm.price} onChange={e => setProductForm({...productForm, price: e.target.value})} />
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
                      <option key={p.id} value={p.id}>{p.name} (Stock: {p.stock} | Precio: ${p.price.toFixed(2)})</option>
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

                <div className="form-row items-end">
                  <div className="form-group mb-0">
                    <label className="form-label">Cantidad *</label>
                    <input required type="number" min="1" max={selectedProductForSale?.stock || 1} className="form-control" value={saleForm.quantity} onChange={e => setSaleForm({...saleForm, quantity: e.target.value})} />
                  </div>
                  <div className="form-group mb-0 flex-1">
                    <div className="bg-bg-dark border border-border rounded-lg p-3 flex justify-between items-center">
                      <span className="text-text-secondary text-sm">Total a Cobrar:</span>
                      <span className="text-2xl font-bold text-success">${totalSaleEstimation}</span>
                    </div>
                  </div>
                </div>

                {!selectedProductForSale && saleForm.productId && (
                  <p className="text-danger text-sm mt-3 flex items-center gap-1"><AlertCircle size={14}/> Producto sin stock disponible.</p>
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
    </AppLayout>
  );
}
