import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ChevronRight, Calendar, Trophy, ShoppingBag, Phone, Mail, MapPin, Send, MessageCircle } from 'lucide-react';
import './Landing.css';

export default function Landing() {
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
    <div className="landing-page">
      {/* Navbar */}
      <nav className={`landing-nav ${scrolled ? 'nav-scrolled' : ''}`}>
        <div className="nav-container">
          <div className="nav-brand">
            <div className="brand-logo">
              <span>HS</span>
            </div>
            <span className="brand-text">HELPER SOFT SPORT</span>
          </div>
          
          <div className="nav-links">
            <a href="#inicio">Inicio</a>
            <a href="#entrenamiento">Entrenamiento</a>
            <a href="#torneos">Torneos</a>
            <a href="#productos">Productos</a>
            <a href="#contacto">Contáctenos</a>
          </div>

          <div className="nav-actions">
            <button className="btn-login" onClick={() => navigate('/login')}>
              Iniciar Sesión
            </button>
          </div>
        </div>
      </nav>

      {/* Floating WhatsApp Button */}
      <a href="https://wa.me/51941883990" target="_blank" rel="noreferrer" className="fab-whatsapp">
        <MessageCircle size={28} />
      </a>

      {/* Hero Section */}
      <section id="inicio" className="hero-section">
        <div className="hero-overlay"></div>
        <div className="hero-content">
          <span className="hero-badge">Bienvenido a la Élite</span>
          <h1 className="hero-title">ACADEMIA DEPORTIVA<br/>HELPER SOFT SPORT</h1>
          <p className="hero-subtitle">
            Formando campeones con valores y pasión por el deporte. Únete a nuestra gran familia y desarrolla tu máximo potencial en la cancha.
          </p>
          <div className="hero-buttons">
            <button className="btn-primary-large" onClick={() => navigate('/registro')}>
              Inscríbete Ahora <ChevronRight size={18} />
            </button>
            <a href="#entrenamiento" className="btn-ghost-large">
              Conoce más
            </a>
          </div>
        </div>
      </section>

      {/* Entrenamiento Section */}
      <section id="entrenamiento" className="training-section">
        <div className="section-header">
          <h2 className="section-title">ENTRENAMIENTO</h2>
          <div className="section-divider"></div>
          <p className="section-desc">Explora nuestras jornadas de entrenamiento donde la técnica y el esfuerzo se unen para formar verdaderos profesionales.</p>
        </div>

        <div className="training-grid">
          <div className="training-card">
            <div className="card-img sub15"></div>
            <div className="card-content">
              <h3>Categoría Sub-15</h3>
              <p>Constancia, disciplina y táctica avanzada para los más grandes de la academia.</p>
            </div>
          </div>
          <div className="training-card">
            <div className="card-img sub13"></div>
            <div className="card-content">
              <h3>Categoría Sub-13</h3>
              <p>Esfuerzo y dedicación en cada práctica, mejorando habilidades técnicas individuales.</p>
            </div>
          </div>
          <div className="training-card">
            <div className="card-img baby"></div>
            <div className="card-content">
              <h3>Baby Fútbol</h3>
              <p>Talento y disciplina desde los primeros pasos. Diversión asegurada mientras aprenden.</p>
            </div>
          </div>
        </div>
      </section>

      {/* Torneos Section */}
      <section id="torneos" className="tournaments-section">
        <div className="section-header dark">
          <h2 className="section-title">TORNEOS Y COMPETENCIAS</h2>
          <div className="section-divider"></div>
          <p className="section-desc">Participamos activamente en ligas competitivas demostrando nuestro nivel.</p>
        </div>

        <div className="tournaments-grid">
          <div className="tournament-card">
            <div className="t-icon"><Trophy size={40} /></div>
            <h4>Torneo Cubacup</h4>
            <p>Nuestro equipo dejando todo en la cancha, alcanzando las fases finales del torneo nacional.</p>
          </div>
          <div className="tournament-card">
            <div className="t-icon"><Calendar size={40} /></div>
            <h4>Liga Regional de Menores</h4>
            <p>Competencia constante fin de semana a fin de semana para mantener el ritmo futbolístico.</p>
          </div>
        </div>
      </section>

      {/* Productos Section */}
      <section id="productos" className="products-section">
        <div className="section-header">
          <h2 className="section-title">NUESTROS PRODUCTOS</h2>
          <div className="section-divider"></div>
          <p className="section-desc">Consulta sobre nuestros equipos, uniformes oficiales y accesorios deportivos.</p>
        </div>

        <div className="products-empty">
          <ShoppingBag size={48} className="empty-icon" />
          <h3>Próximamente Tienda Virtual</h3>
          <p>Estamos preparando el catálogo de indumentaria deportiva oficial de Helper Soft Sport.</p>
        </div>
      </section>

      {/* Contacto & Footer Section */}
      <footer id="contacto" className="footer-section">
        <div className="footer-container">
          
          <div className="footer-info">
            <h2 className="footer-title">CONTÁCTENOS</h2>
            <div className="section-divider left"></div>
            <p className="footer-desc">¿Tienes dudas o deseas inscribirte? Escríbenos o visítanos en nuestra sede principal.</p>
            
            <div className="contact-list">
              <div className="contact-item">
                <div className="c-icon"><Phone size={20} /></div>
                <div>
                  <strong>Teléfono / WhatsApp</strong>
                  <span>+51 941 883 990</span>
                </div>
              </div>
              <div className="contact-item">
                <div className="c-icon"><Mail size={20} /></div>
                <div>
                  <strong>Correo Electrónico</strong>
                  <span>nvelazco@helpersoft.com.pe</span>
                </div>
              </div>
              <div className="contact-item">
                <div className="c-icon"><MapPin size={20} /></div>
                <div>
                  <strong>Sede Principal</strong>
                  <span>Colegio El Divino Maestro - Puente Piedra</span>
                </div>
              </div>
            </div>
          </div>

          <div className="footer-form">
            <div className="form-card">
              <h3>Envíanos un Mensaje</h3>
              <form onSubmit={e => e.preventDefault()}>
                <div className="form-row">
                  <input type="text" placeholder="Nombres" required />
                  <input type="text" placeholder="Apellidos" required />
                </div>
                <div className="form-row">
                  <input type="email" placeholder="Correo Electrónico" required />
                  <input type="tel" placeholder="Celular" required />
                </div>
                <textarea placeholder="¿En qué te podemos ayudar?" rows="4"></textarea>
                <button type="submit" className="btn-submit">
                  Enviar Información <Send size={16} />
                </button>
              </form>
            </div>
          </div>
        </div>
        <div className="footer-bottom">
          <p>&copy; {new Date().getFullYear()} ADHSOFT SPORT. Todos los derechos reservados.</p>
        </div>
      </footer>
    </div>
  );
}
