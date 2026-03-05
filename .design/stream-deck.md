# Stream Deck Integration

## Overview

A Stream Deck plugin that mirrors the Catan game controls, allowing players to execute game
actions (roll dice, deploy soldier, purchase entitlements, advance turn) from physical buttons
instead of the browser UI. The plugin connects to GameService via SignalR to receive real-time
state updates and sends commands via the REST API.

## Architecture

```text
┌──────────────┐   SignalR (GameStateUpdated)   ┌──────────────────┐
│  GameService │ ─────────────────────────────→ │  Stream Deck     │
│  (ASP.NET)   │                                │  Plugin (Node.js)│
│              │ ←───────────────────────────── │                  │
│  REST API    │   POST /api/game/{id}/command  │  @elgato/sdk     │
└──────────────┘                                └──────────────────┘
       ↑ ↓                                             ↑ ↓
   SignalR + REST                               WebSocket (local)
       ↑ ↓                                             ↑ ↓
┌──────────────┐                                ┌──────────────────┐
│  React UI    │                                │  Stream Deck App │
│  (Browser)   │                                │  (Elgato)        │
└──────────────┘                                └──────────────────┘
```

**Key principle:** The Stream Deck plugin is a peer client alongside the React UI. Both receive
the same `GameStateUpdated` broadcasts and both send commands through the same REST API. The
plugin does not proxy through the browser — it connects directly to GameService.

### Plugin Technology

- **Runtime:** Node.js (v20+) via Elgato Stream Deck SDK
- **SDK:** `@elgato/streamdeck` npm package
- **SignalR client:** `@microsoft/signalr` npm package
- **Button images:** Dynamically generated SVGs (144×144px)
- **Distribution:** `.streamDeckPlugin` package (double-click to install)

### Connection Flow

1. User installs plugin (double-click `.streamDeckPlugin` file)
2. Plugin appears in Stream Deck action list under "Catan" category
3. User drags "Catan Game" action to a key on their home profile
4. Clicking the Catan key opens the Catan page (profile switch)
5. Plugin reads GameService URL from global settings (default: `http://localhost:8080`)
6. Plugin connects to SignalR hub at `/gamehub`
7. Plugin calls `JoinGame(gameId, playerId)` to subscribe to updates
8. On each `GameStateUpdated`, plugin updates button images and enabled states

### Configuration

The plugin Property Inspector (HTML settings panel) allows:

- **Local URL** — defaults to `http://localhost:8080`
- **Azure URL** — defaults to `https://catan-api.azurewebsites.net`
- **Player ID** — which player this Stream Deck controls
- **Game ID** — auto-populated from active game, or manually entered

These are stored via `setGlobalSettings()` and persist across sessions.

### Server Toggle (Local / Azure)

The Catan Stream Deck page includes a **server toggle button** in a fixed position (e.g., top-right
key). This is a two-state key:

- **State 0 — Local:** Icon shows a house/laptop glyph, label "Local". Plugin connects to
  `http://localhost:8080`.
- **State 1 — Azure:** Icon shows a cloud glyph, label "Azure". Plugin connects to
  `https://catan-api.azurewebsites.net`.

Pressing the toggle:

1. Disconnects the current SignalR connection
2. Flips to the other state
3. Reconnects to the new URL
4. Refreshes all buttons from the new GameService

The active server is stored in `globalSettings` so it persists across restarts. All layouts
(WaitingForRoll, WaitingForNext, etc.) include this toggle in the same key position so it is
always accessible.

## Page Navigation

The plugin uses a **home page + sub-pages** model. The home page is the hub for navigation,
server selection, and game status. Sub-pages provide the actual game controls.

### Home Page

The home page is always accessible and contains:

```text
Row 1:  [Svr ]  [State]  [Auto]  [    ]  [    ]
Row 2:  [Roll]  [Build]  [    ]  [    ]  [    ]
Row 3:  [Undo]  [Redo ]  [    ]  [    ]  [    ]
```

- **Server toggle (Svr):** Two-state key — "Local" (house icon) or "Azure" (cloud icon).
  Pressing disconnects and reconnects to the other GameService.
- **State:** Display-only key showing the current `GameState` message (e.g., "Select Roll...",
  "Build or click Next."). Styled with current player's color. Not clickable.
- **Auto-navigate toggle (Auto):** Two-state key. When **on**, the plugin automatically
  switches to the relevant page on each `GameStateUpdated` — Rolls page for `WaitingForRoll`,
  Build page for `WaitingForNext`, home page for non-actionable states. When **off**, the
  player navigates manually. Persisted in `globalSettings`. Default: on.
