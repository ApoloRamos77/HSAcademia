import { useEffect, useState } from 'react';
import AppLayout from '../../components/AppLayout';
import api from '../../api/axios';
import toast from 'react-hot-toast';
import { Eye, PauseCircle, PlayCircle, Trash2, Search, Edit2 } from 'lucide-react';

const STATUS_BADGE = {
  Active: <span className="badge badge-success">Activa</span>,
  Suspended: <span className="badge badge-warning">Suspendida</span>,
  Inactive: <span className="badge badge-danger">Inactiva</span>,
  Pending: <span className="badge badge-muted">Pendiente</span>,
  Rejected: <span className="badge badge-danger">Rechazada</span>,
};

export default function AcademiasPage() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('');
  const [detail, setDetail] = useState(null);
  const [modal, setModal] = useState(null); // {type, id, name}
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [editMode, setEditMode] = useState(false);
  const [editForm, setEditForm] = useState({});

  const load = () => {
    setLoading(true);
    api.get('/super-admin/academies', { params: filter ? { status: filter } : {} })
      .then(r => setItems(r.data)).finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, [filter]);

  const openDetail = async id => {
    const r = await api.get(`/super-admin/academies/${id}`);
    setDetail(r.data);
    setEditMode(false);
  };

  const handleEditChange = (e) => setEditForm({ ...editForm, [e.target.name]: e.target.value });

  const submitEdit = async () => {
    setSubmitting(true);
    try {
      await api.put(`/super-admin/academies/${detail.id}`, editForm);
      toast.success('Academia actualizada.');
      setEditMode(false);
      openDetail(detail.id);
      load();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Error al actualizar');
    } finally {
      setSubmitting(false);
    }
  };

  const execute = async () => {
    if (!modal) return;
    if ((modal.type === 'suspend' || modal.type === 'deactivate') && !reason.trim()) {
      toast.error('El motivo es obligatorio'); return;
    }
    setSubmitting(true);
    try {
      if (modal.type === 'suspend') await api.post(`/super-admin/academies/${modal.id}/suspend`, { reason });
      else if (modal.type === 'reactivate') await api.post(`/super-admin/academies/${modal.id}/reactivate`);
      else if (modal.type === 'deactivate') await api.delete(`/super-admin/academies/${modal.id}`, { data: { reason } });
      toast.success('Operación realizada.');
      setModal(null); setDetail(null); setReason(''); load();
    } catch (err) { toast.error(err.response?.data?.message || 'Error'); }
    finally { setSubmitting(false); }
  };

  const filtered = items.filter(i => i.name.toLowerCase().includes(search.toLowerCase()) || i.contactEmail.toLowerCase().includes(search.toLowerCase()));

  return (
    <AppLayout title="Gestión de Academias">
      <div className="card">
        <div className="card-header">
          <h3 className="card-title">Academias ({items.length})</h3>
          <div className="flex gap-2">
            <div className="search-bar">
              <Search size={15} />
              <input className="form-control" placeholder="Buscar..." value={search}
                onChange={e => setSearch(e.target.value)} style={{ width: 220 }} />
            </div>
            <select className="form-control" value={filter} onChange={e => setFilter(e.target.value)} style={{ width: 160 }}>
              <option value="">Todos los estados</option>
              <option value="Active">Activas</option>
              <option value="Suspended">Suspendidas</option>
              <option value="Inactive">Inactivas</option>
            </select>
          </div>
        </div>

        {loading ? <div className="text-center" style={{ padding: 40 }}><span className="spinner" style={{ margin: 'auto', display: 'block', borderTopColor: 'var(--primary)' }} /></div>
        : filtered.length === 0 ? <div className="empty-state"><p>No hay academias</p></div>
        : <div className="table-wrap">
          <table>
            <thead>
              <tr><th>Academia</th><th>Email</th><th>Ciudad</th><th>Deporte</th><th>Usuarios</th><th>Estado</th><th>Acciones</th></tr>
            </thead>
            <tbody>
              {filtered.map(a => (
                <tr key={a.id}>
                  <td>{a.name}</td>
                  <td>{a.contactEmail}</td>
                  <td>{a.city || '—'}</td>
                  <td>{a.sport || '—'}</td>
                  <td>{a.usersCount}</td>
                  <td>{STATUS_BADGE[a.status] || a.status}</td>
                  <td>
                    <div className="flex gap-2">
                      <button className="btn btn-ghost btn-sm" onClick={() => openDetail(a.id)} title="Ver"><Eye size={13} /></button>
                      {a.status === 'Active' && <button className="btn btn-warning btn-sm" onClick={() => { setModal({ type: 'suspend', id: a.id, name: a.name }); setReason(''); }} title="Suspender"><PauseCircle size={13} /></button>}
                      {a.status === 'Suspended' && <button className="btn btn-success btn-sm" onClick={() => { setModal({ type: 'reactivate', id: a.id, name: a.name }); setReason(''); }} title="Reactivar"><PlayCircle size={13} /></button>}
                      {a.status !== 'Inactive' && <button className="btn btn-danger btn-sm" onClick={() => { setModal({ type: 'deactivate', id: a.id, name: a.name }); setReason(''); }} title="Dar de baja"><Trash2 size={13} /></button>}
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
          <div className="modal" style={{ maxWidth: 640 }} onClick={e => e.stopPropagation()}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
              <h3 className="modal-title" style={{ marginBottom: 0 }}>
                {editMode ? 'Editar Academia' : detail.name}
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
                  <label className="form-label">Nombre</label>
                  <input name="name" className="form-control" value={editForm.name || ''} onChange={handleEditChange} />
                </div>
                <div className="form-group">
                  <label className="form-label">Email de Contacto</label>
                  <input name="contactEmail" className="form-control" value={editForm.contactEmail || ''} onChange={handleEditChange} />
                </div>
                <div className="form-group">
                  <label className="form-label">Teléfono de Contacto</label>
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
                  <label className="form-label">Página Web</label>
                  <input name="website" className="form-control" value={editForm.website || ''} onChange={handleEditChange} />
                </div>
                <div className="form-group" style={{ gridColumn: '1 / -1' }}>
                  <label className="form-label">Descripción</label>
                  <textarea name="description" className="form-control" value={editForm.description || ''} onChange={handleEditChange} rows={3}></textarea>
                </div>
              </div>
            ) : (
              <>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px 24px', marginBottom: 16 }}>
                  {[['Estado', STATUS_BADGE[detail.status]], ['Slug', detail.slugName], ['Email', detail.contactEmail], ['Teléfono', detail.contactPhone || '—'], ['Ciudad', detail.city || '—'], ['Deporte', detail.sport || '—'], ['Aprobada', detail.approvedAt ? new Date(detail.approvedAt).toLocaleDateString('es') : '—']].map(([k, v]) => (
                    <div key={k}><div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>{k}</div><div style={{ fontSize: 13 }}>{v}</div></div>
                  ))}
                </div>
                {detail.description && <div style={{ marginBottom: 16 }}><div style={{ fontSize: 11, color: 'var(--text-muted)', marginBottom: 3 }}>Descripción</div><div style={{ fontSize: 13 }}>{detail.description}</div></div>}
                {detail.suspensionReason && <div className="alert alert-warning">Motivo suspensión: {detail.suspensionReason}</div>}
                {detail.users?.length > 0 && (
                  <div>
                    <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 8, fontWeight: 600 }}>USUARIOS ({detail.users.length})</div>
                    <div className="table-wrap">
                      <table>
                        <thead><tr><th>Nombre</th><th>Email</th><th>Rol</th><th>Estado</th></tr></thead>
                        <tbody>
                          {detail.users.map(u => <tr key={u.id}><td>{u.fullName}</td><td>{u.email}</td><td>{u.role}</td><td>{u.status}</td></tr>)}
                        </tbody>
                      </table>
                    </div>
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
                  {detail.status === 'Active' && <button className="btn btn-warning btn-sm" onClick={() => { setModal({ type: 'suspend', id: detail.id, name: detail.name }); setDetail(null); }}>Suspender</button>}
                  {detail.status === 'Suspended' && <button className="btn btn-success btn-sm" onClick={() => { setModal({ type: 'reactivate', id: detail.id, name: detail.name }); setDetail(null); }}>Reactivar</button>}
                  {detail.status !== 'Inactive' && <button className="btn btn-danger btn-sm" onClick={() => { setModal({ type: 'deactivate', id: detail.id, name: detail.name }); setDetail(null); }}>Dar de baja</button>}
                  <button className="btn btn-ghost" onClick={() => setDetail(null)}>Cerrar</button>
                </>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Action Modal */}
      {modal && (
        <div className="modal-overlay">
          <div className="modal">
            <h3 className="modal-title">
              {modal.type === 'suspend' ? '⚠️ Suspender Academia' : modal.type === 'reactivate' ? '✅ Reactivar Academia' : '🚫 Dar de Baja Academia'}
            </h3>
            <p className="text-muted" style={{ marginBottom: 16 }}>
              {modal.type === 'reactivate'
                ? `Se reactivará "${modal.name}" y todos sus usuarios.`
                : `Esta acción afectará a "${modal.name}" y todos sus usuarios.`}
            </p>
            {modal.type !== 'reactivate' && (
              <div className="form-group">
                <label className="form-label">Motivo *</label>
                <textarea className="form-control" rows={3} value={reason} onChange={e => setReason(e.target.value)} placeholder="Indique el motivo..." />
              </div>
            )}
            <div className="modal-footer">
              <button className="btn btn-ghost" onClick={() => setModal(null)}>Cancelar</button>
              <button className={`btn ${modal.type === 'reactivate' ? 'btn-success' : 'btn-danger'}`} onClick={execute} disabled={submitting}>
                {submitting ? <span className="spinner" /> : 'Confirmar'}
              </button>
            </div>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
