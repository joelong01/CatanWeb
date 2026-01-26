'use client';

import React, { useState } from 'react';
import Link from 'next/link';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import type { IconDefinition } from '@fortawesome/fontawesome-svg-core';
import { HEX_CONTENT_SCALE, HEX_HOVER_SCALE, HEX_ICON_HOVER_SCALE } from '../constants';

/**
 * Props for the MenuHex component.
 */
export interface MenuHexProps {
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
  /** Navigation URL (uses Next.js Link) */
  href?: string;
  /** Click handler (alternative to href) */
  onClick?: () => void;
}

/**
 * Clickable menu item hex with hover effects.
 *
 * Supports both link navigation (href) and click actions (onClick).
 * Displays icon, title, and optional subtitle with hover state.
 *
 * @example
 * ```tsx
 * // Link navigation
 * <MenuHex
 *   icon={faGamepad}
 *   title="New Game"
 *   href="/new-game"
 *   accentColor="text-amber-400"
 * />
 *
 * // Click action
 * <MenuHex
 *   icon={faPlay}
 *   title="Return to"
 *   subtitle="Game"
 *   onClick={() => router.push('/game')}
 *   accentColor="text-green-400"
 * />
 * ```
 */
export function MenuHex({
  icon,
  title,
  subtitle,
  accentColor = 'text-amber-400',
  background = 'var(--hex-content-gradient)',
  href,
  onClick,
}: MenuHexProps): React.ReactElement {
  const [isHovered, setIsHovered] = useState(false);

  // Keyboard handler for accessibility (Enter/Space activates)
  const handleKeyDown = (e: React.KeyboardEvent): void => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onClick?.();
    }
  };

  // Accessibility props for non-link usage
  const a11yProps = !href && onClick ? {
    role: 'button' as const,
    tabIndex: 0,
    onKeyDown: handleKeyDown,
  } : {};

  const content = (
    <div
      className="w-full h-full cursor-pointer"
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      {...a11yProps}
    >
      {/* Outer hex - border with hover effect */}
      <div
        className="absolute inset-0 hex-clip-flat transition-colors duration-200"
        style={{
          background: isHovered ? 'var(--hex-border-hover)' : 'var(--hex-border-idle)',
        }}
      />

      {/* Inner hex - content */}
      <div
        className="absolute inset-0 flex items-center justify-center hex-clip-flat transition-transform duration-200"
        style={{
          background,
          transform: `scale(${isHovered ? HEX_HOVER_SCALE : HEX_CONTENT_SCALE})`,
        }}
      >
        <div className="text-center px-4">
          <FontAwesomeIcon
            icon={icon}
            className={`${accentColor} text-5xl mb-2 transition-transform duration-200`}
            style={{ transform: isHovered ? `scale(${HEX_ICON_HOVER_SCALE})` : undefined }}
          />
          <h3 className={`text-xl font-bold ${accentColor} tracking-wide leading-tight`}>
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

  // Wrap in Link if href provided
  if (href) {
    return (
      <Link href={href} className="w-full h-full block">
        {content}
      </Link>
    );
  }

  // Otherwise just return the clickable div (onClick handled by HexGrid item)
  return content;
}
