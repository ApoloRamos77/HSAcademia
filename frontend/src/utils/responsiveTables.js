// En celular las tablas se muestran como tarjetas (ver "TABLAS EN CELULAR" en index.css).
// Para que cada dato lleve su título, se copia el encabezado de la columna a `data-label`
// de cada celda. Se hace una sola vez para toda la app y se repite cuando React cambia el DOM,
// así ninguna pantalla tiene que acordarse de hacerlo.
// Una tabla que deba conservar el formato de grilla (matrices) lleva la clase `table-grid`.

const headerLabels = table => {
  const row = table.tHead?.rows[table.tHead.rows.length - 1];
  if (!row) return null;
  const labels = [];
  for (const th of row.cells) {
    const text = th.textContent.replace(/\s+/g, ' ').trim();
    for (let i = 0; i < (th.colSpan || 1); i++) labels.push(text);
  }
  return labels;
};

const labelTable = table => {
  if (table.classList.contains('table-grid')) return;
  const labels = headerLabels(table);
  if (!labels) return;
  for (const body of table.tBodies) {
    for (const tr of body.rows) {
      let col = 0;
      for (const td of tr.cells) {
        const span = td.colSpan || 1;
        // Una celda que ocupa varias columnas es un título de grupo o un mensaje: va de ancho completo.
        const label = span > 1 ? '' : labels[col] || '';
        if (td.dataset.label !== label) td.dataset.label = label;
        td.classList.toggle('td-full', span > 1);
        col += span;
      }
    }
  }
};

let scheduled = false;
const run = () => {
  scheduled = false;
  document.querySelectorAll('table').forEach(labelTable);
};

export function startResponsiveTables() {
  if (typeof window === 'undefined' || !window.MutationObserver) return;
  const schedule = () => { if (!scheduled) { scheduled = true; requestAnimationFrame(run); } };
  new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true, characterData: true });
  schedule();
}
