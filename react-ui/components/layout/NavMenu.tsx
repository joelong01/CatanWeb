'use client';

import { usePathname, useRouter } from 'next/navigation';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import {
  faXmark,
  faExpand,
  faHouse,
  faPlus,
  faFolderOpen,
  faUsers,
  faChartBar,
  faGear,
  faPlay,
  faRotate,
  faScaleBalanced,
  faTrophy,
  faDownload,
} from '@fortawesome/free-solid-svg-icons';
import { useLayoutStore } from '@/lib/stores/layoutStore';
import type { IconDefinition } from '@fortawesome/fontawesome-svg-core';

/** Props for NavMenu component */
interface NavMenuProps {
  /** Callback when a menu action closes the menu */
  onMenuAction: () => void;
  /** Current active game ID, if any */
  activeGameId?: string | null;
  /** Game-specific callbacks (only used on Game page) */
  gameActions?: {
    /** Whether the game is in PickingBoard state (shows Balance button) */
    isPickingBoard?: boolean;
    /** Balance board callback */
    onBalance?: () => void;
    /** Declare winner callback */
    onWinner?: () => void;
    /** Save copy callback */
    onSaveCopy?: () => void;
  };
}

/** Page context for determining which menu items to show */
type PageContext =
  | 'Home'
  | 'Game'
  | 'NewGame'
  | 'LoadGame'
  | 'EditPlayers'
  | 'Settings'
  | 'Stats'
  | 'Tests'
  | 'Other';

/**
 * Individual navigation menu item button.
 */
function NavMenuItem({
  icon,
  label,
  onClick,
  className = '',
}: {
  icon: IconDefinition;
  label: string;
  onClick: () => void;
  className?: string;
}): React.ReactElement {
  return (
    <button className={`nav-menu-item ${className}`} onClick={onClick}>
      <div className="nav-icon">
        <FontAwesomeIcon icon={icon} />
      </div>
      <div className="nav-label">{label}</div>
    </button>
  );
}

/**
 * Navigation menu component displayed in the sidebar.
 * Shows context-aware menu items based on current page.
 */
