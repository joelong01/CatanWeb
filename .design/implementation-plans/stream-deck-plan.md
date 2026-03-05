# Stream Deck Plugin - Phase 1 Implementation Plan (V1 — Superseded)

> **Status:** SUPERSEDED. This plan describes the V1 "peer client" architecture where the plugin
> connects directly to GameService via SignalR + REST. This architecture was implemented but has
> fundamental game-discovery problems (see `.design/stream-deck.md` § "Why V1 Is Broken").
>
> **V2 plan:** Not yet written. The V2 architecture (browser relay via WebSocket) is documented
> in `.design/stream-deck.md` § "V2: Browser Relay Architecture". When resuming work, write a
> new implementation plan at `.design/implementation-plans/stream-deck-v2-plan.md`.
>
> **What to keep from V1:** build pipeline (`catan.ps1 streamdeck build/pack`), profile ZIP
> generation (`scripts/build-profiles.mjs`), ButtonRenderer.ts, action UI logic (RollAction,
> UndoAction, etc.), manifest.json structure, SVG assets, Settings page download component,
> CORS + MIME type fixes in Program.cs, versioned download pipeline.
>
> **What to discard:** GameConnection.ts, NativeFetchHttpClient.ts, gameConnectionInstance.ts,
> ServerToggleAction.ts, all SignalR/REST communication code, global settings for server
> URL/playerId/gameId.

## Goal (V1)

Implement the Stream Deck plugin with a Home page (server toggle, game state display,
auto-navigate toggle, navigation, undo/redo) and a Rolls page (dice buttons 2–12, soldier,
undo, back) that connects to GameService via SignalR and sends commands via REST API.

## Changes

### 1. Scaffold Plugin Project

Create `streamdeck/` directory in the repo root with the Elgato SDK project structure.

**`streamdeck/package.json`** — Dependencies:

- `@elgato/streamdeck` (SDK)
- `@microsoft/signalr` (SignalR client)
- Dev: `@elgato/cli`, `rollup`, `@rollup/plugin-typescript`, `@rollup/plugin-node-resolve`,
  `@rollup/plugin-commonjs`, `@rollup/plugin-terser`, `@tsconfig/node20`, `typescript`

**`streamdeck/tsconfig.json`** — Extends `@tsconfig/node20`, ES2022 modules, bundler resolution.

**`streamdeck/rollup.config.mjs`** — Bundles `src/plugin.ts` →
`com.catan.streamdeck.sdPlugin/bin/plugin.js`. Includes node-resolve (preferBuiltins: true),
commonjs, typescript, and terser plugins. Emits `{ "type": "module" }` package.json.

### 2. Plugin Manifest

**`streamdeck/com.catan.streamdeck.sdPlugin/manifest.json`**

```json
{
  "$schema": "https://schemas.elgato.com/streamdeck/plugins/manifest.json",
  "Name": "Catan",
  "Version": "1.0.0.0",
  "Author": "Catan Team",
  "UUID": "com.catan.streamdeck",
  "Description": "Control Catan game from your Stream Deck.",
  "Icon": "imgs/plugin/catan-icon",
  "CategoryIcon": "imgs/plugin/category-icon",
  "Category": "Catan",
  "CodePath": "bin/plugin.js",
  "SDKVersion": 2,
  "Software": { "MinimumVersion": "6.7" },
  "OS": [
    { "Platform": "mac", "MinimumVersion": "13" },
    { "Platform": "windows", "MinimumVersion": "10" }
  ],
  "Nodejs": { "Version": "20", "Debug": "enabled" },
  "Actions": [ ... ],
  "Profiles": [
    { "Name": "profiles/CatanHome", "DeviceType": 0,
      "Readonly": false, "DontAutoSwitchWhenInstalled": true, "AutoInstall": true },
    { "Name": "profiles/CatanRoll", "DeviceType": 0,
      "Readonly": false, "DontAutoSwitchWhenInstalled": true, "AutoInstall": true }
  ]
}
```

Actions array entries (9 actions):

