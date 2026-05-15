import { jsPDF } from "jspdf";
import autoTable from "jspdf-autotable";

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
  
  // Header
  doc.setFont("helvetica", "bold");
  doc.setFontSize(12);
  doc.setTextColor(0, 86, 179); // Blue
  doc.text("ACADEMIA DEPORTIVA", 52.5, 12, { align: "center" });
  
  doc.setFontSize(9);
  doc.setFont("helvetica", "normal");
  doc.setTextColor(100, 100, 100);
  doc.text("Helper Soft Sport", 52.5, 17, { align: "center" });
  
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

  // Company Info (Left)
  doc.setTextColor(0, 86, 179);
  doc.setFontSize(8);
  doc.text("ADHSOFT SPORT", 5, 25);
  doc.setFontSize(7);
  doc.setTextColor(100, 100, 100);
  doc.text("Jr. J.C. Mariategui 315 Block E1", 5, 29);
  doc.text("Dpto 102 Condominio La Pradera Club", 5, 33);
  doc.text("Puente Piedra", 5, 37);
  
  // Date Box
  const today = new Date();
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
  doc.line(76, 60, 100, 60);
  
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

  // Abrir en nueva pestaña del navegador (sin descargar ni abrir Acrobat)
  const pdfBlob = doc.output('blob');
  const blobUrl = URL.createObjectURL(pdfBlob);
  const win = window.open(blobUrl, '_blank');
  // Liberar la URL del blob cuando la ventana se cierre
  if (win) win.addEventListener('unload', () => URL.revokeObjectURL(blobUrl));
};
