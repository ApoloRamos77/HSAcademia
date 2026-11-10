import { useSearchParams } from 'react-router-dom';
import { Activity, ShieldCheck, Users } from 'lucide-react';
import AppLayout from '../../components/AppLayout';
import TeamTab from '../../components/access/TeamTab';
import RolesTab from '../../components/access/RolesTab';
import ActivityTab from '../../components/access/ActivityTab';

const TABS = [
  { key: 'equipo', label: 'Equipo', icon: Users },
  { key: 'roles', label: 'Roles y permisos', icon: ShieldCheck },
  { key: 'actividad', label: 'Actividad', icon: Activity },
];

/** Centro de administración de accesos: quién entra, qué puede hacer y qué ha hecho. */
export default function Accesos() {
  const [params, setParams] = useSearchParams();
  const active = TABS.some(t => t.key === params.get('tab')) ? params.get('tab') : 'equipo';

  return (
    <AppLayout title="Accesos y seguridad">
      <div className="fade-in access-scope">
        <div className="access-tabs" role="tablist">
          {TABS.map(({ key, label, icon: Icon }) => (
            <button
              key={key}
              role="tab"
              aria-selected={active === key}
              onClick={() => setParams({ tab: key })}
              className={`btn ${active === key ? 'btn-primary' : 'btn-ghost'}`}
            >
              <Icon size={16} /> {label}
            </button>
          ))}
        </div>

        {active === 'equipo' && <TeamTab />}
        {active === 'roles' && <RolesTab />}
        {active === 'actividad' && <ActivityTab />}
      </div>
    </AppLayout>
  );
}
