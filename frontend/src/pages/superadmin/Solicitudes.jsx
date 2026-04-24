import { useEffect, useState } from 'react';
import AppLayout from '../../components/AppLayout';
import api from '../../api/axios';
import toast from 'react-hot-toast';
import { Eye, CheckCircle, XCircle, Search, Clock, Filter, Edit2 } from 'lucide-react';

const STATUS_BADGE = {
  Pending: <span className="badge badge-warning"><Clock size={10}/>&nbsp;Pendiente</span>,
  Approved: <span className="badge badge-success"><CheckCircle size={10}/>&nbsp;Aprobada</span>,
  Rejected: <span className="badge badge-danger"><XCircle size={10}/>&nbsp;Rechazada</span>,
};

export default function SolicitudesPage() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState('');
  const [search, setSearch] = useState('');
  const [detail, setDetail] = useState(null);
  const [modal, setModal] = useState(null); // {type: 'approve'|'reject', id, name}
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [editForm, setEditForm] = useState({});

  const load = () => {
    setLoading(true);
    api.get('/super-admin/registration-requests', { params: filter ? { status: filter } : {} })
      .then(r => setItems(r.data))
      .finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, [filter]);

  const openDetail = async id => {
    const r = await api.get(`/super-admin/registration-requests/${id}`);
    setDetail(r.data);
    setEditMode(false);
  };

  const handleEditChange = (e) => setEditForm({ ...editForm, [e.target.name]: e.target.value });

  const submitEdit = async () => {
    setSubmitting(true);
    try {
      await api.put(`/super-admin/registration-requests/${detail.id}`, editForm);
      toast.success('Solicitud actualizada.');
      setEditMode(false);
      openDetail(detail.id);
      load();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al actualizar');
    } finally {
      setSubmitting(false);
    }
  };

  const approve = async () => {
    if (!modal) return;
    setSubmitting(true);
    try {
      const r = await api.post(`/super-admin/registration-requests/${modal.id}/approve`, { reviewNotes: reason });
      toast.success(`Academia "${modal.name}" aprobada.\nContraseña temporal: ${r.data.tempPassword}`, { duration: 8000 });
      setModal(null); setDetail(null); setReason(''); load();
    } catch (err) { toast.error(err.response?.data?.message || 'Error'); }
    finally { setSubmitting(false); }
  };

  const reject = async () => {
    if (!reason.trim()) { toast.error('Ingrese un motivo de rechazo'); return; }
    setSubmitting(true);
    try {
      await api.post(`/super-admin/registration-requests/${modal.id}/reject`, { reason });
      toast.success('Solicitud rechazada.');
      setModal(null); setDetail(null); setReason(''); load();
    } catch (err) { toast.error(err.response?.data?.message || 'Error'); }
    finally { setSubmitting(false); }
  };

  const filtered = items.filter(i =>
    i.academyName.toLowerCase().includes(search.toLowerCase()) ||
    i.contactEmail.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <AppLayout title="Solicitudes de Registro">
      <div className="card">
        <div className="card-header">
          <h3 className="card-title">Solicitudes de Academia ({items.length})</h3>
          <div className="flex gap-2 items-center">
            <div className="search-bar">
              <Search size={15} />
              <input className="form-control" placeholder="Buscar..." value={search}
                onChange={e => setSearch(e.target.value)} style={{ width: 220 }} />
            </div>
            <select className="form-control" value={filter} onChange={e => setFilter(e.target.value)} style={{ width: 160 }}>
              <option value="">Todos</option>
              <option value="Pending">Pendientes</option>
              <option value="Approved">Aprobadas</option>
              <option value="Rejected">Rechazadas</option>
            </select>
          </div>
        </div>

        {loading ? <div className="text-center" style={{ padding: 40 }}><span className="spinner" style={{ margin: 'auto', display: 'block', borderTopColor: 'var(--primary)' }} /></div>
        : filtered.length === 0 ? <div className="empty-state"><ClipboardList size={40} /><p>No hay solicitudes</p></div>
        : <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Academia</th><th>Contacto</th><th>Ciudad</th><th>Deporte</th><th>Fecha</th><th>Estado</th><th>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map(r => (
                <tr key={r.id}>
                  <td>{r.academyName}</td>
                  <td><div>{r.contactName}</div><div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{r.contactEmail}</div></td>
                  <td>{r.city || '—'}</td>
                  <td>{r.sport || '—'}</td>
                  <td>{new Date(r.createdAt).toLocaleDateString('es')}</td>
                  <td>{STATUS_BADGE[r.status]}</td>
                  <td>
                    <div className="flex gap-2">
                      <button className="btn btn-ghost btn-sm" onClick={() => openDetail(r.id)} title="Ver detalle"><Eye size={13} /></button>
                      {r.status === 'Pending' && <>
                        <button className="btn btn-success btn-sm" onClick={() => { setModal({ type: 'approve', id: r.id, name: r.academyName }); setReason(''); }}>Aprobar</button>
                        <button className="btn btn-danger btn-sm" onClick={() => { setModal({ type: 'reject', id: r.id, name: r.academyName }); setReason(''); }}>Rechazar</button>
                      </>}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>}
      </div>

      {/* Detail/Edit Modal */}
      {detail && (
        <div className="modal-overlay" onClick={() => { setDetail(null); setEditMode(false); }}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
              <h3 className="modal-title" style={{ marginBottom: 0 }}>
                {editMode ? 'Editar Solicitud' : 'Detalle de Solicitud'}
              </h3>
              {!editMode && (
                <button className="btn btn-ghost btn-sm" onClick={() => { setEditMode(true); setEditForm(detail); }}>
                  <Edit2 size={15} /> Editar
                </button>
              )}
            </div>

            {editMode ? (
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px 24px', marginBottom: 16 }}>
                <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                  <label className="form-label">Nombre de la Academia</label>
                  <input name="academyName" className="form-control" value={editForm.academyName || ''} onChange={handleEditChange} />
                </div>
                <div className="form-group">
                  <label className="form-label">Nombre de Contacto</label>
                  <input name="contactName" className="form-control" value={editForm.contactName || ''} onChange={handleEditChange} />
                </div>
                <div className="form-group">
                  <label className="form-label">Email de Contacto</label>
                  <input name="contactEmail" className="form-control" value={editForm.contactEmail || ''} onChange={handleEditChange} />
                </div>
                <div className="form-group">
                  <label className="form-label">Teléfono</label>
                  <input name="contactPhone" className="form-control" value={editForm.contactPhone || ''} onChange={handleEditChange} />
                </div>
                <div className="form-group">
                  <label className="form-label">Deporte</label>
                  <input name="sport" className="form-control" value={editForm.sport || ''} onChange={handleEditChange} />
                </div>
                <div className="form-group">
                  <label className="form-label">Ciudad</label>
                  <input name="city" className="form-control" value={editForm.city || ''} onChange={handleEditChange} />
                </div>
                <div className="form-group">
                  <label className="form-label">País</label>
                  <input name="country" className="form-control" value={editForm.country || ''} onChange={handleEditChange} />
                </div>
                <div className="form-group">
                  <label className="form-label">Sitio Web</label>
                  <input name="website" className="form-control" value={editForm.website || ''} onChange={handleEditChange} />
                </div>
                <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                  <label className="form-label">Descripción</label>
                  <textarea name="description" className="form-control" value={editForm.description || ''} onChange={handleEditChange} rows={2}></textarea>
                </div>
                <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                  <label className="form-label">Información Adicional</label>
                  <textarea name="additionalInfo" className="form-control" value={editForm.additionalInfo || ''} onChange={handleEditChange} rows={2}></textarea>
                </div>
              </div>
            ) : (
              <>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px 24px' }}>
                  {[
                    ['Academia', detail.academyName], ['Estado', STATUS_BADGE[detail.status]],
                    ['Contacto', detail.contactName], ['Email', detail.contactEmail],
                    ['Teléfono', detail.contactPhone || '—'], ['Deporte', detail.sport || '—'],
                    ['Ciudad', detail.city || '—'], ['País', detail.country || '—'],
                    ['Web', detail.website || '—'], ['Solicitado', new Date(detail.createdAt).toLocaleString('es')],
                  ].map(([k, v]) => (
                    <div key={k}>
                      <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>{k}</div>
                      <div style={{ fontSize: 13 }}>{v}</div>
                    </div>
                  ))}
                </div>
                {detail.description && (
                  <div style={{ marginTop: 16 }}>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 4 }}>Descripción</div>
                    <p style={{ fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.6 }}>{detail.description}</p>
                  </div>
                )}
                {detail.additionalInfo && (
                  <div style={{ marginTop: 12 }}>
                    <div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 4 }}>Info Adicional</div>
                    <p style={{ fontSize: 13, color: 'var(--text-secondary)', lineHeight: 1.6 }}>{detail.additionalInfo}</p>
                  </div>
                )}
                {detail.reviewNotes && (
                  <div className="alert alert-warning" style={{ marginTop: 16 }}>
                    <div><strong>Notas de revisión:</strong> {detail.reviewNotes}</div>
                  </div>
                )}
              </>
            )}

            <div className="modal-footer">
              {editMode ? (
                <>
                  <button className="btn btn-ghost" onClick={() => setEditMode(false)}>Cancelar</button>
                  <button className="btn btn-primary" onClick={submitEdit} disabled={submitting}>
                    {submitting ? <span className="spinner" /> : 'Guardar'}
                  </button>
                </>
              ) : (
                <>
                  {detail.status === 'Pending' && <>
                    <button className="btn btn-success" onClick={() => { setModal({ type: 'approve', id: detail.id, name: detail.academyName }); setDetail(null); }}>Aprobar</button>
                    <button className="btn btn-danger" onClick={() => { setModal({ type: 'reject', id: detail.id, name: detail.academyName }); setDetail(null); }}>Rechazar</button>
                  </>}
                  <button className="btn btn-ghost" onClick={() => setDetail(null)}>Cerrar</button>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Approve/Reject Modal */}
      {modal && (
        <div className="modal-overlay">
          <div className="modal">
            <h3 className="modal-title">
              {modal.type === 'approve' ? '✅ Aprobar Academia' : '❌ Rechazar Solicitud'}
            </h3>
            <p className="text-muted" style={{ marginBottom: 16 }}>
              {modal.type === 'approve'
                ? `Se creará el entorno para "${modal.name}" y se generarán credenciales de acceso.`
                : `Se notificará al solicitante de "${modal.name}" por correo.`}
            </p>
            <div className="form-group">
              <label className="form-label">
                {modal.type === 'approve' ? 'Notas de revisión (opcional)' : 'Motivo de rechazo *'}
              </label>
              <textarea className="form-control" rows={3} value={reason}
                onChange={e => setReason(e.target.value)}
                placeholder={modal.type === 'approve' ? 'Bienvenidos a la plataforma...' : 'Indique el motivo...'} />
            </div>
            <div className="modal-footer">
              <button className="btn btn-ghost" onClick={() => setModal(null)}>Cancelar</button>
              <button
                className={`btn ${modal.type === 'approve' ? 'btn-success' : 'btn-danger'}`}
                onClick={modal.type === 'approve' ? approve : reject}
                disabled={submitting}
              >
                {submitting ? <span className="spinner" /> : (modal.type === 'approve' ? 'Confirmar Aprobación' : 'Confirmar Rechazo')}
              </button>
            </div>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
