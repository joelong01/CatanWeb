'use client';

import { useState, useCallback } from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faBars } from '@fortawesome/free-solid-svg-icons';
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
}

/** Props for MainLayout component */
interface MainLayoutProps {
  /** Child content to render in the main area */
  children: React.ReactNode;
  /** Optional CSS class applied to the root .page div (e.g., "overflow-y-auto" for scrollable pages) */
  className?: string;
  /** Current active game ID, if any */
  activeGameId?: string | null;
  /** Game-specific actions (only used on Game page) */
  gameActions?: GameActions;
}

/**
 * Main layout component providing the hamburger menu and page structure.
 * Matches the Blazor WebUI MainLayout.razor behavior.
 */
export function MainLayout({
  children,
  className,
  activeGameId,
  gameActions,
}: MainLayoutProps): React.ReactElement {
  const [showMenu, setShowMenu] = useState(false);

  // Apply animation speed CSS variables based on user settings
  useAnimationSpeed();

  const toggleSidebar = useCallback((): void => {
    setShowMenu((prev) => !prev);
  }, []);

  return (
    <div className={`page${className ? ` ${className}` : ''}`}>
      {/* Hamburger button - always visible */}
      <button className="hamburger-btn" onClick={toggleSidebar} aria-label="Toggle menu">
        <FontAwesomeIcon icon={faBars} />
      </button>

      {/* Menu overlay and panel */}
      {showMenu && (
        <div className="menu-overlay" onClick={toggleSidebar}>
          <div className="menu-panel" onClick={(e) => e.stopPropagation()}>
            <NavMenu
              onMenuAction={toggleSidebar}
              activeGameId={activeGameId}
              gameActions={gameActions}
            />
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
