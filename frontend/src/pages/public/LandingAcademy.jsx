import { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ChevronRight, Phone, Mail, MapPin, Send, MessageCircle } from 'lucide-react';
import { useAuth } from '../../context/AuthContext';
import { getTenantData } from '../../utils/tenantConfig';
import './LandingAcademy.css';

export default function LandingAcademy() {
  const navigate = useNavigate();
  const { tenantId } = useParams();
  const { user } = useAuth();
  const [scrolled, setScrolled] = useState(false);
  const [tenantData, setTenantData] = useState(null);

  useEffect(() => {
    // In a real app, this might be an API call.
    const data = getTenantData(tenantId);
    setTenantData(data);
  }, [tenantId]);

  useEffect(() => {
    const handleScroll = () => setScrolled(window.scrollY > 50);
    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  if (!tenantData) {
    return <div className="la-not-found">Cargando...</div>;
  }

  // Inject CSS variables for the 5-color theme structure
  const tenantStyles = {
    '--la-primary': tenantData.theme.primary,
    '--la-secondary': tenantData.theme.secondary,
    '--la-accent': tenantData.theme.accent,
    '--la-text-light': tenantData.theme.textLight,
    '--la-text-dark': tenantData.theme.textDark,
  };

  return (
    <div className="landing-academy" style={tenantStyles}>
      {/* Navbar Premium */}
      <nav className={`la-nav ${scrolled ? 'nav-scrolled' : ''}`}>
        <div className="la-nav-container">
          <div className="la-brand">
            <img src={tenantData.logo} alt={tenantData.name} className="la-brand-logo" />
            <span className="la-brand-text">{tenantData.name}</span>
          </div>
          
          <div className="la-nav-links">
            <a href="#inicio">INICIO</a>
            <a href="#entrenamiento">ENTRENAMIENTO</a>
            <a href="#torneos">TORNEOS</a>
            <a href="#productos">PRODUCTOS</a>
            <a href="#contacto">CONTÁCTENOS</a>
          </div>

          <div className="la-nav-actions">
            {user ? (
              <button 
                className="la-btn-login" 
                onClick={() => navigate((user.role === 'AcademyAdmin' || user.role === 'Staff') ? '/dashboard' : '/student/dashboard')}
              >
                {(user.role === 'AcademyAdmin' || user.role === 'Staff') ? 'Administración' : 'Ir a mi Panel'}
              </button>
            ) : (
              <button className="la-btn-login" onClick={() => navigate('/login')}>
                Iniciar Sesión
              </button>
            )}
          </div>
        </div>
      </nav>

      {/* Floating WhatsApp Button */}
      {tenantData.contact.whatsapp && (
        <a href={`https://wa.me/${tenantData.contact.whatsapp}`} target="_blank" rel="noreferrer" className="la-fab-whatsapp">
          <MessageCircle size={28} />
        </a>
      )}

      {/* Hero Section Centered */}
      <section id="inicio" className="la-hero-section">
        <div className="la-hero-overlay"></div>
        <div className="la-hero-content">
          <span className="la-hero-badge">{tenantData.badge || 'BIENVENIDO A LA ÉLITE'}</span>
          <h1 className="la-hero-title" dangerouslySetInnerHTML={{ __html: tenantData.slogan.replace(' ADHSOFT SPORT', '<br/>ADHSOFT SPORT') }} />
          <p className="la-hero-desc">{tenantData.description}</p>
          <div className="la-hero-buttons">
            <button className="la-btn-primary-large glowing" onClick={() => navigate('/registro')}>
              Inscríbete Ahora <ChevronRight size={18} />
            </button>
            <a href="#entrenamiento" className="la-btn-ghost-large">
              Conoce más
            </a>
          </div>
        </div>
      </section>

      {/* Entrenamiento Section */}
      <section id="entrenamiento" className="la-training-section">
        <div className="la-section-header">
          <h2 className="la-section-title">NUESTROS PROGRAMAS</h2>
          <div className="la-section-divider"></div>
        </div>

        <div className="la-training-grid">
          {tenantData.categories.map((cat, idx) => (
            <div key={cat.id} className="la-training-card">
              <div className="la-card-img" style={{ backgroundImage: `url('https://source.unsplash.com/random/400x300/?soccer,training,${idx}')` }}></div>
              <div className="la-card-content">
                <h3>{cat.title}</h3>
                <p>{cat.desc}</p>
              </div>
            </div>
          ))}
        </div>
      </section>

      {/* Contacto & Footer Section */}
      <footer id="contacto" className="la-footer-section">
        <div className="la-footer-container">
          <div className="la-footer-info">
            <h2 className="la-footer-title">CONTÁCTENOS</h2>
            <div className="la-section-divider left"></div>
            
            <div className="la-contact-list">
              <div className="la-contact-item">
                <div className="la-c-icon"><Phone size={20} /></div>
                <div>
                  <strong>Teléfono / WhatsApp</strong>
                  <span>{tenantData.contact.phone}</span>
                </div>
              </div>
              <div className="la-contact-item">
                <div className="la-c-icon"><Mail size={20} /></div>
                <div>
                  <strong>Correo Electrónico</strong>
                  <span>{tenantData.contact.email}</span>
                </div>
              </div>
              <div className="la-contact-item">
                <div className="la-c-icon"><MapPin size={20} /></div>
                <div>
                  <strong>Ubicación</strong>
                  <span>{tenantData.contact.address}</span>
                </div>
              </div>
            </div>
          </div>

          <div className="la-footer-form">
            <div className="la-form-card">
              <h3>Envíanos un Mensaje</h3>
              <form onSubmit={e => e.preventDefault()}>
                <div className="la-form-row">
                  <input type="text" placeholder="Nombre completo" required />
                  <input type="tel" placeholder="Celular" required />
                </div>
                <textarea placeholder="¿En qué te podemos ayudar?" rows="4"></textarea>
                <button type="submit" className="la-btn-submit">
                  Enviar Mensaje <Send size={16} />
                </button>
              </form>
            </div>
          </div>
        </div>
        <div className="la-footer-bottom">
          <p>&copy; {new Date().getFullYear()} {tenantData.name}. Todos los derechos reservados. Desarrollado por <a href="/">CLUBiO</a>.</p>
        </div>
      </footer>
    </div>
  );
}
