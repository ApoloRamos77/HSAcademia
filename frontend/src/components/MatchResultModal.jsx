import { useEffect, useState } from 'react';
import { Trophy, X } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../api/axios';

const num = v => (v === '' || v == null ? 0 : Math.max(0, parseInt(v, 10) || 0));

/**
 * Resultado de un partido y estadísticas por jugador (minutos, goles, asistencias, tarjetas, calificación).
 * El plantel sale de la convocatoria; si no hubo convocatoria, de los alumnos activos de las categorías.
 */
export default function MatchResultModal({ eventId, onClose, onSaved }) {
  const [data, setData] = useState(null);
  const [score, setScore] = useState({ goalsFor: '', goalsAgainst: '', matchNotes: '' });
  const [rows, setRows] = useState([]);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    api.get(`/sports/matches/${eventId}`)
      .then(r => {
        setData(r.data);
        setScore({ goalsFor: r.data.goalsFor ?? '', goalsAgainst: r.data.goalsAgainst ?? '', matchNotes: r.data.matchNotes ?? '' });
        setRows(r.data.players.map(p => ({ ...p })));
      })
      .catch(err => { toast.error(err.response?.data?.message || 'No se pudo cargar el partido.'); onClose(); });
  }, [eventId]);

  const set = (id, field, value) => setRows(rs => rs.map(r => (r.studentId === id ? { ...r, [field]: value } : r)));

  const save = async () => {
    const players = rows.filter(r => r.played).map(r => ({
      studentId: r.studentId, isStarter: !!r.isStarter, minutes: num(r.minutes), goals: num(r.goals),
      assists: num(r.assists), yellowCards: num(r.yellowCards), redCards: num(r.redCards),
      rating: r.rating === '' || r.rating == null ? null : num(r.rating), notes: r.notes || null,
    }));
    setSaving(true);
    try {
      await api.put(`/sports/matches/${eventId}`, {
        goalsFor: score.goalsFor === '' ? null : num(score.goalsFor),
        goalsAgainst: score.goalsAgainst === '' ? null : num(score.goalsAgainst),
        matchNotes: score.matchNotes || null,
        players,
      });
      toast.success('Resultado guardado');
      onSaved?.();
      onClose();
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo guardar el resultado.');
    } finally {
      setSaving(false);
    }
  };

  const cell = { width: 56, padding: '4px 6px' };

  return (
    <div className="modal-overlay" onClick={() => !saving && onClose()}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 960, width: '95vw' }}>
        <div className="flex justify-between items-center mb-4">
          <h3 className="modal-title m-0 flex items-center gap-2"><Trophy size={20} /> {data ? `${data.title}${data.opponent ? ` vs. ${data.opponent}` : ''}` : 'Resultado del partido'}</h3>
          <button className="btn btn-ghost btn-sm" onClick={onClose} aria-label="Cerrar"><X size={16} /></button>
        </div>

        {!data ? (
          <div className="text-center p-8"><span className="spinner" style={{ borderColor: 'var(--primary)', borderTopColor: 'transparent' }} /></div>
        ) : (
          <>
            <div className="flex flex-wrap items-end gap-4 mb-4">
              <div className="form-group m-0">
                <label className="form-label">Goles a favor</label>
                <input type="number" min="0" className="form-control" style={{ width: 110 }} value={score.goalsFor} onChange={e => setScore({ ...score, goalsFor: e.target.value })} />
              </div>
              <div className="form-group m-0">
                <label className="form-label">Goles en contra</label>
                <input type="number" min="0" className="form-control" style={{ width: 110 }} value={score.goalsAgainst} onChange={e => setScore({ ...score, goalsAgainst: e.target.value })} />
              </div>
              <div className="form-group m-0 flex-1" style={{ minWidth: 220 }}>
                <label className="form-label">Notas del partido</label>
                <input className="form-control" maxLength={1000} value={score.matchNotes} onChange={e => setScore({ ...score, matchNotes: e.target.value })} />
              </div>
            </div>

            {rows.length === 0 ? (
              <div className="empty-state"><p>No hay jugadores convocados ni alumnos en las categorías del partido.</p></div>
            ) : (
              <div className="table-wrapper" style={{ maxHeight: '50vh', overflow: 'auto' }}>
                <table className="table">
                  <thead>
                    <tr><th>Jugó</th><th>Jugador</th><th>Titular</th><th>Min</th><th>Goles</th><th>Asist.</th><th>TA</th><th>TR</th><th>Nota (1-10)</th></tr>
                  </thead>
                  <tbody>
                    {rows.map(r => (
                      <tr key={r.studentId} style={{ opacity: r.played ? 1 : 0.6 }}>
                        <td><input type="checkbox" aria-label={`${r.studentName} jugó`} checked={!!r.played} onChange={e => set(r.studentId, 'played', e.target.checked)} /></td>
                        <td>{r.studentName}<div className="text-xs text-text-muted">{r.category}{r.called ? ' · convocado' : ''}</div></td>
                        <td><input type="checkbox" aria-label="Titular" disabled={!r.played} checked={!!r.isStarter} onChange={e => set(r.studentId, 'isStarter', e.target.checked)} /></td>
                        {['minutes', 'goals', 'assists', 'yellowCards', 'redCards'].map(f => (
                          <td key={f}><input type="number" min="0" className="form-control" style={cell} disabled={!r.played} value={r[f]} onChange={e => set(r.studentId, f, e.target.value)} /></td>
                        ))}
                        <td><input type="number" min="1" max="10" className="form-control" style={cell} disabled={!r.played} value={r.rating ?? ''} onChange={e => set(r.studentId, 'rating', e.target.value)} /></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            <div className="modal-footer">
              <button className="btn btn-ghost" onClick={onClose} disabled={saving}>Cancelar</button>
              <button className="btn btn-primary" onClick={save} disabled={saving}>{saving ? 'Guardando…' : 'Guardar resultado'}</button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
