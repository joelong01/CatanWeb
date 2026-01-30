# Game Service Internals As-Built

**Status:** As-Built
**Source:** `Catan3.GameService/Program.cs` & `Services/*`

## 1. Architecture

The `GameService` is a monolithic **ASP.NET Core 9.0 Web API** that acts as the authoritative host for the game state.

* **Port**: 8080 (Configured in Kestrel).
* **State**: "Stateless" processing (state loaded from DB -> Processed -> Saved to DB).
* **Concurrency**: handled via `AsyncCommandProcessor`.

## 2. Startup & Dependency Injection

Based on `Program.cs`, the service registers:

* **Singletons**:
  * `SignalRNotificationService`: Manages real-time broadcasts.
  * `DatabaseBackedPersistenceService`: Implementation of `IPersistenceService`.
  * `AzureSqlDiagnosticService`: Active monitoring for SQL connection health.
* **Scoped**:
  * `IGamePersistence` -> `GamePersistenceService`.

## 3. Persistence Strategy

The system prioritizes data safety by saving state after every meaningful transition.

### Workflow
1.  **Command Execution**: `GameApiController` receives a command (e.g., "Build Road").
2.  **Logic Processing**: `AsyncCommandProcessor` feeds it to `GameStateMachine`.
3.  **State Update**: If valid, the state machine updates the in-memory `GameModel`.
4.  **Database Commit**: The new `GameModel` is immediately serialized and saved to the `GameSaveData` blob.
5.  **Broadcast**: If the save adheres, `SignalR` emits the new state to clients.

### Storage Format
*   **Metadata**: `GameSaveMetadataEntity` (SQL Table) for quick queries (Player names, Scores).
*   **Data**: `GameSaveEntity` (Blob) containing the full JSON calculation state.

## 4. Notification Pipeline (Hybrid)

The service supports two clients with different needs:

1. **Blazor (Legacy)**: Uses SignalR for *commands* and *updates*.
2. **React (Active)**: Uses REST for *commands* and SignalR for *updates*.

**The Pipeline:**

1. **REST Command**: `POST /api/game/action` accepts a JSON `Request`.
2. **Processor**: `AsyncCommandProcessor` handles it.
    * Loads `GameModel` from DB (or cache).
    * Runs `GameStateMachine.Handle[Message]`.
    * **Persists** new state to DB (Metadata + Blob).
    * **Broadcasts** `GameStateUpdated` via SignalR Hub.
3. **Client Update**: React client receives SignalR event `GameStateUpdated` and replaces local zustand store.

## 4. Error Handling

* **Fire-and-Forget**: For React REST calls, the API returns `200 OK` immediately if the message format is valid.
* **Async Failure**: If the logic fails (e.g., "Not your turn"), the `AsyncCommandProcessor` sends a `CommandFailed` SignalR message back to the specific client.
* **Crash Recovery**: `GameLog` persistence ensures that if the server crashes, the exact state (including Undo history) is recovered from the `GameSaveDataEntity` blob.
