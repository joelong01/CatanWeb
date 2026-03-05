import streamDeck, {
  action,
  type KeyDownEvent,
  type WillAppearEvent,
  SingletonAction,
} from "@elgato/streamdeck";
import { renderAutoToggle } from "../services/ButtonRenderer";
import { type GlobalSettings, DEFAULT_SETTINGS } from "../models/types";

/**
 * Two-state toggle: Auto ↔ Manual page navigation.
 * When on, the plugin auto-switches profiles based on GameState.
 */
@action({ UUID: "com.catan.streamdeck.auto-navigate" })
export class AutoNavigateAction extends SingletonAction {
  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    const settings =
      await streamDeck.settings.getGlobalSettings<GlobalSettings>();
    const isOn = settings.autoNavigate ?? DEFAULT_SETTINGS.autoNavigate;
    if (ev.action.isKey()) await ev.action.setState(isOn ? 0 : 1);
    await ev.action.setImage(renderAutoToggle(isOn));
  }

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    const settings =
      await streamDeck.settings.getGlobalSettings<GlobalSettings>();
    const merged = { ...DEFAULT_SETTINGS, ...settings };

    const newValue = !merged.autoNavigate;
    await streamDeck.settings.setGlobalSettings<GlobalSettings>({
      ...merged,
      autoNavigate: newValue,
    });

    if (ev.action.isKey()) await ev.action.setState(newValue ? 0 : 1);
    await ev.action.setImage(renderAutoToggle(newValue));
  }
}
