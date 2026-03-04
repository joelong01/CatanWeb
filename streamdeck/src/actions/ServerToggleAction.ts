import streamDeck, {
  action,
  type KeyDownEvent,
  type WillAppearEvent,
  SingletonAction,
} from "@elgato/streamdeck";
import { renderServerToggle } from "../services/ButtonRenderer";
import { type GlobalSettings, DEFAULT_SETTINGS } from "../models/types";
import { gameConnection } from "../services/gameConnectionInstance";

/**
 * Two-state toggle: Local ↔ Azure.
 * Switches the GameService URL and reconnects.
 */
@action({ UUID: "com.catan.streamdeck.server-toggle" })
export class ServerToggleAction extends SingletonAction {
  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    const settings =
      await streamDeck.settings.getGlobalSettings<GlobalSettings>();
    const isLocal = (settings.activeServer ?? "local") === "local";
    if (ev.action.isKey()) await ev.action.setState(isLocal ? 0 : 1);
    await ev.action.setImage(renderServerToggle(isLocal));
  }

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    const settings =
      await streamDeck.settings.getGlobalSettings<GlobalSettings>();
    const merged = { ...DEFAULT_SETTINGS, ...settings };

    const wasLocal = merged.activeServer === "local";
    const newServer = wasLocal ? "azure" : "local";
    const newUrl = wasLocal ? merged.azureUrl : merged.localUrl;

    await streamDeck.settings.setGlobalSettings<GlobalSettings>({
      ...merged,
      activeServer: newServer,
    });

    if (ev.action.isKey()) await ev.action.setState(newServer === "local" ? 0 : 1);
    await ev.action.setImage(renderServerToggle(newServer === "local"));

    // Reconnect to the new server
    await gameConnection.switchServer(newUrl);
  }
}
