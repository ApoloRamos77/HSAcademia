import { useEffect, useMemo, useState } from 'react';
import {
  User, Calendar, CreditCard, Clock, AlertTriangle, CheckCircle, Upload, X, Megaphone, Pin, Star, Trophy, Inbox,
} from 'lucide-react';
import toast from 'react-hot-toast';
import AppLayout from '../../components/AppLayout';
import { useAuth } from '../../context/AuthContext';
import api from '../../api/axios';
import { todayISO } from '../../utils/dates';

const money = v => `S/ ${Number(v || 0).toFixed(2)}`;
const longDate = s => {
  if (!s) return '—';
  const d = /^\d{4}-\d{2}-\d{2}$/.test(s) ? new Date(`${s}T12:00:00`) : new Date(s);
  return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString('es-PE', { weekday: 'long', day: 'numeric', month: 'long' });
};
const shortDate = s => new Date(s).toLocaleDateString('es-PE', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' });

const ATT = {
  Presente: { cls: 'badge-success', label: 'Asistió' },
  Tardanza: { cls: 'badge-warning', label: 'Tardanza' },
  Justificado: { cls: 'badge-info', label: 'Justificado' },
  Ausente: { cls: 'badge-danger', label: 'Faltó' },
  Pendiente: { cls: 'badge-muted', label: 'Por registrar' },
};
const PROOF = {
  Pending: { cls: 'badge-warning', label: 'En validación' },
  Approved: { cls: 'badge-success', label: 'Aprobado' },
  Rejected: { cls: 'badge-danger', label: 'Rechazado' },
};
const DIMENSIONS = [['technical', 'Técnica'], ['tactical', 'Táctica'], ['physical', 'Física'], ['attitude', 'Actitud']];

function Section({ title, icon: Icon, children, action }) {
  return (
    <section className="card">
      <div className="card-header">
        <h3 className="card-title flex items-center gap-2"><Icon size={20} className="text-primary" /> {title}</h3>
        {action}
      </div>
      {children}
    </section>
  );
}

function ProofModal({ debt, onClose, onSent }) {
  const [form, setForm] = useState({ amount: debt.amountPending.toFixed(2), method: 'Yape', operationNumber: '', paidOn: todayISO(), notes: '' });
  const [file, setFile] = useState(null);
  const [sending, setSending] = useState(false);

  const submit = async e => {
    e.preventDefault();
    if (!file) return toast.error('Adjunta la foto o PDF del comprobante.');
    const data = new FormData();
    data.append('file', file);
    Object.entries(form).forEach(([k, v]) => data.append(k, v));
    setSending(true);
    try {
      await api.post(`/finances/my-debts/${debt.id}/proof`, data, { headers: { 'Content-Type': 'multipart/form-data' } });
      toast.success('Comprobante enviado. La academia lo validará pronto.');
      onSent();
    } catch (err) {
      toast.error(err.response?.data?.message || 'No se pudo enviar el comprobante.');
    } finally {
      setSending(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={() => !sending && onClose()}>
      <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 520 }}>
        <div className="flex justify-between items-center mb-4">
          <h3 className="modal-title m-0">Enviar comprobante</h3>
          <button className="btn btn-ghost btn-sm" onClick={onClose} aria-label="Cerrar"><X size={16} /></button>
        </div>
        <p className="text-sm text-text-secondary mb-4">{debt.description} · saldo {money(debt.amountPending)}</p>
        <form onSubmit={submit}>
          <div className="form-row">
            <div className="form-group">
              <label className="form-label">Monto pagado (S/) *</label>
              <input type="number" min="0.01" step="0.01" max={debt.amountPending} required className="form-control"
                value={form.amount} onChange={e => setForm({ ...form, amount: e.target.value })} />
            </div>
            <div className="form-group">
              <label className="form-label">Medio *</label>
              <select className="form-control" value={form.method} onChange={e => setForm({ ...form, method: e.target.value })}>
                <option value="Yape">Yape</option>
                <option value="Plin">Plin</option>
                <option value="BankTransfer">Transferencia</option>
                <option value="Other">Otro</option>
              </select>
            </div>
          </div>
          <div className="form-row">
            <div className="form-group">
              <label className="form-label">N° de operación</label>
              <input className="form-control" maxLength={100} value={form.operationNumber} onChange={e => setForm({ ...form, operationNumber: e.target.value })} />
            </div>
            <div className="form-group">
              <label className="form-label">Fecha de pago *</label>
              <input type="date" required max={todayISO()} className="form-control" value={form.paidOn} onChange={e => setForm({ ...form, paidOn: e.target.value })} />
            </div>
          </div>
          <div className="form-group">
            <label className="form-label">Comprobante (foto o PDF, máx. 5 MB) *</label>
            <input type="file" accept="image/jpeg,image/png,image/webp,application/pdf" className="form-control" onChange={e => setFile(e.target.files?.[0] || null)} />
          </div>
          <div className="form-group">
            <label className="form-label">Nota para la academia</label>
            <input className="form-control" maxLength={500} value={form.notes} onChange={e => setForm({ ...form, notes: e.target.value })} />
          </div>
          <div className="modal-footer">
            <button type="button" className="btn btn-ghost" onClick={onClose} disabled={sending}>Cancelar</button>
            <button type="submit" className="btn btn-primary flex items-center gap-2" disabled={sending}><Upload size={16} /> {sending ? 'Enviando…' : 'Enviar'}</button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default function StudentDashboard() {
  const { user } = useAuth();
  const [students, setStudents] = useState([]);
  const [studentId, setStudentId] = useState(null);
  const [allDebts, setAllDebts] = useState([]);
  const [proofs, setProofs] = useState([]);
  const [history, setHistory] = useState([]);
  const [evaluations, setEvaluations] = useState([]);
  const [sports, setSports] = useState(null);
  const [news, setNews] = useState([]);
  const [loading, setLoading] = useState(true);
  const [proofDebt, setProofDebt] = useState(null);

  useEffect(() => {
    api.get('/mobile/students/my-students')
      .then(r => { setStudents(r.data); setStudentId(r.data[0]?.id ?? null); if (!r.data.length) setLoading(false); })
      .catch(() => { toast.error('No se pudo cargar la información.'); setLoading(false); });
    api.get('/announcement').then(r => setNews(r.data.slice(0, 5))).catch(() => {});
  }, []);

  const loadFinances = () => Promise.all([
    api.get('/finances/my-debts').then(r => setAllDebts(r.data)),
    api.get('/finances/my-proofs').then(r => setProofs(r.data)),
  ]);

  useEffect(() => {
    if (!studentId) return;
    setLoading(true);
    Promise.allSettled([
      loadFinances(),
      api.get(`/attendance/my-history?studentId=${studentId}&months=3`).then(r => setHistory(r.data)),
      api.get(`/mobile/students/${studentId}/evaluations`).then(r => setEvaluations(r.data)),
      api.get(`/mobile/students/${studentId}/sports-summary`).then(r => setSports(r.data)),
    ]).finally(() => setLoading(false));
  }, [studentId]);

  const debts = useMemo(() => allDebts.filter(d => d.studentId === studentId && !d.isPaid), [allDebts, studentId]);
  const myProofs = useMemo(() => proofs.filter(p => p.studentId === studentId), [proofs, studentId]);
  const pendingProofDebtIds = new Set(myProofs.filter(p => p.status === 'Pending').map(p => p.paymentRecordId));
  const overdue = debts.filter(d => d.status === 'Vencido');
  const records = useMemo(() => history.flatMap(m => m.records).filter(r => r.status !== 'Pendiente')
    .sort((a, b) => b.date.localeCompare(a.date)), [history]);
  const attended = records.filter(r => r.status === 'Presente' || r.status === 'Tardanza').length;
  const countable = records.filter(r => r.status !== 'Justificado').length;
  const rate = countable ? Math.round((100 * attended) / countable) : null;
  const student = students.find(s => s.id === studentId);

  return (
    <AppLayout title="Portal de la familia">
      <div className="fade-in flex flex-col gap-5" style={{ maxWidth: 1100, margin: '0 auto', paddingBottom: 40 }}>
        <div className="card flex flex-col md:flex-row justify-between md:items-center gap-4">
          <div>
            <h1 className="text-2xl font-bold text-primary-dark flex items-center gap-2 m-0"><User className="text-primary" size={26} /> ¡Hola, {user?.fullName?.split(' ')[0]}!</h1>
            <p className="text-sm text-text-secondary mt-1 mb-0">
              {student ? `${student.firstName} ${student.lastName} · ${student.categoryName} · Sede ${student.headquarterName}` : 'Consulta pagos, asistencia y avances deportivos.'}
            </p>
          </div>
          {students.length > 1 && (
            <div className="flex flex-wrap gap-2" role="tablist" aria-label="Alumno">
              {students.map(s => (
                <button key={s.id} role="tab" aria-selected={studentId === s.id}
                  className={`btn btn-sm ${studentId === s.id ? 'btn-primary' : 'btn-ghost'}`} onClick={() => setStudentId(s.id)}>
                  {s.firstName}
                </button>
              ))}
            </div>
          )}
        </div>

        {loading ? (
          <div className="text-center p-8"><span className="spinner" style={{ borderColor: 'var(--primary)', borderTopColor: 'transparent' }} /></div>
        ) : !studentId ? (
          <div className="card empty-state"><User size={40} /><p>No hay alumnos vinculados a tu cuenta. Comunícate con la academia.</p></div>
        ) : (
          <div className="grid gap-5" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))' }}>
            {/* Estado de cuenta */}
            <Section title="Estado de cuenta" icon={CreditCard}>
              {overdue.length > 0 && (
                <div className="flex items-start gap-2 p-3 mb-3 rounded-xl" style={{ background: '#fde2e2', color: '#b42318' }}>
                  <AlertTriangle size={18} className="shrink-0" />
                  <span className="text-sm"><b>{overdue.length} mensualidad(es) vencida(s).</b> Puedes pagar por Yape, Plin o transferencia y enviar aquí tu comprobante.</span>
                </div>
              )}
              {debts.length === 0 ? (
                <div className="empty-state"><CheckCircle size={40} className="text-success" /><p>¡Todo al día! No hay pagos pendientes.</p></div>
              ) : (
                <ul className="flex flex-col gap-3" style={{ listStyle: 'none', margin: 0, padding: 0 }}>
                  {debts.map(d => (
                    <li key={d.id} className="rounded-xl border border-border p-3 flex justify-between items-center gap-3">
                      <div>
                        <div className="font-bold text-text-primary">{d.description}</div>
                        <div className="text-xs text-text-muted flex items-center gap-1 mt-1"><Clock size={12} /> Vence: {longDate(d.dueDate)}</div>
                        <span className={`badge mt-2 ${d.status === 'Vencido' ? 'badge-danger' : 'badge-warning'}`}>{d.status}</span>
                      </div>
                      <div className="text-right">
                        <div className="font-bold text-lg text-text-primary">{money(d.amountPending)}</div>
                        {pendingProofDebtIds.has(d.id) ? (
                          <span className="badge badge-warning mt-2">Comprobante en validación</span>
                        ) : (
                          <button className="btn btn-primary btn-sm mt-2 flex items-center gap-1" onClick={() => setProofDebt(d)}><Upload size={14} /> Enviar comprobante</button>
                        )}
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </Section>

            {/* Comprobantes */}
            <Section title="Mis comprobantes" icon={Inbox}>
              {myProofs.length === 0 ? (
                <div className="empty-state"><Inbox size={36} /><p>Aún no enviaste comprobantes.</p></div>
              ) : (
                <ul className="flex flex-col gap-2" style={{ listStyle: 'none', margin: 0, padding: 0 }}>
                  {myProofs.slice(0, 8).map(p => (
                    <li key={p.id} className="flex justify-between items-start gap-3 border-b border-border pb-2">
                      <div className="text-sm">
                        <div className="font-medium text-text-primary">{p.debtDescription}</div>
                        <div className="text-xs text-text-muted">Pagado el {shortDate(p.paidOn)} · {money(p.amount)}</div>
                        {p.rejectionReason && <div className="text-xs text-danger mt-1">Motivo: {p.rejectionReason}</div>}
                        {p.receiptNumber && <div className="text-xs text-text-muted">Recibo N° {p.receiptNumber}</div>}
                      </div>
                      <span className={`badge ${PROOF[p.status]?.cls}`}>{PROOF[p.status]?.label}</span>
                    </li>
                  ))}
                </ul>
              )}
            </Section>

            {/* Asistencia */}
            <Section title="Asistencia (últimos 3 meses)" icon={Calendar}
              action={rate != null && <span className={`badge ${rate >= 70 ? 'badge-success' : 'badge-danger'}`}>{rate}% de asistencia</span>}>
              {records.length === 0 ? (
                <div className="empty-state"><Calendar size={36} /><p>Sin registros de asistencia todavía.</p></div>
              ) : (
                <ul className="flex flex-col gap-2" style={{ listStyle: 'none', margin: 0, padding: 0 }}>
                  {records.slice(0, 8).map((r, i) => (
                    <li key={`${r.date}-${i}`} className="flex justify-between items-center border-b border-border pb-2">
                      <span className="text-sm capitalize">{longDate(r.date)}<span className="block text-xs text-text-muted normal-case">{r.notes}</span></span>
                      <span className={`badge ${ATT[r.status]?.cls || 'badge-muted'}`}>{ATT[r.status]?.label || r.status}</span>
                    </li>
                  ))}
                </ul>
              )}
            </Section>

            {/* Evaluaciones */}
            <Section title="Evaluaciones deportivas" icon={Star}>
              {evaluations.length === 0 ? (
                <div className="empty-state"><Star size={36} /><p>El entrenador aún no registró evaluaciones.</p></div>
              ) : evaluations.slice(0, 2).map(ev => (
                <div key={ev.id} className="rounded-xl border border-border p-3 mb-3">
                  <div className="flex justify-between items-center">
                    <b className="text-text-primary">{ev.period}</b>
                    <span className="badge badge-info">Promedio {ev.average} / 5</span>
                  </div>
                  <div className="grid gap-2 mt-3" style={{ gridTemplateColumns: 'repeat(2, minmax(0,1fr))' }}>
                    {DIMENSIONS.map(([k, label]) => (
                      <div key={k} className="text-xs">
                        <div className="flex justify-between"><span className="text-text-secondary">{label}</span><b>{ev[k]}/5</b></div>
                        <div style={{ height: 6, background: '#e3eefb', borderRadius: 999, marginTop: 4 }}>
                          <div style={{ width: `${(ev[k] / 5) * 100}%`, height: '100%', background: '#2a78d6', borderRadius: 999 }} />
                        </div>
                      </div>
                    ))}
                  </div>
                  {ev.strengths && <p className="text-sm mt-3 mb-0"><b>Fortalezas:</b> {ev.strengths}</p>}
                  {ev.toImprove && <p className="text-sm mt-1 mb-0"><b>Por mejorar:</b> {ev.toImprove}</p>}
                  {ev.goals && <p className="text-sm mt-1 mb-0"><b>Objetivos:</b> {ev.goals}</p>}
                  <p className="text-xs text-text-muted mt-2 mb-0">{shortDate(ev.evaluationDate)} · {ev.evaluatorName}</p>
                </div>
              ))}
            </Section>

            {/* Partidos */}
            {sports && sports.matches > 0 && (
              <Section title="Partidos" icon={Trophy}>
                <div className="grid gap-3 mb-3" style={{ gridTemplateColumns: 'repeat(4, minmax(0,1fr))' }}>
                  {[['Partidos', sports.matches], ['Minutos', sports.minutes], ['Goles', sports.goals], ['Asistencias', sports.assists]].map(([l, v]) => (
                    <div key={l} className="text-center rounded-xl p-2" style={{ background: 'var(--bg-surface)' }}>
                      <div className="text-xl font-bold text-text-primary">{v}</div>
                      <div className="text-xs text-text-muted">{l}</div>
                    </div>
                  ))}
                </div>
                <ul className="flex flex-col gap-2" style={{ listStyle: 'none', margin: 0, padding: 0 }}>
                  {sports.recentMatches.slice(0, 5).map(m => (
                    <li key={m.eventId} className="flex justify-between text-sm border-b border-border pb-2">
                      <span>{m.title}{m.opponent ? ` vs. ${m.opponent}` : ''}<span className="block text-xs text-text-muted">{shortDate(m.date)} · {m.minutes}′</span></span>
                      <span className="text-right">{m.score && <b>{m.score}</b>}<span className="block text-xs text-text-muted">{m.goals} G · {m.assists} A</span></span>
                    </li>
                  ))}
                </ul>
              </Section>
            )}

            {/* Comunicados */}
            <Section title="Comunicados" icon={Megaphone}>
              {news.length === 0 ? (
                <div className="empty-state"><Megaphone size={36} /><p>Sin comunicados recientes.</p></div>
              ) : news.map(n => (
                <div key={n.id} className="border-b border-border pb-3 mb-3">
                  <div className="font-bold text-text-primary flex items-center gap-2">{n.isPinned && <Pin size={14} className="text-primary" />}{n.title}</div>
                  <p className="text-sm text-text-secondary mt-1 mb-0" style={{ whiteSpace: 'pre-wrap' }}>{n.content}</p>
                  <div className="text-xs text-text-muted mt-1">{new Date(n.datePosted).toLocaleDateString('es-PE', { day: 'numeric', month: 'long' })}</div>
                </div>
              ))}
            </Section>
          </div>
        )}
      </div>

      {proofDebt && (
        <ProofModal debt={proofDebt} onClose={() => setProofDebt(null)} onSent={() => { setProofDebt(null); loadFinances(); }} />
      )}
    </AppLayout>
  );
}