export function NavMenu({ onMenuAction, activeGameId, gameActions }: NavMenuProps): React.ReactElement {
  const pathname = usePathname();
  const router = useRouter();

  /**
   * Determines the current page context from the URL path.
   */
  const getCurrentPage = (): PageContext => {
    const path = pathname.toLowerCase();
    if (path.includes('/game/')) return 'Game';
    if (path.includes('/new-game')) return 'NewGame';
    if (path.includes('/load-game')) return 'LoadGame';
    if (path.includes('/edit-players')) return 'EditPlayers';
    if (path.includes('/settings')) return 'Settings';
    if (path.includes('/stats')) return 'Stats';
    if (path.includes('/tests')) return 'Tests';
    if (path === '/') return 'Home';
    return 'Other';
  };

  const currentPage = getCurrentPage();
  const hasActiveGame = !!activeGameId;
  const resetLayout = useLayoutStore((state) => state.resetLayout);

  /** Navigate and close menu */
  const navigateTo = (path: string): void => {
    onMenuAction();
    router.push(path);
  };

  /** Reset panel layout to defaults */
  const handleResetLayout = (): void => {
    resetLayout();
    onMenuAction();
  };

  /** Toggle fullscreen mode */
  const toggleFullScreen = (): void => {
    if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen().catch(() => {
        // Fullscreen not supported or denied
      });
    } else {
      document.exitFullscreen().catch(() => {
        // Exit fullscreen failed
      });
    }
  };

  return (
    <nav className="nav-menu">
      {/* Always visible: Hide */}
      <NavMenuItem icon={faXmark} label="Hide" onClick={onMenuAction} />

      {/* Always visible: Full Screen */}
      <NavMenuItem icon={faExpand} label="Full Screen" onClick={toggleFullScreen} />

      {/* Context-specific menu items */}
      {currentPage === 'Home' && (
        <>
          <NavMenuItem icon={faPlus} label="New Game" onClick={() => navigateTo('/new-game')} />
          <NavMenuItem
            icon={faFolderOpen}
            label="Open Game"
            onClick={() => navigateTo('/load-game')}
          />
          <NavMenuItem
            icon={faUsers}
            label="Edit Players"
            onClick={() => navigateTo('/edit-players')}
          />
        </>
      )}

      {currentPage === 'NewGame' && (
        <>
          <NavMenuItem icon={faHouse} label="Home" onClick={() => navigateTo('/')} />
          <NavMenuItem
            icon={faFolderOpen}
            label="Open Game"
            onClick={() => navigateTo('/load-game')}
          />
          <NavMenuItem
            icon={faUsers}
            label="Edit Players"
            onClick={() => navigateTo('/edit-players')}
          />
          {hasActiveGame && (
            <NavMenuItem
              icon={faPlay}
              label="Return to Game"
              onClick={() => navigateTo(`/game/${activeGameId}`)}
            />
          )}
        </>
      )}

      {currentPage === 'LoadGame' && (
        <>
          <NavMenuItem icon={faHouse} label="Home" onClick={() => navigateTo('/')} />
          <NavMenuItem icon={faPlus} label="New Game" onClick={() => navigateTo('/new-game')} />
          <NavMenuItem
            icon={faUsers}
            label="Edit Players"
            onClick={() => navigateTo('/edit-players')}
          />
          {hasActiveGame && (
            <NavMenuItem
              icon={faPlay}
              label="Return to Game"
              onClick={() => navigateTo(`/game/${activeGameId}`)}
            />
          )}
        </>
      )}

      {currentPage === 'EditPlayers' && (
        <>
          <NavMenuItem icon={faHouse} label="Home" onClick={() => navigateTo('/')} />
          <NavMenuItem icon={faPlus} label="New Game" onClick={() => navigateTo('/new-game')} />
          <NavMenuItem
            icon={faFolderOpen}
            label="Open Game"
            onClick={() => navigateTo('/load-game')}
          />
          {hasActiveGame && (
            <NavMenuItem
              icon={faPlay}
              label="Return to Game"
              onClick={() => navigateTo(`/game/${activeGameId}`)}
            />
          )}
        </>
      )}

      {currentPage === 'Settings' && (
        <>
          <NavMenuItem icon={faHouse} label="Home" onClick={() => navigateTo('/')} />
          <NavMenuItem icon={faPlus} label="New Game" onClick={() => navigateTo('/new-game')} />
          <NavMenuItem
            icon={faFolderOpen}
            label="Open Game"
            onClick={() => navigateTo('/load-game')}
          />
          {hasActiveGame && (
            <NavMenuItem
              icon={faPlay}
              label="Return to Game"
              onClick={() => navigateTo(`/game/${activeGameId}`)}
            />
          )}
        </>
      )}

      {currentPage === 'Stats' && (
        <>
          <NavMenuItem icon={faHouse} label="Home" onClick={() => navigateTo('/')} />
          {hasActiveGame && (
            <NavMenuItem
              icon={faPlay}
              label="Return to Game"
              onClick={() => navigateTo(`/game/${activeGameId}`)}
            />
          )}
        </>
      )}

      {currentPage === 'Game' && (
        <>
          <NavMenuItem icon={faHouse} label="Home" onClick={() => navigateTo('/')} />
          {gameActions?.onSaveCopy && (
            <NavMenuItem
              icon={faDownload}
              label="Save Copy"
              onClick={() => {
                gameActions.onSaveCopy?.();
                onMenuAction();
              }}
            />
          )}
          <NavMenuItem
            icon={faUsers}
            label="Edit Players"
            onClick={() => navigateTo('/edit-players')}
          />
          {gameActions?.isPickingBoard && gameActions?.onBalance && (
            <NavMenuItem
              icon={faScaleBalanced}
              label="Balance"
              onClick={() => {
                gameActions.onBalance?.();
                onMenuAction();
              }}
            />
          )}
          {gameActions?.onWinner && (
            <NavMenuItem
              icon={faTrophy}
              label="Winner!"
              onClick={() => {
                gameActions.onWinner?.();
                onMenuAction();
              }}
              className="winner-button"
            />
          )}
          <NavMenuItem icon={faRotate} label="Reset Layout" onClick={handleResetLayout} />
        </>
      )}

      {(currentPage === 'Tests' || currentPage === 'Other') && (
        <>
          <NavMenuItem icon={faHouse} label="Home" onClick={() => navigateTo('/')} />
          <NavMenuItem icon={faRotate} label="Reset Layout" onClick={handleResetLayout} />
          {hasActiveGame && (
            <NavMenuItem
              icon={faPlay}
              label="Return to Game"
              onClick={() => navigateTo(`/game/${activeGameId}`)}
            />
          )}
        </>
      )}

      {/* Always visible: Settings */}
      <NavMenuItem icon={faGear} label="Settings" onClick={() => navigateTo('/settings')} />

      {/* Always visible: Stats */}
      <NavMenuItem icon={faChartBar} label="Stats" onClick={() => navigateTo('/stats')} />
    </nav>
  );
}
