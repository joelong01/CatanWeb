'use client';

/**
 * MinimizedBar - Fixed bottom bar showing all minimized panels.
 *
 * This component handles the array subscription for minimized panels,
 * avoiding cross-panel dependencies in individual FloatingPanel components.
 * Click any item to restore (expand) that panel.
 * Right-click (desktop) or long-press (mobile) opens a context menu.
 */

import { useCallback, useEffect, useMemo, useState, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  useLayoutStore,
  PANEL_ORDER,
  PANEL_METADATA,
  type PanelId,
  type MinimizedPanelInfo,
} from '@/lib/stores/layoutStore';
import { ContextMenu, type ContextMenuItem } from './ContextMenu';
import { useGameState } from '@/lib/stores/gameStoreHooks';

/** Long press duration for mobile context menu (ms) */
const LONG_PRESS_DURATION = 400;

/** Layout constants for the minimized bar */
const MINIMIZED_BAR = {
  /** Distance from bottom of viewport */
  bottom: 12,
  /** Distance from left of viewport (clears game info badges) */
  left: 220,
  /** Gap between items */
  gap: 8,
  /** Minimum width of each item */
  itemMinWidth: 100,
  /** Padding inside each item */
  itemPadding: '8px 12px',
} as const;

interface MinimizedBarProps {
  /** Optional className for the container */
  className?: string;
}

/**
 * MinimizedBar renders all minimized panels in a fixed bottom bar.
 *
 * Pattern: Subscribe to stable `panels` object, derive array with useMemo.
 * This avoids infinite loops that occur with useShallow + array-returning selectors.
 */
export function MinimizedBar({ className = '' }: MinimizedBarProps): React.ReactElement | null {
  // Subscribe to the panels object (stable reference from store)
  const panels = useLayoutStore((state) => state.panels);
  const toggleMinimize = useLayoutStore((state) => state.toggleMinimize);
  const resetPanel = useLayoutStore((state) => state.resetPanel);
  const arrangeLayout = useLayoutStore((state) => state.arrangeLayout);
  const saveLayout = useLayoutStore((state) => state.saveLayout);
  const currentLayoutName = useLayoutStore((state) => state.currentLayoutName);
  const gameState = useGameState();

  // Context menu state
  const [contextMenu, setContextMenu] = useState<{
    panelId: PanelId;
    x: number;
    y: number;
  } | null>(null);

  // Long-press tracking refs
  const longPressTimerRef = useRef<NodeJS.Timeout | null>(null);
  const longPressPanelRef = useRef<PanelId | null>(null);

  // Derive minimized panels array - only recomputes when panels object changes
  const minimizedPanels = useMemo((): MinimizedPanelInfo[] =>
    PANEL_ORDER
      .filter((id) => panels[id]?.minimized && panels[id]?.visible)
      .map((id) => ({
        id,
        title: PANEL_METADATA[id].title,
        icon: PANEL_METADATA[id].icon,
      })),
    [panels]
  );

  // Handle click to restore panel
  const handleRestore = useCallback(
    (panel: MinimizedPanelInfo) => {
      toggleMinimize(panel.id);
    },
    [toggleMinimize]
  );

  // Handle right-click context menu
  const handleContextMenu = useCallback(
    (e: React.MouseEvent, panelId: PanelId) => {
      e.preventDefault();
      setContextMenu({ panelId, x: e.clientX, y: e.clientY });
    },
    []
  );

  // Long-press handlers for mobile
  const handleTouchStart = useCallback(
    (panelId: PanelId, e: React.TouchEvent) => {
      longPressPanelRef.current = panelId;
      const touch = e.touches[0];
      longPressTimerRef.current = setTimeout(() => {
        setContextMenu({ panelId, x: touch.clientX, y: touch.clientY });
        if ('vibrate' in navigator) {
          navigator.vibrate(50);
        }
      }, LONG_PRESS_DURATION);
    },
    []
  );

  const handleTouchEnd = useCallback(() => {
    if (longPressTimerRef.current) {
      clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = null;
    }
    longPressPanelRef.current = null;
  }, []);

  const handleTouchMove = useCallback(() => {
    if (longPressTimerRef.current) {
      clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = null;
    }
  }, []);

  // Clean up long-press timer on unmount
  useEffect(() => {
    return () => {
      if (longPressTimerRef.current) {
        clearTimeout(longPressTimerRef.current);
      }
    };
  }, []);

  // Build context menu items for the selected panel
  const contextMenuItems = useMemo((): ContextMenuItem[] => {
    if (!contextMenu) return [];
    const { panelId } = contextMenu;
    return [
      {
        label: 'Restore',
        onClick: () => {
          toggleMinimize(panelId);
          setContextMenu(null);
        },
      },
      {
        label: 'Arrange All',
        onClick: () => {
          arrangeLayout(gameState);
          setContextMenu(null);
        },
      },
      {
        label: 'Save Layout',
        onClick: () => {
          if (currentLayoutName) {
            saveLayout(currentLayoutName);
          }
          setContextMenu(null);
        },
        disabled: !currentLayoutName,
      },
      {
        label: 'Reset Panel',
        onClick: () => {
          resetPanel(panelId);
          setContextMenu(null);
        },
      },
    ];
  }, [contextMenu, toggleMinimize, arrangeLayout, gameState, resetPanel, saveLayout, currentLayoutName]);

  // Don't render if no panels are minimized
  if (minimizedPanels.length === 0 && !contextMenu) {
    return null;
  }

  return (
    <>
      <div
        className={`fixed flex items-center z-[1000] ${className}`}
        style={{
          bottom: MINIMIZED_BAR.bottom,
          left: MINIMIZED_BAR.left,
          gap: MINIMIZED_BAR.gap,
        }}
      >
        <AnimatePresence mode="popLayout">
          {minimizedPanels.map((panel) => (
            <motion.button
              key={panel.id}
              initial={{ opacity: 0, y: 20, scale: 0.8 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, y: 20, scale: 0.8 }}
              transition={{ duration: 0.15 }}
              onClick={() => handleRestore(panel)}
              onContextMenu={(e) => handleContextMenu(e, panel.id)}
              onTouchStart={(e) => handleTouchStart(panel.id, e)}
              onTouchEnd={handleTouchEnd}
              onTouchMove={handleTouchMove}
              className="flex items-center gap-2 bg-gray-900/95 backdrop-blur-sm rounded-lg shadow-xl border border-gray-700/50 cursor-pointer hover:bg-gray-800/95 hover:border-amber-500/50 transition-colors"
              style={{
                padding: MINIMIZED_BAR.itemPadding,
                minWidth: MINIMIZED_BAR.itemMinWidth,
              }}
              title={`${panel.title} (click to expand, right-click for options)`}
            >
              {panel.icon && (
                <span className="text-gray-400 text-sm">{panel.icon}</span>
              )}
              <span className="text-xs text-gray-400 font-medium whitespace-nowrap">
                {panel.title}
              </span>
            </motion.button>
          ))}
        </AnimatePresence>
      </div>

      {/* Context menu */}
      {contextMenu && (
        <ContextMenu
          position={{ x: contextMenu.x, y: contextMenu.y }}
          items={contextMenuItems}
          onClose={() => setContextMenu(null)}
        />
      )}
    </>
  );
}

export default MinimizedBar;
