/**
 * Settings type definitions for the Settings page and store.
 */

/** Animation speed options */
export type AnimationSpeed = 'Slow' | 'Normal' | 'Fast' | 'None';

/** Input types supported by settings */
export type SettingInputType = 'checkbox' | 'dropdown' | 'text';

/**
 * Definition of a single setting from settings.json.
 */
export interface SettingDefinition {
  /** Unique identifier for the setting */
  settingName: string;
  /** User-facing label */
  description: string;
  /** Type of input control to render */
  inputType: SettingInputType;
  /** Available options for dropdown type */
  options?: string[];
  /** Current value */
  value: string | number | boolean;
  /** Default value when reset */
  defaultValue: string | number | boolean;
  /** Tooltip text explaining the setting */
  tooltip?: string;
  /** Category for grouping in UI */
  category: string;
}

/**
 * Settings configuration loaded from settings.json.
 */
export interface SettingsConfig {
  settings: SettingDefinition[];
}

/** Default values for all settings */
export const SETTINGS_DEFAULTS = {
  expansionGoldTiles: 2,
  regularGoldTiles: 1,
  supplementalMinPlayers: 5,
  animationSpeed: 'Normal' as AnimationSpeed,
} as const;

/** localStorage key prefix for individual settings */
export const SETTING_KEY_PREFIX = 'setting_';

/** localStorage key for serialized HouseRules */
export const HOUSE_RULES_KEY = 'HouseRules';
