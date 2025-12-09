# Azure Deployment Design

This document describes the Azure deployment strategy for the Catan3 application, including
the `catan-azure.ps1` management script.

## Design Principles

### Zero Azure Dependencies for Local Development

The inner development loop has **zero dependencies on Azure**:

- Local development uses SQLite database stored in `Catan3.GameService/Data/`
- All features work completely offline
- `./webui.ps1 run` starts everything locally
- Azure deployment is a separate, explicit action

### Test in Production

No separate environments. We deploy directly to production and test there. Keep it simple.

## Architecture Overview

```text
┌─────────────────────────────────────────────────────────────────┐
│                        Azure Resource Group                      │
│                           (rg-catan)                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────┐    ┌──────────────────┐                  │
│  │  App Service     │    │  App Service     │                  │
│  │  (game-service)  │◄──►│  (ui)            │                  │
│  └────────┬─────────┘    └────────┬─────────┘                  │
│           │                       │                             │
│           ▼                       ▼                             │
│  ┌──────────────────┐   ┌──────────────────┐                   │
│  │  Azure Storage   │   │  App Insights    │                   │
│  │  (database)      │   │  (monitoring)    │                   │
│  └──────────────────┘   └──────────────────┘                   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

## Azure Resources

### Required

| Resource | Purpose | Cost |
|----------|---------|------|
| Resource Group | Container for all resources | Free |
| App Service Plan (B1) | Shared compute for both apps | ~$13/mo |
| App Service (GameService) | Backend API + SignalR | Included in plan |
| App Service (WebUI) | Blazor WASM frontend | Included in plan |
| Storage Account + Container | SQLite blob storage | ~$0.01/mo |

### Optional (Recommended)

| Resource | Purpose | Cost |
|----------|---------|------|
| Application Insights | Logging, metrics, alerts | Free up to 5GB/mo |

### Included Automatically

- **HTTPS certificates** for `*.azurewebsites.net` domains
- **Dynamic IP addresses** managed by Azure
- **Basic DDoS protection**

## SQLite Strategy for Azure

SQLite database stored in Azure Blob Storage (~10MB expected size).

- On App Service startup: download DB from blob to local ephemeral storage
- On save operations: upload DB back to blob storage
- Simple, cost-effective, works with existing SQLite code

## Azure Metadata File

Azure resource metadata stored in `.azure/catan-azure.json` (checked into repo, no PII):

```json
{
  "baseName": "catan",
  "resourceGroup": "rg-catan",
  "location": "westus2",
  "storageAccount": "stcatan",
  "storageContainer": "data",
  "gameService": {
    "appServicePlan": "asp-catan",
    "appName": "catan-api",
    "url": "https://catan-api.azurewebsites.net"
  },
  "ui": {
    "appName": "catan",
    "url": "https://catan.azurewebsites.net"
  }
}
```

This file is updated by `install` commands and read by other commands.

## Script Design: catan-azure.ps1

### Command Structure

```powershell
./catan-azure.ps1 <noun> <verb>
```

### Nouns and Verbs

| Noun | Description |
|------|-------------|
| `ui` | WebUI Blazor application |
| `database` | SQLite database in blob storage |
| `game-service` | GameService ASP.NET Core API |

| Verb | Description |
|------|-------------|
| `install` | Create Azure resources (idempotent) |
| `deploy` | Deploy code/data to Azure |
| `doctor` | Check health and status |
| `clean` | Delete Azure resources |

### Authentication

Before any Azure operation, the script checks if user is logged in:

```powershell
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Not logged into Azure. Please run:" -ForegroundColor Red
    Write-Host "  az login" -ForegroundColor Yellow
    exit 1
}
```

### Command Examples

```powershell
# Individual resource commands (from project root)
./.scripts/catan-azure.ps1 ui install
./.scripts/catan-azure.ps1 database deploy
./.scripts/catan-azure.ps1 game-service doctor
```

## Coordinated Commands via webui.ps1

The `webui.ps1` script provides an `azure` noun that coordinates across all resources:

```powershell
# Install everything (game-service, database, ui)
./webui.ps1 azure install

# Deploy everything
./webui.ps1 azure deploy

# Check health of all resources
./webui.ps1 azure doctor

# Clean up everything
./webui.ps1 azure clean
```

### Coordination Order

| Verb | Execution Order |
|------|-----------------|
| `install` | game-service → database → ui |
| `deploy` | database → game-service → ui |
| `doctor` | game-service → database → ui (parallel ok) |
| `clean` | ui → database → game-service (reverse order) |

The `install` order ensures dependencies are created first (game-service before ui needs its URL).
The `clean` order removes dependents before dependencies.

## Verb Details

### install (Idempotent)

Creates Azure resources if they don't exist. Safe to run multiple times.

```text
For each resource:
1. Check if resource exists
2. If exists: verify configuration, update if needed
3. If not exists: create resource
4. Update .azure/catan-azure.json with resource details
5. Run doctor to verify health
```

### deploy

Deploys application code or data to existing Azure resources.

```powershell
# ui deploy
# - Build WebUI in Release mode
# - Publish to output folder
# - Deploy to Azure App Service

# game-service deploy
# - Build GameService in Release mode
# - Publish to output folder
# - Deploy to Azure App Service

