import { useEffect, useState } from 'react';
import { Star, X, Trash2, PlusCircle, Trophy, EyeOff } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../api/axios';
import { todayISO } from '../utils/dates';

const DIMENSIONS = [
  ['technical', 'Técnica', 'Control, pase, conducción, definición'],
  ['tactical', 'Táctica', 'Posicionamiento, lectura de juego, decisiones'],
  ['physical', 'Física', 'Resistencia, velocidad, coordinación'],
  ['attitude', 'Actitud', 'Disciplina, compañerismo, compromiso'],
];
const SCALE = { 1: 'En inicio', 2: 'En proceso', 3: 'Logrado', 4: 'Destacado', 5: 'Sobresaliente' };

const defaultPeriod = () => {
  const d = new Date();
  return `${d.getFullYear()} - Trimestre ${Math.floor(d.getMonth() / 3) + 1}`;
};
const emptyForm = () => ({
  period: defaultPeriod(), evaluationDate: todayISO(), technical: 3, tactical: 3, physical: 3, attitude: 3,
  strengths: '', toImprove: '', goals: '', comments: '', isVisibleToFamily: true,
});

/** Evaluaciones deportivas del alumno + resumen de partidos. */
export default function EvaluationsModal({ student, onClose }) {
  const [items, setItems] = useState([]);
  const [sports, setSports] = useState(null);
  const [form, setForm] = useState(null);
  const [saving, setSaving] = useState(false);

  const load = () => {
    api.get(`/sports/students/${student.id}/evaluations`).then(r => setItems(r.data)).catch(() => toast.error('No se pudieron cargar las evaluaciones.'));
    api.get(`/sports/students/${student.id}/summary`).then(r => setSports(r.data)).catch(() => {});
  };
  useEffect(load, [student.id]);

  const save = async e => {
    e.preventDefault();
    setSaving(true);
    try {
      await api.post(`/sports/students/${student.id}/evaluations`, form);
      toast.success('Evaluación registrada');
      setForm(null);
      load();
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo guardar la evaluación.');
    } finally {
      setSaving(false);
    }
  };

  const remove = async id => {
    try {
      await api.delete(`/sports/evaluations/${id}`);
      setItems(list => list.filter(i => i.id !== id));
      toast.success('Evaluación eliminada');
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo eliminar.');
    }
  };

  return (
    <div className="modal-overlay" onClick={() => !saving && onClose()}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 820, width: '95vw' }}>
        <div className="flex justify-between items-center mb-4">
          <h3 className="modal-title m-0 flex items-center gap-2"><Star size={20} /> Evaluación deportiva · {student.firstName} {student.lastName}</h3>
          <button className="btn btn-ghost btn-sm" onClick={onClose} aria-label="Cerrar"><X size={16} /></button>
        </div>

        {sports && sports.matches > 0 && (
          <div className="flex flex-wrap gap-3 mb-4 p-3 rounded-xl" style={{ background: 'var(--bg-surface)' }}>
            <Trophy size={18} className="text-primary" />
            <span className="text-sm"><b>{sports.matches}</b> partidos · <b>{sports.starts}</b> de titular · <b>{sports.minutes}</b> min · <b>{sports.goals}</b> goles · <b>{sports.assists}</b> asistencias
              {sports.averageRating != null && <> · nota media <b>{sports.averageRating}</b></>}
              {(sports.yellowCards > 0 || sports.redCards > 0) && <> · {sports.yellowCards} TA / {sports.redCards} TR</>}</span>
          </div>
        )}

        {form ? (
          <form onSubmit={save}>
            <div className="form-row">
              <div className="form-group">
                <label className="form-label">Periodo *</label>
                <input className="form-control" required maxLength={60} value={form.period} onChange={e => setForm({ ...form, period: e.target.value })} />
              </div>
              <div className="form-group">
                <label className="form-label">Fecha</label>
                <input type="date" className="form-control" max={todayISO()} value={form.evaluationDate} onChange={e => setForm({ ...form, evaluationDate: e.target.value })} />
              </div>
            </div>
            <div className="grid gap-3 mb-3" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))' }}>
              {DIMENSIONS.map(([k, label, hint]) => (
                <div key={k} className="rounded-xl border border-border p-3">
                  <div className="flex justify-between"><b className="text-sm">{label}</b><span className="text-xs text-text-muted">{SCALE[form[k]]}</span></div>
                  <div className="text-xs text-text-muted mb-2">{hint}</div>
                  <div className="flex gap-1" role="radiogroup" aria-label={label}>
                    {[1, 2, 3, 4, 5].map(v => (
                      <button type="button" key={v} role="radio" aria-checked={form[k] === v}
                        className={`btn btn-sm ${form[k] === v ? 'btn-primary' : 'btn-ghost'}`} style={{ minWidth: 36 }}
                        onClick={() => setForm({ ...form, [k]: v })}>{v}</button>
                    ))}
                  </div>
                </div>
              ))}
            </div>
            {[['strengths', 'Fortalezas'], ['toImprove', 'Aspectos por mejorar'], ['goals', 'Objetivos para el siguiente periodo'], ['comments', 'Comentarios internos / para la familia']].map(([k, label]) => (
              <div className="form-group" key={k}>
                <label className="form-label">{label}</label>
                <textarea className="form-control" rows={2} maxLength={k === 'comments' ? 2000 : 1000} value={form[k]} onChange={e => setForm({ ...form, [k]: e.target.value })} />
              </div>
            ))}
            <label className="flex items-center gap-2 text-sm mb-2" style={{ cursor: 'pointer' }}>
              <input type="checkbox" checked={form.isVisibleToFamily} onChange={e => setForm({ ...form, isVisibleToFamily: e.target.checked })} />
              Visible para la familia (app y portal)
            </label>
            <div className="modal-footer">
              <button type="button" className="btn btn-ghost" onClick={() => setForm(null)} disabled={saving}>Cancelar</button>
              <button type="submit" className="btn btn-primary" disabled={saving}>{saving ? 'Guardando…' : 'Guardar evaluación'}</button>
            </div>
          </form>
        ) : (
          <>
            <div className="flex justify-end mb-3">
              <button className="btn btn-primary btn-sm flex items-center gap-1" onClick={() => setForm(emptyForm())}><PlusCircle size={14} /> Nueva evaluación</button>
            </div>
            {items.length === 0 ? (
              <div className="empty-state"><Star size={36} /><p>Aún no hay evaluaciones para este alumno.</p></div>
            ) : (
              <div className="table-wrapper">
                <table className="table">
                  <thead><tr><th>Periodo</th><th>Técnica</th><th>Táctica</th><th>Física</th><th>Actitud</th><th>Promedio</th><th>Evaluador</th><th></th></tr></thead>
                  <tbody>
                    {items.map(ev => (
                      <tr key={ev.id} title={[ev.strengths && `Fortalezas: ${ev.strengths}`, ev.toImprove && `Por mejorar: ${ev.toImprove}`, ev.goals && `Objetivos: ${ev.goals}`].filter(Boolean).join('\n')}>
                        <td>{ev.period}{!ev.isVisibleToFamily && <EyeOff size={12} className="text-text-muted" style={{ marginLeft: 6 }} aria-label="Solo personal" />}
                          <div className="text-xs text-text-muted">{new Date(ev.evaluationDate).toLocaleDateString('es-PE', { timeZone: 'UTC' })}</div></td>
                        <td>{ev.technical}</td><td>{ev.tactical}</td><td>{ev.physical}</td><td>{ev.attitude}</td>
                        <td><b>{ev.average}</b></td>
                        <td className="text-sm">{ev.evaluatorName}</td>
                        <td><button className="btn btn-ghost btn-sm" title="Eliminar" aria-label="Eliminar evaluación" onClick={() => remove(ev.id)}><Trash2 size={14} /></button></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
