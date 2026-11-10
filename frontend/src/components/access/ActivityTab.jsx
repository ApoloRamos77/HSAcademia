import { useEffect, useState } from 'react';
import { Activity, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, Search } from 'lucide-react';
import toast from 'react-hot-toast';
import api from '../../api/axios';

const MODULES = [
  { value: '', label: 'Toda la actividad' },
  { value: 'accesos', label: 'Accesos' },
  { value: 'finanzas', label: 'Finanzas' },
  { value: 'deportivo', label: 'Deportivo' },
  { value: 'tienda', label: 'Tienda' },
];
const MODULE_BADGE = { accesos: 'badge-info', finanzas: 'badge-success', deportivo: 'badge-warning', tienda: 'badge-muted' };
const FIELDS = {
  nombre: 'Nombre', correo: 'Correo', telefono: 'Teléfono', nivelAcceso: 'Nivel de acceso', roles: 'Roles', estado: 'Estado',
  motivoSuspension: 'Motivo', sedes: 'Sedes', categorias: 'Categorías', tipoPago: 'Tipo de pago', tarifa: 'Tarifa',
};
const VALUES = {
  Active: 'Activo', Suspended: 'Suspendido', Inactive: 'Inactivo', Staff: 'Personal', AcademyAdmin: 'Administrador',
  Monthly: 'Mensual', PerSession: 'Por sesión',
};
const PAGE_SIZE = 30;

const show = v => (v === null || v === undefined || v === '') ? '—' : (VALUES[v] ?? String(v));

function Details({ details, labels }) {
  if (!details) return <p className="text-xs text-text-muted m-0">Sin detalle adicional.</p>;

  if (details.changes) {
    const entries = Object.entries(details.changes);
    return (
      <div className="flex flex-col gap-1">
        {entries.map(([field, change]) => (
          <div key={field} className="text-xs"><strong>{FIELDS[field] ?? field}:</strong> {show(change.from)} → {show(change.to)}</div>
        ))}
        {details.passwordChanged && <div className="text-xs"><strong>Contraseña:</strong> reemplazada</div>}
        {entries.length === 0 && !details.passwordChanged && <p className="text-xs text-text-muted m-0">Sin cambios registrados.</p>}
      </div>
    );
  }

  if (details.before && details.after) {
    const before = details.before.Permissions ?? [];
    const after = details.after.Permissions ?? [];
    const name = id => labels[id] ?? id;
    const added = after.filter(p => !before.includes(p));
    const removed = before.filter(p => !after.includes(p));
    return (
      <div className="flex flex-col gap-1 text-xs">
        {details.before.Name !== details.after.Name && <div><strong>Nombre:</strong> {details.before.Name} → {details.after.Name}</div>}
        {added.length > 0 && <div><strong>Permisos agregados:</strong> {added.map(name).join(', ')}</div>}
        {removed.length > 0 && <div><strong>Permisos quitados:</strong> {removed.map(name).join(', ')}</div>}
        {added.length === 0 && removed.length === 0 && details.before.Name === details.after.Name && <div>Cambió la descripción.</div>}
      </div>
    );
  }

  const entries = Object.entries(details).filter(([, v]) => v !== null && typeof v !== 'object');
  if (entries.length === 0) return <p className="text-xs text-text-muted m-0">Sin detalle adicional.</p>;
  return (
    <div className="flex flex-col gap-1">
      {entries.map(([key, value]) => <div key={key} className="text-xs"><strong>{FIELDS[key] ?? key}:</strong> {show(value)}</div>)}
    </div>
  );
}