| UUID suffix | Name | States | DisableAutomaticStates |
|---|---|---|---|
| `.server-toggle` | Server | 2 (Local/Azure) | true |
| `.game-state` | Game State | 1 | n/a |
| `.auto-navigate` | Auto Navigate | 2 (On/Off) | true |
| `.navigate` | Navigate | 1 | n/a |
| `.roll` | Roll | 1 | n/a |
| `.soldier` | Soldier | 1 | n/a |
| `.undo` | Undo | 1 | n/a |
| `.redo` | Redo | 1 | n/a |
| `.launcher` | Catan | 1 | n/a |

### 3. GameConnection Service

**`streamdeck/src/services/GameConnection.ts`**

Singleton service managing SignalR connection and REST commands.

- **Constructor:** Takes `serviceUrl`, `playerId`
- **`connect()`:** Builds `HubConnection` with `withAutomaticReconnect` (exponential backoff,
  cap at 30s). Retries initial connection indefinitely. Registers `GameStateUpdated` handler.
- **`disconnect()`:** Stops connection, clears state.
- **`switchServer(url)`:** Disconnects, updates URL, reconnects.
- **`joinGame(gameId)`:** Invokes `JoinGame` on hub. Stores gameId for re-join on reconnect.
- **`onreconnected`:** Re-invokes `JoinGame` to rejoin the SignalR group.
- **`sendCommand(messageType, messageJson)`:** POST to
  `/api/game/{gameId}/command` with `{ messageType, messageJson, playerId }`.
- **Helper methods:**
  - `sendRoll(die1, die2)` — sends `RollMessage`
  - `sendNext()` — sends `NextMessage`
  - `sendUndo()` — sends `UndoMessage`
  - `sendRedo()` — sends `RedoMessage`
  - `sendPurchase(entitlement)` — sends `PurchaseMessage`
- **Events (callbacks):**
  - `onGameStateUpdated: (gameModel) => void`
  - `onConnectionStateChanged: (state) => void`

Reference: `react-ui/lib/services/GameServiceProxy.ts` for the existing client-side patterns.

### 4. ButtonRenderer Service

**`streamdeck/src/services/ButtonRenderer.ts`**

Pure functions that return SVG data-URL strings for `setImage()`.

- **`renderRollButton(roll, count, percentage, colors, isEnabled)`** — 144×144 SVG with:
  - Rounded rect background with player gradient
  - Large centered number
  - Small "count / pct%" text below
  - Red text for 6 and 8, black for others
  - Dim opacity when disabled
- **`renderSoldierButton(unspentCount, colors, isEnabled)`** — Knight icon + count badge
- **`renderUndoButton(isEnabled)`** — Rotate-left arrow icon
- **`renderRedoButton(isEnabled)`** — Rotate-right arrow icon
- **`renderServerToggle(isLocal)`** — House icon for local, cloud icon for Azure
- **`renderAutoToggle(isOn)`** — Toggle icon, green when on, gray when off
- **`renderGameState(message, colors)`** — State message text with player color background
- **`renderNavigate(label, icon, isHighlighted)`** — Nav button with optional glow
- **`renderBackButton()`** — Left arrow icon

All SVGs use inline styles (no external CSS). Returns `data:image/svg+xml,${encoded}`.

### 5. Types

**`streamdeck/src/models/types.ts`**

Minimal TypeScript types needed from the GameModel (not a full copy — just what the plugin
reads):

```typescript
interface PluginGameState {
  gameState: string;
  currentPlayerId: string;
  actionFlags: { undoEnabled: boolean; redoEnabled: boolean;
                 nextEnabled: boolean; rollsEnabled: boolean };
  rollStats: Record<number, { count: number; percentage: number }>;
  playerColorMap: Record<string, { primary: string; secondary: string;
                                    foreground: string }>;
  entitlementPurchaseModel: Record<string, { enabled: boolean }>;
  currentPlayerSoldierCount: number;
}

interface GlobalSettings {
  localUrl: string;
  azureUrl: string;
  activeServer: 'local' | 'azure';
  playerId: string;
  gameId: string;
  autoNavigate: boolean;
}
```

### 6. Plugin Entry Point

**`streamdeck/src/plugin.ts`**

- Import and register all actions
- Create singleton `GameConnection` instance
- Read `globalSettings` on startup to get server URL and preferences
- Call `streamDeck.connect()`
- On `GameStateUpdated`, broadcast to all action instances via a shared event emitter

