import streamDeck, {
  action,
  type KeyDownEvent,
  type WillAppearEvent,
  SingletonAction,
} from "@elgato/streamdeck";
import { renderNavigate, renderBackButton } from "../services/ButtonRenderer";
import type { JsonValue } from "../models/types";

type NavigateSettings = {
  /** Target page: "roll", "build", or "home" (back button). */
  targetPage: string;
  [key: string]: JsonValue;
};

/** Profile names corresponding to target pages. */
const PAGE_PROFILES: Record<string, string> = {
  roll: "profiles/CatanRoll",
  build: "profiles/CatanBuild",
};

/** Display labels for navigation buttons. */
const PAGE_LABELS: Record<string, string> = {
  roll: "🎲 Roll",
  build: "🔨 Build",
};

/** Game states that highlight each nav button. */
const PAGE_HIGHLIGHT_STATES: Record<string, string[]> = {
  roll: ["WaitingForRoll"],
  build: ["WaitingForNext", "Supplemental"],
};

/**
 * Navigation button — switches to a target profile page.
 * Per-instance settings determine which page ("roll", "build", or "home" for back).
 */
@action({ UUID: "com.catan.streamdeck.navigate" })
export class NavigateAction extends SingletonAction<NavigateSettings> {
  private currentGameState: string = "";

  override async onWillAppear(
    ev: WillAppearEvent<NavigateSettings>,
  ): Promise<void> {
    const target = ev.payload.settings?.targetPage ?? "home";
    if (target === "home") {
      await ev.action.setImage(renderBackButton());
    } else {
      const label = PAGE_LABELS[target] ?? target;
      const highlighted = this.isHighlighted(target);
      await ev.action.setImage(renderNavigate(label, highlighted));
    }
  }

  override async onKeyDown(
    ev: KeyDownEvent<NavigateSettings>,
  ): Promise<void> {
    const target = ev.payload.settings?.targetPage ?? "home";
    const deviceId = ev.action.device.id;

    if (target === "home") {
      // Switch back to home profile (undefined = previous profile)
      await streamDeck.profiles.switchToProfile(
        deviceId,
        "profiles/CatanHome",
      );
    } else {
      const profile = PAGE_PROFILES[target];
      if (profile) {
        await streamDeck.profiles.switchToProfile(deviceId, profile);
      }
    }
  }

  /** Called from plugin.ts to update highlight state. */
  async updateGameState(gameState: string): Promise<void> {
    this.currentGameState = gameState;
    for (const a of this.actions) {
      const settings = (a as unknown as { getSettings(): Promise<NavigateSettings> });
      const s = await settings.getSettings();
      const target = s?.targetPage ?? "home";
      if (target !== "home") {
        const label = PAGE_LABELS[target] ?? target;
        const highlighted = this.isHighlighted(target);
        await a.setImage(renderNavigate(label, highlighted));
      }
    }
  }

  private isHighlighted(target: string): boolean {
    const states = PAGE_HIGHLIGHT_STATES[target];
    return states?.includes(this.currentGameState) ?? false;
  }
}
