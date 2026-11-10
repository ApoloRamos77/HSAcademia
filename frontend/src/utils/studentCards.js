import { jsPDF } from 'jspdf';
import QRCode from 'qrcode';

const CARD_W = 85.6;
const CARD_H = 54;
const COLUMNS = 2;
const ROWS = 5;
const GAP = 3;
const MARGIN_X = (210 - COLUMNS * CARD_W - (COLUMNS - 1) * GAP) / 2;
const MARGIN_Y = (297 - ROWS * CARD_H - (ROWS - 1) * GAP) / 2;
const HEADER_H = 11;
const QR_SIZE = 36;

/** Reduce el texto hasta que entre en el ancho disponible. */
const fitText = (doc, text, maxWidth, size, minSize = 7) => {
  let current = size;
  doc.setFontSize(current);
  while (doc.getTextWidth(text) > maxWidth && current > minSize) {
    current -= 0.5;
    doc.setFontSize(current);
  }
  return doc.splitTextToSize(text, maxWidth).slice(0, 2);
};

/**
 * PDF A4 con carnés de tamaño tarjeta (10 por hoja) para imprimir y recortar.
 * El QR es el mismo que el del carné de la app: {"u": <Student.Id>}, sin datos personales.
 */
export const printStudentCards = async (students, academyName) => {
  const codes = await Promise.all(students.map(s =>
    QRCode.toDataURL(JSON.stringify({ u: s.id }), { margin: 1, width: 360, errorCorrectionLevel: 'M' })));
  const doc = new jsPDF({ format: 'a4', unit: 'mm', compress: true });

  students.forEach((student, index) => {
    const slot = index % (COLUMNS * ROWS);
    if (index > 0 && slot === 0) doc.addPage();
    const x = MARGIN_X + (slot % COLUMNS) * (CARD_W + GAP);
    const y = MARGIN_Y + Math.floor(slot / COLUMNS) * (CARD_H + GAP);

    doc.setDrawColor(190);
    doc.setLineWidth(0.2);
    doc.rect(x, y, CARD_W, CARD_H);

    doc.setFillColor(19, 35, 64);
    doc.rect(x, y, CARD_W, HEADER_H, 'F');
    doc.setTextColor(255);
    doc.setFont('helvetica', 'bold');
    const header = fitText(doc, academyName || 'Academia', CARD_W - 6, 11);
    doc.text(header[0], x + 3, y + 7);

    const textWidth = CARD_W - QR_SIZE - 8;
    doc.setTextColor(25);
    doc.setFont('helvetica', 'bold');
    const name = fitText(doc, `${student.firstName} ${student.lastName}`.trim(), textWidth, 12, 8);
    doc.text(name, x + 3, y + HEADER_H + 8);

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8.5);
    doc.setTextColor(80);
    const details = [student.categoryName, student.headquarterName ? `Sede ${student.headquarterName}` : null].filter(Boolean);
    doc.text(details.flatMap(d => doc.splitTextToSize(d, textWidth)).slice(0, 3), x + 3, y + HEADER_H + 21);

    doc.setFontSize(7);
    doc.text('Muestra este QR al entrenador', x + 3, y + CARD_H - 3);

    doc.addImage(codes[index], 'PNG', x + CARD_W - QR_SIZE - 3, y + HEADER_H + 3.5, QR_SIZE, QR_SIZE, undefined, 'FAST');
  });

  const url = URL.createObjectURL(doc.output('blob'));
  window.open(url, '_blank');
};
