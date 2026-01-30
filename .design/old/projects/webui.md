# WebUI (Catan3.WebUI)

Source: design_docs/WebUI-Design.md, design_docs/webui-left-panel-refinement.md

## Purpose

Blazor WebAssembly front-end that renders the shared `GameModel` pushed by the Game Service. Uses the thick-client pattern: the browser keeps
a local copy of the current `GameModel`, renders SVG board assets, and issues commands back to the server over SignalR.

## Composition

- `Program.cs`
  - Registers root components (`App`, `HeadOutlet`).
  - Adds singleton services: `GameServiceConfig`, `GameConnectionService`, `GameStateService`, `ClientAssetService`.
  - Initializes theming assets (`ClientAssetService.InitializeAsync`).
- `Services/`
  - `GameConnectionService`: wraps `Catan3.Shared.Services.GameServiceProxy` to manage SignalR connection state and expose async methods for
    every game action (Undo, Roll, RoadPurchase, MoveRobber, etc.). Transitions between `ConnectionState` values and forwards hub events to the
    UI.
  - `GameStateService`: client-side store for the latest `GameModel`, player profiles, and UI-only flags (`ShownStars`). Raises `OnStateChanged`
    for components to re-render.
  - `ClientAssetService`: resolves board/asset SVGs, theme colors, and icon glyphs from `wwwroot/assets` plus `theme.json`.
- `Pages/`
  - `Game.razor`: top-level experience; subscribes to `GameStateService.OnStateChanged` and delegates to board, player panel, and control
    components. Handles initial connection prompts and loading spinners.
  - `Home.razor`, `NewGame.razor`, `LoadGame.razor` map to service APIs (`/api/game/new`, `/api/games`, `/api/game/{id}/load`).
- `Components/`
  - Board rendering pipeline: `BoardCanvas`, `BoardMeasurement`, `BoardSvgGenerator`, plus specialized SVG renderers for tiles, roads,
    buildings, harbors, and robber. Components expect canonical coordinates and colors supplied by the shared models.
  - Control primitives (`IconButton`, `PurchaseButton`, `PlayersPanel`, `PlayerTile`, `Weather`, etc.) mirror Desktop styling via CSS variables
    defined in `wwwroot/css/app.css`.
- `Models/`
  - View models that wrap shared types for the UI (e.g., `PlayerViewModel`, `RollHistoryItem`). Many are projections of `GameModel` for display,
    keeping rendering logic out of components.

## Data Flow

1. `GameConnectionService.ConnectAndJoinAsync` establishes SignalR hub connection using `GameServiceProxy` from shared library.
2. Hub pushes `GameModel` updates via `GameStateUpdated`; service updates `GameStateService.UpdateGameModel`, which triggers UI re-render.
3. Components read `GameStateService.GameModel` for reactive data (board geometry, player resources, robber location).
4. User commands call `GameConnectionService` methods, which invoke `GameServiceProxy` and ultimately execute shared `GameStateMachine` logic on
   the server.
5. REST endpoints (`HttpClient` with base URL from `GameServiceConfig`) support new game creation, saved-game listing, and database health checks.

## Rendering Strategy

- SVG board is built from shared hex coordinates.
  `BoardSvgGenerator` maps `GameModel.Tiles`, `Roads`, and `Buildings` into layered `<svg>` groups.
- CSS uses custom properties for theme colors and icon fonts (`--icon-font-family`, etc.).
  Layout aligns with Desktop counterpart (sidebar + board canvas) per `webui-left-panel-refinement.md` guidance.
- Animations rely on CSS transitions; dice rolls use `RollHistory` components to display results.

## TODO / Gaps

- Mobile layout remains a TODO; components assume desktop-sized viewport. Add responsive breakpoints to match companion design.
- `GameConnectionService` currently overrides `_proxy.PlayerId` with `CurrentPlayerId` from updates, which prevents spectators. Introduce
  explicit spectator mode before enabling multi-observer sessions.
- Client asset caching is memory-only; persist resolved theme selection to Blazored LocalStorage once JS interop timing issues are solved.
- Add error surfaces for command failures (currently only surfaces toast via `CommandFailed` event; UI listeners do not display them).
