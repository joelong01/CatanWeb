# Design Documentation Summary

## Status

- Project, system, and UI documentation now reflects the "as built" code paths across Desktop, GameService, WebUI, Shared, and CLI projects.
- Gameplay design coverage is pending; `.design/gameplay/` is empty and still needs per-state lifecycle documentation.

## Table of Contents

### Projects

- `.design/projects/cli.md`
- `.design/projects/desktop-app.md`
- `.design/projects/game-service.md`
- `.design/projects/shared.md`
- `.design/projects/webui.md`

### Systems

- `.design/systems/board-rendering.md`
- `.design/systems/coordinates.md`
- `.design/systems/database.md`
- `.design/systems/game-service-api.md`
- `.design/systems/mvvm-messaging.md`
- `.design/systems/save-load.md`

### UI

- `.design/ui/assets.md`
- `.design/ui/board-measurement.md`
- `.design/ui/number-token.md`
- `.design/ui/player-viewmodel.md`

### Gameplay

- _Pending: populate `.design/gameplay/`._

## TODO Tracker

| Document | TODO / Gap |
| --- | --- |
| `.design/projects/game-service.md` | Auth missing; persistence service null; SignalR notification unused; companion/demo HTML replacement fragile. |
| `.design/projects/shared.md` | Consolidate hex geometry helpers; document GameRecorder portability; dedupe rule logic with GameFactory. |
| `.design/projects/webui.md` | Responsive/mobile layout; don't override playerId (spectators); persist theme choice; expose command errors. |
| `.design/projects/desktop-app.md` | Surface service-mode errors in UI; move measurement overlay to MVVM component; streamline async init. |
| `.design/projects/cli.md` | Add telemetry for GameRunner; expand test command coverage for resource and flow regressions. |
| `.design/systems/board-rendering.md` | DRY shared geometry constants; consider prebuilt SVG demos; explore diffed SignalR updates. |
| `.design/systems/coordinates.md` | Share pixel conversion constants; derive road adjacency from direction table. |
| `.design/systems/database.md` | Add migrations once auth lands; add indexes for GameType/PlayerCount; normalize StartedBy via identity. |
| `.design/systems/game-service-api.md` | Implement auth pipeline; review `/api/game/action` usage; add export endpoint for `.catan` downloads. |
| `.design/systems/save-load.md` | Replace StartedBy placeholder; dedupe imports; plan desktop/service save convergence. |
| `.design/systems/mvvm-messaging.md` | Reduce local/service handler drift; surface remote errors to desktop; document WebUI command path. |
| `.design/ui/board-measurement.md` | Persist slider and resource filters; extract shared selection helper; log shuffle usage. |
| `.design/ui/assets.md` | Discover themes dynamically; ensure startup persistence; document asset contribution path. |
| `.design/ui/number-token.md` | Centralize token constants in shared view data; add accessibility-focused glyph option. |
| `.design/ui/player-viewmodel.md` | Add avatar fallback asset; persist current-user highlight once auth exists. |
| `.design/gameplay/` | Author per-state lifecycle documentation for the gameplay directory. |

## Suspected Bugs

- `WebUI/Services/GameConnectionService.cs`: `OnGameStateUpdated` overwrites caller playerId and removes spectators.
- `DesktopApp/GameMessageService.cs`: Service-mode failures only reach the debug window, leaving players without user-facing error dialogs.
- `Catan3.GameService/Controllers/GameApiController.cs`: `SaveGameToDatabase` sets `StartedBy = "WebUI"` for every save, breaking provenance.
- `Catan3.GameService/Services/AsyncCommandProcessor.cs`: Imports skip deduping profiles or images, so duplicates accumulate.

## Next Steps

- Fill the gameplay section with per-state behavior docs and ensure they stay aligned with the shared state machine implementation.
- Prioritize fixes for the suspected bugs before expanding documentation further, so new docs capture the corrected behaviors.
