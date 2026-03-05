import streamDeck, {
  action,
  type KeyDownEvent,
  SingletonAction,
} from "@elgato/streamdeck";

/**
 * Placed on the user's main Stream Deck home profile.
 * Pressing it switches to the Catan Home profile.
 */
@action({ UUID: "com.catan.streamdeck.launcher" })
export class CatanLauncher extends SingletonAction {
  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    await streamDeck.profiles.switchToProfile(
      ev.action.device.id,
      "profiles/CatanHome",
    );
  }
}
