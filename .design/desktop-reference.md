# Desktop App Reference (WinUI 3)

**Last verified:** January 30, 2026

**DO NOT MODIFY** the desktop app unless explicitly directed.

## Overview

Windows desktop client built with WinUI 3 + CommunityToolkit MVVM.
Provides the reference implementation for game behavior. Analyze
for behavior, but do not change.

## Startup Flow

1. `App.OnLaunched` initializes `DebugWindowLoggerProvider`,
   `SettingsService`, `FileService`
2. Detects file activation (`.catan` and `.catan_test` files)
3. Toggles `IsTestMode` to disable animations during playback
4. Creates `MainWindow`, then asynchronously instantiates
   `GameMessageService`

## Dual-Mode Operation

The desktop app can operate in two modes:

| Mode | Setting | State Machine | Communication |
|------|---------|---------------|---------------|
| Local | `ServiceGame = false` | Embedded `GameStateMachine` | Direct method calls |
| Service | `ServiceGame = true` | Remote via GameService | `GameServiceProxy` (REST + SignalR) |

## MVVM Messaging Layer

**Files:** `GameMessageService.cs` + `GameMessageServiceProxy.cs`

`GameMessageService` mediates between UI commands and the shared
`GameStateMachine`:

- Registers handlers for every message type
- Local mode: calls `GameStateMachine` directly
- Service mode: delegates to `GameServiceProxy`
- Converts exceptions to `ErrorMessage` for UI dialogs/toasts
- Handles trace recording via `GameRecorder`

## Key View Models

### MainPageViewModel

- Creates `GameViewModel`, tracks command visibility
- Surfaces `GameModelJson` for UI automation testing
- Relays messenger commands
- Maintains `ShowCommands`, `IsRecordMode`, `SmuggledTestData`

### GameViewModel

Projects `GameModel` into UI-friendly collections:

- Resource tallies and purchase availability
- Board overlays and drag/drop ordering
- Dice animations and incremental board updates

## UI Composition

```
MainPage.xaml
├── GameBoardControl (Tiles, Roads, Harbors, Robber)
├── Player Panel
├── Command Buttons
├── Status Bar
└── DebugWindow (logs, recording messages)
```

Dedicated namespaces:

- `Board/` -- Tile, Road, Harbor, Robber visuals
- `Layout/` -- Panels, dialogs, measurement overlays
- `DebugWindow.xaml` -- Developer trace output

Icons use Segoe MDL2 Assets (Windows-only). Theming via WinUI
dark/light resources.

## Persistence & Recording

- `FileService` reads/writes `.catan` save files and `.catan_test`
  recording output
- `Trace<string>` provides undo/redo log with compression
- Recording writes to user Desktop when `RecordMode` toggled
- `DebugWindow` shows recording messages

## Integration Points

- Shares all models/messages with WebUI/GameService via
  `Catan3.Shared` references
- UI tests under `Tests.DesktopApp.UI` automate the WinUI tree
  using `GameModelJson` for state assertions

## Known Gaps

- Service mode lacks surfaced error dialogs for HTTP/SignalR failures
  (errors trace to DebugWindow only)
- Allocation-phase measurement overlay resides in code-behind
  (not yet moved to MVVM component)
- `GameMessageService` initialization uses manual async state flags
