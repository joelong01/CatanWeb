'use client';

import { ReactNode, CSSProperties } from 'react';

/**
 * Props for the HexTile component.
 */
export interface HexTileProps {
  /** Hex width in pixels */
  width: number;
  /** Hex height in pixels */
  height: number;
  /** Absolute position (x, y) */
  position: { x: number; y: number };
  /** Content to render inside hex */
  children: ReactNode;
  /** Optional className for styling */
  className?: string;
  /** Optional inline styles */
  style?: CSSProperties;
  /** Click handler */
  onClick?: () => void;
  /** Whether tile is disabled */
  disabled?: boolean;
}

/**
 * Individual hex tile with clip-path masking.
 *
 * Positions absolutely within parent container.
 * Uses hex-clip-flat CSS utility for hexagon shape.
 *
 * The tile itself only handles positioning and clipping.
 * Content styling (borders, backgrounds, animations) is provided via children.
 */
export function HexTile({
  width,
  height,
  position,
  children,
  className = '',
  style = {},
  onClick,
  disabled = false,
}: HexTileProps): React.ReactElement {
  return (
    <div
      className={`absolute hex-clip-flat ${className} ${disabled ? 'cursor-not-allowed' : onClick ? 'cursor-pointer' : ''}`}
      style={{
        width: `${width}px`,
        height: `${height}px`,
        left: `${position.x}px`,
        top: `${position.y}px`,
        transform: 'translate(-50%, -50%)', // Center on position
        ...style,
      }}
      onClick={disabled ? undefined : onClick}
    >
      {children}
    </div>
  );
}