- **Roll page (Roll):** Navigates to the Rolls page. **Highlighted/glowing** when the game is
  in `WaitingForRoll` to indicate this is the active page. Dice icon.
- **Build page (Build):** Navigates to the Build page. **Highlighted** when in
  `WaitingForNext`. Hammer/building icon.
- **Undo/Redo:** Always available from home. Enabled per `ActionFlags`.

The home page buttons update dynamically — when game state changes, the relevant navigation
button highlights to show which page is needed. The State key updates its label and color in
real time.

### Rolls Page (Phase 1 — implement first)

Navigated from the home page Roll button. Shows 11 roll buttons plus soldier and back.

```text
Row 1:  [ 2 ]  [ 3 ]  [ 4 ]  [ 5 ]  [Back]
Row 2:  [ 6 ]  [ 7 ]  [ 8 ]  [ 9 ]  [10  ]
Row 3:  [11 ]  [12 ]  [Sol]  [Undo]  [    ]
```

- **Back:** Returns to the home page.
- **Roll buttons (2–12):** Show the number with roll count and percentage from `RollStats`.
  Styled with the current player's color gradient. Clicking sends a `RollMessage` with
  appropriate dice values to the REST API.
- **Soldier (Sol):** Enabled only when the current player has unspent Soldier entitlements.
  Clicking sends a `PurchaseMessage` with `Entitlement.Soldier`. Icon uses the Catan knight
  glyph.
- **Undo:** Enabled when `ActionFlags.UndoEnabled` is true. Sends `UndoMessage`.
- **Empty keys:** Blank/dark, no action.

**Roll number → dice mapping:**

The plugin converts a roll number to a valid `(die1, die2)` pair. For each roll total, use
the first valid combination:

| Roll | Die1 | Die2 |
|------|------|------|
| 2    | 1    | 1    |
| 3    | 1    | 2    |
| 4    | 2    | 2    |
| 5    | 2    | 3    |
| 6    | 3    | 3    |
| 7    | 3    | 4    |
| 8    | 4    | 4    |
| 9    | 4    | 5    |
| 10   | 5    | 5    |
| 11   | 5    | 6    |
| 12   | 6    | 6    |

**Button image generation (SVG):**

Each roll button is a 144×144 SVG with:

- Background: player color gradient (from `GameModel.PlayerColorMap`)
- Large centered number (roll value)
- Small text below: roll count and percentage (e.g., "3 / 12%")
- Border color: brighter when enabled, dim when disabled
- Red/black color coding for 6 and 8 (matching board number tokens)

### Build Page (Phase 2)

Navigated from the home page Build button. Shows purchase and turn-advance controls.

```text
Row 1:  [Road]  [Sett]  [City]  [Dev ]  [Back]
Row 2:  [Next]  [Undo]  [Redo]  [Sol ]  [    ]
Row 3:  [    ]  [    ]  [    ]  [    ]  [    ]
```

- **Back:** Returns to the home page.
- **Road/Settlement/City/DevCard:** Enabled based on `entitlementPurchaseModel[x].enabled`.
  Shows purchase count badge (unspent/max). Clicking sends `PurchaseMessage`.
- **Soldier:** Same as Rolls page — enabled when unspent soldiers available.
- **Next:** Enabled when `ActionFlags.NextEnabled` is true. Sends `NextMessage`.
- **Undo/Redo:** Enabled per `ActionFlags`. Sends `UndoMessage`/`RedoMessage`.

### Non-Actionable States

For states that require board interaction (MustMoveRobber, allocation, supplemental, too many
cards, etc.), the plugin stays on the home page. The State key shows the current state message
(e.g., "Move Robber") so the player knows to act in the browser. Undo remains available on the
home page for these states.

## Plugin Project Structure

