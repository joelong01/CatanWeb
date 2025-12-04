# Shared Library (Catan3.Shared)

Source: design_docs/player-viewmodel.md, design_docs/Coordinate-Design.md

## Purpose

`Catan3.Shared` is the cross-platform domain library that supplies the full Settlers of Catan game model, state machine, and serialization
utilities. Both the DesktopApp (WinUI) and GameService (ASP.NET Core) consume these types to guarantee consistent rules, assets, and
network contracts.

## Key Assemblies

- **GameLogic/**
  - `GameStateMachine`: 2k+ line authoritative rule engine containing every transition (`HandleNextAsync`, `HandleRollAsync`, robber flows,
    supplemental phases). Encapsulates undo/redo through an injected `IGameLog` (Desktop: `Trace<string>`, Service: `Log<string>`).
  - `GameRecorder`: optional replay recorder used by Desktop test tooling.
- **Models/**
  - `GameModel`: Observable core state with hash, players, tiles, buildings, roads, harbors, robber, `ActionFlags`, and helper accessors used
    by both UI layers.
  - `PlayerModel`, `TileModel`, `BuildingModel`, `RoadModel`, `HarborModel`, `ResourcesModel`, etc. All rely on CommunityToolkit MVVM source
    generators for change notification.
  - `EntitlementPurchaseModel` / `ResourceRules` / `HouseRules` define configurable constraints.
- **Messages/**
  - Command DTOs (`RollMessage`, `RoadPurchaseMessage`, `MoveRobberMessage`, `ParticipatingInSupplementalMessage`, etc.) that mirror Desktop
    MVVM traffic and SignalR payloads.
- **Interfaces/**
  - Abstractions for persistence (`IGameLog`, `IPersistenceService`), logging (`ICatanDebugTrace`), notification, metadata,
    and the `IGameStateMachine` contract consumed by both app and service.
- **Utility/**
  - `JsonHelper`: central serialization + compression helpers.
  - `ReplayableRandom`: deterministic RNG with seed capture for replays.
  - `Log<T>`: undo/redo stack implementation with compression support (`GetSerializableLog`).
  - `TraceExtensions`: logging helpers ensuring consistent trace formatting.
- **Extensions/**
  - Hex-grid math, adjacency computations (`GameModelExtensions.FindAdjacentHarbor`), resource helpers, and LINQ convenience methods for board
    traversal.
- **PlayerProfile/** and **TestData/**
  - JSON-backed default player avatars and canned logs used by seeding scripts.

## Primary Flows

- `GameStateMachine.HandleNewGameAsync` builds the initial `GameModel`, populates tiles/roads/buildings from `IGameMetadata`, shuffles the board,
  sets `GameState` to `PickingBoard`, and seeds the undo log.
- Every public handler obtains a copy of the current game (`_gameLog.CopyCurrent()`), logs the request, optionally records the action, mutates
  state, calls `LogGameModel()` to push onto the log, and returns the updated `GameModel`.
- Undo/redo (`HandleUndoAsync`/`HandleRedoAsync`) manipulate the log directly without calling `LogGameModel`, keeping history accurate.
- Persistence path:
  - Desktop: `Trace<string>` writes `.catan` files to disk through `FileService`.
  - Service: `Log<string>` serializes to JSON/compressed bytes for SQLite storage.
- Rule enforcement lives entirely inside private helpers (e.g., `RoadPurchase`, `MoveRobber`, `BalanceBoard`, `SoldierDiscardFlow`).
  The helpers ensure the same validation for every client.

## Integration Points

- Desktop `GameMessageService` invokes shared messages and observes mutated `GameModel` via MVVM.
- GameService wraps the shared state machine within REST/SignalR endpoints and persists using `GamePersistenceService`.
- WebUI fetches and renders raw `GameModel` JSON; component view-models are shape-compatible with shared models, enabling direct binding.

## TODO / Gaps

- Hex-grid math is scattered between `Extensions/HexExtensions.cs`, `Utility/HexCoordinates`, and board generators; consolidate into a dedicated
  geometry module for maintainability.
- `GameRecorder` integration is optional in service builds; document how recordings travel between Desktop and service for replay parity.
- Several helper methods in `GameStateMachine` still duplicate logic from `GameFactory` (e.g., settlement placement validation). Consider
  extracting them for reuse when WebUI transitions to server-authoritative moves.
