import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Users, Wallet, AlertTriangle, CalendarDays, Cake, Activity, TrendingDown, Info, ChevronRight,
} from 'lucide-react';
import {
  ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend,
} from 'recharts';
import AppLayout from '../../components/AppLayout';
import api from '../../api/axios';
import { useAuth } from '../../context/AuthContext';
import './Dashboard.css';

// Paleta validada (scripts/validate_palette.js): 2 series, CVD ΔE 24.7, contraste ≥ 3:1.
const SERIES = { collected: '#2a78d6', expenses: '#eb6834' };
const GRID = '#e5edf2';

const money = new Intl.NumberFormat('es-PE', { style: 'currency', currency: 'PEN', maximumFractionDigits: 0 });
const moneyCompact = v => (Math.abs(v) >= 10000
  ? `S/ ${new Intl.NumberFormat('es-PE', { notation: 'compact', maximumFractionDigits: 1 }).format(v)}`
  : money.format(v));
const pct = v => (v == null ? '—' : `${Number(v).toLocaleString('es-PE', { maximumFractionDigits: 1 })}%`);
// Las horas de eventos se guardan como "hora de pared" con sufijo Z: se muestran en UTC (igual que el Calendario).
const dateFmt = new Intl.DateTimeFormat('es-PE', { weekday: 'short', day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', timeZone: 'UTC' });
const dayFmt = new Intl.DateTimeFormat('es-PE', { weekday: 'long', day: 'numeric', month: 'long' });
const EVENT_TYPES = { 1: 'Entrenamiento', 2: 'Amistoso', 3: 'Torneo' };

function StatTile({ label, value, sub, icon: Icon, tone = 'neutral', children }) {
  return (
    <div className={`dash-tile dash-tile--${tone}`}>
      <div className="dash-tile__head">
        <span className="dash-tile__label">{label}</span>
        {Icon && <Icon size={18} className="dash-tile__icon" aria-hidden="true" />}
      </div>
      <div className="dash-tile__value">{value}</div>
      {sub && <div className="dash-tile__sub">{sub}</div>}
      {children}
    </div>
  );
}

function Meter({ value }) {
  const v = Math.max(0, Math.min(100, Number(value) || 0));
  const level = v >= 80 ? 'good' : v >= 50 ? 'warning' : 'danger';
  return (
    <div className={`dash-meter dash-meter--${level}`} role="meter" aria-valuemin={0} aria-valuemax={100} aria-valuenow={v} aria-label="Tasa de cobranza">
      <div className="dash-meter__fill" style={{ width: `${v}%` }} />
    </div>
  );
}

function Panel({ title, icon: Icon, action, children }) {
  return (
    <section className="dash-panel">
      <header className="dash-panel__head">
        <h3>{Icon && <Icon size={16} aria-hidden="true" />} {title}</h3>
        {action}
      </header>
      {children}
    </section>
  );
}

function ChartTooltip({ active, payload, label }) {
  if (!active || !payload?.length) return null;
  return (
    <div className="dash-tooltip">
      <div className="dash-tooltip__title">{label}</div>
      {payload.map(p => (
        <div key={p.dataKey} className="dash-tooltip__row">
          <span className="dash-swatch" style={{ background: p.color }} />
          <span>{p.name}</span>
          <strong>{money.format(p.value)}</strong>
        </div>
      ))}
    </div>
  );
}

export default function Dashboard() {
  const { user } = useAuth();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [showTable, setShowTable] = useState(false);

  useEffect(() => {
    api.get('/dashboard')
      .then(res => setData(res.data))
      .catch(err => setError(err.response?.data?.message || 'No se pudo cargar el panel.'));
  }, []);

  const greeting = (() => {
    const h = new Date().getHours();
    return h < 12 ? 'Buenos días' : h < 19 ? 'Buenas tardes' : 'Buenas noches';
  })();
  const firstName = (user?.fullName || '').split(' ')[0];
  const monthName = new Date().toLocaleDateString('es-PE', { month: 'long', year: 'numeric' });

  if (error) {
    return <AppLayout title="Panel principal"><div className="dash-empty">{error}</div></AppLayout>;
  }
  if (!data) {
    return (
      <AppLayout title="Panel principal">
        <div className="dash-loading"><span className="spinner" style={{ borderColor: 'var(--primary)', borderTopColor: 'transparent' }} /></div>
      </AppLayout>
    );
  }

  const { students, finance, attendance, upcomingEvents, birthdays, alerts } = data;
  const maxCat = Math.max(1, ...students.byCategory.map(c => c.count));

  return (
    <AppLayout title="Panel principal">
      <div className="dash fade-in">
        <div className="dash-hello">
          <div>
            <h1>{greeting}{firstName ? `, ${firstName}` : ''}</h1>
            <p>Resumen de {monthName}{user?.academyName ? ` · ${user.academyName}` : ''}</p>
          </div>
        </div>

        {/* ── Indicadores ───────────────────────────── */}
        <div className="dash-tiles">
          <StatTile
            label="Alumnos activos" icon={Users} value={students.active.toLocaleString('es-PE')}
            sub={<><span className="dash-delta dash-delta--up">+{students.newThisMonth} nuevos</span> · <span className="dash-delta dash-delta--down">−{students.withdrawnThisMonth} retiros</span> este mes</>}
          />
          {finance && (
            <>
              <StatTile
                label="Cobrado este mes" icon={Wallet} value={moneyCompact(finance.collected)}
                sub={`Facturado ${money.format(finance.billed)} · pendiente ${money.format(finance.pending)}`}
              />
              <StatTile label="Cobranza del mes" icon={Activity} value={pct(finance.collectionRate)} sub="del facturado ya está pagado">
                <Meter value={finance.collectionRate} />
              </StatTile>
              <StatTile
                label="Deuda vencida" icon={AlertTriangle} tone={finance.overdueAmount > 0 ? 'danger' : 'neutral'}
                value={moneyCompact(finance.overdueAmount)}
                sub={finance.overdueStudents > 0 ? `${finance.overdueStudents} alumno(s) con atraso` : 'Sin atrasos'}
              />
            </>
          )}
          <StatTile
            label="Asistencia (30 días)" icon={CalendarDays}
            tone={attendance.rateLast30Days != null && attendance.rateLast30Days < 70 ? 'warning' : 'neutral'}
            value={pct(attendance.rateLast30Days)}
            sub={attendance.recordsLast30Days ? `${attendance.recordsLast30Days} registros` : 'Sin registros aún'}
          />
        </div>

        {/* ── Alertas ───────────────────────────────── */}
        {alerts.length > 0 && (
          <ul className="dash-alerts" aria-label="Alertas">
            {alerts.map((a, i) => {
              const Icon = a.level === 'danger' ? AlertTriangle : a.level === 'warning' ? AlertTriangle : Info;
              const label = a.level === 'danger' ? 'Urgente' : a.level === 'warning' ? 'Atención' : 'Aviso';
              const content = (
                <>
                  <Icon size={16} aria-hidden="true" />
                  <span className="dash-alert__level">{label}</span>
                  <span className="dash-alert__msg">{a.message}</span>
                  {a.link && <ChevronRight size={16} aria-hidden="true" className="dash-alert__go" />}
                </>
              );
              return (
                <li key={i} className={`dash-alert dash-alert--${a.level}`}>
                  {a.link ? <Link to={a.link}>{content}</Link> : <div>{content}</div>}
                </li>
              );
            })}
          </ul>
        )}

        <div className="dash-grid">
          {/* ── Finanzas: cobrado vs egresos ─────────── */}
          {finance && (
            <Panel
              title="Cobrado vs. egresos (últimos 6 meses)" icon={Wallet}
              action={<button type="button" className="btn btn-ghost btn-sm" onClick={() => setShowTable(s => !s)}>{showTable ? 'Ver gráfico' : 'Ver tabla'}</button>}
            >
              {showTable ? (
                <table className="dash-table">
                  <thead><tr><th>Mes</th><th>Cobrado</th><th>Egresos</th><th>Balance</th></tr></thead>
                  <tbody>
                    {finance.trend.map(t => (
                      <tr key={t.label}>
                        <td>{t.label}</td><td>{money.format(t.collected)}</td><td>{money.format(t.expenses)}</td>
                        <td className={t.netBalance < 0 ? 'dash-neg' : ''}>{money.format(t.netBalance)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              ) : (
                <div className="dash-chart">
                  <ResponsiveContainer width="100%" height={260}>
                    <BarChart data={finance.trend} barGap={2} barCategoryGap="28%" margin={{ top: 8, right: 8, left: 0, bottom: 0 }}>
                      <CartesianGrid vertical={false} stroke={GRID} strokeWidth={1} />
                      <XAxis dataKey="label" tickLine={false} axisLine={{ stroke: GRID }} tick={{ fill: 'var(--text-muted)', fontSize: 12 }} />
                      <YAxis tickLine={false} axisLine={false} width={64} tick={{ fill: 'var(--text-muted)', fontSize: 12 }} tickFormatter={moneyCompact} />
                      <Tooltip content={<ChartTooltip />} cursor={{ fill: 'rgba(42,120,214,0.06)' }} />
                      <Legend iconType="circle" iconSize={8} wrapperStyle={{ fontSize: 12, color: 'var(--text-secondary)' }} />
                      <Bar dataKey="collected" name="Cobrado" fill={SERIES.collected} maxBarSize={24} radius={[4, 4, 0, 0]} />
                      <Bar dataKey="expenses" name="Egresos" fill={SERIES.expenses} maxBarSize={24} radius={[4, 4, 0, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              )}
            </Panel>
          )}

          {/* ── Alumnos por categoría ─────────────────── */}
          <Panel title="Alumnos por categoría" icon={Users}>
            {students.byCategory.length === 0 ? (
              <div className="dash-empty">Aún no hay alumnos activos.</div>
            ) : (
              <ul className="dash-bars">
                {students.byCategory.map(c => (
                  <li key={c.categoryId} title={`${c.category} · ${c.headquarter}: ${c.count} alumnos`}>
                    <span className="dash-bars__label">{c.category}<small>{c.headquarter}</small></span>
                    <span className="dash-bars__track">
                      <span className="dash-bars__fill" style={{ width: `${(c.count / maxCat) * 100}%` }} />
                    </span>
                    <span className="dash-bars__value">{c.count}</span>
                  </li>
                ))}
              </ul>
            )}
          </Panel>

          {/* ── Mayores deudas ───────────────────────── */}
          {finance && (
            <Panel title="Mayores deudas vencidas" icon={TrendingDown}
              action={<Link to="/academy/finanzas" className="dash-link">Ir a cobranza</Link>}>
              {finance.topOverdue.length === 0 ? (
                <div className="dash-empty">Ningún alumno con mensualidades vencidas. 🎉</div>
              ) : (
                <table className="dash-table">
                  <thead><tr><th>Alumno</th><th>Atraso</th><th className="num">Debe</th></tr></thead>
                  <tbody>
                    {finance.topOverdue.map(o => (
                      <tr key={o.studentId}>
                        <td>{o.name}<small>{o.category}</small></td>
                        <td>{o.maxDaysOverdue} días</td>
                        <td className="num"><strong>{money.format(o.amount)}</strong></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </Panel>
          )}

          {/* ── Faltas seguidas ─────────────────────── */}
          {attendance.absenceStreaks?.length > 0 && (
            <Panel title="Faltas seguidas: llamar a la familia" icon={Activity}>
              <table className="dash-table">
                <thead><tr><th>Alumno</th><th>Teléfono</th><th className="num">Faltas</th></tr></thead>
                <tbody>
                  {attendance.absenceStreaks.map(s => (
                    <tr key={s.studentId}>
                      <td>{s.name}<small>{s.category}</small></td>
                      <td>{s.contactPhone ? <a href={`tel:${s.contactPhone}`}>{s.contactPhone}</a> : '—'}</td>
                      <td className="num"><strong>{s.absences}</strong></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Panel>
          )}

          {/* ── Baja asistencia ─────────────────────── */}
          <Panel title="Asistencia menor al 70%" icon={Activity}
            action={<Link to="/academy/asistencia-metricas" className="dash-link">Ver métricas</Link>}>
            {attendance.lowAttendance.length === 0 ? (
              <div className="dash-empty">Sin alumnos en riesgo de abandono por asistencia.</div>
            ) : (
              <table className="dash-table">
                <thead><tr><th>Alumno</th><th className="num">Asistencia</th></tr></thead>
                <tbody>
                  {attendance.lowAttendance.map(s => (
                    <tr key={s.studentId}>
                      <td>{s.name}<small>{s.category} · {s.records} registros</small></td>
                      <td className="num"><strong>{pct(s.rate)}</strong></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </Panel>

          {/* ── Próximos eventos ────────────────────── */}
          <Panel title="Próximos eventos" icon={CalendarDays}
            action={<Link to="/academy/calendario" className="dash-link">Calendario</Link>}>
            {upcomingEvents.length === 0 ? (
              <div className="dash-empty">No hay eventos programados.</div>
            ) : (
              <ul className="dash-list">
                {upcomingEvents.map(e => (
                  <li key={e.id}>
                    <span className={`dash-chip dash-chip--t${e.type}`}>{EVENT_TYPES[e.type] || 'Evento'}</span>
                    <span className="dash-list__main">
                      <strong>{e.title}{e.opponent ? ` vs. ${e.opponent}` : ''}</strong>
                      <small>{dateFmt.format(new Date(e.startTime))} · {e.categories}</small>
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </Panel>

          {/* ── Cumpleaños ──────────────────────────── */}
          <Panel title="Cumpleaños de la semana" icon={Cake}>
            {birthdays.length === 0 ? (
              <div className="dash-empty">Ningún cumpleaños en los próximos 7 días.</div>
            ) : (
              <ul className="dash-list">
                {birthdays.map(b => (
                  <li key={b.studentId}>
                    <span className={`dash-chip ${b.isToday ? 'dash-chip--today' : ''}`}>{b.isToday ? '¡Hoy!' : `${b.turnsAge} años`}</span>
                    <span className="dash-list__main">
                      <strong>{b.name}</strong>
                      <small>{b.isToday ? `Cumple ${b.turnsAge} años` : dayFmt.format(new Date(b.date))} · {b.category}</small>
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </Panel>
        </div>
      </div>
    </AppLayout>
  );
}
