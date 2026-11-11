import { jsPDF } from "jspdf";
import autoTable from "jspdf-autotable";

/**
 * Encabezado de los recibos (nombre, dirección y teléfono únicos de la academia). Se trae del servidor
 * al iniciar sesión y al volver a la ventana; nunca se usa la dirección de una sede.
 */
export const saveReceiptProfile = (p) => {
  try {
    localStorage.setItem('academyProfile', JSON.stringify({ name: p?.name || '', address: p?.address || '', phone: p?.phone || '' }));
  } catch { /* almacenamiento no disponible */ }
};

export const loadReceiptProfile = (api) =>
  api.get('/academy-config/receipt-profile').then(r => saveReceiptProfile(r.data)).catch(() => {});

const getAcademyProfile = () => {
  try {
    const profile = JSON.parse(localStorage.getItem('academyProfile') || 'null');
    const user = JSON.parse(localStorage.getItem('user') || 'null');
    return { name: profile?.name || user?.academyName || '', address: profile?.address || '', phone: profile?.phone || '' };
  } catch {
    return { name: '', address: '', phone: '' };
  }
};

export const generateReceiptPDF = (data) => {
  // 1. Calcular la altura requerida
  const dummyDoc = new jsPDF({ format: [105, 297] });
  
  let tableData = [];
  if (data.items && data.items.length > 0) {
    tableData = data.items.map(item => [
      item.quantity || 1, 
      item.description || "Descripción no disponible", 
      `S/. ${parseFloat(item.total).toFixed(2)}`
    ]);
  } else {
    tableData = [
      [data.quantity || 1, data.description || "Descripción no disponible", `S/. ${parseFloat(data.total).toFixed(2)}`]
    ];
  }
  
  autoTable(dummyDoc, {
    startY: 65,
    head: [["CANT", "DESCRIPCIÓN", "TOTAL"]],
    body: tableData,
    theme: "grid",
    styles: { fontSize: 8, cellPadding: 2 },
    columnStyles: {
      0: { cellWidth: 12 },
      1: { cellWidth: 63 },
      2: { cellWidth: 20 }
    },
    margin: { left: 5, right: 5 }
  });
  
  const calculatedFinalY = dummyDoc.lastAutoTable.finalY || 80;
  const totalHeight = Math.max(120, calculatedFinalY + 45);

  // 2. Generar el documento real con altura dinámica
  const doc = new jsPDF({ format: [105, totalHeight] });
  
  // Datos de la academia que emite el recibo (multi-academia): se guardan al iniciar sesión.
  const academy = getAcademyProfile();
  const academyName = (academy.name || 'Academia deportiva').toUpperCase();

  // Header
  doc.setFont("helvetica", "bold");
  doc.setFontSize(academyName.length > 28 ? 10 : 12);
  doc.setTextColor(0, 86, 179); // Blue
  doc.text(doc.splitTextToSize(academyName, 95).slice(0, 2), 52.5, 12, { align: "center" });

  doc.setFontSize(7);
  doc.setFont("helvetica", "normal");
  doc.setTextColor(120, 120, 120);
  doc.text("Emitido con Clubio", 52.5, 19, { align: "center" });

  // Receipt Title & Number
  doc.setFillColor(0, 86, 179);
  doc.rect(60, 23, 40, 6, "F");
  doc.setTextColor(255, 255, 255);
  doc.setFontSize(9);
  doc.setFont("helvetica", "bold");
  doc.text("RECIBO", 80, 27.5, { align: "center" });

  doc.setTextColor(200, 0, 0); // Red
  doc.setFontSize(10);
  const receiptNum = data.receiptNumber || 'S/N';
  doc.text(`N° ${receiptNum}`, 80, 35, { align: "center" });

  // Company Info (Left): datos de contacto de la academia
  doc.setTextColor(0, 86, 179);
  doc.setFontSize(8);
  doc.text(doc.splitTextToSize(academy.name || '', 52).slice(0, 1), 5, 25);
  doc.setFontSize(7);
  doc.setTextColor(100, 100, 100);
  const contactLines = [
    ...doc.splitTextToSize(academy.address || '', 52).slice(0, 3),
    academy.phone ? `Tel. ${academy.phone}` : '',
  ].filter(Boolean);
  contactLines.forEach((line, i) => doc.text(line, 5, 28.8 + i * 3.4));

  // Date Box: fecha del pago/venta (no la de hoy: las copias deben mostrar la fecha real)
  const issued = data.date ? new Date(data.date) : new Date();
  const today = Number.isNaN(issued.getTime()) ? new Date() : issued;
  doc.setDrawColor(0, 86, 179);
  doc.setFillColor(0, 86, 179);
  doc.rect(5, 42, 45, 5, "FD");
  doc.setTextColor(255, 255, 255);
  doc.text("DÍA", 12.5, 45.5, { align: "center" });
  doc.text("MES", 27.5, 45.5, { align: "center" });
  doc.text("AÑO", 42.5, 45.5, { align: "center" });

  doc.rect(5, 47, 45, 5, "S");
  doc.setTextColor(0, 0, 0);
  doc.text(today.getDate().toString().padStart(2, '0'), 12.5, 50.5, { align: "center" });
  doc.text((today.getMonth() + 1).toString().padStart(2, '0'), 27.5, 50.5, { align: "center" });
  doc.text(today.getFullYear().toString(), 42.5, 50.5, { align: "center" });

  // Customer Info
  doc.setDrawColor(0, 0, 0);
  doc.line(5, 55, 100, 55);
  
  doc.setFont("helvetica", "bold");
  doc.setFontSize(8);
  doc.text("Alumno/Cliente:", 5, 60);
  doc.setFont("helvetica", "normal");
  doc.text(data.customerName || "Público General", 26, 60, { maxWidth: 40 });
  
  doc.setFont("helvetica", "bold");
  doc.text("D.N.I:", 68, 60);
  if (data.document) {
    doc.setFont("helvetica", "normal");
    doc.text(String(data.document), 77, 59.5);
  } else {
    doc.line(76, 60, 100, 60);
  }
  
  autoTable(doc, {
    startY: 65,
    head: [["CANT", "DESCRIPCIÓN", "TOTAL"]],
    body: tableData,
    theme: "grid",
    headStyles: { fillColor: [0, 86, 179], textColor: 255, fontStyle: 'bold', fontSize: 8 },
    styles: { fontSize: 8, cellPadding: 2 },
    columnStyles: {
      0: { cellWidth: 12, halign: 'center' },
      1: { cellWidth: 63 },
      2: { cellWidth: 20, halign: 'right' }
    },
    margin: { left: 5, right: 5 }
  });
  
  const finalY = doc.lastAutoTable.finalY || 80;

  // Observations
  doc.setDrawColor(0, 86, 179);
  doc.roundedRect(5, finalY + 5, 95, 12, 2, 2, "S");
  doc.setFont("helvetica", "bold");
  doc.setTextColor(0, 86, 179);
  doc.setFontSize(8);
  doc.text("Observaciones:", 7, finalY + 9);
  doc.setFont("helvetica", "normal");
  doc.setTextColor(0, 0, 0);
  doc.text(data.notes || "Ninguna", 7, finalY + 14, { maxWidth: 90 });

  // Thanks message
  doc.setFont("helvetica", "italic");
  doc.setTextColor(0, 86, 179);
  doc.text("Gracias por su Preferencia...", 52.5, finalY + 25, { align: "center" });

  // Total
  doc.setDrawColor(0, 86, 179);
  doc.setFillColor(240, 248, 255); // Light Blue
  doc.roundedRect(55, finalY + 30, 45, 10, 2, 2, "FD");
  doc.setFont("helvetica", "bold");
  doc.setTextColor(0, 0, 0);
  doc.setFontSize(10);
  doc.text(`TOTAL: S/. ${parseFloat(data.total).toFixed(2)}`, 77.5, finalY + 36.5, { align: "center" });

  // Comprobante anulado: sello visible en todas las copias
  if (data.voided) {
    doc.setTextColor(200, 0, 0);
    doc.setFont("helvetica", "bold");
    doc.setFontSize(26);
    doc.text("ANULADO", 52.5, finalY - 2, { align: "center", angle: 18 });
    doc.setFontSize(7);
    if (data.voidReason) doc.text(doc.splitTextToSize(`Motivo: ${data.voidReason}`, 95).slice(0, 2), 5, finalY + 46);
  }

  // Abrir en nueva pestaña del navegador (sin descargar ni abrir Acrobat)
  const pdfBlob = doc.output('blob');
  const blobUrl = URL.createObjectURL(pdfBlob);
  const win = window.open(blobUrl, '_blank');
  // Liberar la URL del blob cuando la ventana se cierre
  if (win) win.addEventListener('unload', () => URL.revokeObjectURL(blobUrl));
};

