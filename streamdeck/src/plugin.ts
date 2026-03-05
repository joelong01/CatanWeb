// Debug: capture crash info before anything else runs
// @ts-expect-error - node:fs types resolved at runtime
import { writeFileSync as _ws, appendFileSync as _as } from "fs";
const crashLog = "/tmp/sd-catan-crash.log";
_ws(crashLog, `STARTED: ${new Date().toISOString()}\n`);
process.on("uncaughtException", (err: Error) => {
  _as(crashLog, `UNCAUGHT: ${err.stack || err}\n`);
  process.exit(1);
});
process.on("unhandledRejection", (reason: unknown) => {
  const msg = reason instanceof Error ? reason.stack : String(reason);
  _as(crashLog, `UNHANDLED REJECTION: ${msg}\n`);
  process.exit(1);
});

import streamDeck from "@elgato/streamdeck";
import { gameConnection } from "./services/gameConnectionInstance";
import { type GlobalSettings, DEFAULT_SETTINGS } from "./models/types";
import type { PluginGameState } from "./models/types";

// Actions
import { CatanLauncher } from "./actions/CatanLauncher";
import { ServerToggleAction } from "./actions/ServerToggleAction";
import { GameStateAction } from "./actions/GameStateAction";
import { AutoNavigateAction } from "./actions/AutoNavigateAction";
import { NavigateAction } from "./actions/NavigateAction";
import { RollAction } from "./actions/RollAction";
import { SoldierAction } from "./actions/SoldierAction";
import { UndoAction } from "./actions/UndoAction";
import { RedoAction } from "./actions/RedoAction";

// ── Action instances (needed for broadcasting state updates) ─────────

const gameStateAction = new GameStateAction();
const navigateAction = new NavigateAction();
const rollAction = new RollAction();
const soldierAction = new SoldierAction();
const undoAction = new UndoAction();
const redoAction = new RedoAction();

// ── Register all actions ─────────────────────────────────────────────

streamDeck.actions.registerAction(new CatanLauncher());
streamDeck.actions.registerAction(new ServerToggleAction());
streamDeck.actions.registerAction(gameStateAction);
streamDeck.actions.registerAction(new AutoNavigateAction());
streamDeck.actions.registerAction(navigateAction as unknown as SingletonAction);
streamDeck.actions.registerAction(rollAction as unknown as SingletonAction);
streamDeck.actions.registerAction(soldierAction);
streamDeck.actions.registerAction(undoAction);
streamDeck.actions.registerAction(redoAction);

// Need the base type for registerAction casts
import { SingletonAction } from "@elgato/streamdeck";

// ── Game state update handler ────────────────────────────────────────

gameConnection.onGameStateUpdated = async (
  gameState: PluginGameState,
) => {
  // Broadcast to all action instances
  await Promise.all([
    gameStateAction.updateState(gameState),
    navigateAction.updateGameState(gameState.gameState),
    rollAction.updateState(gameState),
    soldierAction.updateState(gameState),
    undoAction.updateState(gameState),
    redoAction.updateState(gameState),
  ]);

  // Auto-navigate if enabled
  await handleAutoNavigate(gameState.gameState);
};

gameConnection.onConnectionStateChanged = (state) => {
  streamDeck.logger.info(`GameService connection: ${state}`);
};

// ── Auto-navigate logic ──────────────────────────────────────────────

async function handleAutoNavigate(gameState: string): Promise<void> {
  const settings =
    await streamDeck.settings.getGlobalSettings<GlobalSettings>();
  if (!settings.autoNavigate) return;

  // Find first connected device
  const devices = Array.from(streamDeck.devices);
  if (devices.length === 0) return;
  const deviceId = devices[0].id;

  switch (gameState) {
    case "WaitingForRoll":
      await streamDeck.profiles.switchToProfile(
        deviceId,
        "profiles/CatanRoll",
      );
      break;
    case "WaitingForNext":
      // Phase 2: switch to CatanBuild
      // For now, stay on home or current page
      break;
    default:
      await streamDeck.profiles.switchToProfile(
        deviceId,
        "profiles/CatanHome",
      );
      break;
  }
}

// ── Initialize connection on startup ─────────────────────────────────

async function initialize(): Promise<void> {
  const settings =
    await streamDeck.settings.getGlobalSettings<GlobalSettings>();
  const merged = { ...DEFAULT_SETTINGS, ...settings };

  // Set the correct server URL
  const url =
    merged.activeServer === "azure"
      ? (merged.azureUrl as string)
      : (merged.localUrl as string);
  gameConnection.switchServer(url);

  if (merged.playerId) {
    gameConnection.setPlayerId(merged.playerId as string);
  }

  // Connect (retries indefinitely in background)
  gameConnection.connect().then(() => {
    if (merged.gameId) {
      gameConnection.joinGame(merged.gameId as string);
    }
  });
}

// Listen for settings changes (from Property Inspector)
streamDeck.settings.onDidReceiveGlobalSettings<GlobalSettings>(
  async (settings) => {
    const merged = { ...DEFAULT_SETTINGS, ...settings };
    const url =
      merged.activeServer === "azure"
        ? (merged.azureUrl as string)
        : (merged.localUrl as string);

    gameConnection.setPlayerId(merged.playerId as string);
    await gameConnection.switchServer(url);

    if (merged.gameId) {
      await gameConnection.joinGame(merged.gameId as string);
    }
  },
);

// ── Connect to Stream Deck ───────────────────────────────────────────

streamDeck.connect().then(initialize);
