import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { ChevronRight, ShieldCheck, HeartPulse, Smartphone, TrendingUp, Users, Calendar, Trophy, MessageCircle } from 'lucide-react';
import './LandingProduct.css';

export default function LandingProduct() {
  const navigate = useNavigate();
  const [scrolled, setScrolled] = useState(false);

  useEffect(() => {
    const handleScroll = () => {
      setScrolled(window.scrollY > 50);
    };
    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return (
    <div className="landing-product">
      {/* Navbar */}
      <nav className={`lp-nav ${scrolled ? 'scrolled' : ''}`}>
        <div className="lp-nav-container">
          <div className="lp-brand">
            <span className="lp-brand-text">CLUB<span>iO</span></span>
          </div>
          
          <div className="lp-nav-links">
            <a href="#solucion">La Solución</a>
            <a href="#beneficios">Beneficios</a>
            <a href="#caracteristicas">Características</a>
          </div>

          <div className="lp-nav-actions">
            <button className="lp-btn-outline" onClick={() => navigate('/login')}>
              Ingresar
            </button>
            <button className="lp-btn-primary" onClick={() => navigate('/registro')}>
              Crear Cuenta
            </button>
          </div>
        </div>
      </nav>

      {/* Hero Section */}
      <header className="lp-hero" id="inicio">
        <div className="lp-hero-content">
          <div className="lp-badge">Transforma tu Gestión</div>
          <h1 className="lp-hero-title">El Corazón de tu Academia Deportiva</h1>
          <p className="lp-hero-subtitle">
            Aumenta el recaudo, organiza entrenamientos, controla la asistencia y comunícate con tus alumnos. Todo en una sola plataforma inteligente.
          </p>
          <div className="lp-hero-cta">
            <button className="lp-btn-primary large" onClick={() => navigate('/registro')}>
              Comienza Gratis <ChevronRight size={20} />
            </button>
            <button className="lp-btn-secondary large" onClick={() => window.location.href='#beneficios'}>
              Descubre más
            </button>
          </div>
        </div>
      </header>

      {/* Beneficios */}
      <section className="lp-benefits" id="beneficios">
        <div className="lp-section-header">
          <h2>¿Por qué elegir CLUBiO?</h2>
          <p>La herramienta definitiva para impulsar el crecimiento de tu club.</p>
        </div>
        <div className="lp-benefits-grid">
          <div className="lp-benefit-card">
            <TrendingUp size={40} className="icon green" />
            <h3>Aumenta tus Ingresos</h3>
            <p>Automatiza el control de pagos y reduce la morosidad con recordatorios inteligentes y gestión financiera clara.</p>
          </div>
          <div className="lp-benefit-card">
            <ShieldCheck size={40} className="icon blue" />
            <h3>Organización Total</h3>
            <p>Control exacto de asistencias, historial médico, evaluaciones técnicas y categorización de alumnos.</p>
          </div>
          <div className="lp-benefit-card">
            <MessageCircle size={40} className="icon green" />
            <h3>Comunicación Activa</h3>
            <p>Mantén a los padres y alumnos informados sobre horarios, torneos y noticias directamente en la plataforma.</p>
          </div>
        </div>
      </section>

      {/* Features */}
      <section className="lp-features" id="caracteristicas">
        <div className="lp-features-container">
          <div className="lp-features-text">
            <h2>Gestión, Salud y Deporte unidos</h2>
            <p>Nuestra plataforma cubre todos los aspectos necesarios para operar tu academia de manera profesional.</p>
            <ul className="lp-features-list">
              <li><Users className="f-icon" /> <strong>Alumnos:</strong> Perfiles detallados y control.</li>
              <li><Calendar className="f-icon" /> <strong>Entrenamientos:</strong> Planificación y asistencia.</li>
              <li><Trophy className="f-icon" /> <strong>Torneos:</strong> Estadísticas y seguimiento competitivo.</li>
              <li><HeartPulse className="f-icon" /> <strong>Salud:</strong> Fichas médicas e IMC.</li>
              <li><TrendingUp className="f-icon" /> <strong>Reportes:</strong> Analíticas de rendimiento e ingresos.</li>
              <li><Smartphone className="f-icon" /> <strong>App Integrada:</strong> Portal web responsive para todos.</li>
            </ul>
          </div>
          <div className="lp-features-visual">
            <div className="mockup-placeholder">
              <div className="mockup-content">
                <span className="lp-brand-text">CLUB<span>iO</span></span>
                <p>Dashboard Administrativo</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* CTA Section */}
      <section className="lp-cta-section">
        <div className="lp-cta-box">
          <h2>¿Listo para llevar tu club al siguiente nivel?</h2>
          <p>Únete a las decenas de academias que ya confían en CLUBiO.</p>
          <button className="lp-btn-primary huge" onClick={() => navigate('/registro')}>
            Crea tu Academia Ahora
          </button>
        </div>
      </section>

      {/* Footer */}
      <footer className="lp-footer">
        <div className="lp-footer-content">
          <div className="lp-footer-brand">
            <span className="lp-brand-text">CLUB<span>iO</span></span>
            <p>El aliado tecnológico de los clubes modernos.</p>
          </div>
          <div className="lp-footer-links">
            <h4>Producto</h4>
            <a href="#solucion">Características</a>
            <a href="#beneficios">Beneficios</a>
          </div>
          <div className="lp-footer-links">
            <h4>Soporte</h4>
            <a href="#">Centro de Ayuda</a>
            <a href="#">Contacto</a>
          </div>
        </div>
        <div className="lp-footer-bottom">
          <p>&copy; {new Date().getFullYear()} CLUBiO. Todos los derechos reservados.</p>
        </div>
      </footer>
    </div>
  );
}
