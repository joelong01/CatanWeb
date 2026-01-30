# Player ViewModel

Source: design_docs/player-viewmodel.md, design_docs/players-panel-design.md

## Shared Data

- `PlayerProfile` (shared project) stores `Id`, `Name`, `PlayerColors`, `ImageUri`, and optional `LifetimeStats`.
- Game Service seeds profiles from `Default Data/Players/*.json` and exposes them via `/api/players`.

## WebUI (`PlayerViewModel`)

- Wraps `PlayerProfile` with presentation helpers:
  - `CssGradient` (delegates to `PlayerColors.CssGradient`).
  - `FullImageUrl` (resolved via `baseUrl` for `<img>` tags).
  - `GetRenderColors()` tuple for SVG gradient stops.
- Created in `GameStateService.UpdatePlayerData` so player ordering mirrors `GameModel.Players`.
- Consumed by `PlayersPanel`, `PlayerTile`, and board renderers (roads/buildings use gradient stops). Player cards show name, victory points, and
  resource summary sourced from `GameModel` data.

## Desktop Parity

- WinUI uses `PlayerViewModel` class under `DesktopApp/Player` which mirrors fields and binds to XAML templates in `PlayerCtrl.xaml`.
- Colors and gradients read from shared `PlayerColors` ensuring consistent theme across platforms.

## TODO

- Add cached avatar fallback (currently `PlayerTile` hides `<img>` when URL missing; consider default silhouette asset).
- Persist per-user selection (e.g., highlight current user) once authentication is introduced.
