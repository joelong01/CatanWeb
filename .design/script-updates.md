# Script Infrastructure Modernization

**Date:** 2026-03-29
**Status:** Design — awaiting approval

## Problem Statement

The `.scripts/` directory contains 30 PowerShell scripts with significant inconsistencies
in parameter interfaces, logging, error handling, and Azure support. Several scripts are
broken or hang during execution. The 3700-line `catan-azure.ps1` mega-script mixes four
different resource nouns into a single file with a nested noun/verb dispatch that doesn't
match the rest of the architecture. The codebase has grown organically and needs
standardization to be maintainable.

### Critical: Azure Install Hangs

`./catan.ps1 azure install` hangs indefinitely at "Checking Application Insights."
Root cause: `Invoke-AzCommand` uses `Invoke-Expression` with no timeout. When
`az monitor app-insights component show` hangs (known Azure CLI issue), the entire
install flow stalls with no recovery.

## Architecture Principles

### 1. catan.ps1 is the only entry point users need

Sub-scripts in `.scripts/` are implementation details. Users run `./catan.ps1 <noun> <verb>`.
Direct sub-script calls are for debugging only.

### 2. Scripts are the control plane for inner loop and outer loop

- **Inner loop**: `run`, `stop`, `build`, `test`, `lint`, `format` — local dev cycle
- **Outer loop**: `game-service install -Azure`, `ui deploy -Azure` — cloud infrastructure
- Same tool, same patterns, different targets

### 3. Noun = script file, verb = positional arg, target = switch

Every resource is a single script. Azure vs local is a `-Azure` switch, not a separate file.
The user thinks "I want to do X to the database" — the resource is the primary concept,
the target is a modifier.

```powershell
./catan.ps1 database doctor            # local (default)
./catan.ps1 database doctor -Azure     # azure
./catan.ps1 game-service install -Azure
./catan.ps1 ui deploy -Azure
```

### 4. Three verbs, no ambiguity

| Verb | Contract |
|------|----------|
| `doctor` | **Read-only.** Returns status hashtable. Never modifies state. |
| `install` | **Idempotent.** Checks state first, creates what's missing, repairs what's broken. |
| `clean` | **Destructive.** Removes the resource. Requires `-Yes` or prompts. |

Additional verbs for Azure-capable resources:

| Verb | Contract |
|------|----------|
| `deploy` | **Code push.** Calls doctor first; fails if resource doesn't exist (requires `install`). Builds unless `-NoBuild`. Pushes code to Azure. Returns `$true`/`$false`. Supports `-Slot staging`. Always pushes — not idempotent like `install`. |

Build/quality scripts (`build`, `lint`, `format`, `test`) are verb-scripts — the script
name IS the verb. They don't follow the noun pattern.

### 5. Doctor is the composition primitive

Every resource script implements `doctor` returning a standard hashtable:

```powershell
@{
    Name    = "dotnet"
    Target  = "Local"         # "Local" | "Azure"
    Status  = "Installed"     # Installed | NotInstalled | NeedsFix | Error
    Version = "9.0.100"       # or $null
    Message = "..."           # human-readable summary
    Details = @{}             # script-specific extra data (optional)
}
```

The `Target` field makes results self-describing — aggregators don't need to track which
`-Azure` flag was passed to each child script.

Parent scripts call `<noun>.ps1 doctor -HashTable` to aggregate results. This is how
`dependencies.ps1` builds the summary table and how `catan.ps1 doctor` knows the full
system state.

### 6. Three output modes, always

- **Default**: human-readable via Write-Log (for humans at a terminal)
- **`-HashTable`**: raw PowerShell object (for script composition)
- **`-Json`**: serialized JSON (for external tools / debugging)

### 7. Standard parameters

Every script that imports `utility-scripts.psm1`:

```powershell
param(
    [Parameter(Position = 0)]
    [ValidateSet("doctor", "install", "clean", "help")]
    [string]$Verb = "help",

    [ValidateSet("ERROR", "WARN", "INFO", "DEBUG")]
    [Alias("LogLevel")]
    [string]$TraceLevel = "INFO",

    [switch]$Yes,              # Skip confirmation prompts
    [switch]$Force,            # Override safety checks
    [switch]$HashTable,        # Return doctor result as hashtable
    [switch]$Json,             # Return doctor result as JSON
    [switch]$Help              # Show usage (alias for help verb)
)
```

Azure-capable resources add:

```powershell
    [switch]$Azure,            # Target Azure (default: local)
```

And extend ValidateSet with `"deploy"`:

```powershell
    [ValidateSet("doctor", "install", "clean", "deploy", "help")]
```

Deploy also accepts:

```powershell
    [switch]$NoBuild,          # Skip build step before deploying
    [string]$Slot              # Deployment slot: "staging" or "production" (default)
```

### 8. TraceLevel controls all verbosity

