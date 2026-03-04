/**
 * Minimal TypeScript types for the subset of GameModel the Stream Deck plugin needs.
 * These mirror the C# models in Catan3.Shared but only include fields we read.
 */

import type { JsonValue } from "@elgato/utils";
export type { JsonValue };

export interface ActionFlags {
  undoEnabled: boolean;
  redoEnabled: boolean;
  nextEnabled: boolean;
  rollsEnabled: boolean;
}

export interface RollStats {
  count: number;
  percentage: number;
}

export interface PlayerColor {
  primary: string;
  secondary: string;
  foreground: string;
}

export interface EntitlementPurchaseInfo {
  enabled: boolean;
}

/**
 * The subset of GameModel we extract from GameStateUpdated events.
 * The full GameModel is much larger — we only read what's needed for button rendering.
 */
export interface PluginGameState {
  gameState: string;
  currentPlayerId: string;
  actionFlags: ActionFlags;
  rollStats: Record<number, RollStats>;
  playerColorMap: Record<string, PlayerColor>;
  entitlementPurchaseModel: Record<string, EntitlementPurchaseInfo>;
  currentPlayerSoldierCount: number;
}

export type GlobalSettings = {
  localUrl: string;
  azureUrl: string;
  activeServer: "local" | "azure";
  playerId: string;
  gameId: string;
  autoNavigate: boolean;
  [key: string]: JsonValue;
};

export const DEFAULT_SETTINGS: GlobalSettings = {
  localUrl: "http://localhost:8080",
  azureUrl: "https://catan-api.azurewebsites.net",
  activeServer: "local",
  playerId: "",
  gameId: "",
  autoNavigate: true,
};

/** Roll number → (die1, die2) mapping. Uses first valid combination. */
export const ROLL_TO_DICE: Record<number, [number, number]> = {
  2: [1, 1],
  3: [1, 2],
  4: [2, 2],
  5: [2, 3],
  6: [3, 3],
  7: [3, 4],
  8: [4, 4],
  9: [4, 5],
  10: [5, 5],
  11: [5, 6],
  12: [6, 6],
};
