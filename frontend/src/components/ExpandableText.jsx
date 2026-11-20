import { useLayoutEffect, useRef, useState } from 'react';

/**
 * Texto largo cortado a `lines` líneas con "Ver más" / "Ver menos".
 * El botón solo aparece si el texto realmente no cabe (se mide en pantalla, también al girar el celular).
 */
export default function ExpandableText({ text, lines = 3, className = '' }) {
  const ref = useRef(null);
  const [open, setOpen] = useState(false);
  const [overflows, setOverflows] = useState(false);

  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return undefined;
    const measure = () => { if (!open) setOverflows(el.scrollHeight - el.clientHeight > 1); };
    measure();
    const ro = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(measure) : null;
    ro?.observe(el);
    return () => ro?.disconnect();
  }, [text, open, lines]);

  const clamp = open ? {} : { display: '-webkit-box', WebkitBoxOrient: 'vertical', WebkitLineClamp: lines, overflow: 'hidden' };
  return (
    <div>
      <p ref={ref} className={className} style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', ...clamp }}>{text}</p>
      {(overflows || open) && (
        <button type="button" className="expandable-toggle" onClick={() => setOpen(o => !o)} aria-expanded={open}>
          {open ? 'Ver menos' : 'Ver más'}
        </button>
      )}
    </div>
  );
}