const METHOD_LABELS = { Cash: 'Efectivo', BankTransfer: 'Transferencia', Yape: 'Yape', Plin: 'Plin', Card: 'Tarjeta', Mixed: 'Mixto', Other: 'Otro' };

const paymentLabel = (r) => r.payments?.length
  ? r.payments.map(p => [`${METHOD_LABELS[p.method] || p.method} S/ ${Number(p.amount).toFixed(2)}`, p.operationNumber ? `Op. ${p.operationNumber}` : ''].filter(Boolean).join(' ')).join(' + ')
  : [METHOD_LABELS[r.method] || r.method, r.operationNumber ? `Op. ${r.operationNumber}` : ''].filter(Boolean).join(' · ');

/** Imprime un comprobante con varios ítems tal como lo devuelve la API (/api/receipts). */
export const printReceipt = (r) => {
  const items = (r.lines || []).map(l => ({
    quantity: l.quantity || 1,
    description: [
      l.description,
      l.studentName ? `— ${l.studentName}` : '',
      l.discount > 0 ? `(${l.discountLabel ? l.discountLabel + ': ' : 'desc. '}-S/ ${Number(l.discount).toFixed(2)})` : '',
    ].filter(Boolean).join(' '),
    total: l.total,
  }));
  const payment = paymentLabel(r);
  generateReceiptPDF({
    receiptNumber: r.number,
    customerName: r.payerName,
    document: r.payerDocument,
    date: r.issuedAt,
    items,
    total: r.total,
    notes: [payment, r.notes].filter(Boolean).join(' — ') || 'Ninguna',
    voided: r.status === 'Voided',
    voidReason: r.voidReason,
  });
};
