import { useState } from 'react';
import Sidebar from './Sidebar';
import { Menu } from 'lucide-react';

export default function AppLayout({ children, title }) {
  const [sidebarOpen, setSidebarOpen] = useState(false);

  return (
    <div className="app-layout">
      {/* Overlay — visible en móvil cuando el sidebar está abierto */}
      {sidebarOpen && (
        <div
          className="sidebar-overlay"
          onClick={() => setSidebarOpen(false)}
          aria-hidden="true"
        />
      )}

      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />

      <div className="main-content">
        <header className="topbar">
          {/* Botón hamburguesa — solo visible en móvil vía CSS */}
          <button
            id="btn-hamburger"
            className="btn-hamburger"
            onClick={() => setSidebarOpen(true)}
            aria-label="Abrir menú de navegación"
          >
            <Menu size={20} />
          </button>
          <h2 className="page-title">{title}</h2>
        </header>
        <main className="page-content">{children}</main>
      </div>
    </div>
  );
}