### 7. Action Implementations

Each action extends `SingletonAction` and registers with `@action` decorator.

#### `streamdeck/src/actions/ServerToggleAction.ts`

- Two-state key with `DisableAutomaticStates: true`
- `onKeyDown`: Read current state → flip → update `globalSettings.activeServer` →
  call `GameConnection.switchServer()` → update image via `ButtonRenderer.renderServerToggle()`
- `onWillAppear`: Set image from current `globalSettings.activeServer`

#### `streamdeck/src/actions/GameStateAction.ts`

- Display-only (no click handler)
- `onWillAppear`: Render current state message
- Listens to `GameStateUpdated` → re-renders with new state message and player color

#### `streamdeck/src/actions/AutoNavigateAction.ts`

- Two-state key with `DisableAutomaticStates: true`
- `onKeyDown`: Toggle `globalSettings.autoNavigate`, update image
- `onWillAppear`: Set image from current setting

#### `streamdeck/src/actions/NavigateAction.ts`

- Per-instance settings include `targetPage: 'roll' | 'build'`
- `onKeyDown`: Call `streamDeck.profiles.switchToProfile(deviceId, profileName)`
- `onWillAppear`: Render button, check if current game state matches this page (highlighted)
- Listens to `GameStateUpdated` → update highlight state

#### `streamdeck/src/actions/RollAction.ts`

- Per-instance settings include `rollNumber: number` (2–12)
- `onKeyDown`: Convert roll number to (die1, die2) pair →
  `GameConnection.sendRoll(die1, die2)`
- `onWillAppear`: Render roll button with current stats
- Listens to `GameStateUpdated` → re-render with updated count/percentage
- Disabled (dim) when `gameState !== 'WaitingForRoll'`

#### `streamdeck/src/actions/SoldierAction.ts`

- `onKeyDown`: `GameConnection.sendPurchase('Soldier')`
- `onWillAppear`: Render with current unspent count
- Enabled when current player has unspent soldier entitlements

#### `streamdeck/src/actions/UndoAction.ts`

- `onKeyDown`: `GameConnection.sendUndo()`
- `onWillAppear`: Render enabled/disabled from `ActionFlags.undoEnabled`

#### `streamdeck/src/actions/RedoAction.ts`

- `onKeyDown`: `GameConnection.sendRedo()`
- `onWillAppear`: Render enabled/disabled from `ActionFlags.redoEnabled`

#### `streamdeck/src/actions/CatanLauncher.ts`

- For the user's main Stream Deck home profile
- `onKeyDown`: `streamDeck.profiles.switchToProfile(deviceId, 'profiles/CatanHome')`
- Static Catan icon image

### 8. Profile Files

Create `.streamDeckProfile` files by hand (JSON format). These pre-assign actions to key
positions.

**`streamdeck/com.catan.streamdeck.sdPlugin/profiles/CatanHome.streamDeckProfile`**

15-key layout:

| Position | Action UUID |
|---|---|
| 0 (row1 col1) | `.server-toggle` |
| 1 (row1 col2) | `.game-state` |
| 2 (row1 col3) | `.auto-navigate` |
| 5 (row2 col1) | `.navigate` (settings: targetPage=roll) |
| 6 (row2 col2) | `.navigate` (settings: targetPage=build) |
| 10 (row3 col1) | `.undo` |
| 11 (row3 col2) | `.redo` |

**`streamdeck/com.catan.streamdeck.sdPlugin/profiles/CatanRoll.streamDeckProfile`**

15-key layout:

| Position | Action UUID | Settings |
|---|---|---|
| 0–3 (row1 cols 1–4) | `.roll` | rollNumber: 2, 3, 4, 5 |
| 4 (row1 col5) | `.navigate` | targetPage=home (back) |
| 5–8 (row2 cols 1–4) | `.roll` | rollNumber: 6, 7, 8, 9 |
| 9 (row2 col5) | `.roll` | rollNumber: 10 |
| 10–11 (row3 cols 1–2) | `.roll` | rollNumber: 11, 12 |
| 12 (row3 col3) | `.soldier` | |
| 13 (row3 col4) | `.undo` | |

