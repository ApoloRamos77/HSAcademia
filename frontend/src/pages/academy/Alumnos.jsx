import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { PlusCircle, Search, Users, Activity, FileText, Calendar, MapPin, UserPlus } from 'lucide-react';
import toast from 'react-hot-toast';

export default function Alumnos() {
  const [alumnos, setAlumnos] = useState([]);
  const [sedes, setSedes] = useState([]);
  const [categorias, setCategorias] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [currentStep, setCurrentStep] = useState(1); // 1: Alumno, 2: Apoderado, 3: Ficha Médica
  
  const initialForm = {
    firstName: '', lastName: '', email: '', dateOfBirth: '', headquarterId: '', categoryId: '',
    enrollmentDate: new Date().toISOString().split('T')[0], preferentialFee: '',
    isGuest: false, isScholarship: false, scholarshipPercentage: '',
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
    const matchAge = currentAge !== null ? (currentAge >= c.minAge && currentAge <= c.maxAge) : true;
    return matchHq && matchAge;
  });

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (currentStep < 3) {
      setCurrentStep(currentStep + 1);
      return;
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
            <button onClick={() => { setFormData(initialForm); setEditingId(null); setCurrentStep(1); setShowModal(true); }} className="btn btn-primary">
              <PlusCircle size={16} /> Registrar Alumno
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {alumnos.map(alumno => (
              <div key={alumno.id} className="card p-5" style={{ background: 'var(--bg-surface)' }}>
                <div className="flex justify-between items-start mb-4">
                  <div>
                    <h3 className="font-bold text-lg text-primary-100">{alumno.firstName} {alumno.lastName}</h3>
                    <div className="flex items-center gap-1 text-xs text-text-muted mt-1">
                      <Calendar size={12} /> {alumno.age} años
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
                  <button className="btn btn-ghost btn-sm text-primary" onClick={async () => {
                      try {
                        const recRes = await api.get(`/students/${alumno.id}/medical-record`);
                        setFormData({
                          firstName: alumno.firstName,
                          lastName: alumno.lastName,
                          email: alumno.email || '',
                          dateOfBirth: alumno.dateOfBirth ? alumno.dateOfBirth.split('T')[0] : '',
                          headquarterId: alumno.headquarterId,
                          categoryId: alumno.categoryId,
                          enrollmentDate: alumno.enrollmentDate ? alumno.enrollmentDate.split('T')[0] : '',
                          preferentialFee: alumno.preferentialFee || '',
                          isGuest: alumno.isGuest || false,
                          isScholarship: alumno.isScholarship || false,
                          scholarshipPercentage: alumno.scholarshipPercentage || '',
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
                  }} title="Editar Alumno">Editar</button>
                </div>
              </div>
            ))}
          </div>
          {alumnos.length === 0 && <div className="empty-state"><Users size={40}/><p>No hay alumnos registrados</p></div>}
        </div>

        {showModal && (
          <div className="modal-overlay" onClick={() => setShowModal(false)}>
            <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: '700px' }}>
              <div className="flex justify-between items-center mb-6 border-b border-border pb-4">
                <h3 className="modal-title m-0">{editingId ? 'Editar Alumno' : 'Registrar Nuevo Alumno'}</h3>
                <div className="flex items-center gap-2 text-sm">
                  <span className={`px-2 py-1 rounded ${currentStep === 1 ? 'bg-primary text-bg-dark font-bold' : 'bg-bg-surface text-text-muted'}`}>1. Alumno</span>
                  <span className={`px-2 py-1 rounded ${currentStep === 2 ? 'bg-primary text-bg-dark font-bold' : 'bg-bg-surface text-text-muted'}`}>2. Apoderado</span>
                  <span className={`px-2 py-1 rounded ${currentStep === 3 ? 'bg-primary text-bg-dark font-bold' : 'bg-bg-surface text-text-muted'}`}>3. Ficha Médica</span>
                </div>
              </div>
              
              <form onSubmit={handleSubmit}>
                
                {/* STEP 1: ALUMNO */}
                {currentStep === 1 && (
                  <div className="fade-in">
                    <h4 className="text-primary-400 font-bold mb-4 flex items-center gap-2"><Users size={18}/> Datos Personales del Alumno</h4>
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
                        <label className="form-label">Correo Electrónico (Opcional) *</label>
                        <input type="email" className="form-control" value={formData.email} onChange={e => setFormData({...formData, email: e.target.value})} placeholder="Para su acceso al app" />
                      </div>
                      <div className="form-group">
                        <label className="form-label">Fecha de Nacimiento *</label>
                        <input required type="date" className="form-control" value={formData.dateOfBirth} onChange={e => setFormData({...formData, dateOfBirth: e.target.value})} />
                        {currentAge !== null && (
                          <span className="text-xs text-primary-400 mt-1 inline-block">Edad calculada: {currentAge} años</span>
                        )}
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
                      <div className="form-group">
                        <label className="form-label">Fecha de Inicio *</label>
                        <input required type="date" className="form-control" value={formData.enrollmentDate} onChange={e => setFormData({...formData, enrollmentDate: e.target.value})} />
                      </div>
                    </div>

                    <div className="form-row">
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
                          {availableCategories.map(c => <option key={c.id} value={c.id}>{c.name} (Edades: {c.minAge}-{c.maxAge})</option>)}
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
                        <label className="form-label">Nombres del Apoderado *</label>
                        <input required type="text" className="form-control" value={formData.guardianFirstName} onChange={e => setFormData({...formData, guardianFirstName: e.target.value})} />
                      </div>
                      <div className="form-group">
                        <label className="form-label">Apellidos del Apoderado *</label>
                        <input required type="text" className="form-control" value={formData.guardianLastName} onChange={e => setFormData({...formData, guardianLastName: e.target.value})} />
                      </div>
                    </div>

                    <div className="form-row">
                      <div className="form-group">
                        <label className="form-label">Celular de Contacto *</label>
                        <input required type="tel" className="form-control" placeholder="Ej. 987654321" value={formData.guardianPhone} onChange={e => setFormData({...formData, guardianPhone: e.target.value})} />
                      </div>
                      <div className="form-group">
                        <label className="form-label">Correo Electrónico *</label>
                        <input required type="email" className="form-control" placeholder="Para notificaciones y acceso" value={formData.guardianEmail} onChange={e => setFormData({...formData, guardianEmail: e.target.value})} />
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
                
                <div className="modal-footer mt-6 flex justify-between">
                  {currentStep > 1 ? (
                    <button type="button" onClick={() => setCurrentStep(currentStep - 1)} className="btn btn-ghost">Atrás</button>
                  ) : (
                    <button type="button" onClick={() => setShowModal(false)} className="btn btn-ghost text-danger">Cancelar</button>
                  )}
                  
                  <button type="submit" className="btn btn-primary">
                    {currentStep < 3 ? 'Siguiente Paso' : 'Finalizar y Guardar'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