export default function ActivityTab() {
  const [filters, setFilters] = useState({ module: '', from: '', to: '', search: '' });
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [expanded, setExpanded] = useState(null);
  const [labels, setLabels] = useState({});

  useEffect(() => {
    api.get('/academy-config/permissions')
      .then(r => setLabels(Object.fromEntries(r.data.flatMap(g => g.items.map(i => [i.id, i.label])))))
      .catch(() => {});
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => { setFilters(f => f.search === search ? f : { ...f, search }); setPage(1); }, 350);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    const params = { page, pageSize: PAGE_SIZE };
    Object.entries(filters).forEach(([k, v]) => { if (v) params[k] = v; });
    api.get('/academy-config/activity', { params })
      .then(r => { if (!cancelled) setData(r.data); })
      .catch(() => { if (!cancelled) toast.error('No se pudo cargar la actividad.'); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [filters, page]);

  const setFilter = (key, value) => { setFilters(f => ({ ...f, [key]: value })); setPage(1); };
  const totalPages = data ? Math.max(1, Math.ceil(data.total / PAGE_SIZE)) : 1;

  return (
    <div className="card">
      <div className="card-header">
        <div>
          <h3 className="card-title flex items-center gap-2"><Activity size={18} className="text-primary" /> Actividad</h3>
          <p className="text-muted mt-1">Registro de quién hizo qué y cuándo. No se puede editar ni borrar.</p>
        </div>
      </div>

      <div className="access-filters">
        <div className="access-search">
          <Search size={15} className="absolute left-3 top-1/2 -translate-y-1/2 text-text-muted" />
          <input className="form-control pl-9" placeholder="Buscar por persona" aria-label="Buscar por persona" value={search} onChange={e => setSearch(e.target.value)} />
        </div>
        <select className="form-control" aria-label="Filtrar por módulo" value={filters.module} onChange={e => setFilter('module', e.target.value)}>
          {MODULES.map(m => <option key={m.value} value={m.value}>{m.label}</option>)}
        </select>
        <input type="date" className="form-control" aria-label="Desde" value={filters.from} onChange={e => setFilter('from', e.target.value)} />
        <input type="date" className="form-control" aria-label="Hasta" value={filters.to} onChange={e => setFilter('to', e.target.value)} />
      </div>

      <div className="table-wrap">
        <table>
          <thead><tr><th>Fecha</th><th>Persona</th><th>Acción</th><th>Módulo</th><th /></tr></thead>
          <tbody>
            {loading && !data && <tr><td colSpan="5" className="text-center p-8 text-text-muted">Cargando…</td></tr>}
            {data?.items.length === 0 && <tr><td colSpan="5" className="text-center p-8 text-text-muted">No hay actividad con estos filtros.</td></tr>}
            {data?.items.map(item => (
              <FragmentRow key={item.id} item={item} labels={labels} open={expanded === item.id} onToggle={() => setExpanded(expanded === item.id ? null : item.id)} />
            ))}
          </tbody>
        </table>
      </div>

      {data && data.total > 0 && (
        <div className="flex justify-between items-center mt-4">
          <span className="text-xs text-text-muted">{data.total} {data.total === 1 ? 'registro' : 'registros'}</span>
          <div className="flex items-center gap-2">
            <button className="btn btn-ghost btn-sm" disabled={page <= 1} onClick={() => setPage(p => p - 1)} aria-label="Página anterior"><ChevronLeft size={14} /></button>
            <span className="text-xs">Página {page} de {totalPages}</span>
            <button className="btn btn-ghost btn-sm" disabled={page >= totalPages} onClick={() => setPage(p => p + 1)} aria-label="Página siguiente"><ChevronRight size={14} /></button>
          </div>
        </div>
      )}
    </div>
  );
}

function FragmentRow({ item, labels, open, onToggle }) {
  const failed = item.action === 'auth.login_failed';
  return (
    <>
      <tr>
        <td className="text-xs whitespace-nowrap">{new Date(item.createdAt).toLocaleString('es-PE', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' })}</td>
        <td className="text-sm">{item.actorName}</td>
        <td>
          <span className={`text-sm ${failed ? 'text-danger' : ''}`}>{item.description}</span>
          {item.targetName && <span className="text-sm text-text-muted"> · {item.targetName}</span>}
        </td>
        <td><span className={`badge ${MODULE_BADGE[item.module] ?? 'badge-muted'}`} style={{ textTransform: 'capitalize' }}>{item.module}</span></td>
        <td className="text-right">
          <button className="btn-icon inline-flex" onClick={onToggle} aria-expanded={open} aria-label="Ver detalle">{open ? <ChevronUp size={15} /> : <ChevronDown size={15} />}</button>
        </td>
      </tr>
      {open && (
        <tr>
          <td colSpan="5" style={{ background: 'var(--bg-surface)' }}>
            <Details details={item.details} labels={labels} />
            <div className="text-xs text-text-muted mt-2">{item.device}{item.ipAddress ? ` · IP ${item.ipAddress}` : ''}</div>
          </td>
        </tr>
      )}
    </>
  );
}