`$PSDefaultParameterValues = @{ 'Write-Log:TraceLevel' = $TraceLevel }` propagates
automatically through every Write-Log call. Four levels:

| Level | Shows | Use for |
|-------|-------|---------|
| ERROR | Errors only | CI pipelines |
| WARN | Errors + warnings | Quiet operation |
| INFO | Normal output (default) | Interactive use |
| DEBUG | Everything + timestamps | Troubleshooting |

### 9. Configuration derived from baseName

`.azure/catan-azure.json` stores one key value: `baseName`. All resource names are derived
by convention via `Get-AzureResourceNames`:

```text
baseName = "catan"
→ rg-catan, asp-catan, catan-api, ai-catan, st-catan, ...
```

No duplication. One function. Convention over configuration.

### 10. External calls must have timeouts

Every `az` CLI call, `Invoke-RestMethod`, `dotnet build` — must have a timeout. No
hanging forever. Failed calls return clean errors with actionable next steps.

### 11. Invoke-AzCommand is the Azure CLI wrapper

All `az` CLI calls go through `Invoke-AzCommand` which provides:

- **Command echo at DEBUG**: prints the full `az ...` command for copy-paste debugging
- **Timeout**: kills hung commands after configurable seconds (default 120s)
- **`-Check` parameter**: for existence probes where not-found is expected (not an error).
  Returns `$null` silently instead of red error text. Replaces `-FailOnError $false`.
- **Response suppression**: JSON blobs only shown at DEBUG, not INFO

### 11. Best-of-class PowerShell

- **Idempotent**: `install` checks state via `doctor` before making changes
- **Composable**: doctor returns structured data, not just console output
- **Testable**: pure functions where possible, side effects isolated
- **Reliable**: timeouts, retry with backoff, clean error messages
- **Cross-platform**: `$IsWindows`/`$IsMacOS`/`$IsLinux` detection where needed

## Target File Structure

### Current → Target mapping

```text
.scripts/
├── utility-scripts.psm1       # Shared module (Write-Log, timeouts, Azure config)
├── utility-scripts.psd1       # Module manifest
│
│   ── Resource scripts (noun = file, verb = positional arg) ──
├── dotnet.ps1                 # .NET SDK (local only)
├── node.ps1                   # Node.js + npm (local only)
├── docker.ps1                 # Docker Desktop (local only)
├── claude-cli.ps1             # Claude Code CLI (local only)
├── vcpp-debug.ps1             # VC++ debug runtime (Windows only)
├── database.ps1               # CosmosDB (-Azure / local emulator) [exists, good shape]
├── game-service.ps1           # GameService app (-Azure / local dotnet run) [NEW: from catan-azure.ps1]
├── ui.ps1                     # React UI app (-Azure / local next dev) [NEW: from catan-azure.ps1]
├── github.ps1                 # GitHub OIDC setup (Azure only) [NEW: from catan-azure.ps1]
│
│   ── Orchestrators ──
├── dependencies.ps1           # Aggregates doctor from all resource scripts
│
│   ── Verb scripts (script name = verb, no noun) ──
├── build.ps1                  # Build + test [merge build.ps1 + build_worker.ps1]
├── lint.ps1                   # Linting [absorb lint-all.ps1]
├── format.ps1                 # Auto-formatting
│
│   ── One-off / migration tools (not user-facing) ──
├── convert-write-host.ps1     # Write-Host → Write-Log migration tool
├── export-sql.ps1             # SQL → JSON export (migration)
├── transform-to-cosmos.ps1    # JSON → CosmosDB transform (migration)
├── Add-HexClip.ps1            # SVG hex clip-path tool
├── themes.ps1                 # Theme validation
└── cli_e2e.ps1                # End-to-end CLI test harness
```

### Scripts to remove

| Script | Reason |
|--------|--------|
| `catan-azure.ps1` | Split into game-service.ps1, ui.ps1, github.ps1 |
| `build_worker.ps1` | Merge into build.ps1 |
| `lint-all.ps1` | Redundant with `lint.ps1 -All` |
| `run-tests-clean.ps1` | Covered by `./catan.ps1 test` |
| `run-tests-quiet.ps1` | Covered by `./catan.ps1 test` |
| `run-game-service.ps1` | Covered by `./catan.ps1 run` |
| `setup-nodejs-and-mermaid.ps1` | Covered by `node.ps1 install` |
| `setup-github-actions-azure.ps1` | Absorbed into github.ps1 |
| `generate-diagrams.ps1` | Low-use; keep as-is or remove |
| `check-diagram-status.ps1` | Low-use; keep as-is or remove |
| `test-scripts.ps1` | Rework after restructure |
| `catan-cicd.ps1` | Deploy logic moves into noun scripts; orchestration into catan.ps1 |

### Key structural change: Break apart catan-azure.ps1

