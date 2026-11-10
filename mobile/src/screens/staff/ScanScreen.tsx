import React from 'react';
import QrScanner from '../../components/QrScanner';

/** Escáner suelto, pensado para la puerta de la cancha: el servidor asigna cada carné a la sesión de hoy más cercana. */
export default function ScanScreen() {
  return <QrScanner />;
}
