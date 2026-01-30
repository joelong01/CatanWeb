# Blazor WebUI (Legacy Reference)

**Last verified:** January 30, 2026

The Blazor WebAssembly frontend is being replaced by the React UI.
This document preserves reference information about the Blazor
implementation for features not yet ported.

## Architecture

Thick-client pattern: the browser keeps a local `GameModel` copy,
renders SVG board assets, and issues commands back to the server
over SignalR.

### Services

| Service | Purpose |
|---------|---------|
| `GameConnectionService` | Wraps `GameServiceProxy` for SignalR management |
| `GameStateService` | Client-side store for latest `GameModel` and UI flags |
| `ClientAssetService` | Resolves board/asset SVGs, theme colors, icon glyphs |

### Pages

| Page | Route | Purpose |
|------|-------|---------|
| `Game.razor` | `/game` | Main game experience (~1500+ lines) |
| `Home.razor` | `/` | Landing page |
| `NewGame.razor` | `/newgame` | Game creation |
| `LoadGame.razor` | `/loadgame` | Load saved games |

### Data Flow

1. `GameConnectionService.ConnectAndJoinAsync` establishes SignalR
2. Hub pushes `GameModel` via `GameStateUpdated`
3. `GameStateService.UpdateGameModel` triggers UI re-render
4. Components read `GameStateService.GameModel` for reactive data
5. User commands call `GameConnectionService` methods -> proxy ->
   server

## Rendering Pipeline

SVG-based board rendering (differs from React's DOM-based approach):

| Component | Purpose |
|-----------|---------|
| `BoardCanvas` | SVG viewport with zoom/pan |
| `BoardMeasurement` | Resource star display, shuffle controls |
| `BoardSvgGenerator` | Maps `GameModel` to layered SVG groups |
| `TileSvgRenderer` | Individual tile rendering with flip support |
| `RoadSvgRenderer` | Road segment rendering |
| `BuildingSvgRenderer` | Settlement/city markers |
| `HarborSvgRenderer` | Harbor hex rendering |
| `RobberLayer` | Robber animation with GriefDodgy fake-out |

CSS custom properties for theming: `--icon-font-family`, player
colors, etc. in `wwwroot/css/app.css`.

## Features Fully Implemented in Blazor Only

These features exist in Blazor but have no React equivalent yet:

| Feature | Files | Notes |
|---------|-------|-------|
| GriefDodgy tile flip | `TileSvgRenderer`, `BaseLayer`, `Game.razor` | Full animation |
| GriefDodgy fake-out | `RobberLayer.razor` | 3-phase state machine |
| GriefDodgy celebration | `Game.razor`, `app.css` | Particle animation |
| Portrait tabbed UI | `Game.razor` | 3-tab interface |
| ViewportScaler | `viewportScaler.js` | Uniform scaling |
| Settings page | `Settings.razor` | Full house rules UI |
| Edit Players page | `EditPlayers.razor` | Player profile management |
| Statistics page | `Stats.razor` | Lifetime stats display |
| TooManyCards discard | `Game.razor` | 7-card discard UI |

## Board Measurement Panel

Displays during `PickingBoard` state:

- Resource star counts per resource type (Wheat, Wood, Sheep,
  Brick, Ore)
- Fixed card order with three-selection cap
- Star computation: `GameModel.TilesForBuildings(...).Stars()`
- Slider updates `GameStateService.ShownStars`

## Number Token Rendering

`CatanNumberSvg.Render()` builds SVG:

- Background circle
- Centered number text
- Probability stars below (`PipsOffsetY`)
- Star mapping: 2/12=1, 3/11=2, 4/10=3, 5/9=4, 6/8=5

## Player ViewModel

`PlayerViewModel` wraps `PlayerProfile` with presentation helpers:

- `CssGradient` -- delegated to `PlayerColors.CssGradient`
- `FullImageUrl` -- resolved via base URL
- Created in `GameStateService.UpdatePlayerData`
- Consumed by `PlayersPanel`, `PlayerTile`, board renderers

## Viewport Scaling

Architectural pattern replacing fragile percentage-based scaling:

- Fixed base dimensions: 1920x1080 (landscape), 1080x1920
  (portrait)
- Uniform scale factor: `min(viewport / base, 1.0)`
- CSS Grid: 25fr/60fr/26fr columns for landscape layout
- JavaScript `ViewportScaler` class handles orientation detection
  and scale calculation

## Known Gaps (Blazor)

- Mobile layout assumes desktop viewport; responsive breakpoints
  incomplete
- `GameConnectionService` overrides `_proxy.PlayerId` with
  `CurrentPlayerId`, preventing spectator mode
- Client asset caching is memory-only (no localStorage persistence)
- Command failure surfaces only trace to console; no visible error
  toasts in all cases
