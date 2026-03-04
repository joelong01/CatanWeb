import {
  action,
  type KeyDownEvent,
  type WillAppearEvent,
  SingletonAction,
} from "@elgato/streamdeck";
import { renderRedoButton } from "../services/ButtonRenderer";
import type { PluginGameState } from "../models/types";
import { gameConnection } from "../services/gameConnectionInstance";

/**
 * Redo button. Enabled when ActionFlags.redoEnabled is true.
 */
@action({ UUID: "com.catan.streamdeck.redo" })
export class RedoAction extends SingletonAction {
  private isEnabled = false;

  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    await ev.action.setImage(renderRedoButton(this.isEnabled));
  }

  override async onKeyDown(_ev: KeyDownEvent): Promise<void> {
    if (!this.isEnabled) return;
    await gameConnection.sendRedo();
  }

  /** Called from plugin.ts on each GameStateUpdated. */
  async updateState(gameState: PluginGameState): Promise<void> {
    this.isEnabled = gameState.actionFlags?.redoEnabled ?? false;
    for (const a of this.actions) {
      await a.setImage(renderRedoButton(this.isEnabled));
    }
  }
}
