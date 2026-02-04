'use client';

import { useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faCheck, faChevronRight, faChevronDown } from '@fortawesome/free-solid-svg-icons';
import type { IconDefinition } from '@fortawesome/fontawesome-svg-core';

export interface DropdownItem {
  id: string;
  label: string;
  icon?: IconDefinition;
  onClick?: () => void;
  disabled?: boolean;
  checked?: boolean;
  type?: 'separator';
  children?: DropdownItem[];
}

interface DropdownMenuProps {
  items: DropdownItem[];
  anchorEl: HTMLElement;
  onClose: () => void;
}

/**
 * Floating dropdown menu positioned to the right of an anchor element.
 * Renders via portal to escape sidebar stacking context.
 * Supports icons, checkmarks, separators, and expandable sub-sections.
 */
export function DropdownMenu({ items, anchorEl, onClose }: DropdownMenuProps): React.ReactElement | null {
  const menuRef = useRef<HTMLDivElement>(null);
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const [mounted, setMounted] = useState(false);

  // Position to the right of anchor, computed from bounding rect
  const [position, setPosition] = useState({ x: 0, y: 0 });

  // SSR guard + initial position
  useEffect(() => {
    const rect = anchorEl.getBoundingClientRect();
    setPosition({ x: rect.right + 4, y: rect.top });
    setMounted(true);
  }, [anchorEl]);

  // Clamp to viewport after render or sub-menu expansion
  useEffect(() => {
    if (!mounted) return;
    const el = menuRef.current;
    if (!el) return;

    const rect = el.getBoundingClientRect();
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    let { x, y } = position;
    if (x + rect.width > vw - 8) x = Math.max(8, vw - rect.width - 8);
    if (y + rect.height > vh - 8) y = Math.max(8, vh - rect.height - 8);

    if (x !== position.x || y !== position.y) {
      setPosition({ x, y });
    }
  }, [mounted, position, expandedId]);

  // Escape key dismisses
  useEffect(() => {
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handleKey);
    return () => window.removeEventListener('keydown', handleKey);
  }, [onClose]);

  if (!mounted) return null;

  const renderChild = (child: DropdownItem) => (
    <div
      key={child.id}
      className={`flex items-center gap-2 pl-9 pr-3 py-1.5 text-sm whitespace-nowrap transition-colors ${
        child.disabled
          ? 'text-gray-600 cursor-default'
          : 'text-white cursor-pointer hover:bg-[#3a3a3a]'
      }`}
      onClick={() => {
        if (child.disabled) return;
        if (child.onClick) {
          child.onClick();
          onClose();
        }
      }}
    >
      <span className="w-4 text-center text-xs shrink-0">
        {child.checked ? <FontAwesomeIcon icon={faCheck} /> :
         child.icon ? <FontAwesomeIcon icon={child.icon} /> : null}
      </span>
      <span>{child.label}</span>
    </div>
  );

  const renderItem = (item: DropdownItem) => {
    if (item.type === 'separator') {
      return <div key={item.id} className="my-1 mx-2 border-t border-[#444]" />;
    }

    const hasChildren = item.children && item.children.length > 0;
    const isExpanded = expandedId === item.id;

    return (
      <div key={item.id}>
        <div
          className={`flex items-center gap-2 px-3 py-1.5 text-sm whitespace-nowrap transition-colors ${
            item.disabled
              ? 'text-gray-600 cursor-default'
              : 'text-white cursor-pointer hover:bg-[#3a3a3a]'
          }`}
          onClick={() => {
            if (item.disabled) return;
            if (hasChildren) {
              setExpandedId(isExpanded ? null : item.id);
            } else if (item.onClick) {
              item.onClick();
              onClose();
            }
          }}
        >
          <span className="w-4 text-center text-xs shrink-0">
            {item.checked ? <FontAwesomeIcon icon={faCheck} /> :
             item.icon ? <FontAwesomeIcon icon={item.icon} /> : null}
          </span>
          <span className="flex-1">{item.label}</span>
          {hasChildren && (
            <FontAwesomeIcon
              icon={isExpanded ? faChevronDown : faChevronRight}
              className="text-[10px] text-gray-400 ml-3"
            />
          )}
        </div>

        {hasChildren && isExpanded && (
          <div className="bg-[#222]">
            {item.children!.map(renderChild)}
          </div>
        )}
      </div>
    );
  };

  return createPortal(
    <>
      {/* Transparent backdrop for click-away dismissal */}
      <div className="fixed inset-0 z-[1600]" onClick={onClose} />

      {/* Menu panel */}
      <div
        ref={menuRef}
        className="fixed z-[1601] min-w-[180px] rounded-lg overflow-hidden py-1"
        style={{
          left: position.x,
          top: position.y,
          backgroundColor: '#2a2a2a',
          border: '1px solid #444',
          boxShadow: '0 4px 20px rgba(0, 0, 0, 0.5)',
          maxHeight: 'calc(100vh - 16px)',
          overflowY: 'auto',
        }}
      >
        {items.map(renderItem)}
      </div>
    </>,
    document.body
  );
}
