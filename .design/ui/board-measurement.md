# Board Measurement Panel

Source: design_docs/board-measurement-design.md, design_docs/board-measurement-call-flow.md, design_docs/board-measurement-impl-plan.md

## Purpose

During the `PickingBoard` phase players evaluate board balance. The Board Measurement panel exposes resource star counts and shuffle controls
identical to the Desktop app.

## Desktop Implementation

- `Resources/BoardMeasurementCtrl.xaml` renders the panel. Data binds to `GameViewModel` properties: `GameModel`, `CanShuffle`, `ShownStars`, and
  `ResourceSelection`.
- Commands:
  - `ShuffleCommand` sends `ShuffleMessage` via messenger.
  - `StarSliderValue` updates `GameViewModel.ShowStars` which hides low-star buildings on the board overlay.
  - Resource toggles update `GameViewModel.SelectedResources` (max 3 selections enforced).
- Star totals computed via `GameModel.Buildings` + `TilesForBuildings` extension.

## WebUI Implementation

- Component: `Components/Board/BoardMeasurement.razor`.
  - Parameters: `GameModel`, `CanShuffle`, `ShownStars`, `OnShuffle`, `ShownStarsChanged`, `SelectedResourcesChanged`.
  - Uses `ResourceCard`, `StarCounter`, and `IconButton` child components.
  - `GetDisplayedResources()` fixes card order (Wheat, Wood, Sheep, Brick, Ore) to match Desktop design.
  - `HandleResourceToggle` enforces the three-selection cap (removes oldest when exceeding).
  - `GetStarCount` mirrors Desktop star calculation via `GameModel.TilesForBuildings(...).Stars()`.
  - Slider emits `ShownStarsChanged` for parent to persist threshold in `GameStateService`.

## Data Sources

- `GameModel` originates from shared state machine; `ActionFlags` determine `CanShuffle` (true only in `PickingBoard`).
- Star counts rely on `TileModel.Stars` populated by board generation logic (`GameFactory` / `GameStateMachine`).

## Call Flow (WebUI)

1. `GameStateService.UpdateGameModel` invoked from SignalR update.
2. `Game.razor` passes `GameModel` and `CanShuffle` (`GameModel.GameState == GameState.PickingBoard`) into `BoardMeasurement`.
3. Slider changes -> `GameStateService.ShownStars` updated; `BoardCanvas` reads `ShownStars` to toggle building visibility.
4. Resource toggle -> `SelectedResourcesChanged` updates parent filter; board component highlights matching buildings.
5. Shuffle button -> parent triggers `GameConnectionService.ShuffleAsync`, resulting update persists through SignalR.

## TODO / Opportunities

- Persist selected resources and star threshold in `Blazored.LocalStorage` for session continuity (pending asset service JS timing fix).
- Desktop `BoardMeasurementCtrl` and Blazor component duplicate resource order and selection logic; extract shared helper in `Catan3.Shared`
  to prevent divergence.
- Add analytics/tracing for shuffle usage (currently only visible via verbose logs).
