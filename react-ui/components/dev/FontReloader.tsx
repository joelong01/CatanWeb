'use client';

import { useCallback, useEffect, useState } from 'react';

/**
 * Dev-only component that enables hot-reloading the Catan font.
 *
 * Press Ctrl+Shift+F to force-reload the font from the API route without
 * a full page refresh. The API route reads the TTF from disk with no-cache
 * headers, so any rebuild of the font is picked up immediately.
 *
 * How it works: bumps the @font-face src URL with a cache-busting query param,
 * which forces the browser to re-fetch and re-render all text using the font.
 */
export function FontReloader() {
  const [version, setVersion] = useState(0);
  const [flash, setFlash] = useState(false);

  const reloadFont = useCallback(() => {
    setVersion((v) => v + 1);
    setFlash(true);
    setTimeout(() => setFlash(false), 600);
  }, []);

  // Keyboard shortcut: Ctrl+Shift+F
  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if (e.ctrlKey && e.shiftKey && e.key === 'F') {
        e.preventDefault();
        reloadFont();
      }
    }
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [reloadFont]);

  // Inject updated @font-face rule when version changes
  useEffect(() => {
    if (version === 0) return; // Skip initial render (layout.tsx handles v0)

    const style = document.createElement('style');
    style.setAttribute('data-font-reload', String(version));
    style.textContent = `
      @font-face {
        font-family: 'CatanDev';
        src: url('/api/dev/font?v=${version}') format('truetype');
        font-display: swap;
      }
    `;
    document.head.appendChild(style);

    return () => {
      document.head.removeChild(style);
    };
  }, [version]);

  if (!flash) return null;

  return (
    <div
      style={{
        position: 'fixed',
        bottom: 16,
        right: 16,
        padding: '8px 16px',
        background: 'rgba(34, 197, 94, 0.9)',
        color: 'white',
        borderRadius: 8,
        fontSize: 14,
        fontFamily: 'system-ui',
        zIndex: 99999,
        pointerEvents: 'none',
      }}
    >
      Font reloaded (v{version})
    </div>
  );
}