```text
com.catan.streamdeck.sdPlugin/
├── manifest.json              # Plugin metadata, actions, profiles
├── bin/
│   └── plugin.js              # Compiled Node.js entry point
├── src/
│   ├── plugin.ts              # Main entry — registers actions, connects SignalR
│   ├── actions/
│   │   ├── CatanLauncher.ts      # Home key — switches to Catan profile
│   │   ├── ServerToggleAction.ts # Local/Azure server toggle
│   │   ├── GameStateAction.ts    # Display-only current state indicator
│   │   ├── AutoNavigateAction.ts # Toggle auto page-switching on state change
│   │   ├── NavigateAction.ts     # Page navigation (Roll, Build, Back)
│   │   ├── RollAction.ts         # Roll button (2-12)
│   │   ├── SoldierAction.ts      # Deploy soldier
│   │   ├── PurchaseAction.ts     # Road/Settlement/City/DevCard
│   │   ├── NextAction.ts         # Advance turn
│   │   ├── UndoAction.ts         # Undo
│   │   └── RedoAction.ts         # Redo
│   ├── services/
│   │   ├── GameConnection.ts  # SignalR + REST client
│   │   └── ButtonRenderer.ts  # SVG generation for button images
│   └── models/
│       └── types.ts           # TypeScript types mirroring GameModel
├── ui/
│   └── property-inspector.html  # Settings panel (URL, player, game)
├── static/
│   └── imgs/
│       ├── catan-icon.svg     # Plugin icon (Catan logo)
│       ├── roll-default.svg   # Default roll button
│       └── ...                # Other static assets
├── profiles/
│   ├── CatanHome.streamDeckProfile    # Home page (navigation hub)
│   ├── CatanRoll.streamDeckProfile    # Rolls page (dice 2-12)
│   └── CatanBuild.streamDeckProfile   # Build page (purchases + next)
├── package.json
├── tsconfig.json
└── rollup.config.js           # Bundle for Stream Deck runtime
```

## Communication Protocol

### Plugin → GameService (Commands)

All commands use the existing REST endpoint:

```text
POST /api/game/{gameId}/command
Content-Type: application/json

{
  "messageType": "RollMessage",
  "messageJson": "{\"Roll\":{\"Die1\":3,\"Die2\":4}}",
  "playerId": "{playerId}"
}
```

The plugin reuses the same endpoint and message format as the React UI.

### GameService → Plugin (State Updates)

The plugin connects as a SignalR client:

```typescript
const connection = new HubConnectionBuilder()
  .withUrl(`${serviceUrl}/gamehub`)
  .withAutomaticReconnect()
  .build();

connection.on('GameStateUpdated', (gameModel: GameModel) => {
  // Update all visible buttons based on new state
  updateButtons(gameModel);
});

await connection.start();
await connection.invoke('JoinGame', gameId, playerId);
```

### State Update → Button Refresh

On each `GameStateUpdated`:

1. Read `gameModel.GameState` to determine which profile/page to show
2. Switch Stream Deck profile if state category changed (roll → build)
3. Update each visible button's image and enabled state
4. For roll buttons: update count/percentage from `gameModel.RollStats`
5. For purchase buttons: update enabled from `entitlementPurchaseModel`

## Installation and Distribution

### "Download Catan Stream Deck" Command

The React UI settings page or nav menu provides a download link:

1. GameService serves the `.streamDeckPlugin` file at `/api/streamdeck/plugin`
2. User clicks "Download Stream Deck Plugin"
3. Browser downloads `CatanStreamDeck.streamDeckPlugin`
4. User double-clicks the file — Stream Deck app installs it
5. Catan icon appears in the Stream Deck action list
6. User drags it to a key — clicking it switches to the Catan profile

### Auto-Configuration via Deep Link

After installation, the React UI can send configuration via deep link:

```text
streamdeck://plugins/message/com.catan.streamdeck/configure?url=http://localhost:8080&gameId=xxx
```

This passes the GameService URL and current game ID to the plugin without manual entry.

## Phase 1 Scope: WaitingForRoll

The first implementation covers only the WaitingForRoll state:

### Deliverables

1. **Stream Deck plugin project** scaffolded with `@elgato/cli`
2. **CatanLauncher action** — icon on home page, switches to Catan roll profile
3. **RollAction** — 11 instances (2–12) with dynamic SVG images
4. **SoldierAction** — deploy soldier button (enabled when available)
5. **UndoAction** — undo button
6. **GameConnection service** — SignalR subscription + REST command sender
7. **ButtonRenderer service** — SVG generation with player colors
8. **CatanRoll profile** — pre-configured 15-key layout for roll state
9. **Download endpoint** — GameService serves the plugin file
10. **Build integration** — `./catan.ps1 streamdeck build` command

### What Phase 1 does NOT include

- WaitingForNext layout (Phase 2)
- Purchase buttons (Phase 2)
- MustMoveRobber status display (Phase 3)
- Property Inspector settings UI (use defaults for Phase 1)
- Marketplace distribution (sideload only)
- Multi-device support (standard 15-key Stream Deck only)

## Open Questions (V1 — Resolved)

