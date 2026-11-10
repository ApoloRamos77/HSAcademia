// Permisos que abren cada sección de la web. El servidor es quien los hace cumplir.
export const FINANCE_ACCESS = [
  'collections.view', 'collections.collect', 'collections.approve_proofs', 'cash.receive',
  'finances.config', 'finances.expenses', 'finances.staff_payments', 'finances.summary', 'finances.close',
];
export const STORE_ACCESS = ['store.sell', 'store.inventory'];
export const REPORTS_ACCESS = ['reports.finance', 'reports.students', 'reports.attendance'];
