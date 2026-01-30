# CLI Tooling (catan.ps1)

**Last verified:** January 30, 2026

## Overview

`catan.ps1` is the unified entry point for all development, testing, and
deployment operations. It abstracts platform differences (Windows vs
macOS) and manages the multi-project build.

**Usage:** `pwsh ./catan.ps1 [Verb] [SubCommand] [Flags]`

Always use `pwsh` (PowerShell 7+), never legacy `powershell`.

## Core Verbs

| Verb | Purpose | Key Flags |
|------|---------|-----------|
| `run` | Build and start full stack (Service + React UI) | `-Network`, `-Razor`, `-Desktop` |
| `stop` | Terminate all running services | |
| `restart` | Stop then run services | |
| `update` | Rebuild and restart (when hot-reload fails) | `-Terminate` |
| `build` | Compile solution (no tests) | `-Desktop` |
| `test` | Build and run all tests | |
| `clean` | Remove build artifacts (preserves database) | |
| `doctor` | Validate environment health | |
| `install` | Install dependencies and seed database | |
| `generate-types` | Generate TypeScript types from C# models | |

## Subsystems

### Database (`./catan.ps1 database ...`)

| SubCommand | Purpose |
|------------|---------|
| `doctor` | Diagnose connection, schema, and seed data |
| `install` | Recreate database file and seed defaults |
| `clean` | Delete local `.db` file |

### Azure (`./catan.ps1 azure ...`)

| SubCommand | Purpose |
|------------|---------|
| `install` | Provision App Service, SQL Server, identities |
| `deploy` | Publish code to Azure |
| `doctor` | Check cloud resource health |
| `clean` | Tear down all resources |

### Recording (`./catan.ps1 recording ...`)

| SubCommand | Purpose |
|------------|---------|
| `list` | Show saved recordings |
| `replay` | Re-run recorded game for verification |
| `save` | Export recording |
| `load` | Import recording |
| `delete` | Remove recording |

### Dependencies (`./catan.ps1 dependencies ...`)

| SubCommand | Purpose |
|------------|---------|
| `doctor` | Check status of required tools |
| `install` | Attempt to install missing tools |

## Key Flags

| Flag | Purpose |
|------|---------|
| `-Network` | Bind to `0.0.0.0` (accessible from other LAN devices) |
| `-Razor` | Build Blazor WebUI instead of React UI |
| `-Desktop` | Include WinUI 3 desktop app |
| `-Force` | Bypass confirmations and caches |
| `-Yes` | Skip confirmation prompts |
| `-TraceLevel` | Set logging: `ERROR`, `WARN`, `INFO`, `DEBUG` |
| `-Json` | Output as JSON (where supported) |

## Service Ports

| Service | Port |
|---------|------|
| GameService | 8080 |
| React UI | 3000 |
| WebUI (Blazor) | 5296 |

## Platform Specifics

- **Windows:** Uses `Start-Process` to spawn separate terminal windows
- **macOS:** Uses `osascript` (AppleScript) to spawn new Terminal tabs

## Helper Scripts

| Script | Purpose |
|--------|---------|
| `.scripts/cli_e2e.ps1` | End-to-end automation scenarios |
| `.scripts/update-test-files.ps1` | Batch update recording files |
| `.scripts/catan-azure.ps1` | Azure deployment orchestration |
| `.scripts/setup-github-actions-azure.ps1` | Azure OIDC setup for GitHub Actions |

## Catan3.CLI Project

**Directory:** `Catan3.CLI/`

Command-line harness for integration testing and automation.
Connects to a running GameService instance and drives the shared
`GameStateMachine` end-to-end over SignalR.

### CLI Commands

| Command | Purpose |
|---------|---------|
| `expansion` / `regular` | Run automated game with options |
| `test --mvvm-objects` | Validate message DTO serialization |
| `extract` | Extract GameModel from `.catan` archives |

### Game Runner Options

| Option | Purpose |
|--------|---------|
| `--player-count` | Number of players |
| `--run-to` | Stop at first matching GameState |
| `--complete` | Run full game script |
| `--no-exit` | Keep session alive after completion |
| `--log-level` | Logging verbosity |
| `--uri` | GameService endpoint |

### Architecture

- Uses .NET Generic Host for DI (singleton `GameRunner`, logger)
- `System.CommandLine` for parsing
- Communicates via `GameServiceProxy` (shared library)
- Doubles as CI smoke-test tool

### Extract Command

`ExtractCommand` extracts a `GameModel` snapshot from `.catan`
archives (`Log<string>` compressed payloads). With `--actions` flag,
builds `.catan_test` files for replay testing.

## TypeScript Type Generation

**Verb:** `generate-types`

Runs `TypeGenRunner` to regenerate TypeScript types from C# models.
See [serialization.md](serialization.md) for the full pipeline.
