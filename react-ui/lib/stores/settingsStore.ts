/**
 * Settings store - manages user preferences and house rules.
 *
 * Uses localStorage for persistence with keys matching Blazor implementation:
 * - Individual settings: `setting_{SettingName}`
 * - Serialized house rules: `HouseRules`
 */

import { create } from 'zustand';
import type { AnimationSpeed } from '@/types/settings';
import { SETTINGS_DEFAULTS, SETTING_KEY_PREFIX, HOUSE_RULES_KEY } from '@/types/settings';
import type { HouseRules, GameType } from '@/types/generated/models';

// ============================================================================
// Types
// ============================================================================

interface SettingsState {
  /** Gold tiles for expansion games (0-4) */
  expansionGoldTiles: number;
  /** Gold tiles for regular games (0-2) */
  regularGoldTiles: number;
  /** Min players for supplemental build phase (3-6) */
  supplementalMinPlayers: number;
  /** Animation speed for UI transitions */
  animationSpeed: AnimationSpeed;
  /** Whether the store has been initialized from localStorage */
  isInitialized: boolean;
}

interface SettingsActions {
  /** Initialize store from localStorage */
  initialize: () => void;
  /** Set a single setting value */
  setExpansionGoldTiles: (value: number) => void;
  setRegularGoldTiles: (value: number) => void;
  setSupplementalMinPlayers: (value: number) => void;
  setAnimationSpeed: (value: AnimationSpeed) => void;
  /** Reset all settings to defaults */
  resetToDefaults: () => void;
  /** Save current settings to localStorage */
  saveToStorage: () => void;
  /** Build HouseRules object for game creation */
  getHouseRules: (gameType: GameType) => Partial<HouseRules>;
}

type SettingsStore = SettingsState & SettingsActions;

// ============================================================================
// localStorage helpers
// ============================================================================

/**
 * Read a setting from localStorage.
 */
function readSetting<T>(name: string, defaultValue: T): T {
  if (typeof window === 'undefined') return defaultValue;

  try {
    const stored = localStorage.getItem(`${SETTING_KEY_PREFIX}${name}`);
    if (stored === null) return defaultValue;

    // Parse based on expected type
    if (typeof defaultValue === 'boolean') {
      return (stored.toLowerCase() === 'true') as T;
    }
    if (typeof defaultValue === 'number') {
      const num = parseInt(stored, 10);
      return (isNaN(num) ? defaultValue : num) as T;
    }
    return stored as T;
  } catch {
    return defaultValue;
  }
}

/**
 * Write a setting to localStorage.
 */
function writeSetting(name: string, value: string | number | boolean): void {
  if (typeof window === 'undefined') return;

  try {
    localStorage.setItem(`${SETTING_KEY_PREFIX}${name}`, String(value));
  } catch {
    // localStorage might be full or disabled
  }
}

/**
 * Remove a setting from localStorage.
 */
function removeSetting(name: string): void {
  if (typeof window === 'undefined') return;

  try {
    localStorage.removeItem(`${SETTING_KEY_PREFIX}${name}`);
  } catch {
    // Ignore errors
  }
}

/**
 * Save the complete HouseRules object to localStorage.
 */
function saveHouseRules(state: SettingsState): void {
  if (typeof window === 'undefined') return;

  try {
    // Build a HouseRules-like object for storage
    // Note: goldTiles will be determined by game type at runtime
    const houseRules = {
      goldTiles: state.expansionGoldTiles, // Default to expansion
      wallsProtectCities: true,
      hideBaronBeforeInvasion: false,
      knightMovesBaronBeforeRoll: true,
      hideRobberBeforeInvasion: false,
      knightMovesRobberBeforeRoll: false,
      supplementalMinPlayers: state.supplementalMinPlayers,
      griefDodgy: false, // Deferred - always false for now
    };
    localStorage.setItem(HOUSE_RULES_KEY, JSON.stringify(houseRules));
  } catch {
    // localStorage might be full or disabled
  }
}

// ============================================================================
// Initial state
// ============================================================================

