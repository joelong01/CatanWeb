# Desktop App (WinUI 3)

Source: design_docs/window-layout.md, design_docs/mvvm-pattern.md

## Purpose

Windows desktop client built with WinUI 3 + CommunityToolkit MVVM. Provides the reference experience for playing games locally, recording
replays, and validating behaviour used by the WebUI and Game Service.

## Startup Flow

1. `App.OnLaunched`
   - Initializes `DebugWindowLoggerProvider`, `SettingsService`, `FileService`, and records command-line activation (supports `.catan` and
     `.catan_test`).
   - Detects ActivatedFilePath and toggles `IsTestMode` to disable animations when playback is driven by UI automation.
   - Creates `MainWindow`, then asynchronously instantiates `GameMessageService` via `GameMessageService.CreateGameMessageService` once the UI
     thread is idle.
2. `MainWindow` hosts `MainPage` (XAML). DebugWindow auto-opens for trace output and recording feedback in test mode.

## MVVM Messaging Layer

- `GameMessageService` (partial: `GameMessageService.cs` + `GameMessageServiceProxy.cs`) mediates between UI commands and the shared
  `GameStateMachine`.
  - Registers handlers for every message type (`UndoMessage`, `RollMessage`, `SwapTileResources`, etc.).
  - Supports **Local Mode** (embedded `GameStateMachine`) and **Service Mode** (delegates to remote `GameServiceProxy`) based on the
    `ServiceGame` setting.
  - Converts exceptions to `ErrorMessage` objects consumed by UI dialogs/toasts.
  - Handles trace recording by interacting with `GameRecorder` when `App.RecordMode` is toggled.
- `GameMessageServiceProxy` contains the remote execution branch, mapping messages to REST/SignalR operations through shared proxy types.

## View Models & Data Flow

- `MainPageViewModel`
  - Creates `GameViewModel`, tracks command visibility, surfaces `GameModelJson` for UI automation, and relays messenger commands.
  - `CreateMainPageViewModel` sends either `NewGameMessage` or `LoadLocalCatanGameMessage` immediately after instantiation.
  - Maintains `ShowCommands`, `IsRecordMode`, `SmuggledTestData` for testing scenarios.
- `GameViewModel` (under `Game/GameView`) projects `GameModel` into UI-friendly collections (resource tallies, purchase availability, board
  overlays). Handles drag/drop ordering, dice animations, and incremental board updates.
- `SettingsService` + `SettingsModel` persist WinUI settings and raise messenger updates when toggles change (e.g., `ServiceGame`).

## UI Composition

- `MainPage.xaml` arranges `GameBoardControl`, player panel, command buttons, and status bar to mirror the classic Catan layout.
- Dedicated XAML/controls namespaces:
  - `Board` (Tiles, Roads, Harbors, Robber visuals) stored under `Tiles/`, `Roads/`, `Harbors/`.
  - `Layout/` contains panels, dialogs, and measurement overlays used during allocation phases.
  - `DebugWindow.xaml` surfaces logs and recording messages.
- Icons use Unicode glyphs per project standard; theming leverages WinUI resources for dark/light modes.

## Persistence & Recording

- `Catan.Services.FileService` reads/writes `.catan` save files and `.catan_test` recording output.
- `Trace<string>` (shared utility) provides undo/redo log; GameMessageService persists via `PersistGameMessage` handlers.
- Recording pipeline writes to user Desktop when `RecordMode` toggled; sampled via `MainPageViewModel.IsRecordMode` and `DebugWindow` messages.

## Integration Points

- Desktop can operate disconnected (pure local state machine) or as a thin client to `GameService` by enabling `ServiceGame` in settings.
- Shares all models/messages with WebUI/GameService through references to `Catan3.Shared` and `Catan3.Shared.Services`.
- UI tests under `Tests.DesktopApp.UI` automate the WinUI tree using `GameModelJson` for state assertions.

## TODO / Gaps

- Service mode UI flows lack surfaced error dialogs for HTTP/SignalR failures; errors currently trace to DebugWindow only.
- Allocation-phase measurement overlay logic still resides in code-behind; consider moving to a dedicated MVVM component for parity with WebUI
  board measurement pipeline.
- `GameMessageService` initialization is asynchronous with manual state flags; evaluate using hosted services or DI container once WinUI
  upgrades permit.
