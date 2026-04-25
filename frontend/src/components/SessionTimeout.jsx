import { useEffect, useRef } from 'react';
import { useAuth } from '../context/AuthContext';
import { useLocation, useNavigate } from 'react-router-dom';

export default function SessionTimeout({ timeout = 60000 }) {
  const { logout, user } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const timerRef = useRef(null);

  const resetTimer = () => {
    if (timerRef.current) clearTimeout(timerRef.current);
    
    // Don't auto-logout if we are on public routes or login pages
    if (location.pathname === '/login' || location.pathname === '/' || location.pathname === '/registro') {
      return;
    }

    if (user) {
      timerRef.current = setTimeout(() => {
        logout();
        navigate('/login');
      }, timeout);
    }
  };

  useEffect(() => {
    resetTimer();

    const events = ['mousedown', 'mousemove', 'keypress', 'scroll', 'touchstart'];
    const handleEvent = () => resetTimer();

    events.forEach(e => document.addEventListener(e, handleEvent));

    return () => {
      if (timerRef.current) clearTimeout(timerRef.current);
      events.forEach(e => document.removeEventListener(e, handleEvent));
    };
  }, [user, location.pathname, timeout]);

  return null;
}
