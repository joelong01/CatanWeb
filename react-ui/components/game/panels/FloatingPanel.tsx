'use client';

/**
 * FloatingPanel - Draggable, resizable panel without title bar.
 *
 * Drag: CTRL+click (desktop) or long press (mobile)
 * Resize: Drag corner handle (always enabled)
 * Position persists to localStorage via layoutStore.
 *
 * When minimized, this component returns null.
 * The MinimizedBar component handles rendering minimized panels.
 */

import { useState, useRef, useEffect, useCallback, ReactNode } from 'react';
import { motion } from 'framer-motion';
import { useLayoutStore, type PanelId, type WindowPosition } from '@/lib/stores/layoutStore';

/** Long press duration for mobile drag (ms) */
const LONG_PRESS_DURATION = 400;

interface FloatingPanelProps {
  /** Panel identifier for persistence */
  panelId: PanelId;
  /** Panel title (shown in tooltip) */
  title: string;
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
  /**
   * Force panel to always be on top (z-index 1000+).
   * Use for modal overlays like goFirst, supplemental, etc.
   */
  alwaysOnTop?: boolean;
}

/** Default panel layout for new/unknown panels */
const DEFAULT_WINDOW_POSITION: WindowPosition = {
  left: 100,
  top: 100,
  width: 300,
  height: 200,
  minimized: false,
  visible: true,
  zIndex: 20,
};

