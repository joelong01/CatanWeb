'use client';

/**
 * FloatingPanel - Draggable, resizable panel without title bar.
 *
 * Drag: CTRL+click (desktop) or long press (mobile)
 * Resize: Drag corner handle (always enabled)
 * Position persists to localStorage via layoutStore.
 */

import { useState, useRef, useEffect, useCallback, ReactNode } from 'react';
import { motion } from 'framer-motion';
import { useLayoutStore, type PanelId } from '@/lib/stores/layoutStore';

/** Long press duration for mobile drag (ms) */
const LONG_PRESS_DURATION = 400;

interface FloatingPanelProps {
  /** Panel identifier for persistence */
  panelId: PanelId;
  /** Panel title (shown in tooltip and minimized state) */
  title: string;
  /** Icon to show when minimized (optional) */
  icon?: ReactNode;
  /** Panel content */
  children: ReactNode;
  /** Additional class names */
  className?: string;
  /** Additional inline styles */
  style?: React.CSSProperties;
  /** Whether the panel can be resized and dragged (for future toggle mode) */
  resizable?: boolean;
  /** Minimum width when resizing */
  minWidth?: number;
  /** Minimum height when resizing */
  minHeight?: number;
  /**
   * Enable drag by clicking on empty space (no CTRL needed).
   * Elements with data-clickable attribute or onClick won't trigger drag.
   * Default: true (recommended for panels with sparse content like hex grids)
   */
  enableBackgroundDrag?: boolean;
}

/** Default panel layout for new/unknown panels */
const DEFAULT_PANEL_LAYOUT = {
  position: { x: 100, y: 100 },
  size: { width: 300, height: 200 },
  minimized: false,
  visible: true,
  zIndex: 20,
};

/** Safely get panel with complete defaults */
function getPanelWithDefaults(stored: typeof DEFAULT_PANEL_LAYOUT | undefined) {
  if (!stored) return DEFAULT_PANEL_LAYOUT;
  return {
    position: stored.position ?? DEFAULT_PANEL_LAYOUT.position,
    size: stored.size ?? DEFAULT_PANEL_LAYOUT.size,
    minimized: stored.minimized ?? DEFAULT_PANEL_LAYOUT.minimized,
    visible: stored.visible ?? DEFAULT_PANEL_LAYOUT.visible,
    zIndex: stored.zIndex ?? DEFAULT_PANEL_LAYOUT.zIndex,
  };
}

/**
 * Check if an element or any ancestor is interactive (clickable).
 * Used to determine if a click should start drag or be handled by child.
 *
 * Elements with data-drag-through are explicitly non-interactive (e.g., water tiles).
 */
function isInteractiveElement(element: HTMLElement | null, stopAt: HTMLElement | null): boolean {
  let current = element;
  while (current && current !== stopAt) {
    // Check for explicit drag-through marker (e.g., water tiles, decorative elements)
    // If found, this element and its ancestors up to this point are not interactive
    if (current.hasAttribute('data-drag-through')) return false;
    // Check for explicit clickable markers
    if (current.hasAttribute('data-clickable')) return true;
    // Check for standard interactive elements
    if (current.tagName === 'BUTTON' || current.tagName === 'A') return true;
    // Check for role="button"
    if (current.getAttribute('role') === 'button') return true;
    // Check for cursor-pointer class (common pattern for clickable elements)
    if (current.classList.contains('cursor-pointer')) return true;
    // Check for onClick handler via data attribute (set by HexTile)
    if (current.hasAttribute('data-has-click')) return true;
    current = current.parentElement;
  }
  return false;
}

