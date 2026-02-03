/**
 * Theme store - manages theme state, asset resolution, and font configs.
 *
 * Uses Zustand with persist middleware (localStorage key: "catan-theme").
 * Themes are loaded from /themes/<name>/theme.json at initialization.
 *
 * Fallback chain: each theme has an optional `parent` field.
 *   modern → classic → base
 * Asset/config resolution walks the chain until a value is found.
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { CatanGlyph } from '@/lib/constants/catanGlyphs';
import type {
  AssetName,
  HarborFontConfig,
  RenderMode,
  ThemeDefinition,
  ThemeMetadata,
  TileFontConfig,
} from './types';

// ============================================================================
// Resolved types (glyph names -> actual glyph characters)
// ============================================================================

/** TileFontConfig with glyph name resolved to actual CatanFont character. */
export interface ResolvedTileFontConfig {
  glyph: string;
  color: string;
  bgColor: string;
  borderColor: string;
}

/** HarborFontConfig with glyph names resolved to actual CatanFont characters. */
export interface ResolvedHarborFontConfig {
  harborGlyph: string;
  hexGlyph?: string;
  color: string;
  bgColor?: string;
  hexOpacity?: number;
  faIcon?: string;
}

// ============================================================================
// Glyph resolution helper
// ============================================================================

function resolveGlyph(glyphName: string): string | undefined {
  return (CatanGlyph as Record<string, string>)[glyphName];
}

/**
 * Walk the parent chain starting from `themeName`, yielding each theme.
 * Chain terminates at base or when a parent isn't found.
 * Guards against cycles with a visited set.
 */
function* themeChain(
  themeName: string,
  themes: Record<string, ThemeDefinition>,
): Generator<ThemeDefinition> {
  const visited = new Set<string>();
  let current: string | undefined = themeName;
  while (current && !visited.has(current)) {
    visited.add(current);
    const theme: ThemeDefinition | undefined = themes[current];
    if (!theme) break;
    yield theme;
    current = theme.parent;
  }
}

// ============================================================================
// Store
// ============================================================================

interface ThemeState {
  currentTheme: string;
  themes: Record<string, ThemeDefinition>;
  initialized: boolean;
}

interface ThemeActions {
  initialize: () => Promise<void>;
  setTheme: (name: string) => void;
  getAssetPath: (asset: AssetName) => string;
  getRenderMode: () => RenderMode;
  getTileFontConfig: (resourceType: string) => ResolvedTileFontConfig | undefined;
  getHarborFontConfig: (harborType: string) => ResolvedHarborFontConfig | undefined;
  getAvailableThemes: () => ThemeMetadata[];
}

type ThemeStore = ThemeState & ThemeActions;

const THEME_NAMES = ['base', 'classic', 'modern', 'modern-dark'] as const;

async function fetchTheme(name: string): Promise<ThemeDefinition> {
  const response = await fetch(`/themes/${name}/theme.json`);
  if (!response.ok) {
    throw new Error(`Failed to load theme "${name}": ${response.statusText}`);
  }
  return response.json();
}

export const useThemeStore = create<ThemeStore>()(
  persist(
    (set, get) => ({
      currentTheme: 'classic',
      themes: {},
      initialized: false,

      initialize: async () => {
        if (get().initialized) return;

        try {
          const results = await Promise.all(
            THEME_NAMES.map((name) => fetchTheme(name))
          );
          const themes: Record<string, ThemeDefinition> = {};
          for (const theme of results) {
            themes[theme.name] = theme;
          }
          set({ themes, initialized: true });
        } catch (err) {
          console.error('Theme initialization failed:', err);
        }
      },

      setTheme: (name) => {
        const { themes } = get();
        if (themes[name]) {
          set({ currentTheme: name });
        } else {
          console.warn(`Theme "${name}" not found`);
        }
      },

      getAssetPath: (asset) => {
        const { themes, currentTheme } = get();
        for (const theme of themeChain(currentTheme, themes)) {
          const path = theme.assets[asset];
          if (path) return path;
        }
        return '';
      },

      getRenderMode: () => {
        const { themes, currentTheme } = get();
        return themes[currentTheme]?.renderMode ?? 'image';
      },

      getTileFontConfig: (resourceType) => {
        const { themes, currentTheme } = get();
        for (const theme of themeChain(currentTheme, themes)) {
          const raw: TileFontConfig | undefined =
            theme.colors?.tiles?.[resourceType];
          if (!raw) continue;

          const glyph = resolveGlyph(raw.glyph);
          if (!glyph) continue;

          return {
            glyph,
            color: raw.color,
            bgColor: raw.bgColor,
            borderColor: raw.borderColor,
          };
        }
        return undefined;
      },

      getHarborFontConfig: (harborType) => {
        const { themes, currentTheme } = get();
        for (const theme of themeChain(currentTheme, themes)) {
          const raw: HarborFontConfig | undefined =
            theme.colors?.harbors?.[harborType];
          if (!raw) continue;

          const harborGlyph = resolveGlyph(raw.harborGlyph);
          if (!harborGlyph) continue;

          const hexGlyph = raw.hexGlyph ? resolveGlyph(raw.hexGlyph) : undefined;

          return {
            harborGlyph,
            hexGlyph,
            color: raw.color,
            bgColor: raw.bgColor,
            hexOpacity: raw.hexOpacity,
            faIcon: raw.faIcon,
          };
        }
        return undefined;
      },

      getAvailableThemes: () => {
        const { themes } = get();
        return Object.values(themes)
          .filter((t) => t.name !== 'base')
          .map((t) => ({
            name: t.name,
            displayName: t.displayName,
            description: t.description,
            preview: t.preview,
            renderMode: t.renderMode ?? 'image',
          }));
      },
    }),
    {
      name: 'catan-theme',
      partialize: (state) => ({
        currentTheme: state.currentTheme,
      }),
    }
  )
);