/** Safely get panel with complete defaults */
function getPanelWithDefaults(stored: WindowPosition | undefined): WindowPosition {
  if (!stored) return DEFAULT_WINDOW_POSITION;
  return {
    left: stored.left ?? DEFAULT_WINDOW_POSITION.left,
    top: stored.top ?? DEFAULT_WINDOW_POSITION.top,
    width: stored.width ?? DEFAULT_WINDOW_POSITION.width,
    height: stored.height ?? DEFAULT_WINDOW_POSITION.height,
    minimized: stored.minimized ?? DEFAULT_WINDOW_POSITION.minimized,
    visible: stored.visible ?? DEFAULT_WINDOW_POSITION.visible,
    zIndex: stored.zIndex ?? DEFAULT_WINDOW_POSITION.zIndex,
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
  children,
  className = '',
  style,
  minWidth = 120,
  minHeight = 80,
  enableBackgroundDrag = true,
  alwaysOnTop = false,
}: FloatingPanelProps): React.ReactElement | null {
  // Get panel state from store (with fallback for missing/incomplete panels)
  const storedPanel = useLayoutStore((state) => state.panels[panelId]);
  const panel = getPanelWithDefaults(storedPanel);
  const setPanelPosition = useLayoutStore((state) => state.setPanelPosition);

  // DEBUG: Log what we're reading from store on mount/update
  console.log(`[FloatingPanel:${panelId}] storedPanel:`, storedPanel);
  console.log(`[FloatingPanel:${panelId}] panel after defaults:`, panel);
  const setPanelSize = useLayoutStore((state) => state.setPanelSize);
  const toggleMinimize = useLayoutStore((state) => state.toggleMinimize);
  const bringToFront = useLayoutStore((state) => state.bringToFront);

  // Local state
  const [isDragging, setIsDragging] = useState(false);
  const [isResizing, setIsResizing] = useState(false);
  const [ctrlHeld, setCtrlHeld] = useState(false);
  const [longPressActive, setLongPressActive] = useState(false);
  const [isOverEmptySpace, setIsOverEmptySpace] = useState(false);

  // Refs
  const dragStartRef = useRef({ x: 0, y: 0, posX: 0, posY: 0 });
  const resizeStartRef = useRef({ x: 0, y: 0, width: 0, height: 0 });
  const longPressTimerRef = useRef<NodeJS.Timeout | null>(null);
  const panelRef = useRef<HTMLDivElement>(null);
  // Track latest position during drag to avoid stale closure issues
  const latestPositionRef = useRef({ x: panel.left, y: panel.top });

  // Position state - just use raw values from store, no conversions
  const [actualPosition, setActualPosition] = useState({
    x: panel.left,
    y: panel.top
  });

  // Sync position when store changes (e.g., after reset)
  useEffect(() => {
    console.log(`[FloatingPanel:${panelId}] Sync effect triggered - panel.left=${panel.left}, panel.top=${panel.top}`);
    setActualPosition({ x: panel.left, y: panel.top });
    latestPositionRef.current = { x: panel.left, y: panel.top };
  }, [panel.left, panel.top, panelId]);

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

  // Start drag (desktop: CTRL+click, mobile: after long press)
  const startDrag = useCallback((clientX: number, clientY: number) => {
    console.log(`[FloatingPanel:${panelId}] startDrag called at (${clientX}, ${clientY}), starting from position (${actualPosition.x}, ${actualPosition.y})`);
    setIsDragging(true);
    dragStartRef.current = {
      x: clientX,
      y: clientY,
      posX: actualPosition.x,
      posY: actualPosition.y,
    };
  }, [actualPosition, panelId]);

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
      const isInteractive = isInteractiveElement(target, panelRef.current);
      console.log(`[FloatingPanel:${panelId}] Background drag check - target:`, target.tagName, target.className, 'isInteractive:', isInteractive);
      // Don't start drag if clicking on an interactive element
      if (!isInteractive) {
        e.preventDefault();
        e.stopPropagation();
        startDrag(e.clientX, e.clientY);
      }
    }
  }, [startDrag, enableBackgroundDrag, panelId]);

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

    // If we were dragging, save position using ref (avoids stale closure)
    if (isDragging) {
      setIsDragging(false);
      setPanelPosition(panelId, latestPositionRef.current.x, latestPositionRef.current.y);
    }

    setLongPressActive(false);
  }, [isDragging, panelId, setPanelPosition]);

  const handleTouchMove = useCallback((e: React.TouchEvent) => {
    // Cancel long press if moved before timer fires
    if (!isDragging && longPressTimerRef.current) {
      clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = null;
    }

    // Continue drag if active - no constraints, user can move anywhere
    if (isDragging) {
      const touch = e.touches[0];
      const deltaX = touch.clientX - dragStartRef.current.x;
      const deltaY = touch.clientY - dragStartRef.current.y;
      const newPos = {
        x: dragStartRef.current.posX + deltaX,
        y: dragStartRef.current.posY + deltaY,
      };
      setActualPosition(newPos);
      latestPositionRef.current = newPos;
    }
  }, [isDragging]);

  // Drag effect (mouse) - no constraints, user can move anywhere
  useEffect(() => {
    if (!isDragging) return;

    const handleMouseMove = (e: MouseEvent) => {
      const deltaX = e.clientX - dragStartRef.current.x;
      const deltaY = e.clientY - dragStartRef.current.y;
      const newPos = {
        x: dragStartRef.current.posX + deltaX,
        y: dragStartRef.current.posY + deltaY,
      };
      setActualPosition(newPos);
      // Update ref immediately so mouse up has the latest position
      latestPositionRef.current = newPos;
    };

    const handleMouseUp = () => {
      setIsDragging(false);
      setLongPressActive(false);
      // Save position using ref to avoid stale closure issues
      console.log(`[FloatingPanel:${panelId}] Saving position on drag end: x=${latestPositionRef.current.x}, y=${latestPositionRef.current.y}`);
      setPanelPosition(panelId, latestPositionRef.current.x, latestPositionRef.current.y);
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);
    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
    };
  }, [isDragging, panelId, setPanelPosition]);

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
      width: panel.width,
      height: panel.height,
    };
  }, [panel.width, panel.height]);

  useEffect(() => {
    if (!isResizing) return;

    const handleMove = (e: MouseEvent | TouchEvent) => {
      const clientX = 'touches' in e ? e.touches[0].clientX : e.clientX;
      const clientY = 'touches' in e ? e.touches[0].clientY : e.clientY;

      const deltaX = clientX - resizeStartRef.current.x;
      const deltaY = clientY - resizeStartRef.current.y;
      const newWidth = Math.max(minWidth, resizeStartRef.current.width + deltaX);
      const newHeight = Math.max(minHeight, resizeStartRef.current.height + deltaY);
      setPanelSize(panelId, newWidth, newHeight);
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

  // Don't render if not visible or minimized
  // MinimizedBar handles rendering for minimized panels
  if (!panel.visible || panel.minimized) return null;

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
        width: panel.width,
        height: panel.height,
        zIndex: isDragging ? 1001 : alwaysOnTop ? 1000 : panel.zIndex,
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
      {/* Content - overflow-hidden prevents scrollbar flash during animations */}
      <div className="absolute inset-0 overflow-hidden">
        {children}
      </div>

      {/* Minimize button (top-right corner) */}
      <button
        onClick={(e) => {
          e.stopPropagation();
          toggleMinimize(panelId);
        }}
        className="absolute top-1 right-1 w-5 h-5 flex items-center justify-center bg-gray-900/70 text-gray-400 hover:text-white hover:bg-gray-700 rounded text-xs transition-colors z-10 backdrop-blur-sm"
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
