import {
  action,
  type KeyDownEvent,
  type WillAppearEvent,
  SingletonAction,
} from "@elgato/streamdeck";
import { renderRollButton } from "../services/ButtonRenderer";
import {
  type PluginGameState,
  type PlayerColor,
  type JsonValue,
  ROLL_TO_DICE,
} from "../models/types";
import { gameConnection } from "../services/gameConnectionInstance";

type RollSettings = {
  rollNumber: number;
  [key: string]: JsonValue;
};

/**
 * Roll button (one instance per number 2–12).
 * Shows roll stats, sends RollMessage on click.
 */
@action({ UUID: "com.catan.streamdeck.roll" })
export class RollAction extends SingletonAction<RollSettings> {
  private lastState: PluginGameState | null = null;

  override async onWillAppear(
    ev: WillAppearEvent<RollSettings>,
  ): Promise<void> {
    const roll = ev.payload.settings?.rollNumber ?? 7;
    const isEnabled = this.lastState?.gameState === "WaitingForRoll";
    const colors = this.getColors();
    const stats = this.lastState?.rollStats?.[roll];

    await ev.action.setImage(
      renderRollButton(
        roll,
        stats?.count ?? 0,
        stats?.percentage ?? 0,
        colors,
        isEnabled,
      ),
    );
  }

  override async onKeyDown(
    ev: KeyDownEvent<RollSettings>,
  ): Promise<void> {
    const roll = ev.payload.settings?.rollNumber;
    if (!roll) return;

    // Only allow rolls during WaitingForRoll
    if (this.lastState?.gameState !== "WaitingForRoll") return;

    const dice = ROLL_TO_DICE[roll];
    if (!dice) return;

    await gameConnection.sendRoll(dice[0], dice[1]);
  }

  /** Called from plugin.ts on each GameStateUpdated. */
  async updateState(gameState: PluginGameState): Promise<void> {
    this.lastState = gameState;
    const isEnabled = gameState.gameState === "WaitingForRoll";
    const colors = this.getColors();

    for (const a of this.actions) {
      const settings = (a as unknown as { getSettings(): Promise<RollSettings> });
      const s = await settings.getSettings();
      const roll = s?.rollNumber ?? 7;
      const stats = gameState.rollStats?.[roll];

      await a.setImage(
        renderRollButton(
          roll,
          stats?.count ?? 0,
          stats?.percentage ?? 0,
          colors,
          isEnabled,
        ),
      );
    }
  }

  private getColors(): PlayerColor | undefined {
    if (!this.lastState) return undefined;
    return this.lastState.playerColorMap?.[this.lastState.currentPlayerId];
  }
}
