'use client';

/**
 * ContextMenu - Generic positioned context menu.
 *
 * Follows the RobberTargetMenu pattern: fixed positioning at
 * click/touch coordinates with transparent backdrop for dismiss.
 * Clamps position to stay within viewport.
 */

import { useEffect, useRef, useState } from 'react';

export interface ContextMenuItem {
  label: string;
  onClick: () => void;
  disabled?: boolean;
}

interface ContextMenuProps {
  /** Position to render menu at (client coordinates) */
  position: { x: number; y: number };
  /** Menu items */
  items: ContextMenuItem[];
  /** Called when menu should close */
  onClose: () => void;
}

export function ContextMenu({
  position,
  items,
  onClose,
}: ContextMenuProps): React.ReactElement {
  const menuRef = useRef<HTMLDivElement>(null);
  const [adjustedPos, setAdjustedPos] = useState(position);

  // Clamp menu position to viewport after first render
  useEffect(() => {
    const el = menuRef.current;
    if (!el) return;

    const rect = el.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    let { x, y } = position;
    if (x + rect.width > vw) x = vw - rect.width - 8;
    if (y + rect.height > vh) y = vh - rect.height - 8;
    if (x < 8) x = 8;
    if (y < 8) y = 8;

    setAdjustedPos({ x, y });
  }, [position]);

  // Escape key dismisses
  useEffect(() => {
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleKey);
    return () => window.removeEventListener('keydown', handleKey);
  }, [onClose]);

  return (
    <>
      {/* Transparent backdrop for click-away dismissal */}
      <div
        className="fixed inset-0 z-[999]"
        onClick={onClose}
      />

      {/* Menu */}
      <div
        ref={menuRef}
        className="fixed z-[1000] min-w-[160px] rounded-lg overflow-hidden py-1"
        style={{
          left: adjustedPos.x,
          top: adjustedPos.y,
          backgroundColor: '#2a2a2a',
          border: '1px solid #444',
          boxShadow: '0 4px 20px rgba(0, 0, 0, 0.5)',
        }}
      >
        {items.map((item) => (
          <div
            key={item.label}
            className={`px-4 py-2 text-sm transition-colors ${
              item.disabled
                ? 'text-gray-600 cursor-default'
                : 'text-white cursor-pointer hover:bg-[#3a3a3a]'
            }`}
            onClick={() => {
              if (!item.disabled) {
                item.onClick();
              }
            }}
          >
            {item.label}
          </div>
        ))}
      </div>
    </>
  );
}
