import {
  action,
  type WillAppearEvent,
  SingletonAction,
} from "@elgato/streamdeck";
import { renderGameState } from "../services/ButtonRenderer";
import type { PluginGameState, PlayerColor } from "../models/types";

/** Game state messages matching the React UI. */
const STATE_MESSAGES: Record<string, string> = {
  WaitingForRoll: "Select Roll...",
  WaitingForNext: "Build or click Next.",
  MustMoveRobber: "Move Robber",
  Supplemental: "Supplemental",
  TooManyCards: "Discard Cards",
  MustDestroyCity: "Destroy City",
  GameOver: "Game Over",
  WaitingForNewGame: "New Game",
  WaitingForPlayers: "Waiting...",
  PickingBoard: "Pick Board",
};

/**
 * Display-only key showing the current game state message.
 * Styled with the current player's colors.
 */
@action({ UUID: "com.catan.streamdeck.game-state" })
export class GameStateAction extends SingletonAction {
  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    await ev.action.setImage(renderGameState("No Game", undefined));
  }

  /** Called from plugin.ts on each GameStateUpdated. */
  async updateState(gameState: PluginGameState): Promise<void> {
    const message =
      STATE_MESSAGES[gameState.gameState] ?? gameState.gameState;
    const colors: PlayerColor | undefined =
      gameState.playerColorMap?.[gameState.currentPlayerId];

    for (const a of this.actions) {
      await a.setImage(renderGameState(message, colors));
    }
  }
}