The 3700-line `catan-azure.ps1` currently handles four nouns (game-service, ui, database,
github) with a nested `switch ($Noun) { switch ($Verb) { } }` pattern.

**Split into:**

- **game-service.ps1**: App Service provisioning, deployment, App Insights, managed identity,
  doctor health checks. Handles both local (`dotnet run` on port 8080) and Azure
  (`az webapp create/deploy`).
- **ui.ps1**: React/Next.js static web app. Local is `next dev` on port 3000. Azure is
  `az webapp create/deploy` with staging slots.
- **github.ps1**: One-time OIDC setup for GitHub Actions. Azure only. Simple script.
- **database.ps1**: Already exists and handles `-Azure`. Absorb any remaining Azure DB
  logic from catan-azure.ps1.

Shared Azure helpers (`Invoke-AzCommand`, `Get-AzureConfig`, `Get-AzureResourceNames`,
`Save-AzureConfig`) stay in `utility-scripts.psm1`.

### catan.ps1 dispatch simplification

After the restructure, the main dispatch in `catan.ps1` becomes:

```powershell
switch ($Verb) {
    "run"          { Build; InitDB; StartGameService; StartReactUI }
    "stop"         { StopServices }
    "build"        { & "$scripts/build.ps1" @passthrough }
    "test"         { & "$scripts/build.ps1" -Test @passthrough }
    "lint"         { & "$scripts/lint.ps1" @passthrough }
    "format"       { & "$scripts/format.ps1" @passthrough }
    "doctor"       { & "$scripts/dependencies.ps1" doctor @passthrough }
    "install"      { & "$scripts/dependencies.ps1" install @passthrough }
    "clean"        { & "$scripts/dependencies.ps1" clean @passthrough }
    "database"     { & "$scripts/database.ps1" $SubCommand @passthrough }
    "game-service" { & "$scripts/game-service.ps1" $SubCommand @passthrough }
    "ui"           { & "$scripts/ui.ps1" $SubCommand @passthrough }
    "github"       { & "$scripts/github.ps1" $SubCommand @passthrough }
    "help"         { ShowHelp }
}
```

No more inline implementations of stats, recordings, etc. in catan.ps1. Those move into
the appropriate noun script or stay as thin pass-throughs.

## Prioritized Issue List

### P0 — Blocking (scripts don't work)

- **#126** Add -Check and timeout to Invoke-AzCommand
- **#113** Application Insights install hangs indefinitely
- **#114** PR #109 review fixes (-f bug, Gray→DEBUG, Write-Host in utilities)

### P1 — Architecture (structural changes)

- **#129** Create _template.ps1 reference implementation
- **#124** Move Invoke-AzCommand and Azure helpers to utility-scripts.psm1
- **#123** Break catan-azure.ps1 into game-service.ps1, ui.ps1, github.ps1
- **#125** Merge build_worker.ps1 into build.ps1
- **#117** Standardize verb pattern (positional Verb, not switch params)
- **#127** Simplify catan.ps1 dispatch after restructure
- **#128** Write README.md for script usage

### P2 — Consistency (logging, params, output)

- **#115** Migrate build scripts to Write-Log
- **#116** Migrate lint/format scripts to Write-Log
- **#118** Standardize Doctor output shape
- **#119** Extract shared Write-Section utility
- **#122** Audit Gray→DEBUG log level conversions

### P3 — Cleanup

- **#120** Consolidate/remove redundant scripts
- **#121** Remove lint-all.ps1

## Implementation Strategy

### Phase 1: Fix What's Broken (P0)

Unblock `./catan.ps1 azure install`. Add timeouts, fix bugs.

### Phase 2: Restructure (P1)

This is the big change. Order matters:

1. Move Azure helpers (`Invoke-AzCommand`, config functions) to utility module
2. Create `game-service.ps1` by extracting from catan-azure.ps1
3. Create `ui.ps1` by extracting from catan-azure.ps1
4. Create `github.ps1` by extracting from catan-azure.ps1
5. Merge build scripts
6. Standardize verb pattern across resource scripts
7. Simplify catan.ps1 dispatch
8. Delete catan-azure.ps1 and other removed scripts
9. Write README.md

### Phase 3: Polish (P2/P3)

Migrate remaining scripts to Write-Log, standardize output shapes, remove dead scripts.

## Verification

1. `./catan.ps1 doctor` — all checks pass locally
2. `./catan.ps1 doctor -Azure` — all checks pass against Azure
3. `./catan.ps1 azure install` → `./catan.ps1 game-service install -Azure` completes
4. `./catan.ps1 build` — passes
5. `./catan.ps1 help` — shows all nouns and verbs
6. `./catan.ps1 game-service doctor -HashTable` — returns standard shape
7. `./catan.ps1 game-service doctor -Json` — returns valid JSON
8. Each noun script works standalone: `.scripts/database.ps1 doctor -HashTable`
