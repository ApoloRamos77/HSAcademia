import { jsPDF } from "jspdf";
import "jspdf-autotable";

export const generateReceiptPDF = (data) => {
  const doc = new jsPDF();
  
  // Header
  doc.setFont("helvetica", "bold");
  doc.setFontSize(16);
  doc.setTextColor(0, 86, 179); // Blue
  doc.text("ACADEMIA DEPORTIVA", 105, 20, { align: "center" });
  
  doc.setFontSize(12);
  doc.setFont("helvetica", "normal");
  doc.setTextColor(100, 100, 100);
  doc.text("Helper Soft Sport", 105, 27, { align: "center" });
  
  // Receipt Title & Number
  doc.setFillColor(0, 86, 179);
  doc.rect(130, 35, 60, 8, "F");
  doc.setTextColor(255, 255, 255);
  doc.setFontSize(12);
  doc.setFont("helvetica", "bold");
  doc.text("RECIBO", 160, 41, { align: "center" });
  
  doc.setTextColor(200, 0, 0); // Red
  doc.setFontSize(14);
  const receiptNum = data.receiptNumber || Math.floor(100000 + Math.random() * 900000).toString();
  doc.text(`N° ${receiptNum}`, 160, 50, { align: "center" });

  // Company Info (Left)
  doc.setTextColor(0, 86, 179);
  doc.setFontSize(10);
  doc.text("ADHSOFT SPORT", 20, 20);
  doc.setFontSize(8);
  doc.setTextColor(100, 100, 100);
  doc.text("Jr. J.C. Mariategui 315 Block E1", 20, 25);
  doc.text("Dpto 102 Condominio La Pradera Club", 20, 30);
  doc.text("Puente Piedra", 20, 35);
  
  // Date Box
  const today = new Date();
  doc.setDrawColor(0, 86, 179);
  doc.setFillColor(0, 86, 179);
  doc.rect(20, 45, 60, 6, "FD");
  doc.setTextColor(255, 255, 255);
  doc.text("DÍA", 30, 49, { align: "center" });
  doc.text("MES", 50, 49, { align: "center" });
  doc.text("AÑO", 70, 49, { align: "center" });
  
  doc.rect(20, 51, 60, 6, "S");
  doc.setTextColor(0, 0, 0);
  doc.text(today.getDate().toString().padStart(2, '0'), 30, 55, { align: "center" });
  doc.text((today.getMonth() + 1).toString().padStart(2, '0'), 50, 55, { align: "center" });
  doc.text(today.getFullYear().toString(), 70, 55, { align: "center" });

  // Customer Info
  doc.setDrawColor(0, 0, 0);
  doc.line(20, 65, 190, 65);
  
  doc.setFont("helvetica", "bold");
  doc.setFontSize(10);
  doc.text("Alumno / Cliente:", 20, 72);
  doc.setFont("helvetica", "normal");
  doc.text(data.customerName || "Público General", 55, 72);
  
  doc.setFont("helvetica", "bold");
  doc.text("D.N.I:", 140, 72);
  doc.line(152, 73, 190, 73);

  // Table
  const tableData = [
    [data.quantity || 1, data.description || "Descripción no disponible", `S/ ${parseFloat(data.total).toFixed(2)}`]
  ];
  
  doc.autoTable({
    startY: 85,
    head: [["CANT.", "DESCRIPCIÓN", "TOTAL"]],
    body: tableData,
    theme: "grid",
    headStyles: { fillColor: [0, 86, 179], textColor: 255, fontStyle: 'bold' },
    styles: { fontSize: 10, cellPadding: 4 },
    columnStyles: {
      0: { cellWidth: 20, halign: 'center' },
      1: { cellWidth: 130 },
      2: { cellWidth: 40, halign: 'right' }
    }
  });
  
  const finalY = doc.lastAutoTable.finalY || 120;

  // Observations
  doc.setDrawColor(0, 86, 179);
  doc.roundedRect(20, finalY + 10, 170, 15, 2, 2, "S");
  doc.setFont("helvetica", "bold");
  doc.setTextColor(0, 86, 179);
  doc.text("Observaciones:", 23, finalY + 15);
  doc.setFont("helvetica", "normal");
  doc.setTextColor(0, 0, 0);
  doc.text(data.notes || "Ninguna", 23, finalY + 20);

  // Thanks message
  doc.setFont("helvetica", "italic");
  doc.setTextColor(0, 86, 179);
  doc.text("Gracias por su Preferencia...", 105, finalY + 35, { align: "center" });

  // Total
  doc.setDrawColor(0, 86, 179);
  doc.setFillColor(240, 248, 255); // Light Blue
  doc.roundedRect(130, finalY + 45, 60, 12, 2, 2, "FD");
  doc.setFont("helvetica", "bold");
  doc.setTextColor(0, 0, 0);
  doc.setFontSize(14);
  doc.text(`TOTAL: S/ ${parseFloat(data.total).toFixed(2)}`, 160, finalY + 53, { align: "center" });

  doc.save(`Recibo_${receiptNum}.pdf`);
};