export function FloatingPanel({
  panelId,
  title,
  icon,
  children,
  className = '',
  style,
  resizable = true, // All panels resizable by default; future: toggle this
  minWidth = 120,
  minHeight = 80,
  enableBackgroundDrag = true,
}: FloatingPanelProps): React.ReactElement | null {
  // Get panel state from store (with fallback for missing/incomplete panels)
  const storedPanel = useLayoutStore((state) => state.panels[panelId]);
  const panel = getPanelWithDefaults(storedPanel);
  const setPanelPosition = useLayoutStore((state) => state.setPanelPosition);
  const setPanelSize = useLayoutStore((state) => state.setPanelSize);
  const toggleMinimize = useLayoutStore((state) => state.toggleMinimize);
  const bringToFront = useLayoutStore((state) => state.bringToFront);

  // Local state
  const [isDragging, setIsDragging] = useState(false);
  const [isResizing, setIsResizing] = useState(false);
  const [ctrlHeld, setCtrlHeld] = useState(false);
  const [longPressActive, setLongPressActive] = useState(false);
  const [justDragged, setJustDragged] = useState(false);
  const [isOverEmptySpace, setIsOverEmptySpace] = useState(false);

  // Refs
  const dragStartRef = useRef({ x: 0, y: 0, posX: 0, posY: 0 });
  const resizeStartRef = useRef({ x: 0, y: 0, width: 0, height: 0 });
  const longPressTimerRef = useRef<NodeJS.Timeout | null>(null);
  const panelRef = useRef<HTMLDivElement>(null);

  // Calculate actual position (handle negative values for right/bottom anchoring)
  // Start with raw position to avoid hydration mismatch, then update on client
  const [actualPosition, setActualPosition] = useState({
    x: panel.position.x,
    y: panel.position.y
  });
  const [isClient, setIsClient] = useState(false);

  // Mark as client-side and calculate real position with constraints
  useEffect(() => {
    setIsClient(true);

    // Convert negative values to actual positions
    // Only treat as right/bottom-anchored if the result places panel in right/bottom half
    // Small negatives (left/top overhang) should be kept as-is
    let x = panel.position.x;
    let y = panel.position.y;

    if (panel.position.x < 0) {
      const rightAnchoredX = window.innerWidth + panel.position.x;
      // If right-anchored position is in right half of screen, use it
      // Otherwise, keep as left overhang
      if (rightAnchoredX > window.innerWidth / 2) {
        x = rightAnchoredX;
      }
      // else: x stays as panel.position.x (negative = left overhang)
    }

    if (panel.position.y < 0) {
      const bottomAnchoredY = window.innerHeight + panel.position.y;
      // If bottom-anchored position is in bottom half of screen, use it
      // Otherwise, keep as top overhang
      if (bottomAnchoredY > window.innerHeight / 2) {
        y = bottomAnchoredY;
      }
      // else: y stays as panel.position.y (negative = top overhang)
    }

    // Constrain to keep panel visible on screen (handles window size changes)
    // Keep at least 50px of the panel visible
    const minX = -(panel.size.width - 50);
    const maxX = window.innerWidth - 50;
    const minY = 0;
    const maxY = window.innerHeight - 50;
    x = Math.max(minX, Math.min(maxX, x));
    y = Math.max(minY, Math.min(maxY, y));

    setActualPosition({ x, y });
  }, [panel.position, panel.size]);

  // Track CTRL key state
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Control') setCtrlHeld(true);
    };
    const handleKeyUp = (e: KeyboardEvent) => {
      if (e.key === 'Control') setCtrlHeld(false);
    };

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);
    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
    };
  }, []);

  // Update position on window resize (with constraints)
  useEffect(() => {
    const handleResize = () => {
      // Convert negative values to actual positions
      // Only treat as right/bottom-anchored if the result places panel in right/bottom half
      let x = panel.position.x;
      let y = panel.position.y;

      if (panel.position.x < 0) {
        const rightAnchoredX = window.innerWidth + panel.position.x;
        if (rightAnchoredX > window.innerWidth / 2) {
          x = rightAnchoredX;
        }
      }

      if (panel.position.y < 0) {
        const bottomAnchoredY = window.innerHeight + panel.position.y;
        if (bottomAnchoredY > window.innerHeight / 2) {
          y = bottomAnchoredY;
        }
      }

      // Constrain to keep panel visible
      const minX = -(panel.size.width - 50);
      const maxX = window.innerWidth - 50;
      const minY = 0;
      const maxY = window.innerHeight - 50;
      x = Math.max(minX, Math.min(maxX, x));
      y = Math.max(minY, Math.min(maxY, y));

      setActualPosition({ x, y });
    };

    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, [panel.position, panel.size]);

  // Start drag (desktop: CTRL+click, mobile: after long press)
  const startDrag = useCallback((clientX: number, clientY: number) => {
    setIsDragging(true);
    dragStartRef.current = {
      x: clientX,
      y: clientY,
      posX: actualPosition.x,
      posY: actualPosition.y,
    };
  }, [actualPosition]);

  // Mouse down handler
  const handleMouseDown = useCallback((e: React.MouseEvent) => {
    if (e.button !== 0) return;

    // CTRL+click to drag (always works)
    if (e.ctrlKey || e.metaKey) {
      e.preventDefault();
      e.stopPropagation();
      startDrag(e.clientX, e.clientY);
      return;
    }

    // Background drag: click on non-interactive areas starts drag
    if (enableBackgroundDrag) {
      const target = e.target as HTMLElement;
      // Don't start drag if clicking on an interactive element
      if (!isInteractiveElement(target, panelRef.current)) {
        e.preventDefault();
        e.stopPropagation();
        startDrag(e.clientX, e.clientY);
      }
    }
  }, [startDrag, enableBackgroundDrag]);

  // Track mouse position for cursor change when enableBackgroundDrag is active
  const handleMouseMove = useCallback((e: React.MouseEvent) => {
    if (!enableBackgroundDrag || isDragging) return;
    const target = e.target as HTMLElement;
    const overEmpty = !isInteractiveElement(target, panelRef.current);
    setIsOverEmptySpace(overEmpty);
  }, [enableBackgroundDrag, isDragging]);

  const handleMouseLeave = useCallback(() => {
    setIsOverEmptySpace(false);
  }, []);

  // Touch handlers for long press
  const handleTouchStart = useCallback((e: React.TouchEvent) => {
    const touch = e.touches[0];

    // Start long press timer
    longPressTimerRef.current = setTimeout(() => {
      setLongPressActive(true);
      startDrag(touch.clientX, touch.clientY);
      // Haptic feedback if available
      if ('vibrate' in navigator) {
        navigator.vibrate(50);
      }
    }, LONG_PRESS_DURATION);
  }, [startDrag]);

  const handleTouchEnd = useCallback(() => {
    if (longPressTimerRef.current) {
      clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = null;
    }

    // If we were dragging, save position and prevent click
    if (isDragging) {
      setIsDragging(false);
      setJustDragged(true);
      setTimeout(() => setJustDragged(false), 100);

      // Save to store (convert to edge-anchored if near edge)
      // Only anchor if the panel's edge is truly near the window edge
      const threshold = 100;

      // Check if panel RIGHT edge is near window RIGHT edge
      const panelRight = actualPosition.x + panel.size.width;
      const nearRight = panelRight > window.innerWidth - threshold;
      const saveX = nearRight ? actualPosition.x - window.innerWidth : actualPosition.x;

      // Check if panel BOTTOM edge is near window BOTTOM edge
      const panelBottom = actualPosition.y + panel.size.height;
      const nearBottom = panelBottom > window.innerHeight - threshold;
      const saveY = nearBottom ? actualPosition.y - window.innerHeight : actualPosition.y;

      setPanelPosition(panelId, { x: saveX, y: saveY });
    }

    setLongPressActive(false);
  }, [isDragging, actualPosition, panel.size, panelId, setPanelPosition]);

  const handleTouchMove = useCallback((e: React.TouchEvent) => {
    // Cancel long press if moved before timer fires
    if (!isDragging && longPressTimerRef.current) {
      clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = null;
    }

    // Continue drag if active
    if (isDragging) {
      const touch = e.touches[0];
      const deltaX = touch.clientX - dragStartRef.current.x;
      const deltaY = touch.clientY - dragStartRef.current.y;
      const newX = dragStartRef.current.posX + deltaX;
      const newY = dragStartRef.current.posY + deltaY;

      // Allow panels to go partially off-screen (keep at least 50px visible)
      const minX = -(panel.size.width - 50);
      const maxX = window.innerWidth - 50;
      const minY = 0;
      const maxY = window.innerHeight - 50;
      const constrainedX = Math.max(minX, Math.min(maxX, newX));
      const constrainedY = Math.max(minY, Math.min(maxY, newY));

      setActualPosition({ x: constrainedX, y: constrainedY });
    }
  }, [isDragging]);

  // Drag effect (mouse)
  useEffect(() => {
    if (!isDragging) return;

    const handleMouseMove = (e: MouseEvent) => {
      const deltaX = e.clientX - dragStartRef.current.x;
      const deltaY = e.clientY - dragStartRef.current.y;
      const newX = dragStartRef.current.posX + deltaX;
      const newY = dragStartRef.current.posY + deltaY;

      // Allow panels to go partially off-screen (keep at least 50px visible)
      const minX = -(panel.size.width - 50);
      const maxX = window.innerWidth - 50;
      const minY = 0;
      const maxY = window.innerHeight - 50;
      const constrainedX = Math.max(minX, Math.min(maxX, newX));
      const constrainedY = Math.max(minY, Math.min(maxY, newY));

      setActualPosition({ x: constrainedX, y: constrainedY });
    };

    const handleMouseUp = () => {
      setIsDragging(false);
      setLongPressActive(false);
      // Prevent click from firing after drag
      setJustDragged(true);
      setTimeout(() => setJustDragged(false), 100);

      // Save to store (convert to edge-anchored if near edge)
      // Only anchor if the panel's edge is truly near the window edge
      const threshold = 100;

      // Check if panel RIGHT edge is near window RIGHT edge
      const panelRight = actualPosition.x + panel.size.width;
      const nearRight = panelRight > window.innerWidth - threshold;
      const saveX = nearRight ? actualPosition.x - window.innerWidth : actualPosition.x;

      // Check if panel BOTTOM edge is near window BOTTOM edge
      const panelBottom = actualPosition.y + panel.size.height;
      const nearBottom = panelBottom > window.innerHeight - threshold;
      const saveY = nearBottom ? actualPosition.y - window.innerHeight : actualPosition.y;

      setPanelPosition(panelId, { x: saveX, y: saveY });
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isDragging, actualPosition, panelId, panel.size, setPanelPosition]);

  // Resize handlers
  const handleResizeStart = useCallback((e: React.MouseEvent | React.TouchEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsResizing(true);

    const clientX = 'touches' in e ? e.touches[0].clientX : e.clientX;
    const clientY = 'touches' in e ? e.touches[0].clientY : e.clientY;

    resizeStartRef.current = {
      x: clientX,
      y: clientY,
      width: panel.size.width,
      height: panel.size.height,
    };
  }, [panel.size]);

  useEffect(() => {
    if (!isResizing) return;

    const handleMove = (e: MouseEvent | TouchEvent) => {
      const clientX = 'touches' in e ? e.touches[0].clientX : e.clientX;
      const clientY = 'touches' in e ? e.touches[0].clientY : e.clientY;

      const deltaX = clientX - resizeStartRef.current.x;
      const deltaY = clientY - resizeStartRef.current.y;
      const newWidth = Math.max(minWidth, resizeStartRef.current.width + deltaX);
      const newHeight = Math.max(minHeight, resizeStartRef.current.height + deltaY);
      setPanelSize(panelId, { width: newWidth, height: newHeight });
    };

    const handleEnd = () => {
      setIsResizing(false);
    };

    document.addEventListener('mousemove', handleMove);
    document.addEventListener('mouseup', handleEnd);
    document.addEventListener('touchmove', handleMove);
    document.addEventListener('touchend', handleEnd);
    return () => {
      document.removeEventListener('mousemove', handleMove);
      document.removeEventListener('mouseup', handleEnd);
      document.removeEventListener('touchmove', handleMove);
      document.removeEventListener('touchend', handleEnd);
    };
  }, [isResizing, minWidth, minHeight, panelId, setPanelSize]);

  // Bring to front when panel is interacted with (must be before conditional returns)
  const handlePanelFocus = useCallback(() => {
    bringToFront(panelId);
  }, [bringToFront, panelId]);

  // Don't render if not visible
  if (!panel.visible) return null;

  // Don't render during SSR to avoid hydration mismatch
  // (position calculation depends on window dimensions)
  if (!isClient) return null;

  // Minimized state - just show icon
  if (panel.minimized) {
    return (
      <motion.div
        ref={panelRef}
        className="absolute bg-gray-900/95 backdrop-blur-sm rounded-lg shadow-xl border border-gray-700/50 cursor-pointer hover:bg-gray-800/95 transition-colors"
        style={{
          left: actualPosition.x,
          top: actualPosition.y,
          zIndex: isDragging ? 100 : panel.zIndex,
        }}
        initial={{ opacity: 0, scale: 0.8 }}
        animate={{ opacity: 1, scale: 1 }}
        transition={{ duration: 0.15 }}
        onClick={() => !justDragged && toggleMinimize(panelId)}
        onMouseDown={(e) => {
          handlePanelFocus();
          handleMouseDown(e);
        }}
        onTouchStart={(e) => {
          handlePanelFocus();
          handleTouchStart(e);
        }}
        onTouchEnd={handleTouchEnd}
        onTouchMove={handleTouchMove}
        title={`${title} (click to expand, CTRL+click to drag)`}
      >
        <div className="px-3 py-2 flex items-center gap-2">
          {icon && <span className="text-gray-400">{icon}</span>}
          <span className="text-xs text-gray-400 font-medium">{title}</span>
        </div>
      </motion.div>
    );
  }

  // Determine cursor style
  const showMoveCursor = isDragging || ctrlHeld || (enableBackgroundDrag && isOverEmptySpace);

  return (
    <motion.div
      ref={panelRef}
      className={`absolute bg-gray-900/95 backdrop-blur-sm rounded-lg shadow-xl border overflow-hidden select-none ${className} ${
        isDragging || longPressActive
          ? 'border-amber-500/50 ring-2 ring-amber-500/30'
          : ctrlHeld || (enableBackgroundDrag && isOverEmptySpace)
          ? 'border-gray-600'
          : 'border-gray-700/50'
      }`}
      style={{
        ...style,
        left: actualPosition.x,
        top: actualPosition.y,
        width: panel.size.width,
        height: panel.size.height,
        zIndex: isDragging ? 100 : panel.zIndex,
        cursor: showMoveCursor ? 'move' : undefined,
      }}
      onMouseDown={(e) => {
        handlePanelFocus();
        handleMouseDown(e);
      }}
      onMouseMove={handleMouseMove}
      onMouseLeave={handleMouseLeave}
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ duration: 0.15 }}
      onTouchStart={(e) => {
        handlePanelFocus();
        handleTouchStart(e);
      }}
      onTouchEnd={handleTouchEnd}
      onTouchMove={handleTouchMove}
      title={ctrlHeld ? 'Click and drag to move' : enableBackgroundDrag ? 'Drag empty space to move, long press on mobile' : 'CTRL+click to drag, long press on mobile'}
    >
      {/* Content */}
      <div className="absolute inset-0 overflow-auto">
        {children}
      </div>

      {/* Minimize button (top-right corner) */}
      <button
        onClick={(e) => {
          e.stopPropagation();
          toggleMinimize(panelId);
        }}
        className="absolute top-1 right-1 w-5 h-5 flex items-center justify-center text-gray-500 hover:text-white hover:bg-gray-700/50 rounded text-xs transition-colors z-10"
        title="Minimize"
      >
        ─
      </button>

      {/* Resize handle (bottom-right corner) */}
      <div
        className="absolute bottom-0 right-0 w-4 h-4 cursor-se-resize z-10"
        onMouseDown={handleResizeStart}
        onTouchStart={handleResizeStart}
      >
        <svg
          className="w-full h-full text-gray-600 hover:text-gray-400 transition-colors"
          viewBox="0 0 16 16"
          fill="currentColor"
        >
          <path d="M14 14H12V12H14V14ZM14 10H12V8H14V10ZM10 14H8V12H10V14Z" />
        </svg>
      </div>

      {/* Drag indicator (shows when CTRL is held) */}
      {ctrlHeld && !isDragging && (
        <div className="absolute inset-0 bg-amber-500/5 pointer-events-none flex items-center justify-center">
          <span className="text-amber-500/50 text-xs font-medium">Click to drag</span>
        </div>
      )}
    </motion.div>
  );
}

export default FloatingPanel;
