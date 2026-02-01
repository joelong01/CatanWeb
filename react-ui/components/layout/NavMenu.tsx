'use client';

import { useState } from 'react';
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
  faTableColumns,
  faWindowMinimize,
  faFloppyDisk,
  faChevronRight,
  faChevronDown,
  faObjectGroup,
} from '@fortawesome/free-solid-svg-icons';
import { useLayoutStore, PANEL_ORDER, PANEL_METADATA, type PanelId } from '@/lib/stores/layoutStore';
import { useGameState } from '@/lib/stores/gameStoreHooks';
import { SaveLayoutDialog } from '@/components/game/panels/SaveLayoutDialog';
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
 * Sub-menu item with smaller sizing and indentation.
 */
function NavSubItem({
  label,
  onClick,
  icon,
  chevron,
  disabled = false,
}: {
  label: string;
  onClick: () => void;
  icon?: IconDefinition;
  chevron?: 'right' | 'down';
  disabled?: boolean;
}): React.ReactElement {
  return (
    <button
      className={`nav-menu-item ${disabled ? 'opacity-40 cursor-default' : ''}`}
      style={{ minHeight: 36, padding: '4px 8px' }}
      onClick={disabled ? undefined : onClick}
      disabled={disabled}
    >
      <div className="nav-icon" style={{ fontSize: 12 }}>
        {icon && <FontAwesomeIcon icon={icon} />}
      </div>
      <div className="nav-label" style={{ fontSize: 10 }}>
        {label}
        {chevron && (
          <FontAwesomeIcon
            icon={chevron === 'down' ? faChevronDown : faChevronRight}
            style={{ marginLeft: 4, fontSize: 8 }}
          />
        )}
      </div>
    </button>
  );
}

/** Expandable section types for the Layout menu */
type ExpandedSection = 'reset' | 'open' | null;

/**
 * Layout management section rendered inside NavMenu.
 * Provides Minimize All, Reset (per-panel), Save, Save As, Open.
 */
function LayoutSection({ onMenuAction }: { onMenuAction: () => void }): React.ReactElement {
  const resetLayout = useLayoutStore((s) => s.resetLayout);
  const resetPanel = useLayoutStore((s) => s.resetPanel);
  const minimizeAll = useLayoutStore((s) => s.minimizeAll);
  const arrangeLayout = useLayoutStore((s) => s.arrangeLayout);
  const saveLayout = useLayoutStore((s) => s.saveLayout);
  const loadLayout = useLayoutStore((s) => s.loadLayout);
  const savedLayouts = useLayoutStore((s) => s.savedLayouts);
  const currentLayoutName = useLayoutStore((s) => s.currentLayoutName);
  const gameState = useGameState();

  const [layoutExpanded, setLayoutExpanded] = useState(false);
  const [expandedSection, setExpandedSection] = useState<ExpandedSection>(null);
  const [showSaveDialog, setShowSaveDialog] = useState(false);

  const handleSave = (): void => {
    if (currentLayoutName) {
      saveLayout(currentLayoutName);
      onMenuAction();
    } else {
      setShowSaveDialog(true);
    }
  };

  const handleSaveAs = (): void => {
    setShowSaveDialog(true);
  };

  const handleSaveDialogSave = (name: string): void => {
    saveLayout(name);
    setShowSaveDialog(false);
    onMenuAction();
  };

  return (
    <>
      {/* Top-level Layout button */}
      <NavMenuItem
        icon={faTableColumns}
        label="Layout"
        onClick={() => {
          setLayoutExpanded(!layoutExpanded);
          if (layoutExpanded) setExpandedSection(null);
        }}
      />

      {/* Expanded sub-items */}
      {layoutExpanded && (
        <div style={{ paddingLeft: 4 }}>
          {/* Arrange -- game-state-aware optimal layout */}
          <NavSubItem
            label="Arrange"
            icon={faObjectGroup}
            onClick={() => {
              arrangeLayout(gameState);
              onMenuAction();
            }}
          />

          {/* Minimize All */}
          <NavSubItem
            label="Minimize All"
            icon={faWindowMinimize}
            onClick={() => {
              minimizeAll();
              onMenuAction();
            }}
          />

          {/* Reset (with expandable panel list) */}
          <NavSubItem
            label="Reset"
            icon={faRotate}
            chevron={expandedSection === 'reset' ? 'down' : 'right'}
            onClick={() => setExpandedSection(expandedSection === 'reset' ? null : 'reset')}
          />
          {expandedSection === 'reset' && (
            <div style={{ paddingLeft: 8 }}>
              <NavSubItem
                label="All"
                onClick={() => {
                  resetLayout();
                  onMenuAction();
                }}
              />
              {PANEL_ORDER.map((id: PanelId) => (
                <NavSubItem
                  key={id}
                  label={PANEL_METADATA[id].title}
                  onClick={() => {
                    resetPanel(id);
                    onMenuAction();
                  }}
                />
              ))}
            </div>
          )}

          {/* Save */}
          <NavSubItem
            label="Save"
            icon={faFloppyDisk}
            onClick={handleSave}
          />

          {/* Save As */}
          <NavSubItem
            label="Save As..."
            icon={faFloppyDisk}
            onClick={handleSaveAs}
          />

          {/* Open (with expandable saved layouts list) */}
          <NavSubItem
            label="Open"
            icon={faFolderOpen}
            chevron={expandedSection === 'open' ? 'down' : 'right'}
            onClick={() => setExpandedSection(expandedSection === 'open' ? null : 'open')}
          />
          {expandedSection === 'open' && (
            <div style={{ paddingLeft: 8 }}>
              {savedLayouts.length === 0 ? (
                <NavSubItem label="(no saved layouts)" onClick={() => {}} disabled />
              ) : (
                savedLayouts.map((layout) => (
                  <NavSubItem
                    key={layout.name}
                    label={layout.name}
                    onClick={() => {
                      loadLayout(layout.name);
                      onMenuAction();
                    }}
                  />
                ))
              )}
            </div>
          )}
        </div>
      )}

      {/* Save Layout Dialog */}
      {showSaveDialog && (
        <SaveLayoutDialog
          defaultName={currentLayoutName}
          existingNames={savedLayouts.map((l) => l.name)}
          onSave={handleSaveDialogSave}
          onCancel={() => setShowSaveDialog(false)}
        />
      )}
    </>
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

  /** Navigate and close menu */
  const navigateTo = (path: string): void => {
    onMenuAction();
    router.push(path);
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
          <LayoutSection onMenuAction={onMenuAction} />
        </>
      )}

      {(currentPage === 'Tests' || currentPage === 'Other') && (
        <>
          <NavMenuItem icon={faHouse} label="Home" onClick={() => navigateTo('/')} />
          <LayoutSection onMenuAction={onMenuAction} />
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
