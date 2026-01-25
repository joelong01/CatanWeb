'use client';

import React from 'react';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import type { IconDefinition } from '@fortawesome/fontawesome-svg-core';
import { HEX_CONTENT_SCALE } from '../constants';

/**
 * Props for the CenterHex component.
 */
export interface CenterHexProps {
  /** FontAwesome icon to display */
  icon: IconDefinition;
  /** Primary title text */
  title: string;
  /** Optional subtitle text */
  subtitle?: string;
  /** Tailwind class for icon/text color (default: 'text-amber-400') */
  accentColor?: string;
  /** CSS background value (default: gradient) */
  background?: string;
}

/**
 * Non-interactive branding/title hex for grid centers.
 *
 * Displays an icon with title/subtitle text. Used for decorative
 * center hexes like "Catan" branding or section labels.
 *
 * @example
 * ```tsx
 * // Home page center
 * <CenterHex
 *   icon={faDice}
 *   title="Catan"
 *   accentColor="text-amber-400"
 * />
 *
 * // Section label
 * <CenterHex
 *   icon={faUser}
 *   title="Choose"
 *   subtitle="Players"
 *   accentColor="text-blue-400"
 * />
 * ```
 */
export function CenterHex({
  icon,
  title,
  subtitle,
  accentColor = 'text-amber-400',
  background = 'linear-gradient(160deg, rgba(30, 41, 59, 0.9) 0%, rgba(15, 23, 42, 0.95) 100%)',
}: CenterHexProps): React.ReactElement {
  return (
    <div className="w-full h-full">
      {/* Outer hex - subtle border */}
      <div className="absolute inset-0 hex-clip-flat bg-white/20" />

      {/* Inner hex - content */}
      <div
        className="absolute inset-0 flex items-center justify-center hex-clip-flat"
        style={{
          background,
          transform: `scale(${HEX_CONTENT_SCALE})`,
        }}
      >
        <div className="text-center px-3">
          <FontAwesomeIcon icon={icon} className={`${accentColor} text-2xl mb-1`} />
          <h3 className={`text-sm font-bold ${accentColor} tracking-wide leading-tight`}>
            {title}
            {subtitle && (
              <>
                <br />
                {subtitle}
              </>
            )}
          </h3>
        </div>
      </div>
    </div>
  );
}
