import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { PlusCircle, Search, Users, Activity, FileText, Calendar, MapPin, UserPlus, X, Filter } from 'lucide-react';
import toast from 'react-hot-toast';
import { useAuth } from '../../context/AuthContext';
import { Apple, Activity as ActivityIcon } from 'lucide-react';

export default function Alumnos() {
  const { user } = useAuth();
  const [alumnos, setAlumnos] = useState([]);
  const [sedes, setSedes] = useState([]);
  const [categorias, setCategorias] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [currentStep, setCurrentStep] = useState(1); // 1: Alumno, 2: Apoderado, 3: Ficha Médica
  const [showNutritionModal, setShowNutritionModal] = useState(false);
  const [nutritionStudent, setNutritionStudent] = useState(null);
  const [nutritionRecords, setNutritionRecords] = useState([]);
  const [nutritionForm, setNutritionForm] = useState({
    weightKg: '', heightCm: '', muscleMassPercentage: '', fatPercentage: '', notes: '', recordDate: new Date().toISOString().split('T')[0]
  });

  const [showFinancialModal, setShowFinancialModal] = useState(false);
  const [financialStudent, setFinancialStudent] = useState(null);
  const [financialData, setFinancialData] = useState({ debts: [], sales: [] });
  const [finFilterMonth, setFinFilterMonth] = useState('');
  const [finFilterYear, setFinFilterYear] = useState('');
  
  const [search, setSearch] = useState('');
  const [filterSede, setFilterSede] = useState('');
  const [filterCat, setFilterCat] = useState('');
  const [filterStatus, setFilterStatus] = useState(''); // '' | 'active' | 'inactive'
  const [showFilters, setShowFilters] = useState(false);

  const initialForm = {
    firstName: '', lastName: '', email: '', phone: '', dateOfBirth: '', documentNumber: '', headquarterId: '', categoryId: '',
    enrollmentDate: new Date().toISOString().split('T')[0], paymentStartDate: '', withdrawalDate: '', preferentialFee: '',
    isGuest: false, isScholarship: false, scholarshipPercentage: '', isActive: true,
    guardianFirstName: '', guardianLastName: '', guardianEmail: '', guardianPhone: '',
    medicalRecord: {
      allergies: '', medicalConditions: '', emergencyContactName: '', emergencyContactPhone: '',
      weightKg: '', heightCm: '', bmi: '', nutritionPlan: ''
    }
  };
  const [formData, setFormData] = useState(initialForm);
  const [editingId, setEditingId] = useState(null);

  // Auto-calculate BMI
  useEffect(() => {
    if (formData.medicalRecord.weightKg && formData.medicalRecord.heightCm) {
      const weight = parseFloat(formData.medicalRecord.weightKg);
      const heightM = parseFloat(formData.medicalRecord.heightCm) / 100;
      if (weight > 0 && heightM > 0) {
        const bmi = (weight / (heightM * heightM)).toFixed(2);
        if (bmi !== formData.medicalRecord.bmi) {
          setFormData(prev => ({ ...prev, medicalRecord: { ...prev.medicalRecord, bmi } }));
        }
      }
    } else {
      if (formData.medicalRecord.bmi !== '') {
        setFormData(prev => ({ ...prev, medicalRecord: { ...prev.medicalRecord, bmi: '' } }));
      }
    }
  }, [formData.medicalRecord.weightKg, formData.medicalRecord.heightCm]);

  useEffect(() => {
    fetchData();
  }, []);

  const fetchData = async () => {
    try {
      const [aRes, sRes, cRes] = await Promise.all([
        api.get('/students'),
        api.get('/academy-config/headquarters'),
        api.get('/academy-config/categories')
      ]);
      setAlumnos(aRes.data);
      setSedes(sRes.data);
      setCategorias(cRes.data);
    } catch (err) {
      toast.error('Error al cargar datos');
    } finally {
      setLoading(false);
    }
  };

  const calculateAge = (dobString) => {
    if (!dobString) return null;
    const today = new Date();
    const birthDate = new Date(dobString);
    let age = today.getFullYear() - birthDate.getFullYear();
    const m = today.getMonth() - birthDate.getMonth();
    if (m < 0 || (m === 0 && today.getDate() < birthDate.getDate())) {
      age--;
    }
    return age;
  };

  const currentAge = calculateAge(formData.dateOfBirth);
  
  // Filter categories based on headquarter and age
  const availableCategories = categorias.filter(c => {
    const matchHq = formData.headquarterId ? c.headquarterId === formData.headquarterId : true;
    let matchDate = true;
    if (formData.dateOfBirth && c.startDateOfBirth && c.endDateOfBirth) {
      const dob = new Date(formData.dateOfBirth);
      const start = new Date(c.startDateOfBirth);
      const end = new Date(c.endDateOfBirth);
      matchDate = dob >= start && dob <= end;
    }
    return matchHq && matchDate;
  });

  const filteredAlumnos = alumnos.filter(a => {
    const q = search.toLowerCase();
    const matchSearch = !q ||
      `${a.firstName} ${a.lastName}`.toLowerCase().includes(q) ||
      (a.documentNumber || '').toLowerCase().includes(q) ||
      (a.guardianName || '').toLowerCase().includes(q);
    const matchSede   = !filterSede   || a.headquarterId === filterSede;
    const matchCat    = !filterCat    || a.categoryId    === filterCat;
    const matchStatus = !filterStatus || (filterStatus === 'active' ? a.isActive : !a.isActive);
    return matchSearch && matchSede && matchCat && matchStatus;
  });

  const activeFiltersCount = [filterSede, filterCat, filterStatus].filter(Boolean).length;

  const handleSubmit = async (e) => {
    e.preventDefault();
    
    if (currentStep === 1) {
      setCurrentStep(currentStep + 1);
      return;
    }
    
    if (currentStep === 2) {
      setCurrentStep(currentStep + 1);
      return;
    }

    if (currentStep === 3) {
      // No validation needed
    }

    try {
      const payload = {
        ...formData,
        preferentialFee: formData.preferentialFee !== '' && formData.preferentialFee !== null ? parseFloat(formData.preferentialFee) : null,
        scholarshipPercentage: formData.scholarshipPercentage !== '' && formData.scholarshipPercentage !== null ? parseFloat(formData.scholarshipPercentage) : null,
        medicalRecord: {
          ...formData.medicalRecord,
          weightKg: formData.medicalRecord.weightKg !== '' && formData.medicalRecord.weightKg !== null ? parseFloat(formData.medicalRecord.weightKg) : null,
          heightCm: formData.medicalRecord.heightCm !== '' && formData.medicalRecord.heightCm !== null ? parseFloat(formData.medicalRecord.heightCm) : null,
          bmi: formData.medicalRecord.bmi !== '' && formData.medicalRecord.bmi !== null ? parseFloat(formData.medicalRecord.bmi) : null,
        }
      };

      if (editingId) {
        await api.put(`/students/${editingId}`, payload);
        toast.success('Alumno actualizado correctamente');
      } else {
        await api.post('/students', payload);
        toast.success('Alumno registrado correctamente');
      }
      
      setShowModal(false);
      setFormData(initialForm);
      setCurrentStep(1);
      setEditingId(null);
      fetchData();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al registrar alumno.');
    }
  };

  const openNutritionModal = async (alumno) => {
    setNutritionStudent(alumno);
    setNutritionForm({
      weightKg: '', heightCm: '', muscleMassPercentage: '', fatPercentage: '', notes: '', recordDate: new Date().toISOString().split('T')[0]
    });
    try {
      const res = await api.get(`/students/${alumno.id}/nutrition-records`);
      setNutritionRecords(res.data);
      setShowNutritionModal(true);
    } catch(err) {
      toast.error("Error al cargar historial nutricional");
    }
  };

  const handleNutritionSubmit = async (e) => {
    e.preventDefault();
    try {
      const payload = {
        ...nutritionForm,
        weightKg: nutritionForm.weightKg ? parseFloat(nutritionForm.weightKg) : null,
        heightCm: nutritionForm.heightCm ? parseFloat(nutritionForm.heightCm) : null,
        muscleMassPercentage: nutritionForm.muscleMassPercentage ? parseFloat(nutritionForm.muscleMassPercentage) : null,
        fatPercentage: nutritionForm.fatPercentage ? parseFloat(nutritionForm.fatPercentage) : null,
      };
      const res = await api.post(`/students/${nutritionStudent.id}/nutrition-records`, payload);
      setNutritionRecords([res.data, ...nutritionRecords]);
      setNutritionForm({
        weightKg: '', heightCm: '', muscleMassPercentage: '', fatPercentage: '', notes: '', recordDate: new Date().toISOString().split('T')[0]
      });
      toast.success("Registro guardado");
      // Optionally update the medical record in the main view
      fetchData();
    } catch(err) {
      toast.error("Error al guardar registro");
    }
  };

  const openFinancialModal = async (alumno) => {
    setFinancialStudent(alumno);
    setFinFilterMonth('');
    setFinFilterYear('');
    await fetchFinancialHistory(alumno.id, '', '');
    setShowFinancialModal(true);
  };

  const fetchFinancialHistory = async (studentId, month, year) => {
    try {
      let url = `/students/${studentId}/financial-history`;
      const params = new URLSearchParams();
      if (month) params.append('month', month);
      if (year) params.append('year', year);
      if (params.toString()) url += `?${params.toString()}`;
      
      const res = await api.get(url);
      setFinancialData(res.data);
    } catch (err) {
      toast.error("Error al cargar historial financiero");
    }
  };

  useEffect(() => {
    if (financialStudent && showFinancialModal) {
      fetchFinancialHistory(financialStudent.id, finFilterMonth, finFilterYear);
    }
  }, [finFilterMonth, finFilterYear]);

  if (loading) return <AppLayout title="Alumnos"><div className="text-center p-8"><span className="spinner" style={{borderColor: 'var(--primary)', borderTopColor: 'transparent'}}></span></div></AppLayout>;

  return (
    <AppLayout title="Gestión de Alumnos">
      <div className="fade-in">
        <div className="card">
          <div className="card-header">
            <div>
              <h3 className="card-title flex items-center gap-2">
                <Users className="text-primary-400" size={20} /> Alumnos
              </h3>
              <p className="text-muted mt-1">Registra alumnos, apoderados y su ficha médica/nutricional.</p>
            </div>
            {user?.role !== 'Staff' && (
              <button onClick={() => { setFormData(initialForm); setEditingId(null); setCurrentStep(1); setShowModal(true); }} className="btn btn-primary">
                <PlusCircle size={16} /> Registrar Alumno
              </button>
            )}
          </div>

          {/* ── Filter Bar ── */}
          <div className="px-1 pt-2 pb-4 border-b border-border/40 mb-4">
            <div className="flex gap-2 flex-wrap items-center">
              {/* Search */}
              <div className="relative flex-1 min-w-[180px]">
                <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
                <input
                  type="text"
                  className="form-control pl-8 py-1.5 text-sm"
                  placeholder="Buscar nombre, DNI o apoderado..."
                  value={search}
                  onChange={e => setSearch(e.target.value)}
                />
                {search && <button onClick={() => setSearch('')} className="absolute right-3 top-1/2 -translate-y-1/2 text-text-muted hover:text-danger"><X size={14}/></button>}
              </div>

              {/* Filter toggle */}
              <button
                onClick={() => setShowFilters(v => !v)}
                className={`btn btn-sm flex items-center gap-1.5 ${ activeFiltersCount > 0 ? 'btn-primary' : 'btn-outline'}`}
              >
                <Filter size={14}/> Filtros {activeFiltersCount > 0 && <span className="badge badge-warning text-xs ml-1">{activeFiltersCount}</span>}
              </button>

              {/* Clear all */}
              {(search || activeFiltersCount > 0) && (
                <button onClick={() => { setSearch(''); setFilterSede(''); setFilterCat(''); setFilterStatus(''); }} className="btn btn-ghost btn-sm text-danger flex items-center gap-1">
                  <X size={13}/> Limpiar
                </button>
              )}
            </div>

            {/* Expanded filter panel */}
            {showFilters && (
              <div className="grid grid-cols-1 md:grid-cols-3 gap-3 mt-3 fade-in">
                {/* Sede */}
                <div>
                  <label className="form-label text-xs mb-1">Sede</label>
                  <select className="form-control text-sm py-1.5" value={filterSede} onChange={e => setFilterSede(e.target.value)}>
                    <option value="">Todas las sedes</option>
                    {sedes.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
                  </select>
                </div>
                {/* Categoría */}
                <div>
                  <label className="form-label text-xs mb-1">Categoría</label>
                  <select className="form-control text-sm py-1.5" value={filterCat} onChange={e => setFilterCat(e.target.value)}>
                    <option value="">Todas las categorías</option>
                    {categorias
                      .filter(c => !filterSede || c.headquarterId === filterSede)
                      .map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                  </select>
                </div>
                {/* Estado */}
                <div>
                  <label className="form-label text-xs mb-1">Estado</label>
                  <select className="form-control text-sm py-1.5" value={filterStatus} onChange={e => setFilterStatus(e.target.value)}>
                    <option value="">Todos</option>
                    <option value="active">Activos</option>
                    <option value="inactive">Inactivos</option>
                  </select>
                </div>
              </div>
            )}

            {/* Result count */}
            <div className="flex items-center gap-3 mt-3 text-xs text-text-muted">
              <span>Mostrando <strong className="text-text-main">{filteredAlumnos.length}</strong> de <strong>{alumnos.length}</strong> alumnos</span>
              {filterSede && <span className="badge badge-primary text-xs">{sedes.find(s=>s.id===filterSede)?.name}</span>}
              {filterCat  && <span className="badge badge-success text-xs">{categorias.find(c=>c.id===filterCat)?.name}</span>}
              {filterStatus && <span className={`badge text-xs ${filterStatus==='active' ? 'badge-success' : 'badge-danger'}`}>{filterStatus==='active' ? 'Activos' : 'Inactivos'}</span>}
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {filteredAlumnos.map(alumno => (
              <div key={alumno.id} className="card p-5" style={{ background: 'var(--bg-surface)' }}>
                <div className="flex justify-between items-start mb-4">
                  <div>
                    <h3 className="font-bold text-lg text-primary-100">{alumno.firstName} {alumno.lastName}</h3>
                    <div className="flex items-center gap-1 text-xs text-text-muted mt-1">
                      <Calendar size={12} /> {alumno.age} años | DNI: {alumno.documentNumber || 'N/A'}
                    </div>
                  </div>
                  <span className={`badge ${alumno.isActive ? 'badge-success' : 'badge-danger'}`}>
                    {alumno.isActive ? 'Activo' : 'Inactivo'}
                  </span>
                </div>
                
                <div className="flex flex-col gap-2 text-sm text-text-secondary mb-4">
                  <div className="flex items-center gap-2"><MapPin size={15} className="text-primary-400"/> Sede: {alumno.headquarterName}</div>
                  <div className="flex items-center gap-2"><Activity size={15} className="text-success"/> Categoría: {alumno.categoryName}</div>
                  <div className="flex items-center gap-2 border-t border-border/50 pt-2 mt-2">
                    <UserPlus size={15} className="text-warning"/> Apoderado: {alumno.guardianName}
                  </div>
                </div>

                <div className="flex justify-end gap-2 border-t border-border pt-4">
                  <button className="btn btn-outline btn-sm text-primary" onClick={() => openFinancialModal(alumno)} title="Finanzas">
                    <FileText size={14} /> Finanzas
                  </button>
                  <button className="btn btn-outline btn-sm text-success" onClick={() => openNutritionModal(alumno)} title="Nutrición">
                    <Apple size={14} /> Historial
                  </button>
                  <button className="btn btn-ghost btn-sm text-primary" onClick={async () => {
                      try {
                        const recRes = await api.get(`/students/${alumno.id}/medical-record`);
                        setFormData({
                          firstName: alumno.firstName,
                          lastName: alumno.lastName,
                          email: alumno.email || '',
                          phone: alumno.phone || '',
                          documentNumber: alumno.documentNumber || '',
                          dateOfBirth: alumno.dateOfBirth ? alumno.dateOfBirth.split('T')[0] : '',
                          headquarterId: alumno.headquarterId,
                          categoryId: alumno.categoryId,
                          enrollmentDate: alumno.enrollmentDate ? alumno.enrollmentDate.split('T')[0] : '',
                          paymentStartDate: alumno.paymentStartDate ? alumno.paymentStartDate.split('T')[0] : '',
                          withdrawalDate: alumno.withdrawalDate ? alumno.withdrawalDate.split('T')[0] : '',
                          preferentialFee: alumno.preferentialFee || '',
                          isGuest: alumno.isGuest || false,
                          isScholarship: alumno.isScholarship || false,
                          scholarshipPercentage: alumno.scholarshipPercentage || '',
                          isActive: alumno.isActive,
                          guardianFirstName: alumno.guardianName.split(' ')[0] || '',
                          guardianLastName: alumno.guardianName.split(' ').slice(1).join(' ') || '',
                          guardianPhone: alumno.guardianPhone || '',
                          guardianEmail: alumno.guardianEmail || '',
                          medicalRecord: {
                            allergies: recRes.data.allergies || '',
                            medicalConditions: recRes.data.medicalConditions || '',
                            emergencyContactName: recRes.data.emergencyContactName || '',
                            emergencyContactPhone: recRes.data.emergencyContactPhone || '',
                            weightKg: recRes.data.weightKg || '',
                            heightCm: recRes.data.heightCm || '',
                            bmi: recRes.data.bmi || '',
                            nutritionPlan: recRes.data.nutritionPlan || ''
                          }
                        });
                        setEditingId(alumno.id);
                        setCurrentStep(1);
                        setShowModal(true);
                      } catch(e) { toast.error("Error al cargar datos"); }
                  }} title={user?.role === 'Staff' ? "Ver Alumno" : "Editar Alumno"}>{user?.role === 'Staff' ? "Ver" : "Editar"}</button>
                </div>
              </div>
            ))}
          </div>
          {filteredAlumnos.length === 0 && <div className="empty-state"><Users size={40}/><p>{alumnos.length === 0 ? 'No hay alumnos registrados' : 'No hay alumnos que coincidan con los filtros'}</p></div>}
        </div>

        {showModal && (
          <div className="modal-overlay" onClick={() => setShowModal(false)}>
            <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '700px' }}>
              <div className="flex justify-between items-center mb-6 border-b border-border pb-4">
                <h3 className="modal-title m-0">{user?.role === 'Staff' ? 'Detalles del Alumno' : (editingId ? 'Editar Alumno' : 'Registrar Nuevo Alumno')}</h3>
                <div className="flex items-center gap-2 text-sm">
                  <span className={`px-2 py-1 rounded ${currentStep === 1 ? 'bg-primary text-bg-dark font-bold' : 'bg-bg-surface text-text-muted'}`}>1. Alumno</span>
                  <span className={`px-2 py-1 rounded ${currentStep === 2 ? 'bg-primary text-bg-dark font-bold' : 'bg-bg-surface text-text-muted'}`}>2. Apoderado</span>
                  <span className={`px-2 py-1 rounded ${currentStep === 3 ? 'bg-primary text-bg-dark font-bold' : 'bg-bg-surface text-text-muted'}`}>3. Ficha Médica</span>
                </div>
              </div>
              
              <form onSubmit={handleSubmit}>
                <fieldset disabled={user?.role === 'Staff'} style={{ border: 'none', padding: 0, margin: 0 }}>
                {/* STEP 1: ALUMNO */}
                {currentStep === 1 && (
                  <div className="fade-in">
                    <div className="flex justify-between items-center mb-4">
                      <h4 className="text-primary-400 font-bold flex items-center gap-2"><Users size={18}/> Datos Personales del Alumno</h4>
                      
                      {editingId && (
                        <div className="flex items-center gap-2">
                          <label className="text-sm font-medium text-text-muted">Estado:</label>
                          <label className="flex items-center cursor-pointer">
                            <div className="relative">
                              <input type="checkbox" className="sr-only" checked={formData.isActive} onChange={(e) => setFormData({...formData, isActive: e.target.checked})} />
                              <div className={`block w-10 h-6 rounded-full transition-colors ${formData.isActive ? 'bg-success' : 'bg-danger'}`}></div>
                              <div className={`dot absolute left-1 top-1 bg-white w-4 h-4 rounded-full transition-transform ${formData.isActive ? 'transform translate-x-4' : ''}`}></div>
                            </div>
                            <span className={`ml-2 text-sm font-bold ${formData.isActive ? 'text-success' : 'text-danger'}`}>
                              {formData.isActive ? 'Activo' : 'Inactivo'}
                            </span>
                          </label>
                        </div>
                      )}
                    </div>
                    <div className="form-row">
                      <div className="form-group">
                        <label className="form-label">Nombres *</label>
                        <input required type="text" className="form-control" value={formData.firstName} onChange={e => setFormData({...formData, firstName: e.target.value})} />
                      </div>
                      <div className="form-group">
                        <label className="form-label">Apellidos *</label>
                        <input required type="text" className="form-control" value={formData.lastName} onChange={e => setFormData({...formData, lastName: e.target.value})} />
                      </div>
                    </div>

                    <div className="form-row">
                      <div className="form-group">
                        <label className="form-label">DNI / Documento Identidad <span className="text-text-muted text-xs font-normal">(Opcional)</span></label>
                        <input type="text" className="form-control" value={formData.documentNumber} onChange={e => setFormData({...formData, documentNumber: e.target.value})} />
                      </div>
                      <div className="form-group">
                        <label className="form-label">Fecha de Nacimiento *</label>
                        <input required type="date" min="1900-01-01" max="2100-12-31" className="form-control" value={formData.dateOfBirth} onChange={e => setFormData({...formData, dateOfBirth: e.target.value})} />
                        {currentAge !== null && (
                          <span className="text-xs text-primary-400 mt-1 inline-block">Edad calculada: {currentAge} años</span>
                        )}
                      </div>
                    </div>

                    <div className="form-row">
                      <div className="form-group">
                        <label className="form-label flex justify-between items-center">
                          <span>Correo Electrónico <span className="text-text-muted text-xs font-normal">(Opcional)</span></span>
                          <span className="text-xs text-primary-400 font-normal">Para acceso al app</span>
                        </label>
                        <input type="email" className="form-control" value={formData.email} onChange={e => setFormData({...formData, email: e.target.value})} placeholder="Para su acceso al app" />
                      </div>
                      <div className="form-group">
                        <label className="form-label flex justify-between items-center">
                          <span>Celular del Alumno <span className="text-text-muted text-xs font-normal">(Opcional)</span></span>
                        </label>
                        <input type="tel" className="form-control" value={formData.phone} onChange={e => setFormData({...formData, phone: e.target.value})} placeholder="Ej. 987654321" />
                        {(formData.email && formData.phone) && <p className="text-xs text-success mt-1">✓ Usuario del app se creará automáticamente al guardar.</p>}
                        {(formData.email && !formData.phone) && <p className="text-xs text-warning mt-1">⚠ Se necesita celular + correo para crear acceso al app.</p>}
                      </div>
                    </div>

                    <div className="form-row">
                      <div className="form-group">
                        <label className="form-label">Sede de Entrenamiento *</label>
                        <select required className="form-control" value={formData.headquarterId} onChange={e => setFormData({...formData, headquarterId: e.target.value})}>
                          <option value="">-- Seleccione Sede --</option>
                          {sedes.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}
                        </select>
                      </div>
                    </div>

                    <div className="form-row">
                      <div className="form-group">
                        <label className="form-label">Fecha de Inscripción (Original) *</label>
                        <input required type="date" min="1900-01-01" max="2100-12-31" className="form-control" value={formData.enrollmentDate} onChange={e => setFormData({...formData, enrollmentDate: e.target.value})} />
                      </div>
                      <div className="form-group">
                        <label className="form-label flex justify-between items-center">
                          <span>Fecha de Reingreso / Inicio de Pagos</span>
                          <span className="text-xs text-text-muted font-normal">(Opcional)</span>
                        </label>
                        <input type="date" min="1900-01-01" max="2100-12-31" className="form-control" value={formData.paymentStartDate} onChange={e => setFormData({...formData, paymentStartDate: e.target.value})} />
                        <span className="text-xs text-warning mt-1 inline-block">Si el alumno se retiró y vuelve, ingrese esta fecha para calcular el prorrateo correctamente sin perder su fecha original.</span>
                      </div>
                    </div>

                    <div className="form-row">
                      <div className="form-group">
                        <label className="form-label flex justify-between items-center">
                          <span className="text-danger">Fecha de Cese / Inactividad</span>
                          <span className="text-xs text-text-muted font-normal">(Opcional)</span>
                        </label>
                        <input type="date" min="1900-01-01" max="2100-12-31" className="form-control" value={formData.withdrawalDate} onChange={e => setFormData({...formData, withdrawalDate: e.target.value})} disabled={formData.isActive} />
                        <span className="text-xs text-text-muted mt-1 inline-block">Habilitado solo si el estado es Inactivo. Permite calcular deuda solo hasta esta fecha.</span>
                      </div>
                      <div className="form-group">
                        <label className="form-label">Costo Preferente (Opcional)</label>
                        <input type="number" step="0.01" className="form-control" placeholder="Ej. 150.00" value={formData.preferentialFee} onChange={e => setFormData({...formData, preferentialFee: e.target.value})} />
                      </div>
                    </div>

                    <div className="form-row">
                      <div className="form-group flex gap-2 items-center" style={{ marginTop: 'auto', marginBottom: 15 }}>
                        <input type="checkbox" id="chkGuest" checked={formData.isGuest} onChange={e => setFormData({...formData, isGuest: e.target.checked})} />
                        <label htmlFor="chkGuest" className="form-label m-0 cursor-pointer">Es Invitado (Exonerado 100%)</label>
                      </div>
                      <div className="form-group flex gap-2 items-center" style={{ marginTop: 'auto', marginBottom: 15 }}>
                        <input type="checkbox" id="chkScholarship" checked={formData.isScholarship} onChange={e => setFormData({...formData, isScholarship: e.target.checked})} />
                        <label htmlFor="chkScholarship" className="form-label m-0 cursor-pointer">Es Becado al 100%</label>
                      </div>
                    </div>

                    <div className="form-row">
                      <div className="form-group">
                        <label className="form-label text-warning">Porcentaje de Beca / Media Beca (Opcional)</label>
                        <input type="number" step="0.1" max="100" className="form-control" placeholder="Ej. 50" value={formData.scholarshipPercentage} onChange={e => setFormData({...formData, scholarshipPercentage: e.target.value})} disabled={formData.isScholarship || formData.isGuest} />
                        <span className="text-xs text-text-muted mt-1 inline-block">Si el alumno tiene un % de descuento. Inhabilidato si es Invitado o Becado al 100%.</span>
                      </div>
                    </div>

                    <div className="form-group bg-bg-dark p-4 rounded-lg border border-border/50">
                      <label className="form-label text-warning mb-2">Sugerencia Automática de Categoría *</label>
                      {(!formData.dateOfBirth || !formData.headquarterId) ? (
                        <p className="text-sm text-text-muted italic">Complete la fecha de nacimiento y sede para ver categorías disponibles.</p>
                      ) : availableCategories.length === 0 ? (
                        <p className="text-sm text-danger italic">No hay categorías disponibles para esta edad en la sede seleccionada.</p>
                      ) : (
                        <select required className="form-control border-warning/50 focus:border-warning focus:ring-warning/20" value={formData.categoryId} onChange={e => setFormData({...formData, categoryId: e.target.value})}>
                          <option value="">-- Asigne la Categoría --</option>
                          {availableCategories.map(c => <option key={c.id} value={c.id}>{c.name} ({c.startDateOfBirth?.split('T')[0]} a {c.endDateOfBirth?.split('T')[0]})</option>)}
                        </select>
                      )}
                    </div>
                  </div>
                )}

                {/* STEP 2: APODERADO */}
                {currentStep === 2 && (
                  <div className="fade-in">
                    <h4 className="text-warning font-bold mb-4 flex items-center gap-2"><UserPlus size={18}/> Datos del Apoderado</h4>
                    <p className="text-sm text-text-secondary mb-4">El apoderado será registrado como usuario tipo Guardian y podrá tener a cargo múltiples alumnos.</p>
                    
                    <div className="form-row">
                      <div className="form-group">
                        <label className="form-label">Nombres del Apoderado</label>
                        <input type="text" className="form-control" value={formData.guardianFirstName} onChange={e => setFormData({...formData, guardianFirstName: e.target.value})} />
                      </div>
                      <div className="form-group">
                        <label className="form-label">Apellidos del Apoderado</label>
                        <input type="text" className="form-control" value={formData.guardianLastName} onChange={e => setFormData({...formData, guardianLastName: e.target.value})} />
                      </div>
                    </div>

                    <div className="form-row">
                      <div className="form-group">
                        <label className="form-label">Celular de Contacto</label>
                        <input type="tel" className="form-control" placeholder="Ej. 987654321" value={formData.guardianPhone} onChange={e => setFormData({...formData, guardianPhone: e.target.value})} />
                      </div>
                      <div className="form-group">
                        <label className="form-label">Correo Electrónico</label>
                        <input type="email" className="form-control" placeholder="Para notificaciones y acceso" value={formData.guardianEmail} onChange={e => setFormData({...formData, guardianEmail: e.target.value})} />
                      </div>
                    </div>
                  </div>
                )}

                {/* STEP 3: FICHA MEDICA */}
                {currentStep === 3 && (
                  <div className="fade-in">
                    <h4 className="text-success font-bold mb-4 flex items-center gap-2"><Activity size={18}/> Ficha Médica y Nutricional (Opcional)</h4>
                    
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-4">
                      <div className="bg-bg-dark p-4 rounded-lg border border-border">
                        <h5 className="text-sm font-bold text-primary-400 mb-3 border-b border-border/50 pb-2">Datos Médicos</h5>
                        <div className="form-group mb-3">
                          <label className="form-label text-xs">Alergias (Medicamentos, alimentos)</label>
                          <textarea className="form-control text-sm" rows="2" value={formData.medicalRecord.allergies} onChange={e => setFormData({...formData, medicalRecord: {...formData.medicalRecord, allergies: e.target.value}})}></textarea>
                        </div>
                        <div className="form-group">
                          <label className="form-label text-xs">Condiciones Médicas / Lesiones previas</label>
                          <textarea className="form-control text-sm" rows="2" value={formData.medicalRecord.medicalConditions} onChange={e => setFormData({...formData, medicalRecord: {...formData.medicalRecord, medicalConditions: e.target.value}})}></textarea>
                        </div>
                      </div>

                      <div className="bg-bg-dark p-4 rounded-lg border border-border">
                        <h5 className="text-sm font-bold text-success mb-3 border-b border-border/50 pb-2">Datos Nutricionales</h5>
                        <div className="flex gap-2 mb-3">
                          <div className="form-group flex-1">
                            <label className="form-label text-xs">Peso (Kg)</label>
                            <input type="number" step="0.1" className="form-control text-sm" value={formData.medicalRecord.weightKg} onChange={e => setFormData({...formData, medicalRecord: {...formData.medicalRecord, weightKg: e.target.value}})} />
                          </div>
                          <div className="form-group flex-1">
                            <label className="form-label text-xs">Altura (Cm)</label>
                            <input type="number" step="0.1" className="form-control text-sm" value={formData.medicalRecord.heightCm} onChange={e => setFormData({...formData, medicalRecord: {...formData.medicalRecord, heightCm: e.target.value}})} />
                          </div>
                        </div>
                        <div className="flex gap-2 mb-3">
                          <div className="form-group flex-1">
                            <label className="form-label text-xs text-warning">IMC (Automático)</label>
                            <input type="text" readOnly className="form-control text-sm bg-bg-dark border-border" value={formData.medicalRecord.bmi || ''} />
                          </div>
                        </div>
                        <div className="form-group">
                          <label className="form-label text-xs">Plan Nutricional / Dieta</label>
                          <input type="text" className="form-control text-sm" value={formData.medicalRecord.nutritionPlan} onChange={e => setFormData({...formData, medicalRecord: {...formData.medicalRecord, nutritionPlan: e.target.value}})} />
                        </div>
                      </div>
                    </div>

                    <div className="bg-danger/10 p-4 rounded-lg border border-danger/30">
                      <h5 className="text-sm font-bold text-danger mb-3">Contactos de Emergencia</h5>
                      <div className="form-row m-0">
                        <div className="form-group mb-0">
                          <label className="form-label text-xs">Nombre Completo</label>
                          <input type="text" className="form-control text-sm" value={formData.medicalRecord.emergencyContactName} onChange={e => setFormData({...formData, medicalRecord: {...formData.medicalRecord, emergencyContactName: e.target.value}})} />
                        </div>
                        <div className="form-group mb-0">
                          <label className="form-label text-xs">Teléfono</label>
                          <input type="tel" className="form-control text-sm" value={formData.medicalRecord.emergencyContactPhone} onChange={e => setFormData({...formData, medicalRecord: {...formData.medicalRecord, emergencyContactPhone: e.target.value}})} />
                        </div>
                      </div>
                    </div>
                  </div>
                )}
                </fieldset>
                
                <div className="modal-footer mt-6 flex justify-between">
                  {currentStep > 1 ? (
                    <button type="button" onClick={() => setCurrentStep(currentStep - 1)} className="btn btn-ghost">Atrás</button>
                  ) : (
                    <button type="button" onClick={() => setShowModal(false)} className="btn btn-ghost text-danger">Cancelar</button>
                  )}
                  
                  {currentStep < 3 ? (
                    <button type="submit" className="btn btn-primary">Siguiente Paso</button>
                  ) : (
                    user?.role !== 'Staff' && (
                      <button type="submit" className="btn btn-primary">Finalizar y Guardar</button>
                    )
                  )}
                </div>
              </form>
            </div>
          </div>
        )}

        {showNutritionModal && nutritionStudent && (
          <div className="modal-overlay" onClick={() => setShowNutritionModal(false)}>
            <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '800px' }}>
              <div className="flex justify-between items-center mb-4 border-b border-border pb-4">
                <h3 className="modal-title m-0 flex items-center gap-2">
                  <Apple className="text-success" size={20} /> Historial Nutricional - {nutritionStudent.firstName} {nutritionStudent.lastName}
                </h3>
                <button onClick={() => setShowNutritionModal(false)} className="text-text-muted hover:text-danger"><X size={20}/></button>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div className="md:col-span-1">
                  <div className="bg-bg-dark p-4 rounded-lg border border-border">
                    <h5 className="text-sm font-bold text-primary-400 mb-3">Nuevo Registro</h5>
                    <form onSubmit={handleNutritionSubmit}>
                      <div className="form-group mb-3">
                        <label className="form-label text-xs">Fecha</label>
                        <input type="date" required className="form-control text-sm" value={nutritionForm.recordDate} onChange={e => setNutritionForm({...nutritionForm, recordDate: e.target.value})} />
                      </div>
                      <div className="grid grid-cols-2 gap-2 mb-3">
                        <div className="form-group mb-0">
                          <label className="form-label text-xs">Peso (Kg)</label>
                          <input type="number" step="0.1" className="form-control text-sm" value={nutritionForm.weightKg} onChange={e => setNutritionForm({...nutritionForm, weightKg: e.target.value})} />
                        </div>
                        <div className="form-group mb-0">
                          <label className="form-label text-xs">Altura (Cm)</label>
                          <input type="number" step="0.1" className="form-control text-sm" value={nutritionForm.heightCm} onChange={e => setNutritionForm({...nutritionForm, heightCm: e.target.value})} />
                        </div>
                      </div>
                      <div className="grid grid-cols-2 gap-2 mb-3">
                        <div className="form-group mb-0">
                          <label className="form-label text-xs">% Grasa</label>
                          <input type="number" step="0.1" className="form-control text-sm" value={nutritionForm.fatPercentage} onChange={e => setNutritionForm({...nutritionForm, fatPercentage: e.target.value})} />
                        </div>
                        <div className="form-group mb-0">
                          <label className="form-label text-xs">% Músculo</label>
                          <input type="number" step="0.1" className="form-control text-sm" value={nutritionForm.muscleMassPercentage} onChange={e => setNutritionForm({...nutritionForm, muscleMassPercentage: e.target.value})} />
                        </div>
                      </div>
                      <div className="form-group mb-3">
                        <label className="form-label text-xs">Notas / Dieta</label>
                        <textarea className="form-control text-sm" rows="2" value={nutritionForm.notes} onChange={e => setNutritionForm({...nutritionForm, notes: e.target.value})}></textarea>
                      </div>
                      <button type="submit" className="btn btn-primary btn-sm w-full">Guardar Registro</button>
                    </form>
                  </div>
                </div>

                <div className="md:col-span-2">
                  <div className="bg-bg-dark p-4 rounded-lg border border-border h-full max-h-[400px] overflow-y-auto custom-scrollbar">
                    <h5 className="text-sm font-bold text-success mb-3">Historial ({nutritionRecords.length})</h5>
                    {nutritionRecords.length === 0 ? (
                      <div className="text-center p-4 text-text-muted text-sm">No hay registros previos.</div>
                    ) : (
                      <div className="flex flex-col gap-3">
                        {nutritionRecords.map(rec => (
                          <div key={rec.id} className="p-3 bg-bg-surface rounded border border-border/50 text-sm">
                            <div className="flex justify-between text-xs text-text-muted mb-2 border-b border-border/30 pb-1">
                              <span><Calendar size={10} className="inline mr-1"/> {new Date(rec.recordDate).toLocaleDateString()}</span>
                              <span>Reg: {rec.registeredByName || 'Sistema'}</span>
                            </div>
                            <div className="grid grid-cols-3 md:grid-cols-5 gap-2 text-center">
                              <div>
                                <span className="block text-xs text-text-muted">Peso</span>
                                <strong className="text-primary-100">{rec.weightKg || '-'} kg</strong>
                              </div>
                              <div>
                                <span className="block text-xs text-text-muted">Altura</span>
                                <strong>{rec.heightCm || '-'} cm</strong>
                              </div>
                              <div>
                                <span className="block text-xs text-text-muted">IMC</span>
                                <strong>{rec.bmi || '-'}</strong>
                              </div>
                              <div>
                                <span className="block text-xs text-text-muted">% Grasa</span>
                                <strong className="text-danger">{rec.fatPercentage || '-'}%</strong>
                              </div>
                              <div>
                                <span className="block text-xs text-text-muted">% Músculo</span>
                                <strong className="text-success">{rec.muscleMassPercentage || '-'}%</strong>
                              </div>
                            </div>
                            {rec.notes && (
                              <div className="mt-2 text-xs text-text-secondary bg-bg-dark/50 p-2 rounded">
                                <span className="font-bold">Notas: </span> {rec.notes}
                              </div>
                            )}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}

        {showFinancialModal && financialStudent && (
          <div className="modal-overlay" onClick={() => setShowFinancialModal(false)}>
            <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '800px' }}>
              <div className="flex justify-between items-center mb-4 border-b border-border pb-4">
                <h3 className="modal-title m-0 flex items-center gap-2">
                  <FileText className="text-primary" size={20} /> Historial Financiero - {financialStudent.firstName} {financialStudent.lastName}
                </h3>
                <button onClick={() => setShowFinancialModal(false)} className="text-text-muted hover:text-danger"><X size={20}/></button>
              </div>

              <div className="flex items-center gap-3 mb-4">
                <select className="form-control text-sm py-2 w-auto" value={finFilterMonth} onChange={e => setFinFilterMonth(e.target.value)}>
                  <option value="">Todos los meses</option>
                  {['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'].map((m, i) => (
                    <option key={i+1} value={i+1}>{m}</option>
                  ))}
                </select>
                <select className="form-control text-sm py-2 w-auto" value={finFilterYear} onChange={e => setFinFilterYear(e.target.value)}>
                  <option value="">Todos los años</option>
                  {[2024, 2025, 2026, 2027].map(y => <option key={y} value={y}>{y}</option>)}
                </select>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="bg-bg-dark p-4 rounded-lg border border-border">
                  <h5 className="font-bold text-warning mb-3">Mensualidades ({financialData.debts.length})</h5>
                  <div className="max-h-[300px] overflow-y-auto custom-scrollbar flex flex-col gap-2 pr-1">
                    {financialData.debts.length === 0 && <div className="text-sm text-text-muted">No hay cobros registrados.</div>}
                    {financialData.debts.map(d => (
                      <div key={d.id} className="p-3 bg-bg-surface rounded border border-border/50 text-sm">
                        <div className="flex justify-between font-bold text-primary-100 mb-1">
                          <span>{d.description}</span>
                          <span>S/. {d.amount.toFixed(2)}</span>
                        </div>
                        <div className="flex justify-between text-xs text-text-muted">
                          <span>Vence: {new Date(d.dueDate).toLocaleDateString()}</span>
                          <span className={d.isPaid ? 'text-success font-bold' : 'text-danger font-bold'}>{d.isPaid ? 'Pagado' : 'Pendiente'}</span>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="bg-bg-dark p-4 rounded-lg border border-border">
                  <h5 className="font-bold text-success mb-3">Compras en Tienda ({financialData.sales.length})</h5>
                  <div className="max-h-[300px] overflow-y-auto custom-scrollbar flex flex-col gap-2 pr-1">
                    {financialData.sales.length === 0 && <div className="text-sm text-text-muted">No hay compras registradas.</div>}
                    {financialData.sales.map(s => (
                      <div key={s.id} className="p-3 bg-bg-surface rounded border border-border/50 text-sm">
                        <div className="flex justify-between font-bold text-primary-100 mb-1">
                          <span>{s.productName} (x{s.quantity})</span>
                          <span>S/. {s.totalPrice.toFixed(2)}</span>
                        </div>
                        <div className="flex justify-between text-xs text-text-muted">
                          <span>Fecha: {new Date(s.saleDate).toLocaleDateString()}</span>
                          {s.isGift && <span className="text-warning">Obsequio</span>}
                          {s.discountAmount > 0 && !s.isGift && <span className="text-warning">Desc: S/. {s.discountAmount.toFixed(2)}</span>}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
