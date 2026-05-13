import { useState, useEffect } from 'react';
import api from '../../api/axios';
import AppLayout from '../../components/AppLayout';
import { Trophy, Plus, Edit, Trash2, X, RefreshCw } from 'lucide-react';
import toast from 'react-hot-toast';
import ConfirmDialog from '../../components/ConfirmDialog';

export default function Torneos() {
  const [tournaments, setTournaments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [confirmDelete, setConfirmDelete] = useState(null); // null | tournament id

  const [showModal, setShowModal] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  const [editingId, setEditingId] = useState(null);
  const [saving, setSaving] = useState(false);
  
  const [form, setForm] = useState({
    name: '',
    organizer: '',
    mainLocation: ''
  });

  const fetchTournaments = async () => {
    try {
      setLoading(true);
      const { data } = await api.get('/calendar/tournaments');
      setTournaments(data);
    } catch {
      toast.error('Error al cargar torneos');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchTournaments();
  }, []);

  const handleSave = async () => {
    if (!form.name || !form.organizer || !form.mainLocation) {
      toast.error('Todos los campos son obligatorios');
      return;
    }

    setSaving(true);
    try {
      if (isEditing) {
        await api.put(`/calendar/tournaments/${editingId}`, form);
        toast.success('Torneo actualizado');
      } else {
        await api.post('/calendar/tournaments', form);
        toast.success('Torneo creado');
      }
      setShowModal(false);
      fetchTournaments();
    } catch {
      toast.error('Error al guardar torneo');
    } finally {
      setSaving(false);
    }
  };

  const openNew = () => {
    setForm({ name: '', organizer: '', mainLocation: '' });
    setIsEditing(false);
    setEditingId(null);
    setShowModal(true);
  };

  const openEdit = (t) => {
    setForm({ name: t.name, organizer: t.organizer, mainLocation: t.mainLocation });
    setIsEditing(true);
    setEditingId(t.id);
    setShowModal(true);
  };

  const handleDelete = (id) => setConfirmDelete(id);

  const confirmDeleteTournament = async () => {
    try {
      await api.delete(`/calendar/tournaments/${confirmDelete}`);
      toast.success('Torneo eliminado');
      fetchTournaments();
    } catch {
      toast.error('Error al eliminar');
    } finally {
      setConfirmDelete(null);
    }
  };

  return (
    <AppLayout>
      <div className="page-header">
        <div>
          <h2 className="page-title"><Trophy size={26} /> Torneos</h2>
          <p className="page-subtitle">Gestiona los torneos en los que participa la academia.</p>
        </div>
        <button className="btn btn-primary" onClick={openNew}>
          <Plus size={16} /> Nuevo Torneo
        </button>
      </div>

      <div className="card" style={{ padding: '1rem' }}>
        {loading ? (
          <p>Cargando...</p>
        ) : tournaments.length === 0 ? (
          <p className="text-muted">No hay torneos registrados.</p>
        ) : (
          <table className="table" style={{ width: '100%', textAlign: 'left', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border)' }}>
                <th style={{ padding: '12px 8px' }}>Nombre</th>
                <th style={{ padding: '12px 8px' }}>Organizador</th>
                <th style={{ padding: '12px 8px' }}>Lugar Principal</th>
                <th style={{ padding: '12px 8px', textAlign: 'right' }}>Acciones</th>
              </tr>
            </thead>
            <tbody>
              {tournaments.map(t => (
                <tr key={t.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td style={{ padding: '12px 8px', fontWeight: 'bold' }}>{t.name}</td>
                  <td style={{ padding: '12px 8px' }}>{t.organizer}</td>
                  <td style={{ padding: '12px 8px' }}>{t.mainLocation}</td>
                  <td style={{ padding: '12px 8px', textAlign: 'right' }}>
                    <button className="btn-icon text-primary" onClick={() => openEdit(t)}><Edit size={16}/></button>
                    <button className="btn-icon text-danger" onClick={() => handleDelete(t.id)}><Trash2 size={16}/></button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={e => e.target === e.currentTarget && setShowModal(false)}>
          <div className="modal-box" style={{ maxWidth: 500 }}>
            <div className="modal-header">
              <h3>{isEditing ? 'Editar Torneo' : 'Nuevo Torneo'}</h3>
              <button className="btn-icon" onClick={() => setShowModal(false)}><X size={18} /></button>
            </div>
            <div className="modal-body">
              <div className="form-group">
                <label>Nombre del Torneo *</label>
                <input className="form-control" value={form.name} onChange={e => setForm({...form, name: e.target.value})} placeholder="Ej: Copa Federacion" />
              </div>
              <div className="form-group">
                <label>Organizador *</label>
                <input className="form-control" value={form.organizer} onChange={e => setForm({...form, organizer: e.target.value})} placeholder="Ej: FPF" />
              </div>
              <div className="form-group">
                <label>Lugar Principal *</label>
                <input className="form-control" value={form.mainLocation} onChange={e => setForm({...form, mainLocation: e.target.value})} placeholder="Ej: Videna" />
              </div>
            </div>
            <div className="modal-footer">
              <button className="btn btn-secondary" onClick={() => setShowModal(false)}>Cancelar</button>
              <button className="btn btn-primary" onClick={handleSave} disabled={saving}>
                {saving ? 'Guardando...' : (isEditing ? 'Actualizar' : 'Crear')}
              </button>
            </div>
          </div>
        </div>
      )}

      <ConfirmDialog
        isOpen={!!confirmDelete}
        title="Eliminar Torneo"
        message="¿Estás seguro de que deseas eliminar este torneo?"
        detail="Esta acción no se puede deshacer."
        confirmLabel="Sí, eliminar"
        onConfirm={confirmDeleteTournament}
        onCancel={() => setConfirmDelete(null)}
      />
    </AppLayout>
  );
}
