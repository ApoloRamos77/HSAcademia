import { Archive, BarChart3, DollarSign, Inbox, Receipt, TrendingDown, Users, Wallet } from 'lucide-react';
import { FINANCE_ACCESS } from './permissions';

/**
 * Opciones de Finanzas en el menú lateral: cada una es su propia página (/academy/finanzas/:key).
 * `visible` decide con los permisos del usuario (el servidor los vuelve a validar).
 * La contabilidad general (resumen, gastos, planilla) no se acota a una sede: exige alcance de toda la academia.
 */
export const FINANCE_SECTIONS = [
  { key: 'resumen', label: 'Resumen', title: 'Resumen financiero', icon: BarChart3,
    visible: ({ can, academyWide }) => can('finances.summary') && academyWide },
  // Quien solo puede generar mensualidades (sin ver deudas) entra para usar "Acciones del mes", como antes en Configuración.
  { key: 'cobranzas', label: 'Cobranzas', title: 'Cobranzas', icon: DollarSign,
    visible: (auth) => auth.can('collections.view', 'collections.collect') || (canRunMonthActions(auth) && auth.can(...FINANCE_ACCESS)) },
  { key: 'comprobantes', label: 'Comprobantes', title: 'Comprobantes de las familias', icon: Inbox,
    visible: ({ can }) => can('collections.approve_proofs') },
  { key: 'recibos', label: 'Recibos', title: 'Recibos', icon: Receipt,
    visible: ({ can }) => can('collections.view', 'collections.collect') },
  { key: 'caja', label: 'Caja', title: 'Caja', icon: Wallet,
    labelFor: ({ can, academyWide }) => (can('cash.receive') && academyWide ? 'Caja' : 'Mi caja'),
    visible: ({ can, academyWide }) => (can('cash.receive') && academyWide) || can('collections.collect') },
  { key: 'gastos', label: 'Gastos', title: 'Gastos', icon: TrendingDown,
    visible: ({ can, academyWide }) => can('finances.expenses') && academyWide },
  { key: 'planilla', label: 'Planilla', title: 'Planilla', icon: Users,
    visible: ({ can, academyWide }) => can('finances.staff_payments') && academyWide },
];

export const PETTY_ICON = Archive;

export const visibleFinanceSections = (auth) => FINANCE_SECTIONS.filter(s => s.visible(auth));

/** Configuración → Cobros y descuentos (día de cobro, mora, datos de los recibos, descuentos, referidos). */
export const canConfigureCollections = ({ can, academyWide }) => can('finances.config') && academyWide;

/** Generar mensualidades / liquidar sesiones (Cobranzas → Acciones del mes). */
export function canRunMonthActions({ can, academyWide }) { return can('collections.generate') && academyWide; }
