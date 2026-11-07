import { Fragment, useCallback, useEffect, useState } from 'react';
import { Wallet, CheckCircle, AlertTriangle, X, Inbox, ChevronDown, ChevronUp } from 'lucide-react';
import MyCashPanel from './MyCashPanel';
import toast from 'react-hot-toast';
import api from '../../api/axios';

const METHOD = { Cash: 'Efectivo', BankTransfer: 'Transferencia', Yape: 'Yape', Plin: 'Plin', Card: 'Tarjeta', Other: 'Otro' };
const money = (n) => `S/. ${(Number(n) || 0).toFixed(2)}`;
const isoDay = (d) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const when = (d) => d ? `${new Date(d).toLocaleDateString('es-PE')} ${new Date(d).toLocaleTimeString('es-PE', { hour: '2-digit', minute: '2-digit' })}` : '';

const Diff = ({ value }) => {
  if (value == null) return null;
  if (Math.abs(value) < 0.01) return <span className="badge badge-success">Cuadra</span>;
  return <span className={`badge ${value < 0 ? 'badge-danger' : 'badge-warning'}`}>{value < 0 ? 'Falta' : 'Sobra'} {money(Math.abs(value))}</span>;
};

/** Cuadre de caja: lo cobrado por cada cobrador, rendiciones pendientes y recepción del efectivo. */
export default function CashTab() {
  const [date, setDate] = useState(isoDay(new Date()));
  const [summary, setSummary] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [receive, setReceive] = useState(null); // handover + { receivedCash, notes }
  const [expanded, setExpanded] = useState(null);
  const [detail, setDetail] = useState({});
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      const s = await api.get('/cash/summary', { params: { date } });
      setSummary(s.data);
    } catch { toast.error('No se pudo cargar la caja.'); }
  }, [date]);
  useEffect(() => { load(); }, [load]);

  const toggleDetail = async (h) => {
    if (expanded === h.id) { setExpanded(null); return; }
    setExpanded(h.id);
    if (!detail[h.id]) {
      try { const { data } = await api.get(`/cash/handovers/${h.id}/receipts`); setDetail(d => ({ ...d, [h.id]: data })); } catch { /* sin detalle */ }
    }
  };

  const doReceive = async (e) => {
    e.preventDefault();
    setBusy(true);
    try {
      await api.post(`/cash/handovers/${receive.id}/receive`, { receivedCash: parseFloat(receive.receivedCash) || 0, notes: receive.notes || null });
      toast.success('Efectivo recibido.');
      setReceive(null); load(); setRefreshKey(k => k + 1);
    } catch (err) { toast.error(err.response?.data?.message || 'No se pudo registrar la recepción.'); }
    finally { setBusy(false); }
  };

  const pendingHandovers = summary?.handovers.filter(h => h.status === 'Pending') || [];

  return (
    <div className="fade-in">
      <MyCashPanel refreshKey={refreshKey} onChanged={load} />

      {/* Resumen del día */}
      <div className="card mb-4">
        <div className="p-4 border-b border-border/50 flex flex-wrap items-center justify-between gap-3">
          <h3 className="card-title m-0 flex items-center gap-2"><Wallet size={16} /> Cuadre de caja</h3>
          <input type="date" className="form-control py-1.5 text-sm w-auto" value={date} onChange={e => setDate(e.target.value)} />
        </div>
        {summary && (
          <>
            <div className="stats-grid p-4" style={{ marginBottom: 0 }}>
              <div className="stat-card"><div className="stat-value">{money(summary.total)}</div><div className="stat-label">Cobrado ({summary.receiptsCount} recibos{summary.voidedCount ? ` · ${summary.voidedCount} anulados` : ''})</div></div>
              <div className="stat-card"><div className="stat-value">{money(summary.cash)}</div><div className="stat-label">Efectivo</div></div>
              <div className="stat-card"><div className="stat-value">{money(summary.digital)}</div><div className="stat-label">Digital ({summary.byMethod.filter(m => m.method !== 'Cash').map(m => `${METHOD[m.method] || m.method} ${money(m.total)}`).join(' · ') || '—'})</div></div>
            </div>
            <div className="table-container">
              <table className="table">
                <thead><tr><th>Cobrador</th><th>Recibos del día</th><th>Efectivo del día</th><th>Digital del día</th><th>Efectivo sin rendir</th><th>Rendido por recibir</th></tr></thead>
                <tbody>
                  {summary.collectors.map(c => (
                    <tr key={c.collectorUserId || 'x'}>
                      <td className="font-medium">{c.collectorName}</td>
                      <td>{c.receiptsCount}</td>
                      <td>{money(c.cash)}</td>
                      <td className="text-sm">{money(c.digital)}{c.byMethod.filter(m => m.method !== 'Cash').length > 0 && <div className="text-xs text-text-muted">{c.byMethod.filter(m => m.method !== 'Cash').map(m => `${METHOD[m.method]} ${money(m.total)}`).join(' · ')}</div>}</td>
                      <td className={c.pendingCash > 0 ? 'font-bold text-warning' : ''}>{money(c.pendingCash)}{c.pendingReceipts > 0 && <div className="text-xs text-text-muted">{c.pendingReceipts} recibo(s)</div>}</td>
                      <td className={c.inTransitCash > 0 ? 'font-bold text-primary' : ''}>{money(c.inTransitCash)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {summary.collectors.length === 0 && <div className="empty-state"><Wallet size={32} className="text-text-muted" /><p>Sin cobros en esta fecha.</p></div>}
          </>
        )}
      </div>

      {/* Rendiciones */}
      <div className="card">
        <div className="p-4 border-b border-border/50 flex items-center gap-2">
          <Inbox size={16} /><h3 className="card-title m-0">Rendiciones</h3>
          {pendingHandovers.length > 0 && <span className="badge badge-warning">{pendingHandovers.length} por recibir</span>}
        </div>
        <div className="table-container">
          <table className="table">
            <thead><tr><th>Cobrador</th><th>Declarada</th><th>Recibos</th><th>Efectivo esperado</th><th>Declarado</th><th>Recibido</th><th>Estado</th><th></th></tr></thead>
            <tbody>
              {summary?.handovers.map(h => (
                <Fragment key={h.id}>
                  <tr>
                    <td className="font-medium">{h.collectorName}{h.notes && <div className="text-xs text-text-muted italic">“{h.notes}”</div>}</td>
                    <td className="text-sm">{when(h.declaredAt)}</td>
                    <td>{h.receiptsCount}{h.digitalTotal > 0 && <div className="text-xs text-text-muted">+ digital {money(h.digitalTotal)}</div>}</td>
                    <td className="font-bold">{money(h.expectedCash)}</td>
                    <td>{money(h.declaredCash)} <Diff value={h.declaredDifference} /></td>
                    <td>{h.receivedCash != null ? <>{money(h.receivedCash)} <Diff value={h.receivedDifference} /><div className="text-xs text-text-muted">{h.receivedByName} · {when(h.receivedAt)}</div>{h.receivedNotes && <div className="text-xs italic">“{h.receivedNotes}”</div>}</> : '—'}</td>
                    <td>{h.status === 'Received' ? <span className="badge badge-success flex items-center gap-1 w-max"><CheckCircle size={11} /> Recibida</span> : <span className="badge badge-warning">Por recibir</span>}</td>
                    <td className="text-right whitespace-nowrap">
                      {h.status === 'Pending' && <button className="btn btn-sm btn-success" onClick={() => setReceive({ ...h, receivedCash: h.declaredCash.toFixed(2), notes: '' })}>Recibir</button>}
                      <button className="btn btn-sm btn-ghost" onClick={() => toggleDetail(h)}>{expanded === h.id ? <ChevronUp size={13} /> : <ChevronDown size={13} />}</button>
                    </td>
                  </tr>
                  {expanded === h.id && (
                    <tr><td colSpan={8} className="bg-bg-dark/50 text-xs">
                      {(detail[h.id] || []).map(r => (
                        <div key={r.id} className="flex gap-3 py-0.5">
                          <b>N° {r.number}</b><span>{when(r.issuedAt)}</span><span>{r.payerName}</span><span>{METHOD[r.method]}</span><b>{money(r.total)}</b>
                          {r.status === 'Voided' && <span className="badge badge-danger">Anulado</span>}
                        </div>
                      ))}
                      {!detail[h.id] && 'Cargando…'}
                    </td></tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
        {summary && summary.handovers.length === 0 && <div className="empty-state"><Inbox size={32} className="text-text-muted" /><p>No hay rendiciones en esta fecha.</p></div>}
      </div>

      {receive && (
        <div className="modal-overlay" onClick={() => setReceive(null)}>
          <div className="modal" onClick={e => e.stopPropagation()} style={{ maxWidth: 440 }}>
            <div className="flex justify-between items-center mb-4 border-b border-border pb-3">
              <h3 className="modal-title m-0">Recibir caja de {receive.collectorName}</h3>
              <button onClick={() => setReceive(null)} className="btn btn-ghost btn-sm"><X size={16} /></button>
            </div>
            <p className="text-sm mb-1">Efectivo esperado según recibos: <b>{money(receive.expectedCash)}</b></p>
            <p className="text-sm mb-3">Declarado por el cobrador: <b>{money(receive.declaredCash)}</b> <Diff value={receive.declaredDifference} /></p>
            <form onSubmit={doReceive}>
              <div className="form-group">
                <label className="form-label">Efectivo contado *</label>
                <input className="form-control text-lg font-bold" type="number" step="0.01" min="0" required value={receive.receivedCash} onChange={e => setReceive({ ...receive, receivedCash: e.target.value })} />
              </div>
              {Math.abs((parseFloat(receive.receivedCash) || 0) - receive.expectedCash) >= 0.01 && (
                <div className="text-sm text-warning flex items-center gap-2 mb-2"><AlertTriangle size={14} /> Hay diferencia con lo esperado: registra el motivo.</div>
              )}
              <div className="form-group">
                <label className="form-label">Nota</label>
                <input className="form-control" maxLength={300} value={receive.notes} onChange={e => setReceive({ ...receive, notes: e.target.value })} />
              </div>
              <div className="modal-footer mt-4 flex justify-end gap-2">
                <button type="button" onClick={() => setReceive(null)} className="btn btn-ghost">Cancelar</button>
                <button type="submit" disabled={busy} className="btn btn-success">Confirmar recepción</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
