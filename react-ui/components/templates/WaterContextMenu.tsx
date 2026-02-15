'use client';

import React, { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faPlus, faAnchor } from '@fortawesome/free-solid-svg-icons';
import type { HexCoordinate } from '@/components/hex-grid/hex-geometry';

interface WaterContextMenuProps {
  coord: HexCoordinate;
  adjacentTiles: { q: number; r: number; side: string }[];
  position: { x: number; y: number };
  onAddTile: () => void;
  onAddHarbor: (tileQ: number, tileR: number, side: string) => void;
  onClose: () => void;
}

export function WaterContextMenu({
  coord,
  adjacentTiles,
  position,
  onAddTile,
  onAddHarbor,
  onClose,
}: WaterContextMenuProps): React.ReactElement {
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

  return createPortal(
    <div
      ref={menuRef}
      className="fixed z-50 bg-gray-900 border border-white/20 rounded-lg shadow-xl p-3 space-y-2 min-w-[180px]"
      style={{ left: position.x, top: position.y }}
    >
      <div className="text-xs text-gray-400 font-medium uppercase tracking-wider">
        Water ({coord.q}, {coord.r})
      </div>

      <button
        onClick={onAddTile}
        className="w-full flex items-center gap-2 px-3 py-1.5 rounded text-sm text-white bg-blue-600/20 hover:bg-blue-600/30 transition-colors"
      >
        <FontAwesomeIcon icon={faPlus} className="text-xs text-blue-400" />
        Add Tile
      </button>

      {adjacentTiles.length > 0 && (
        <>
          <div className="border-t border-white/10" />
          {adjacentTiles.map((at) => (
            <button
              key={`${at.q},${at.r},${at.side}`}
              onClick={() => onAddHarbor(at.q, at.r, at.side)}
              className="w-full flex items-center gap-2 px-3 py-1.5 rounded text-sm text-white bg-amber-600/20 hover:bg-amber-600/30 transition-colors"
            >
              <FontAwesomeIcon icon={faAnchor} className="text-xs text-amber-400" />
              Harbor ({at.q},{at.r}) {at.side}
            </button>
          ))}
        </>
      )}

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
