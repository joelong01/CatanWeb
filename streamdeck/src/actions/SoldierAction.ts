import {
  action,
  type KeyDownEvent,
  type WillAppearEvent,
  SingletonAction,
} from "@elgato/streamdeck";
import { renderSoldierButton } from "../services/ButtonRenderer";
import type { PluginGameState, PlayerColor } from "../models/types";
import { gameConnection } from "../services/gameConnectionInstance";

/**
 * Deploy soldier button. Enabled when the current player has unspent soldier entitlements.
 */
@action({ UUID: "com.catan.streamdeck.soldier" })
export class SoldierAction extends SingletonAction {
  private lastState: PluginGameState | null = null;

  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    const count = this.lastState?.currentPlayerSoldierCount ?? 0;
    const colors = this.getColors();
    await ev.action.setImage(renderSoldierButton(count, colors, count > 0));
  }

  override async onKeyDown(_ev: KeyDownEvent): Promise<void> {
    const count = this.lastState?.currentPlayerSoldierCount ?? 0;
    if (count <= 0) return;
    await gameConnection.sendPurchase("Soldier");
  }

  /** Called from plugin.ts on each GameStateUpdated. */
  async updateState(gameState: PluginGameState): Promise<void> {
    this.lastState = gameState;
    const count = gameState.currentPlayerSoldierCount ?? 0;
    const colors = this.getColors();

    for (const a of this.actions) {
      await a.setImage(renderSoldierButton(count, colors, count > 0));
    }
  }

  private getColors(): PlayerColor | undefined {
    if (!this.lastState) return undefined;
    return this.lastState.playerColorMap?.[this.lastState.currentPlayerId];
  }
}