1. **Plugin location:** Same repo (`streamdeck/`). Confirmed.
2. **Player identification:** V1 used global settings. V2 eliminates this — the browser knows.
3. **Multiple games:** V1 stored gameId in plugin. V2 eliminates this — the browser has one active
   game.
4. **Stream Deck Plus:** Deferred to a future phase.

---

## V2: Browser Relay Architecture (Planned)

> **Status:** On hold. This section captures the architectural decision and enough context to
> resume implementation later.

### Why V1 Is Broken

The V1 "peer client" architecture — where the plugin connects directly to GameService via SignalR
and REST — has a fundamental problem: **game discovery**. The plugin needs to know which game to
connect to, which player it controls, and which server to talk to. This creates several issues:

1. **Game discovery is unsolvable without the browser.** The plugin can't know which game the
   user has open in their browser. An IP-scoped "active game" API endpoint was proposed, but it
   breaks with multiple browser tabs or multiple users on the same network.
2. **Duplicate connection management.** The plugin reimplements the same SignalR connection logic
   as the browser (reconnection, group membership, state synchronization).
3. **Server toggle is user-hostile.** Requiring the user to manually switch between Local and
   Azure on the Stream Deck (separately from the browser) is confusing.
4. **Player ID synchronization.** The plugin needs the player ID from the browser session, but
   has no way to get it automatically.

### V2 Architecture: Plugin as Browser Remote Control

The core insight: **the browser already has the game connection, the game state, and the player
identity.** The plugin should be a remote control for the browser, not an independent game client.

```text
┌──────────────┐                                ┌──────────────────┐
│  GameService  │ ← SignalR + REST ──────────── │  React UI        │
│  (ASP.NET)   │                                │  (Browser)       │
└──────────────┘                                │                  │
                                                │  WebSocket relay │
                                                │  endpoint        │
                                                └────────┬─────────┘
                                                         │ ws://localhost:3001/streamdeck
                                                         │
                                                ┌────────┴─────────┐
                                                │  Stream Deck     │
                                                │  Plugin (Node.js)│
                                                └──────────────────┘
```

**Key principle:** The Stream Deck plugin NEVER communicates with GameService. It connects to the
browser via WebSocket. The browser is the publisher (pushes game state to plugin), and the plugin
is the consumer (sends command messages to browser, which relays them to GameService).

### Communication Protocol (V2)

#### Browser → Plugin (State Push)

On each `GameStateUpdated` from SignalR, the browser pushes a simplified state object to the
plugin over WebSocket:

```typescript
// Browser sends this on every game state change
interface StreamDeckState {
  gameState: string;                    // e.g., "WaitingForRoll"
  stateMessage: string;                 // e.g., "Select Roll..."
  currentPlayerColors: PlayerColor;     // { primary, secondary, foreground }
  actionFlags: ActionFlags;             // { undoEnabled, redoEnabled, nextEnabled, rollsEnabled }
  rollStats: Record<number, { count: number; percentage: number }>;
  soldierCount: number;                 // unspent soldiers for current player
  entitlements: Record<string, { enabled: boolean }>;
  connected: boolean;                   // browser's connection to GameService
}
```

#### Plugin → Browser (Commands)

The plugin sends command messages. The browser receives them and calls the appropriate
`GameServiceProxy` method:

```typescript
// Plugin sends these, browser relays to GameService
type StreamDeckCommand =
  | { type: "roll"; die1: number; die2: number }
  | { type: "next" }
  | { type: "undo" }
  | { type: "redo" }
  | { type: "purchase"; entitlement: string };
```

### WebSocket Endpoint

The WebSocket server runs alongside the Next.js dev server (or as part of the standalone build).
Options:

1. **Next.js custom server** (`react-ui/server.ts`) — wraps the Next.js handler and adds a `ws`
   upgrade handler on `/streamdeck`.
2. **Separate sidecar** — a tiny Node.js process on port 3001 that the browser connects to as a
   publisher and the plugin connects to as a consumer.
3. **Next.js API route with upgrade** — limited by Next.js's serverless model, won't work for
   persistent WebSocket connections.

**Recommended: Option 1** (custom server). It keeps everything in one process and one port.

### What Changes from V1

#### Removed from Plugin

- `GameConnection.ts` — no SignalR, no REST API calls
- `NativeFetchHttpClient.ts` — no fetch-based HTTP client
- `gameConnectionInstance.ts` — no singleton connection
- `ServerToggleAction.ts` — no server selection (browser handles this)
- `AutoNavigateAction.ts` — auto-navigate can stay, but driven by browser state push
- Global settings for `localUrl`, `azureUrl`, `activeServer`, `playerId`, `gameId`

