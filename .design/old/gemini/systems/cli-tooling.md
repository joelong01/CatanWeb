# CLI Tooling As-Built

**Status:** As-Built
**Source:** `catan.ps1`

## Overview

The `catan.ps1` script is the unified entry point for all development, testing, and deployment operations. It abstracts platform differences (Windows vs macOS) and manages the complex interactions between the GameService, Database, and UI frontends.

**Usage:**

```powershell
./catan.ps1 [Verb] [SubCommand] [Flags]
```

## Core Verbs

| Verb | Description | Key Flags |
|---|---|---|
| **run** | Builds and starts the full stack (Service + UI). Default: React UI. Hot-reload enabled. | `-Network` (0.0.0.0 binding), `-Razor` (Use Blazor), `-Desktop` (Launch WinUI) |
| **stop** | Terminates all running services (GameService, WebUI, ReactUI). | |
| **restart** | Stops then runs services. | |
| **update** | Rebuilds and restarts services (for when hot-reload fails). | `-Terminate` (Close terminals on macOS) |
| **build** | Compiles the solution. Default: Service + React. | `-Desktop` (Include WinUI) |
| **test** | Runs all unit and integration tests. | |
| **clean** | Removes build artifacts (`/bin`, `/obj`). Preserves DB. | |
| **doctor** | Validates environment health (Node, Dotnet, DB, Ports). | |
| **install** | Installs all dependencies (npm, nuget) and seeds DB. | |
| **lint** | Function: Format, lint, and spell check (PS, TS, MD). | |
| **generate-types** | Generates TypeScript types from C# models (TypeGen). | |

## Subsystems

### Database (`./catan.ps1 database ...`)

Wraps local SQLite and Azure SQL management.

* `doctor`: Diagnoses connection, schema validity, and seed data.
* `install`: Re-creates the DB file and seeds default data.
* `clean`: Deletes the local `.db` file.

### Azure (`./catan.ps1 azure ...`)

Orchestrates cloud deployment via `.scripts/catan-azure.ps1`.

* `install`: Provisions App Service, SQL Server, Managed Identities.
* `deploy`: Publishes code/binaries to Azure.
* `doctor`: Checks cloud resource health and connectivity.
* `clean`: Tears down all resources.

### Recording (`./catan.ps1 recording ...`)

Manages game replays for regression testing.

* `list`: Shows saved recordings.
* `replay`: Re-runs a recorded game against the current engine to verify consistency.
* `save/load`: Exports/Imports recording blobs.

### dependencies (`./catan.ps1 dependencies ...`)

Manages external tool dependencies.

* `doctor`: Checks status of required tools (dotnet, node, sqlcmd, etc).
* `install`: Attempts to install missing tools.

## Key Flags Reference

* `-Network`: Binds services to `0.0.0.0` allowing access from other devices (e.g., phones on LAN).
* `-Force`: Bypasses confirmation prompts or caches.
* `-TraceLevel`: Sets logging verbosity (`INFO`, `DEBUG`, `WARN`, `ERROR`).
* `-Json`: Outputs command results as JSON (where supported) for parsing.

## Platform Specifics

* **Windows**: Uses `Start-Process` to spawn separate windows for Service/UI.
* **macOS**: Uses `osascript` (AppleScript) to spawn new Terminal tabs.

## Helper Scripts (.scripts/)

| Script | Purpose |
|---|---|
| `cli_e2e.ps1` | Specialized automation for End-to-End scenarios testing via the CLI. |
| `update-test-files.ps1` | Batch update of recording files after format changes. |