# database deploy
# - Upload local SQLite DB to blob storage
# - Overwrites existing blob
```

### doctor

Checks health of Azure resources. Supports multiple output formats.

```powershell
# Human-readable (default)
./catan-azure.ps1 game-service doctor

# JSON output for scripts
./catan-azure.ps1 game-service doctor -Json

# PowerShell hashtable for internal use
./catan-azure.ps1 game-service doctor -HashTable
```

#### Doctor Output Examples

Human-readable:

```text
GameService Health Check
========================
Resource: app-catan3-gameservice
Status: OK
URL: https://app-catan3-gameservice.azurewebsites.net
Health Endpoint: healthy
```

JSON (`-Json`):

```json
{
  "resource": "game-service",
  "name": "app-catan3-gameservice",
  "status": "ok",
  "url": "https://app-catan3-gameservice.azurewebsites.net",
  "healthy": true,
  "timestamp": "2025-12-08T10:30:00Z"
}
```

HashTable (`-HashTable`):

```powershell
@{
    Resource = "game-service"
    Name = "app-catan3-gameservice"
    Status = "ok"
    Url = "https://app-catan3-gameservice.azurewebsites.net"
    Healthy = $true
}
```

### clean

Deletes Azure resources. Prompts for confirmation unless `-Force` is specified.

```powershell
# With confirmation prompt
./catan-azure.ps1 game-service clean

# Skip confirmation (for scripts)
./catan-azure.ps1 game-service clean -Force
```

## Resource Naming Convention

### Name Discovery Process

On every `install`, the script discovers/validates the base name:

```text
1. Read baseName from .azure/catan-azure.json (if exists)
2. If baseName exists:
   a. Check if resources still exist with that name
   b. If yes: use it (idempotent)
   c. If no (deleted or taken): fall through to discovery
3. Discovery: try names until one is available
   a. catan
   b. catangame
   c. catan-{random4}
4. Update .azure/catan-azure.json with chosen baseName
```

This handles:

- Fresh installs (no JSON file)
- Re-running install (idempotent, uses existing name)
- Name was taken by someone else (discovers new name)
- Resources were deleted (re-discovers available name)

| Resource Type | Pattern | Example |
|--------------|---------|---------|
| Resource Group | `rg-{base}` | `rg-catan` |
| Storage Account | `st{base}` | `stcatan` |
| Blob Container | `data` | `data` |
| App Service Plan | `asp-{base}` | `asp-catan` |
| GameService App | `{base}-api` | `catan-api` |
| WebUI App | `{base}` | `catan` |

Target URLs:

- **WebUI**: `https://catan.azurewebsites.net`
- **GameService**: `https://catan-api.azurewebsites.net`

## File Structure

```text
Catan3/
├── .azure/
│   └── catan-azure.json      # Azure resource metadata (checked in)
├── .scripts/
│   ├── utility-scripts.psm1  # Common logging/utility functions
│   └── catan-azure.ps1       # Azure management script
├── webui.ps1                 # Main dev script (calls .scripts/catan-azure.ps1)
└── ...
```

## Implementation Order

### Phase 1: Core Infrastructure

1. Create script skeleton with noun/verb routing
2. Implement login check
3. Implement `game-service install` (resource group, app service plan, app service)
4. Implement `game-service doctor`
5. Implement `game-service deploy`
6. Implement `game-service clean`

### Phase 2: Database

1. Implement `database install` (storage account, container)
2. Implement `database deploy` (upload SQLite to blob)
3. Implement `database doctor` (check blob exists, size)
4. Implement `database clean`
5. Modify GameService to download DB on startup

### Phase 3: UI

1. Implement `ui install`
2. Implement `ui deploy`
3. Implement `ui doctor`
4. Implement `ui clean`

## Usage Workflow

### Simple (Coordinated)

```powershell
# First time setup
./webui.ps1 azure install

# Verify everything is healthy
./webui.ps1 azure doctor

# Deploy after code changes
./webui.ps1 azure deploy

# Tear everything down
./webui.ps1 azure clean
```

### Advanced (Individual Resources)

```powershell
# Deploy only game-service after backend changes
./.scripts/catan-azure.ps1 game-service deploy

# Upload local database to Azure
./.scripts/catan-azure.ps1 database deploy

# Check just the UI health
./.scripts/catan-azure.ps1 ui doctor

# Verbose output for debugging
./.scripts/catan-azure.ps1 game-service install -TraceLevel DEBUG
```

## Future Enhancements

### Progress Indicators for Long-Running Commands

**Status**: TODO

Long-running Azure CLI commands (like `az webapp deploy` which takes 3-5 minutes) currently
show no progress. Add `Invoke-AzCommandWithProgress` function using the pattern from
`utility-scripts.psm1:Invoke-BackgroundInstaller`:

- Show periodic STATUS messages with elapsed time
- Keep full command logged at DEBUG for copy/paste debugging
- Use `Start-Process` with output redirection for async monitoring

Example output:

```text
[DEBUG] az webapp deploy --name catan-api --resource-group rg-catan --src-path "..." --type zip
[STATUS] Deploying GameService... (15s elapsed)
[STATUS] Deploying GameService... (1.2m elapsed)
[DEBUG]   completed in 3.5 min
```
