# Catan Scripts

All development and deployment tasks go through `./catan.ps1`.
Scripts in `.scripts/` are the implementation — callable directly for debugging.

## Quick Start

```powershell
./catan.ps1 run              # Build, start services, open browser
./catan.ps1 doctor           # Check if everything is healthy
./catan.ps1 build            # Build without running
./catan.ps1 test             # Build and run all tests
```

## Commands

### Development (inner loop)

| Command | Purpose |
|---------|---------|
| `run` | Build, start GameService + React UI, open browser |
| `run -Network` | Same, but accessible from other devices on LAN |
| `stop` | Stop running services |
| `restart` | Stop and restart |
| `update` | Rebuild and restart (when hot reload fails) |
| `build` | Build .NET + generate TypeScript types |
| `test` | Build and run all tests (.NET + TypeScript) |
| `lint` | Lint and spell check (PS, TS, MD) |
| `format` | Auto-format code |
| `clean` | Stop services, clean build artifacts |

### Setup

| Command | Purpose |
|---------|---------|
| `doctor` | Check dependencies and database health |
| `doctor -Azure` | Check all Azure resources (GameService, DB, UI) |
| `doctor -Azure -Staging` | Check Azure staging slots |
| `install` | Install dependencies + local Cosmos emulator |
| `install -Azure` | Create Azure infrastructure + deploy code |
| `deploy -Azure` | Push code to Azure production (fast, skips infra) |
| `deploy -Azure -Staging` | Push code to Azure staging slots |

### Noun Commands

Resources are managed with noun scripts. Each supports `doctor`, `install`,
`deploy`, `clean`, and `help`.

```powershell
./catan.ps1 game-service doctor -Azure     # Check GameService health
./catan.ps1 ui deploy -Azure -Staging      # Deploy React app to staging
./catan.ps1 database doctor -Azure         # Check CosmosDB health
./catan.ps1 github install -Azure          # Set up GitHub Actions OIDC
```

Each noun script works standalone too:

```powershell
.scripts/game-service.ps1 doctor           # Local: check port 8080
.scripts/game-service.ps1 doctor -Azure    # Azure: check App Service
.scripts/database.ps1 doctor               # Local: check Cosmos emulator
.scripts/database.ps1 doctor -Azure        # Azure: check CosmosDB account
```

### Database

| Command | Purpose |
|---------|---------|
| `database doctor` | Check local Cosmos emulator |
| `database doctor -Azure` | Check Azure CosmosDB |
| `database install` | Install local emulator + seed data |
| `database install -Azure` | Create CosmosDB account + seed data |
| `database clean` | Delete local emulator |
| `database clean -Azure` | Delete Azure CosmosDB account |

## Architecture

### Verb Contract

| Verb | Behavior |
|------|----------|
| `doctor` | **Read-only.** Returns status. Never modifies state. |
| `install` | **Idempotent.** Creates what's missing, repairs what's broken, deploys code. |
| `deploy` | **Code push.** Builds and pushes to Azure. Requires `install` first. |
| `clean` | **Destructive.** Removes the resource. Prompts unless `-Yes`. |

### Standard Flags

| Flag | Purpose |
|------|---------|
| `-Azure` | Target Azure instead of local (default) |
| `-Staging` | Target staging deployment slot |
| `-Force` | Override skip-if-unchanged checks |
| `-Yes` | Skip confirmation prompts |
| `-TraceLevel` | Output verbosity: `ERROR`, `WARN`, `INFO` (default), `DEBUG` |
| `-HashTable` | Return doctor result as PowerShell hashtable |
| `-Json` | Return doctor result as JSON |

### Doctor Composition

Every noun script's `doctor` returns a standard hashtable:

```powershell
@{
    Name    = "game-service"
    Target  = "Local"          # or "Azure"
    Status  = "Installed"      # Installed | NotInstalled | NeedsFix | Error
    Version = "c78dde5"
    Message = "GameService running on port 8080"
    Details = @{}
}
```

Use `-HashTable` for script composition, `-Json` for external tools:

```powershell
$result = .scripts/game-service.ps1 doctor -HashTable
if ($result.Status -ne "Installed") { ... }
```

### File Structure

```text
catan.ps1                          # Entry point — dispatches to .scripts/
.scripts/
  utility-scripts.psm1             # Shared module (Write-Log, Invoke-AzCommand, Azure helpers)
  _template.ps1                    # Copy this to create a new noun script
  game-service.ps1                 # GameService (.NET API + SignalR)
  ui.ps1                           # React UI (Next.js)
  github.ps1                       # GitHub Actions OIDC
  database.ps1                     # CosmosDB (local emulator + Azure)
  dependencies.ps1                 # Aggregates doctor from dotnet, node, docker, etc.
  dotnet.ps1                       # .NET SDK
  node.ps1                         # Node.js + npm
  docker.ps1                       # Docker Desktop
  claude-cli.ps1                   # Claude Code CLI
  build.ps1                        # .NET build + test
  lint.ps1                         # Code quality checks
  format.ps1                       # Auto-formatting
  catan-azure.ps1                  # Azure operations (being migrated to noun scripts)
```

### Configuration

Azure resource names are derived from a single `baseName` in `.azure/catan-azure.json`:

```text
baseName = "catan"
→ rg-catan, asp-catan, catan-api, ai-catan, cosmos-catan, ...
```

`Get-AzureResourceNames` in the utility module is the single source of truth.
No hardcoded resource names in scripts.

### Invoke-AzCommand

All `az` CLI calls go through `Invoke-AzCommand` (in `utility-scripts.psm1`):

- **Timeout**: Default 120s, configurable. Kills hung processes.
- **`-Check`**: For existence probes. Returns `$null` on failure (no error output).
- **DEBUG echo**: Prints the full `az` command at DEBUG level for copy-paste debugging.
- **JSON parsing**: `-JsonOutput` parses the response automatically.

```powershell
# Check if resource exists (expected failure is silent):
$app = Invoke-AzCommand "webapp show --name foo -g rg-foo" -Check -JsonOutput

# Create resource (failure throws):
Invoke-AzCommand "webapp create --name foo -g rg-foo --plan asp-foo" -SuppressOutput

# Long operation with extended timeout:
Invoke-AzCommand "cosmosdb create ..." -SuppressOutput -TimeoutSeconds 300
```

## Troubleshooting

**Scripts hang**: Run with `-TraceLevel DEBUG` to see which `az` command is slow.
All commands have a 120s timeout — if it hangs, it will be killed.

**"Not logged in"**: Run `az login` first.

**Staging cold start**: Staging slots don't have Always On. First request takes 30-60s.
Browse the URL once to wake it up.

**npm ci fails with EPERM**: Close VS Code or other editors that lock `node_modules` files.
The deploy script deletes `node_modules` before `npm ci` to break locks.

**Build fails**: Run `./catan.ps1 doctor` to check dependencies.
