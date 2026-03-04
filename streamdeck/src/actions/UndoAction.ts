import {
  action,
  type KeyDownEvent,
  type WillAppearEvent,
  SingletonAction,
} from "@elgato/streamdeck";
import { renderUndoButton } from "../services/ButtonRenderer";
import type { PluginGameState } from "../models/types";
import { gameConnection } from "../services/gameConnectionInstance";

/**
 * Undo button. Enabled when ActionFlags.undoEnabled is true.
 */
@action({ UUID: "com.catan.streamdeck.undo" })
export class UndoAction extends SingletonAction {
  private isEnabled = false;

  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    await ev.action.setImage(renderUndoButton(this.isEnabled));
  }

  override async onKeyDown(_ev: KeyDownEvent): Promise<void> {
    if (!this.isEnabled) return;
    await gameConnection.sendUndo();
  }

  /** Called from plugin.ts on each GameStateUpdated. */
  async updateState(gameState: PluginGameState): Promise<void> {
    this.isEnabled = gameState.actionFlags?.undoEnabled ?? false;
    for (const a of this.actions) {
      await a.setImage(renderUndoButton(this.isEnabled));
    }
  }
}
