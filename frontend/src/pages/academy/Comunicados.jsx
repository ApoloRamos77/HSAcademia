import { useEffect, useState } from 'react';
import { Megaphone, Pin, PlusCircle, Trash2, X } from 'lucide-react';
import toast from 'react-hot-toast';
import AppLayout from '../../components/AppLayout';
import ConfirmDialog from '../../components/ConfirmDialog';
import api from '../../api/axios';

const dateFmt = new Intl.DateTimeFormat('es-PE', { day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' });
const emptyForm = { title: '', content: '', isPinned: false };

/**
 * Comunicados de la academia: los ven alumnos y apoderados en la app móvil y en su portal.
 */
export default function Comunicados() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [saving, setSaving] = useState(false);
  const [toDelete, setToDelete] = useState(null);

  const load = () => {
    setLoading(true);
    api.get('/announcement')
      .then(res => setItems(res.data))
      .catch(() => toast.error('No se pudieron cargar los comunicados.'))
      .finally(() => setLoading(false));
  };
  useEffect(load, []);

  const submit = async e => {
    e.preventDefault();
    if (!form.title.trim() || !form.content.trim()) return toast.error('Completa el título y el mensaje.');
    setSaving(true);
    try {
      await api.post('/announcement', { ...form, title: form.title.trim(), content: form.content.trim() });
      toast.success('Comunicado publicado');
      setForm(emptyForm);
      setShowForm(false);
      load();
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo publicar el comunicado.');
    } finally {
      setSaving(false);
    }
  };

  const remove = async () => {
    try {
      await api.delete(`/announcement/${toDelete.id}`);
      toast.success('Comunicado eliminado');
      setItems(list => list.filter(i => i.id !== toDelete.id));
    } catch {
      toast.error('No se pudo eliminar el comunicado.');
    } finally {
      setToDelete(null);
    }
  };

  return (
    <AppLayout title="Comunicados">
      <div className="fade-in flex flex-col gap-5">
        <div className="card">
          <div className="card-header">
            <div>
              <h3 className="card-title flex items-center gap-2"><Megaphone size={20} className="text-primary" /> Comunicados</h3>
              <p className="text-muted mt-1">Avisos para alumnos y apoderados (se muestran en la app y en su portal). Los fijados aparecen primero.</p>
            </div>
            <button className="btn btn-primary" onClick={() => setShowForm(true)}>
              <PlusCircle size={16} /> Nuevo comunicado
            </button>
          </div>

          {loading ? (
            <div className="text-center p-8"><span className="spinner" style={{ borderColor: 'var(--primary)', borderTopColor: 'transparent' }} /></div>
          ) : items.length === 0 ? (
            <div className="empty-state"><Megaphone size={40} /><p>Aún no hay comunicados publicados.</p></div>
          ) : (
            <ul className="flex flex-col gap-3" style={{ listStyle: 'none', margin: 0, padding: 0 }}>
              {items.map(a => (
                <li key={a.id} className="rounded-xl border border-border p-4" style={{ background: a.isPinned ? 'var(--primary-light)' : 'var(--bg-card)' }}>
                  <div className="flex justify-between items-start gap-3">
                    <div className="min-w-0">
                      <h4 className="font-bold text-text-primary flex items-center gap-2 m-0" style={{ fontSize: 16 }}>
                        {a.isPinned && <span className="badge badge-info flex items-center gap-1"><Pin size={12} /> Fijado</span>}
                        {a.title}
                      </h4>
                      <p className="text-xs text-text-muted mt-1 mb-0">{dateFmt.format(new Date(a.datePosted))} · {a.authorName}</p>
                    </div>
                    <button className="btn btn-ghost btn-sm" title="Eliminar" aria-label="Eliminar comunicado" onClick={() => setToDelete(a)}>
                      <Trash2 size={14} />
                    </button>
                  </div>
                  <p className="text-sm text-text-secondary mt-3 mb-0" style={{ whiteSpace: 'pre-wrap' }}>{a.content}</p>
                </li>
              ))}
            </ul>
          )}
        </div>

        {showForm && (
          <div className="modal-overlay" onClick={() => !saving && setShowForm(false)}>
            <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 560 }}>
              <div className="flex justify-between items-center mb-4">
                <h3 className="modal-title m-0">Nuevo comunicado</h3>
                <button className="btn btn-ghost btn-sm" onClick={() => setShowForm(false)} aria-label="Cerrar"><X size={16} /></button>
              </div>
              <form onSubmit={submit}>
                <div className="form-group">
                  <label className="form-label">Título *</label>
                  <input className="form-control" maxLength={150} required value={form.title}
                    placeholder="Ej. Suspensión de entrenamientos por feriado"
                    onChange={e => setForm({ ...form, title: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="form-label">Mensaje *</label>
                  <textarea className="form-control" rows={6} maxLength={4000} required value={form.content}
                    placeholder="Escribe el detalle del aviso para las familias…"
                    onChange={e => setForm({ ...form, content: e.target.value })} />
                  <small className="text-xs text-text-muted">{form.content.length}/4000</small>
                </div>
                <label className="flex items-center gap-2 text-sm" style={{ cursor: 'pointer' }}>
                  <input type="checkbox" checked={form.isPinned} onChange={e => setForm({ ...form, isPinned: e.target.checked })} />
                  Fijar al inicio (para avisos importantes)
                </label>
                <div className="modal-footer">
                  <button type="button" className="btn btn-ghost" onClick={() => setShowForm(false)} disabled={saving}>Cancelar</button>
                  <button type="submit" className="btn btn-primary" disabled={saving}>{saving ? 'Publicando…' : 'Publicar'}</button>
                </div>
              </form>
            </div>
          </div>
        )}

        <ConfirmDialog
          isOpen={!!toDelete}
          title="Eliminar comunicado"
          message={`¿Eliminar "${toDelete?.title}"?`}
          detail="Dejará de mostrarse a alumnos y apoderados."
          confirmLabel="Sí, eliminar"
          onConfirm={remove}
          onCancel={() => setToDelete(null)}
        />
      </div>
    </AppLayout>
  );
}
