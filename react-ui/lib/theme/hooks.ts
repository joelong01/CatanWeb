/**
 * Theme hooks - reactive wrappers around the theme store for components.
 *
 * IMPORTANT: Zustand selectors must return referentially stable values.
 * Selectors that compute new objects/arrays cause infinite render loops.
 * These hooks select raw state and derive values via useMemo.
 */

import { useMemo } from 'react';
import { CatanGlyph } from '@/lib/constants/catanGlyphs';
import { useThemeStore } from './themeStore';
import type { ResolvedHarborFontConfig, ResolvedTileFontConfig } from './themeStore';
import type { AssetName, RenderMode, ThemeDefinition, ThemeMetadata } from './types';

/** Whether theme data has been loaded from theme.json files. */
export function useThemeInitialized(): boolean {
  return useThemeStore((s) => s.initialized);
}

/** Current theme's render mode ('image' or 'font'). */
export function useRenderMode(): RenderMode {
  const currentTheme = useThemeStore((s) => s.currentTheme);
  const themes = useThemeStore((s) => s.themes);
  return themes[currentTheme]?.renderMode ?? 'image';
}

/** Whether current theme uses font rendering. */
export function useFontRendering(): boolean {
  const currentTheme = useThemeStore((s) => s.currentTheme);
  const themes = useThemeStore((s) => s.themes);
  return (themes[currentTheme]?.renderMode ?? 'image') === 'font';
}

/** Resolve an asset path through the full theme parent chain. */
export function useAssetPath(asset: AssetName): string {
  const currentTheme = useThemeStore((s) => s.currentTheme);
  const themes = useThemeStore((s) => s.themes);

  // Walk parent chain: currentTheme -> parent -> ... -> base
  const visited = new Set<string>();
  let name: string | undefined = currentTheme;
  while (name && !visited.has(name)) {
    visited.add(name);
    const theme: ThemeDefinition | undefined = themes[name];
    if (!theme) break;
    const path = theme.assets[asset];
    if (path) return path;
    name = theme.parent;
  }
  return '';
}

function resolveGlyph(glyphName: string): string | undefined {
  return (CatanGlyph as Record<string, string>)[glyphName];
}

/** Get resolved tile font config for a resource type, or undefined if not available. */
export function useTileFontConfig(resourceType: string): ResolvedTileFontConfig | undefined {
  const currentTheme = useThemeStore((s) => s.currentTheme);
  const themes = useThemeStore((s) => s.themes);

  return useMemo(() => {
    const raw = themes[currentTheme]?.colors?.tiles?.[resourceType];
    if (!raw) return undefined;
    const glyph = resolveGlyph(raw.glyph);
    if (!glyph) return undefined;
    return { glyph, color: raw.color, bgColor: raw.bgColor, borderColor: raw.borderColor };
  }, [themes, currentTheme, resourceType]);
}

/** Get resolved harbor font config for a harbor type, or undefined if not available. */
export function useHarborFontConfig(harborType: string): ResolvedHarborFontConfig | undefined {
  const currentTheme = useThemeStore((s) => s.currentTheme);
  const themes = useThemeStore((s) => s.themes);

  return useMemo(() => {
    const raw = themes[currentTheme]?.colors?.harbors?.[harborType];
    if (!raw) return undefined;
    const harborGlyph = resolveGlyph(raw.harborGlyph);
    if (!harborGlyph) return undefined;
    const hexGlyph = raw.hexGlyph ? resolveGlyph(raw.hexGlyph) : undefined;
    return {
      harborGlyph,
      hexGlyph,
      color: raw.color,
      bgColor: raw.bgColor,
      hexOpacity: raw.hexOpacity,
      faIcon: raw.faIcon,
    };
  }, [themes, currentTheme, harborType]);
}

/** List of available themes (excludes base). */
export function useAvailableThemes(): ThemeMetadata[] {
  const themes = useThemeStore((s) => s.themes);

  return useMemo(() => {
    return Object.values(themes)
      .filter((t) => t.name !== 'base')
      .map((t) => ({
        name: t.name,
        displayName: t.displayName,
        description: t.description,
        preview: t.preview,
        renderMode: t.renderMode ?? ('image' as const),
      }));
  }, [themes]);
}

/** Current theme name. */
export function useCurrentThemeName(): string {
  return useThemeStore((s) => s.currentTheme);
}