const initialState: SettingsState = {
  expansionGoldTiles: SETTINGS_DEFAULTS.expansionGoldTiles,
  regularGoldTiles: SETTINGS_DEFAULTS.regularGoldTiles,
  supplementalMinPlayers: SETTINGS_DEFAULTS.supplementalMinPlayers,
  animationSpeed: SETTINGS_DEFAULTS.animationSpeed,
  isInitialized: false,
};

// ============================================================================
// Store
// ============================================================================

/**
 * Zustand store for application settings.
 */
export const useSettingsStore = create<SettingsStore>()((set, get) => ({
  ...initialState,

  initialize: () => {
    if (get().isInitialized) return;

    const newState: Partial<SettingsState> = {
      expansionGoldTiles: readSetting('ExpansionGoldTiles', SETTINGS_DEFAULTS.expansionGoldTiles),
      regularGoldTiles: readSetting('RegularGoldTiles', SETTINGS_DEFAULTS.regularGoldTiles),
      supplementalMinPlayers: readSetting(
        'SupplementalMinPlayers',
        SETTINGS_DEFAULTS.supplementalMinPlayers
      ),
      animationSpeed: readSetting(
        'AnimationSpeed',
        SETTINGS_DEFAULTS.animationSpeed
      ) as AnimationSpeed,
      isInitialized: true,
    };

    set(newState);
  },

  setExpansionGoldTiles: (value) => {
    set({ expansionGoldTiles: value });
  },

  setRegularGoldTiles: (value) => {
    set({ regularGoldTiles: value });
  },

  setSupplementalMinPlayers: (value) => {
    set({ supplementalMinPlayers: value });
  },

  setAnimationSpeed: (value) => {
    set({ animationSpeed: value });
  },

  resetToDefaults: () => {
    // Remove all settings from localStorage
    removeSetting('ExpansionGoldTiles');
    removeSetting('RegularGoldTiles');
    removeSetting('SupplementalMinPlayers');
    removeSetting('AnimationSpeed');

    // Reset state to defaults
    set({
      expansionGoldTiles: SETTINGS_DEFAULTS.expansionGoldTiles,
      regularGoldTiles: SETTINGS_DEFAULTS.regularGoldTiles,
      supplementalMinPlayers: SETTINGS_DEFAULTS.supplementalMinPlayers,
      animationSpeed: SETTINGS_DEFAULTS.animationSpeed,
    });

    // Save defaults to HouseRules
    saveHouseRules({
      ...initialState,
      isInitialized: true,
    });
  },

  saveToStorage: () => {
    const state = get();

    // Save individual settings
    writeSetting('ExpansionGoldTiles', state.expansionGoldTiles);
    writeSetting('RegularGoldTiles', state.regularGoldTiles);
    writeSetting('SupplementalMinPlayers', state.supplementalMinPlayers);
    writeSetting('AnimationSpeed', state.animationSpeed);

    // Save HouseRules object
    saveHouseRules(state);
  },

  getHouseRules: (gameType) => {
    // Ensure settings are loaded from localStorage before reading
    const { initialize } = get();
    initialize();

    const state = get();
    const goldTiles = gameType === 'Expansion' ? state.expansionGoldTiles : state.regularGoldTiles;

    return {
      goldTiles,
      wallsProtectCities: true,
      hideBaronBeforeInvasion: false,
      knightMovesBaronBeforeRoll: true,
      hideRobberBeforeInvasion: false,
      knightMovesRobberBeforeRoll: false,
      supplementalMinPlayers: state.supplementalMinPlayers,
      griefDodgy: false, // Deferred - always false for now
    };
  },
}));

// ============================================================================
// Selectors
// ============================================================================

/** Select expansion gold tiles setting */
export const selectExpansionGoldTiles = (state: SettingsStore) => state.expansionGoldTiles;

/** Select regular gold tiles setting */
export const selectRegularGoldTiles = (state: SettingsStore) => state.regularGoldTiles;

/** Select supplemental min players setting */
export const selectSupplementalMinPlayers = (state: SettingsStore) => state.supplementalMinPlayers;

/** Select animation speed setting */
export const selectAnimationSpeed = (state: SettingsStore) => state.animationSpeed;

/** Select whether store is initialized */
export const selectIsInitialized = (state: SettingsStore) => state.isInitialized;
