'use client';

import React, { useEffect, useRef, useCallback } from 'react';
import { createPortal } from 'react-dom';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faTrash } from '@fortawesome/free-solid-svg-icons';
import type { TemplateHarbor } from '@/types/generated/models';

const HARBOR_TYPE_OPTIONS = ['ThreeForOne', 'Wood', 'Brick', 'Wheat', 'Sheep', 'Ore'];
const HARBOR_SIDE_OPTIONS = ['Top', 'TopRight', 'BottomRight', 'Bottom', 'BottomLeft', 'TopLeft'];

interface HarborContextMenuProps {
  harbor: TemplateHarbor;
  harborIndex: number;
  position: { x: number; y: number };
  onUpdateHarbor: (index: number, updates: Partial<TemplateHarbor>) => void;
  onRemoveHarbor: (index: number) => void;
  onClose: () => void;
}

export function HarborContextMenu({
  harbor,
  harborIndex,
  position,
  onUpdateHarbor,
  onRemoveHarbor,
  onClose,
}: HarborContextMenuProps): React.ReactElement {
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const handleKey = (e: KeyboardEvent): void => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [onClose]);

  useEffect(() => {
    const handleClick = (e: MouseEvent): void => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };
    const timer = setTimeout(() => {
      document.addEventListener('mousedown', handleClick);
    }, 0);
    return () => {
      clearTimeout(timer);
      document.removeEventListener('mousedown', handleClick);
    };
  }, [onClose]);

  useEffect(() => {
    if (!menuRef.current) return;
    const rect = menuRef.current.getBoundingClientRect();
    const el = menuRef.current;
    if (rect.right > window.innerWidth) {
      el.style.left = `${position.x - rect.width}px`;
    }
    if (rect.bottom > window.innerHeight) {
      el.style.top = `${position.y - rect.height}px`;
    }
  }, [position]);

  const handleRemove = useCallback(() => {
    onRemoveHarbor(harborIndex);
  }, [harborIndex, onRemoveHarbor]);

  return createPortal(
    <div
      ref={menuRef}
      className="fixed z-50 bg-gray-900 border border-white/20 rounded-lg shadow-xl p-3 space-y-3 min-w-[200px]"
      style={{ left: position.x, top: position.y }}
    >
      <div className="text-xs text-gray-400 font-medium uppercase tracking-wider">
        Harbor ({harbor.q}, {harbor.r}) {harbor.side}
      </div>

      <label className="block">
        <span className="text-xs text-gray-400">Type</span>
        <select
          value={harbor.type}
          onChange={(e) => onUpdateHarbor(harborIndex, { type: e.target.value })}
          className="w-full mt-1 bg-white/5 border border-white/10 rounded px-2 py-1 text-sm text-white"
        >
          {HARBOR_TYPE_OPTIONS.map((t) => (
            <option key={t} value={t}>
              {t}
            </option>
          ))}
        </select>
      </label>

      <label className="block">
        <span className="text-xs text-gray-400">Side</span>
        <select
          value={harbor.side}
          onChange={(e) => onUpdateHarbor(harborIndex, { side: e.target.value })}
          className="w-full mt-1 bg-white/5 border border-white/10 rounded px-2 py-1 text-sm text-white"
        >
          {HARBOR_SIDE_OPTIONS.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
      </label>

      <button
        onClick={handleRemove}
        className="w-full flex items-center justify-center gap-2 px-3 py-1.5 rounded text-sm text-red-400 bg-red-500/10 hover:bg-red-500/20 transition-colors"
      >
        <FontAwesomeIcon icon={faTrash} className="text-xs" />
        Remove Harbor
      </button>

      <div className="border-t border-white/10" />
      <button
        onClick={onClose}
        className="w-full flex items-center justify-center px-3 py-1.5 rounded text-sm text-gray-300 bg-white/5 hover:bg-white/10 transition-colors"
      >
        Close
      </button>
    </div>,
    document.body
  );
}
