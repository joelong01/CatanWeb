# MVVM Messaging Pattern

Source: design_docs/mvvm-pattern.md

## Overview

Desktop uses CommunityToolkit.Mvvm `Messenger` to decouple UI interactions from the shared `GameStateMachine`.
Every user action is expressed as an immutable message in `Catan3.Shared.Messages` (records).
Handlers exist in **two** execution paths:

- **Local mode** – `GameMessageService` owns an in-process `GameStateMachine`.
- **Service mode** – `GameMessageServiceProxy` relays commands to `GameServiceProxy` (SignalR/REST) and waits for hub updates.

## Adding a New Message (As-Built Steps)

1. **Define the message** in `Catan3.Shared.Messages` (records with XML docs). Validation happens in the state machine, not the message.
2. **Recording support** (optional but standard):
   - Add `[JsonDerivedType]` entry in `RecordedMessage.cs`.
   - Implement `IRecordedMessage` record (e.g., `SwapTileResourcesRecord`).
   - Extend `MessageConverters.ToRecord`.
3. **ViewModel emits message** via `Messenger.Send(message)` (e.g., `TileViewModel`, `MainPageViewModel`). UI only checks obvious preconditions.
4. **GameMessageService registration**:
    - Add to `RegisterLocalGameMessages`, `RegisterServiceGameMessages`, and `UnregisterGameMessages`.
    - Implement `Handle<Message>Async` (local) and `Handle<Message>ServiceAsync` (proxy).
       Local handler calls `_gameStateMachine.Handle*Async` and dispatches `UpdateGameModel` on success.
       Service handler invokes `_gameServiceProxy.Execute*Async` and relies on hub updates.
5. **GameStateMachine logic** (shared library) implements `Handle*Async` method, logs the request, validates state, mutates `GameModel`, calls
   `LogGameModel`, and returns the updated model.
6. **GameServiceProxy** (shared service layer) optionally exposes `Execute*Async` wrappers that call the hub (`GameHub`) or REST endpoints.

## Message Recording & Replay

- `GameRecorder` (Desktop only) subscribes via `GameMessageService` when `App.RecordMode` is enabled.
- Recorded actions serialize to `.catan_test` (JSON lines with discriminators). Replay harness uses `GameRecorder.ReplayAsync` to feed messages
  back through `GameMessageService`, ensuring deterministic outcomes.

## Error Handling

- Local handlers catch `GameException` and call `SendErrorMessage`, which routes to UI toast dialogs.
- Service handlers wrap exceptions from proxy calls and display critical errors (currently surfaces in DebugWindow).
- `GameServiceProxy` events `CommandFailed` provide additional metadata for WebUI and Desktop service mode.

## Testing Hooks

- UI automation reads `MainPageViewModel.GameModelJson` to verify state after message execution.
- CLI `MvvmObjectTester` ensures new messages serialize/deserialize correctly (calls `MessageConverters`).

## TODO / Enhancements

- Consolidate duplicate service/local handler bodies using shared helper methods to reduce drift.
- Surface service-mode errors in desktop UI (e.g., dialog queue) rather than DebugWindow traces only.
- Document plan for WebUI command coverage once Blazor emits messages via WebAssembly components instead of direct service calls.
