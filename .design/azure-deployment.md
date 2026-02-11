# Azure Deployment

**Last verified:** January 30, 2026

## Overview

The application deploys to Azure App Service with Azure SQL
Serverless for persistence. Deployment is managed by PowerShell
scripts and GitHub Actions CI/CD.

**Zero Azure dependencies** for local development -- the app uses
SQLite locally and Azure SQL in production.

## Architecture

```
GitHub (main branch)
  └── GitHub Actions (deploy-azure.yml)
        └── OIDC Authentication (no stored secrets)
              ├── GameService → catan-api.azurewebsites.net
              ├── WebUI → catan.azurewebsites.net
              └── Azure SQL → sql-catan / catan
```

## Azure Resources

Configuration stored in `.azure/catan-azure.json`:

| Resource | Name | Type |
|----------|------|------|
| Resource Group | `rg-catan` | Resource Group |
| GameService | `catan-api` | App Service |
| WebUI | `catan` | App Service |
| SQL Server | `sql-catan` | Azure SQL Serverless |
| Database | `catan` | Azure SQL Database |
| Storage | `stcatan` | Storage Account |
| Monitoring | `ai-catan` | App Insights |
| Region | `westus2` | |

## Scripts

### Primary Script

**File:** `.scripts/catan-azure.ps1` (~2800 lines)

Invoked via `./catan.ps1 azure <verb>`:

| Command | Purpose |
|---------|---------|
| `azure install` | Create all Azure resources (idempotent) |
| `azure deploy` | Build and deploy GameService + WebUI |
| `azure doctor` | Health check all resources |
| `azure clean` | Remove all Azure resources (with confirmation) |

### Deployment Functions

**Deploy-GameService:**
1. `dotnet publish` GameService in Release mode
2. Create zip package
3. `az webapp deploy --type zip` (async to avoid CLI polling bugs)
4. Store deployment metadata (commit hash, timestamp) as app settings
5. Enable logging for diagnostics

**Deploy-UI:**
1. `dotnet publish` WebUI.Server (hosts Blazor WASM)
2. Remove BlazorDebugProxy (~11 MB optimization)
3. Deploy via zip
4. Track deployment info

**Deploy-Database:**
1. Configure SQL Server connection strings
2. Set up managed identity authentication
3. Verify database connectivity

### Intelligence Features

- **Change detection**: Skips deployment if no changes since last deploy
- **Async deployment**: Uses `--async true` to avoid Azure CLI polling bugs
- **Resource discovery**: Automatic naming pattern fallback
  (`catan` -> `catangame` -> `catan-{random4}`)

### Setup Script

**File:** `.scripts/setup-github-actions-azure.ps1`

Creates Azure AD App Registration with OIDC federated credentials
for GitHub Actions. Eliminates need for stored secrets.

## CI/CD

**File:** `.github/workflows/deploy-azure.yml`

| Trigger | Action |
|---------|--------|
| Push to `main` | Full deploy (GameService + WebUI) |
| Manual dispatch | Full deploy |

**Steps:**
1. Authenticate via Azure OIDC (no secrets)
2. Build all projects
3. Run `./catan.ps1 azure deploy`
4. Fix database connectivity if needed

## Database Strategy

| Environment | Database | Provider |
|-------------|----------|----------|
| Local | SQLite | `Data/catan.db` |
| Azure | Azure SQL Serverless | Connection string in App Settings |

Azure SQL Serverless was chosen over CosmosDB for simplicity:
- Same EF Core code works everywhere
- Connection string switching only
- ~$5-15/month with auto-pause
- No complex DAL abstraction needed

See [proposals.md](proposals.md) for the CosmosDB alternative that
was evaluated and rejected.

## Health Endpoints

| Endpoint | Purpose |
|----------|---------|
| `/health` | Service uptime metadata |
| `/api/database/health` | Database connectivity and stats |

Used by `azure doctor` and provisioning scripts.

## What's Not Implemented

- **Deployment slots** (staging/production swap) -- designed but
  not configured
- **Infrastructure as Code** (Bicep/Terraform) -- planned future
- **CDN/load balancing** -- not needed at current scale
- **Branch-based environments** -- staging branch workflow designed
  but not active
