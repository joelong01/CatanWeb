/**
 * Animation speed hook - applies CSS custom properties based on animation speed setting.
 *
 * Speed categories (following Desktop app pattern):
 * - Fast: Quick UI feedback (buttons, hovers)
 * - Medium/Normal: Standard animations (card flips, transitions)
 * - Slow: Longer transitions (tile dimming)
 * - ExtraSlow: Delayed animations
 *
 * The setting controls a multiplier:
 * - Slow: 2x normal
 * - Normal: 1x (base)
 * - Fast: 0.5x normal
 * - None: 0ms (instant)
 */

import { useEffect } from 'react';
import { useSettingsStore } from '@/lib/stores/settingsStore';
import type { AnimationSpeed } from '@/types/settings';

/** Base durations in milliseconds (when setting is "Normal") */
const BASE_DURATIONS = {
  fast: 100,
  medium: 250,
  slow: 500,
  extraSlow: 2000,
  buttonPress: 200,
  colorTransition: 300,
  delay: 1000,
} as const;

/** Multipliers for each animation speed setting */
const SPEED_MULTIPLIERS: Record<AnimationSpeed, number> = {
  Slow: 2,
  Normal: 1,
  Fast: 0.5,
  None: 0,
};

/**
 * Hook that applies animation speed CSS custom properties to :root.
 * Should be called once at the app root level.
 */
export function useAnimationSpeed(): void {
  const animationSpeed = useSettingsStore((state) => state.animationSpeed);
  const isInitialized = useSettingsStore((state) => state.isInitialized);
  const initialize = useSettingsStore((state) => state.initialize);

  // Initialize store on mount
  useEffect(() => {
    initialize();
  }, [initialize]);

  // Apply CSS variables when animation speed changes
  useEffect(() => {
    if (!isInitialized) return;

    const multiplier = SPEED_MULTIPLIERS[animationSpeed];
    const root = document.documentElement;

    // Apply each duration as a CSS custom property
    root.style.setProperty('--animation-fast', `${BASE_DURATIONS.fast * multiplier}ms`);
    root.style.setProperty('--animation-medium', `${BASE_DURATIONS.medium * multiplier}ms`);
    root.style.setProperty('--animation-slow', `${BASE_DURATIONS.slow * multiplier}ms`);
    root.style.setProperty('--animation-extra-slow', `${BASE_DURATIONS.extraSlow * multiplier}ms`);
    root.style.setProperty('--animation-button-press', `${BASE_DURATIONS.buttonPress * multiplier}ms`);
    root.style.setProperty('--animation-color-transition', `${BASE_DURATIONS.colorTransition * multiplier}ms`);
    root.style.setProperty('--animation-delay', `${BASE_DURATIONS.delay * multiplier}ms`);

    // Also set the legacy variable for backwards compatibility
    root.style.setProperty('--animation-duration', `${BASE_DURATIONS.medium * multiplier}ms`);
  }, [animationSpeed, isInitialized]);
}

/**
 * Get animation duration in milliseconds for a given speed category.
 * Useful for JavaScript animations that can't use CSS variables.
 */
export function getAnimationDuration(
  category: keyof typeof BASE_DURATIONS,
  speed: AnimationSpeed
): number {
  return BASE_DURATIONS[category] * SPEED_MULTIPLIERS[speed];
}