#### Added to Plugin

- `BrowserConnection.ts` — WebSocket client connecting to `ws://localhost:3000/streamdeck`
- Simpler types — just `StreamDeckState` and `StreamDeckCommand`
- Reconnection logic — reconnect to browser WebSocket if connection drops

#### Added to React UI

- WebSocket relay server (custom `server.ts` or hook)
- `useStreamDeckRelay` hook — publishes game state over WebSocket, receives commands
- The hook subscribes to `GameStateUpdated` events and forwards them to the WebSocket

#### Kept As-Is

- Action classes (RollAction, UndoAction, RedoAction, NavigateAction, SoldierAction,
  GameStateAction, CatanLauncher) — same user-facing behavior, just backed by
  `BrowserConnection` instead of `GameConnection`
- ButtonRenderer.ts — SVG generation unchanged
- Profile structure (CatanHome, CatanRoll) — unchanged
- Build pipeline (Rollup, profile ZIP generation, `catan.ps1 streamdeck build`) — unchanged
- manifest.json — remove `server-toggle` action, otherwise unchanged
- Download from Settings page — unchanged
- CORS and MIME type configuration in Program.cs — unchanged

### SVG Cleanup

The current icon set has 60+ files (SVG + PNG at 1x and 2x for 10 actions). With
`server-toggle` removed and potentially simplifying other icons, the set can be reduced.
Minimum needed:

- `plugin/catan-icon` (SVG + PNG @1x @2x) — 3 files
- `plugin/category-icon` (SVG + PNG @1x @2x) — 3 files
- Per remaining action (8): `icon` + `key` (SVG + PNG @1x @2x) — 4 files each = 32 files
- **Total:** ~38 files (down from 60+)

Actions that use only dynamic SVG rendering at runtime (roll, soldier, game-state) may not need
`key.png` variants since the plugin overrides them immediately. This could reduce further to ~24
files.

---

## Lessons Learned from V1 Implementation

### ESM Bundling with SignalR

`@microsoft/signalr`'s `FetchHttpClient` dynamically `require()`s `tough-cookie`,
`fetch-cookie`, and `node-fetch` when `Platform.isNode` is true. This crashes in ESM bundles
where `require` is undefined.

**Fix:** Created `NativeFetchHttpClient.ts` using Node 20's native `fetch` and passed it +
explicit `WebSocket` import from `ws` to `HubConnectionBuilder.withUrl()` options. This bypasses
all dynamic requires.

**V2 impact:** Not needed — V2 plugin uses plain WebSocket (`ws` package), no SignalR.

### Profile ZIP Format

Stream Deck `.streamDeckProfile` files must be ZIP archives, not plain JSON. Internal structure:

```text
{uuid}.sdProfile/
  manifest.json                    # { Device, Name, Pages, Version }
  Profiles/{page-uuid}/
    manifest.json                  # { Controllers: [{ Actions, Layout }], Name }
```

Actions are keyed by `"col,row"` (e.g., `"0,0"`, `"4,2"`), not flat position numbers.

**Fix:** Created `scripts/build-profiles.mjs` that converts simple JSON definitions to proper
ZIP archives with correct internal structure.

**V2 impact:** Keep this script and the JSON→ZIP pipeline. It works correctly.

### DeviceModel for Stream Deck MK.2

The correct model identifier is `"20GBA9901"` (confirmed from existing installed profiles).
The `DeviceType: 0` in manifest.json's `Profiles` section corresponds to the standard 15-key
Stream Deck.

### ASP.NET Middleware Ordering

`UseCors()` must come before `UseStaticFiles()` for CORS headers to appear on static file
responses. The React app on port 3000 couldn't fetch `streamdeck-latest.json` from GameService
on port 8080 without this fix.

**V2 impact:** Keep this fix in Program.cs.

### Versioned Downloads

The download pipeline writes `streamdeck-latest.json` with `{ version, filename }` to
`wwwroot/downloads/`. The Settings page fetches this metadata to build the download URL with
the correct versioned filename. Shows explicit error/loading states instead of silently falling
back to a stale URL.

**V2 impact:** Keep this — it works well.

### TypeScript Type Workarounds

`IHttpConnectionOptions` type doesn't expose `WebSocket` property even though runtime accepts
it. Required `as Parameters<HubConnectionBuilder["withUrl"]>[1]` cast. The `ws` package also
needs `@ts-expect-error` for missing type declarations.

**V2 impact:** The `ws` `@ts-expect-error` may still be needed for the WebSocket client.
