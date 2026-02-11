'use client';

import { useEffect } from 'react';
import { useThemeStore } from '@/lib/theme';

/**
 * Initializes the theme store by loading theme.json files on mount.
 * Renders nothing - purely a side-effect component.
 */
export function ThemeInitializer(): null {
  const initialize = useThemeStore((s) => s.initialize);

  useEffect(() => {
    void initialize();
  }, [initialize]);

  return null;
}
