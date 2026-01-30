# Game Service (Catan3.GameService)

Source: design_docs/database-design.md, design_docs/GameServiceAPIs.md

## Purpose

ASP.NET Core 8 web service that hosts game orchestration, persistence, and SignalR endpoints for the Settlers of Catan experience. The
service mirrors the Desktop app's MVVM messaging while persisting game state to SQLite.

## Startup Pipeline

1. `Program.cs` configures logging, JSON serializer options via `JsonHelper`, CORS (`AllowLocalhost`), and SignalR (`GameHub`).
2. Kestrel listens on `0.0.0.0:8080` (HTTP only in development). Console banner lists URLs when not in Testing environment.
3. Database seeding (`DatabaseSeeder.SeedAsync`) runs once per startup against `Data/catan.db`; `--seed-database` exits after seeding.
4. Static files are served from `wwwroot` with caching for images and fonts. `/companion` and `/demo` HTML assets are hydrated with runtime
   script injections.

## Dependency Injection

- `AddControllersWithViews` (no Razor Pages) with JSON options powering REST endpoints in `Controllers/GameApiController`.
- Entity Framework Core `CatanDbContext` bound to SQLite file under `Data/`.
- Persistence:
  - scoped `IGamePersistence` → `GamePersistenceService` (saves metadata + compressed logs).
  - singleton `IPersistenceService` → `NullPersistenceService` (desktop-compatible log sink).
- Real-time notifications:
  - singleton `SignalRNotificationService` implementing `IClientNotification` for hub/message bridge.
  - singleton `AsyncCommandProcessor` to handle fire-and-forget HTTP commands.

## HTTP Surface (Controllers/GameApiController)

- `/api/game/new` creates a fully initialized game using `GameStateMachine.HandleNewGameAsync`, registers it in `GameStateMachineRegistry`,
  and returns `{gameId}`. Supports Regular/Expansion boards.
- `/api/game/action` accepts raw JSON commands, enqueues them through `AsyncCommandProcessor`, and responds immediately.
- `/api/game/{gameId}/shuffle|load|persist|end` and other verbs call directly into the shared `GameStateMachine` operations, then persist via
  `ProcessGameActionResult` (`SaveGameToDatabase` + SignalR broadcast).
- `/api/gamestate/{gameId}` returns the current `GameModel` (no extra metadata). `/api/games` and `/api/players` expose database data.
- `/api/game/import` ingests `.catan` archives by decompressing to `SerializableLog` and storing through `GamePersistenceService`.

## SignalR Hub (Hubs/GameHub)

- Clients call `JoinGame` to enter a SignalR group keyed by `gameId`; the hub immediately pushes the latest `GameModel` to all members.
- Implements discrete methods mirroring Desktop actions (`Shuffle`, `Undo`, `ExecutePurchase`, `ExecuteMoveRobber`, etc.). Each method:
  1. Fetches the in-memory `GameStateMachine` (`GameStateMachineRegistry`).
  2. Validates acting player (`ValidateCaller`).
  3. Invokes the async handler (`HandleShuffleAsync`, `HandleRoadPurchaseAsync`, ...).
  4. Broadcasts updated `GameModel` via `Clients.Group(gameId).SendAsync("GameStateUpdated", model)`.
  5. Emits completion/failure callbacks to the caller for optimistic UI updates.
- Presence tracking: `PlayerPresenceChanged` events fire on join/leave and disconnect.
- `CreateDetailedErrorInfo` formats failures with metadata for client debugging (exception names, inner exception, timestamp).

## Persistence Layer

- Database schema uses `GameSaveMetadata` + `GameSaveData` tables to separate metadata from compressed payloads.
- `GamePersistenceService.SaveAsync` stores `GameMetadata` (state, players, turn count) plus the gzipped `SerializableLog` bytes.
- Loading from DB reconstructs the `Log<string>` via `JsonHelper.Decompress` and `Log<string>.FromSerializableLog`, preserving undo/redo stacks.

## Background Command Processing

`AsyncCommandProcessor` (singleton) deserializes HTTP command payloads into strongly typed messages, executes the corresponding
`GameStateMachine` handler, saves the game, and notifies SignalR clients. Ensures HTTP endpoint responsiveness while keeping command logic in
one place.

## Health & Utilities

- `/health` returns service uptime metadata.
- `/api/database/health` drives provisioning scripts (`webui.ps1`) by reporting player/game counts and seeding needs.
- Console startup banner enumerates LAN URLs for companion and demo clients.

## TODO / Gaps

- Authentication/authorization is not implemented; all endpoints trust caller-supplied `playerId` values.
- `IPersistenceService` is still `NullPersistenceService`; integrate durable file storage for server-driven saves.
- `SignalRNotificationService` wiring is registered but not currently used outside hub logging; evaluate removal or extend for push
  instrumentation.
- Companion/demo HTML rely on string replacement for asset URLs; consider Razor or SPA build step for maintainability.
