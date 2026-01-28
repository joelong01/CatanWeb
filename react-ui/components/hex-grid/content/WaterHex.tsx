'use client';

import React from 'react';
import { HEX_CONTENT_SCALE } from '../constants';

/**
 * Props for the WaterHex component.
 */
export interface WaterHexProps {
  /** Opacity of the water effect (default: 0.6) */
  opacity?: number;
  /** Optional image URL to use instead of gradient */
  imageUrl?: string;
  /** Whether to show border (two-polygon pattern) */
  showBorder?: boolean;
}

/**
 * Decorative water hex for filling unused grid positions.
 *
 * Renders either a water texture image or semi-transparent blue gradient.
 * Used as placeholder in hex grids where not all positions have content.
 *
 * @example
 * ```tsx
 * // Gradient water (default)
 * <WaterHex />
 *
 * // Image-based water with border
 * <WaterHex imageUrl="/water.png" showBorder />
 * ```
 */
export function WaterHex({
  opacity = 0.6,
  imageUrl,
  showBorder = false,
}: WaterHexProps): React.ReactElement {
  // Image-based water
  if (imageUrl) {
    return (
      <div className="w-full h-full" style={{ opacity }} data-drag-through>
        {showBorder && (
          <div className="absolute inset-0 hex-clip-flat bg-blue-500/40" />
        )}
        <div
          className="absolute inset-0 hex-clip-flat"
          style={{
            transform: showBorder ? `scale(${HEX_CONTENT_SCALE})` : undefined,
            backgroundImage: `url(${imageUrl})`,
            backgroundSize: 'cover',
            backgroundPosition: 'center',
          }}
        />
      </div>
    );
  }

  // Gradient water (default)
  return (
    <div
      className="w-full h-full hex-clip-flat"
      style={{
        background: `linear-gradient(180deg,
          rgba(30, 64, 120, ${opacity}) 0%,
          rgba(20, 50, 100, ${opacity}) 50%,
          rgba(15, 40, 80, ${opacity}) 100%)`,
      }}
      data-drag-through
    />
  );
}