### 9. Static Assets

**`streamdeck/com.catan.streamdeck.sdPlugin/imgs/`**

Placeholder SVG icons for:

- `plugin/catan-icon.svg` — Catan logo (256×256)
- `plugin/category-icon.svg` — Category sidebar icon
- `actions/*/icon.svg` — Action list icons (one per action)
- `actions/*/key.svg` — Default key images (144×144)

These are static defaults; the plugin overrides them at runtime with dynamic SVGs.

### 10. Auto-Navigate Logic

In `plugin.ts`, on each `GameStateUpdated`:

```typescript
if (globalSettings.autoNavigate) {
  if (gameState === 'WaitingForRoll') {
    switchToProfile(deviceId, 'profiles/CatanRoll');
  } else if (gameState === 'WaitingForNext') {
    // Phase 2: switchToProfile(deviceId, 'profiles/CatanBuild');
    switchToProfile(deviceId, 'profiles/CatanHome');
  } else {
    switchToProfile(deviceId, 'profiles/CatanHome');
  }
}
```

### 11. Build Integration

**Update `catan.ps1`** — Add `streamdeck` verb:

```powershell
"streamdeck" {
    switch ($SubVerb) {
        "build"   { Push-Location streamdeck; npm run build; Pop-Location }
        "watch"   { Push-Location streamdeck; npm run watch; Pop-Location }
        "pack"    { Push-Location streamdeck; streamdeck pack ...; Pop-Location }
        "link"    { Push-Location streamdeck; streamdeck link ...; Pop-Location }
        default   { # show help }
    }
}
```

## Files Modified

| File | Action |
|---|---|
| `streamdeck/package.json` | Create |
| `streamdeck/tsconfig.json` | Create |
| `streamdeck/rollup.config.mjs` | Create |
| `streamdeck/com.catan.streamdeck.sdPlugin/manifest.json` | Create |
| `streamdeck/src/plugin.ts` | Create |
| `streamdeck/src/models/types.ts` | Create |
| `streamdeck/src/services/GameConnection.ts` | Create |
| `streamdeck/src/services/ButtonRenderer.ts` | Create |
| `streamdeck/src/actions/ServerToggleAction.ts` | Create |
| `streamdeck/src/actions/GameStateAction.ts` | Create |
| `streamdeck/src/actions/AutoNavigateAction.ts` | Create |
| `streamdeck/src/actions/NavigateAction.ts` | Create |
| `streamdeck/src/actions/RollAction.ts` | Create |
| `streamdeck/src/actions/SoldierAction.ts` | Create |
| `streamdeck/src/actions/UndoAction.ts` | Create |
| `streamdeck/src/actions/RedoAction.ts` | Create |
| `streamdeck/src/actions/CatanLauncher.ts` | Create |
| `streamdeck/com.catan.streamdeck.sdPlugin/profiles/CatanHome.streamDeckProfile` | Create |
| `streamdeck/com.catan.streamdeck.sdPlugin/profiles/CatanRoll.streamDeckProfile` | Create |
| `streamdeck/com.catan.streamdeck.sdPlugin/imgs/` (multiple) | Create |
| `catan.ps1` | Modify (add streamdeck verb) |
| `.gitignore` | Modify (add streamdeck build artifacts) |

## Verification

1. **Build:** `cd streamdeck && npm install && npm run build` — should produce
   `com.catan.streamdeck.sdPlugin/bin/plugin.js`
2. **Link:** `streamdeck link com.catan.streamdeck.sdPlugin` — symlinks into Stream Deck
3. **Sideload test:** Open Stream Deck app → Catan category appears → drag Catan Launcher
   to home → click → Catan Home profile loads
4. **Connection test:** Start GameService (`./catan.ps1 run`) → plugin connects via SignalR →
   State key shows game state
5. **Roll test:** Navigate to Rolls page → click a number → game receives roll → state
   updates → buttons refresh
6. **Server toggle:** Click Local/Azure toggle → plugin reconnects to other server
7. **Auto-navigate:** Start game → reach WaitingForRoll → Stream Deck auto-switches to
   Rolls page
8. **Pack:** `streamdeck pack com.catan.streamdeck.sdPlugin` → produces installable file
