'use client';

import React, { useEffect, useRef, useCallback } from 'react';
import { createPortal } from 'react-dom';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faTrash } from '@fortawesome/free-solid-svg-icons';
import type { TemplateTile } from '@/types/generated/models';

const RESOURCE_OPTIONS = ['Desert', 'Wood', 'Brick', 'Wheat', 'Sheep', 'Ore', 'GoldMine'];
const NUMBER_OPTIONS = [0, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12];

interface TileContextMenuProps {
  tile: TemplateTile;
  tileIndex: number;
  position: { x: number; y: number };
  onUpdateTile: (index: number, updates: Partial<TemplateTile>) => void;
  onRemoveTile: (index: number) => void;
  onClose: () => void;
}

export function TileContextMenu({
  tile,
  tileIndex,
  position,
  onUpdateTile,
  onRemoveTile,
  onClose,
}: TileContextMenuProps): React.ReactElement {
  const menuRef = useRef<HTMLDivElement>(null);

  // Close on Escape
  useEffect(() => {
    const handleKey = (e: KeyboardEvent): void => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [onClose]);

  // Close on click outside
  useEffect(() => {
    const handleClick = (e: MouseEvent): void => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        onClose();
      }
    };
    // Delay to avoid closing immediately from the triggering right-click
    const timer = setTimeout(() => {
      document.addEventListener('mousedown', handleClick);
    }, 0);
    return () => {
      clearTimeout(timer);
      document.removeEventListener('mousedown', handleClick);
    };
  }, [onClose]);

  // Keep menu within viewport
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

  const handleResourceChange = useCallback(
    (resource: string) => {
      const updates: Partial<TemplateTile> = { resource };
      if (resource === 'Desert') {
        updates.number = 0;
      }
      onUpdateTile(tileIndex, updates);
    },
    [tileIndex, onUpdateTile]
  );

  const handleNumberChange = useCallback(
    (number: number) => {
      onUpdateTile(tileIndex, { number });
    },
    [tileIndex, onUpdateTile]
  );

  const handleRemove = useCallback(() => {
    onRemoveTile(tileIndex);
    onClose();
  }, [tileIndex, onRemoveTile, onClose]);

  const isDesert = tile.resource === 'Desert';

  return createPortal(
    <div
      ref={menuRef}
      className="fixed z-50 bg-gray-900 border border-white/20 rounded-lg shadow-xl p-3 space-y-3 min-w-[200px]"
      style={{ left: position.x, top: position.y }}
    >
      {/* Header */}
      <div className="text-xs text-gray-400 font-medium uppercase tracking-wider">
        Tile ({tile.q}, {tile.r})
      </div>

      {/* Resource */}
      <label className="block">
        <span className="text-xs text-gray-400">Resource</span>
        <select
          value={tile.resource}
          onChange={(e) => handleResourceChange(e.target.value)}
          className="w-full mt-1 bg-white/5 border border-white/10 rounded px-2 py-1 text-sm text-white"
        >
          {RESOURCE_OPTIONS.map((r) => (
            <option key={r} value={r}>
              {r}
            </option>
          ))}
        </select>
      </label>

      {/* Number */}
      <label className="block">
        <span className="text-xs text-gray-400">Number</span>
        <select
          value={tile.number}
          onChange={(e) => handleNumberChange(parseInt(e.target.value, 10))}
          disabled={isDesert}
          className="w-full mt-1 bg-white/5 border border-white/10 rounded px-2 py-1 text-sm text-white disabled:opacity-40 disabled:cursor-not-allowed"
        >
          {NUMBER_OPTIONS.map((n) => (
            <option key={n} value={n}>
              {n === 0 ? '—' : n}
            </option>
          ))}
        </select>
      </label>

      {/* Remove */}
      <button
        onClick={handleRemove}
        className="w-full flex items-center justify-center gap-2 px-3 py-1.5 rounded text-sm text-red-400 bg-red-500/10 hover:bg-red-500/20 transition-colors"
      >
        <FontAwesomeIcon icon={faTrash} className="text-xs" />
        Remove Tile
      </button>

      {/* Separator + Close */}
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
