import api from '../api/axios';

/** URL absoluta de un archivo servido por la API (ej. "/uploads/vouchers/x.jpg"). */
export function fileUrl(path) {
  if (!path) return '';
  if (/^https?:\/\//i.test(path)) return path;
  const base = (api.defaults.baseURL || '').replace(/\/api\/?$/, '');
  return `${base}${path}`;
}

/** Descarga un archivo protegido (con el token) y lo guarda con su nombre. */
export async function downloadFile(url, fallbackName) {
  const res = await api.get(url, { responseType: 'blob' });
  const disposition = res.headers['content-disposition'] || '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  const name = match ? decodeURIComponent(match[1]) : fallbackName;
  const href = URL.createObjectURL(res.data);
  const a = document.createElement('a');
  a.href = href;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(href);
}
