'use client';

import { useState, useCallback } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faBars, faArrowLeft } from '@fortawesome/free-solid-svg-icons';
import { NavMenu } from './NavMenu';
import { useAnimationSpeed } from '@/lib/hooks';

/** Game-specific actions for NavMenu */
interface GameActions {
  /** Whether the game is in PickingBoard state (shows Balance button) */
  isPickingBoard?: boolean;
  /** Balance board callback */
  onBalance?: () => void;
  /** Declare winner callback */
  onWinner?: () => void;
  /** Save copy callback */
  onSaveCopy?: () => void;
  /** Close game: evict from server registry, clear active-game pointer, go home */
  onClose?: () => void;
}

/** Props for MainLayout component */
interface MainLayoutProps {
  /** Child content to render in the main area */
  children: React.ReactNode;
  /** Full-screen mode (game page) — no nav column or header bar */
  fullScreen?: boolean;
  /** Page title shown in header bar */
  title?: string;
  /** Subtitle shown next to title */
  subtitle?: string;
  /** Back button click handler. If provided, back button appears in header. */
  onBack?: () => void;
  /** Suppress header bar entirely (e.g., home page) */
  hideHeader?: boolean;
  /** Current active game ID, if any */
  activeGameId?: string | null;
  /** Game-specific actions (only used on Game page) */
  gameActions?: GameActions;
  /** Optional className on the content area (standard) or root (fullscreen) */
  className?: string;
}

/**
 * App shell layout with two modes:
 *
 * **Standard mode** (default): CSS Grid with nav column + header bar + content area.
 * The nav column holds the hamburger toggle; the header bar shows page title and
 * optional back button; the content area is the only zone that scrolls.
 *
 * **Full-screen mode** (`fullScreen`): Content covers the entire viewport.
 * The hamburger button floats over content at `position: fixed`. Used by the
 * game page where every pixel matters for the board and floating panels.
 *
 * See .design/app-shell.md for architecture details.
 */
export function MainLayout({
  children,
  fullScreen,
  title,
  subtitle,
  onBack,
  hideHeader,
  activeGameId,
  gameActions,
  className,
}: MainLayoutProps): React.ReactElement {
  const [showMenu, setShowMenu] = useState(false);

  // Apply animation speed CSS variables based on user settings
  useAnimationSpeed();

  const toggleSidebar = useCallback((): void => {
    setShowMenu((prev) => !prev);
  }, []);

  // Menu overlay shared by both modes
  const menuOverlay = showMenu && (
    <div className="menu-overlay" onClick={toggleSidebar}>
      <div className="menu-panel" onClick={(e) => e.stopPropagation()}>
        <NavMenu
          onMenuAction={toggleSidebar}
          activeGameId={activeGameId}
          gameActions={gameActions}
        />
      </div>
    </div>
  );

  // ── Full-screen mode (game page) ──
  if (fullScreen) {
    return (
      <div className={`app-shell-fullscreen${className ? ` ${className}` : ''}`}>
        <button className="hamburger-btn" onClick={toggleSidebar} aria-label="Toggle menu">
          <FontAwesomeIcon icon={faBars} />
        </button>
        {menuOverlay}
        <main>{children}</main>
      </div>
    );
  }

  // ── Standard mode (nav column + header + content) ──
  return (
    <div className="app-shell">
      {/* Nav Column */}
      <nav className="app-nav">
        <button className="nav-toggle" onClick={toggleSidebar} aria-label="Toggle menu">
          <FontAwesomeIcon icon={faBars} />
        </button>
      </nav>

      {menuOverlay}

      {/* Main area: header + content */}
      <div className="app-main">
        {!hideHeader && (title || onBack) && (
          <header className="app-header">
            {onBack && (
              <button className="back-button" onClick={onBack}>
                <FontAwesomeIcon icon={faArrowLeft} />
                <span>Back</span>
              </button>
            )}
            {title && <h1 className="page-title">{title}</h1>}
            {subtitle && <span className="page-subtitle">{subtitle}</span>}
          </header>
        )}

        <main className={`app-content${className ? ` ${className}` : ''}`}>{children}</main>
      </div>
    </div>
  );
}
