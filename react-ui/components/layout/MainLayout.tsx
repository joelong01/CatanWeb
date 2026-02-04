'use client';

import { useState, useCallback } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faBars } from '@fortawesome/free-solid-svg-icons';
import { NavMenu } from './NavMenu';

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
}

/** Props for MainLayout component */
interface MainLayoutProps {
  /** Child content to render in the main area */
  children: React.ReactNode;
  /** Current active game ID, if any */
  activeGameId?: string | null;
  /** Game-specific actions (only used on Game page) */
  gameActions?: GameActions;
  /** Optional CSS class to apply to the root page element */
  className?: string;
}

/**
 * Main layout component providing the hamburger menu and page structure.
 * Matches the Blazor WebUI MainLayout.razor behavior.
 */
export function MainLayout({ children, activeGameId, gameActions, className }: MainLayoutProps): React.ReactElement {
  const [showMenu, setShowMenu] = useState(false);

  const toggleSidebar = useCallback((): void => {
    setShowMenu((prev) => !prev);
  }, []);

  return (
    <div className={className ? `page ${className}` : 'page'}>
      {/* Hamburger button - always visible */}
      <button className="hamburger-btn" onClick={toggleSidebar} aria-label="Toggle menu">
        <FontAwesomeIcon icon={faBars} />
      </button>

      {/* Menu overlay and panel */}
      {showMenu && (
        <div className="menu-overlay" onClick={toggleSidebar}>
          <div className="menu-panel" onClick={(e) => e.stopPropagation()}>
            <NavMenu onMenuAction={toggleSidebar} activeGameId={activeGameId} gameActions={gameActions} />
          </div>
        </div>
      )}

      {/* Main content */}
      <main>
        <article className="content">{children}</article>
      </main>
    </div>
  );
}
