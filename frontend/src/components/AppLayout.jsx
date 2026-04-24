import Sidebar from './Sidebar';

export default function AppLayout({ children, title }) {
  return (
    <div className="app-layout">
      <Sidebar />
      <div className="main-content">
        <header className="topbar">
          <h2 className="page-title">{title}</h2>
        </header>
        <main className="page-content">{children}</main>
      </div>
    </div>
  );
}
