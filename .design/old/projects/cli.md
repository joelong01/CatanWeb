# CLI (Catan3.CLI)

Source: design_docs/Session-Bootstrap-Design.md

## Purpose

Command-line harness for integration testing and automation. Connects to a running GameService instance, drives the shared `GameStateMachine`
end-to-end over SignalR, and validates MVVM serialization.

## Commands (Program.cs)

- `expansion` / `regular`
  - Spins up a hosted service container with `GameRunner`, `GameSessionManager`, and logging.
  - Options: `--player-count`, `--run-to` (stop at first `GameState` match), `--complete` (full script), `--no-exit`, `--log-level`, `--uri`.
  - Validates options via `GameRunOptions.Validate` then calls `GameRunner.RunGameAsync`, which orchestrates new game creation, command loops,
    and teardown.
- `test`
  - Currently supports `--mvvm-objects` to run `MvvmObjectTester.TestAllMvvmObjectsAsync`, ensuring message DTOs serialize/deserialize cleanly
    across shared assemblies.
- `extract`
  - `ExtractCommand` extracts a `GameModel` snapshot from `.catan` archives (`Log<string>` compressed payloads). Optional `--actions` builds
    `.catan_test` files for replay.

## Architecture

- .NET Generic Host houses services for dependency injection so commands can share state (`Singleton` GameRunner, logger factory).
- Uses `System.CommandLine` for parsing; asynchronous handlers interact with GameService via `Catan3.Shared.Services.GameServiceProxy`.
- CLI doubles as smoke-test tool during CI to ensure service-game parity with Desktop flows.

## TODO / Gaps

- `GameRunner` lacks telemetry for command timings; future work could echo the Desktop trace log for debugging.
- `test` command needs additional subtests (e.g., resource allocation scripts) to mirror UI regression coverage.
