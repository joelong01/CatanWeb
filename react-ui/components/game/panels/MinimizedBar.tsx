'use client';

/**
 * MinimizedBar - Fixed bottom bar showing all minimized panels.
 *
 * This component handles the array subscription for minimized panels,
 * avoiding cross-panel dependencies in individual FloatingPanel components.
 * Click any item to restore (expand) that panel.
 */

import { useCallback, useMemo } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  useLayoutStore,
  PANEL_ORDER,
  PANEL_METADATA,
  type MinimizedPanelInfo,
} from '@/lib/stores/layoutStore';

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

  // Don't render if no panels are minimized
  if (minimizedPanels.length === 0) {
    return null;
  }

  return (
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
            className="flex items-center gap-2 bg-gray-900/95 backdrop-blur-sm rounded-lg shadow-xl border border-gray-700/50 cursor-pointer hover:bg-gray-800/95 hover:border-amber-500/50 transition-colors"
            style={{
              padding: MINIMIZED_BAR.itemPadding,
              minWidth: MINIMIZED_BAR.itemMinWidth,
            }}
            title={`${panel.title} (click to expand)`}
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
  );
}

export default MinimizedBar;
