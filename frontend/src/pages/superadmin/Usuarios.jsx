import { useEffect, useState } from 'react';
import AppLayout from '../../components/AppLayout';
import api from '../../api/axios';
import toast from 'react-hot-toast';
import { PauseCircle, PlayCircle, Trash2, Search } from 'lucide-react';

const STATUS_BADGE = {
  Active: <span className="badge badge-success">Activo</span>,
  Suspended: <span className="badge badge-warning">Suspendido</span>,
  Inactive: <span className="badge badge-danger">Inactivo</span>,
};

const ROLE_LABEL = { SuperAdmin: 'Super Admin', AcademyAdmin: 'Admin Academia', Staff: 'Personal', Student: 'Alumno', Guardian: 'Apoderado' };

export default function UsuariosPage() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [filter, setFilter] = useState('');
  const [modal, setModal] = useState(null);
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const load = () => {
    setLoading(true);
    api.get('/super-admin/users', { params: filter ? { role: filter } : {} })
      .then(r => setItems(r.data)).finally(() => setLoading(false));
  };

  useEffect(() => { load(); }, [filter]);

  const execute = async () => {
    if (!modal) return;
    if (modal.type !== 'reactivate' && !reason.trim()) { toast.error('El motivo es obligatorio'); return; }
    setSubmitting(true);
    try {
      if (modal.type === 'suspend') await api.post(`/super-admin/users/${modal.id}/suspend`, { reason });
      else if (modal.type === 'reactivate') await api.post(`/super-admin/users/${modal.id}/reactivate`);
      else await api.delete(`/super-admin/users/${modal.id}`, { data: { reason } });
      toast.success('Operación realizada.');
      setModal(null); setReason(''); load();
    } catch (err) { toast.error(err.response?.data?.message || 'Error'); }
    finally { setSubmitting(false); }
  };

  const filtered = items.filter(i =>
    i.fullName.toLowerCase().includes(search.toLowerCase()) ||
    i.email.toLowerCase().includes(search.toLowerCase())
  );

  return (
    <AppLayout title="Gestión de Usuarios">
      <div className="card">
        <div className="card-header">
          <h3 className="card-title">Usuarios ({items.length})</h3>
          <div className="flex gap-2">
            <div className="search-bar">
              <Search size={15} />
              <input className="form-control" placeholder="Buscar..." value={search}
                onChange={e => setSearch(e.target.value)} style={{ width: 220 }} />
            </div>
            <select className="form-control" value={filter} onChange={e => setFilter(e.target.value)} style={{ width: 180 }}>
              <option value="">Todos los roles</option>
              <option value="AcademyAdmin">Admin Academia</option>
              <option value="Staff">Personal</option>
              <option value="Student">Alumno</option>
            </select>
          </div>
        </div>

        {loading ? <div className="text-center" style={{ padding: 40 }}><span className="spinner" style={{ margin: 'auto', display: 'block', borderTopColor: 'var(--primary)' }} /></div>
        : filtered.length === 0 ? <div className="empty-state"><p>No hay usuarios</p></div>
        : <div className="table-wrap">
          <table>
            <thead>
              <tr><th>Nombre</th><th>Email</th><th>Rol</th><th>Academia</th><th>Último acceso</th><th>Estado</th><th>Acciones</th></tr>
            </thead>
            <tbody>
              {filtered.map(u => (
                <tr key={u.id}>
                  <td>{u.fullName}</td>
                  <td style={{ color: 'var(--text-muted)', fontSize: 12 }}>{u.email}</td>
                  <td><span className="badge badge-info">{ROLE_LABEL[u.role] || u.role}</span></td>
                  <td>{u.academyName || <span style={{ color: 'var(--text-muted)' }}>—</span>}</td>
                  <td style={{ fontSize: 12, color: 'var(--text-muted)' }}>{u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleDateString('es') : 'Nunca'}</td>
                  <td>{STATUS_BADGE[u.status] || u.status}</td>
                  <td>
                    <div className="flex gap-2">
                      {u.status === 'Active' && <button className="btn btn-warning btn-sm" onClick={() => { setModal({ type: 'suspend', id: u.id, name: u.fullName }); setReason(''); }}><PauseCircle size={13} /></button>}
                      {u.status === 'Suspended' && <button className="btn btn-success btn-sm" onClick={() => { setModal({ type: 'reactivate', id: u.id, name: u.fullName }); setReason(''); }}><PlayCircle size={13} /></button>}
                      {u.status !== 'Inactive' && <button className="btn btn-danger btn-sm" onClick={() => { setModal({ type: 'deactivate', id: u.id, name: u.fullName }); setReason(''); }}><Trash2 size={13} /></button>}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>}
      </div>

      {modal && (
        <div className="modal-overlay">
          <div className="modal">
            <h3 className="modal-title">
              {modal.type === 'suspend' ? '⚠️ Suspender Usuario' : modal.type === 'reactivate' ? '✅ Reactivar Usuario' : '🚫 Dar de Baja Usuario'}
            </h3>
            <p className="text-muted" style={{ marginBottom: 16 }}>
              Usuario: <strong>{modal.name}</strong>
            </p>
            {modal.type !== 'reactivate' && (
              <div className="form-group">
                <label className="form-label">Motivo *</label>
                <textarea className="form-control" rows={3} value={reason} onChange={e => setReason(e.target.value)} />
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
