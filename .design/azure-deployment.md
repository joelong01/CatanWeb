# Azure Deployment

**Last verified:** February 12, 2026

## Overview

The application deploys to Azure App Service with Azure SQL
Serverless for persistence. GitHub Actions with OIDC authentication
(no stored secrets) drive all deployments.

**Zero Azure dependencies** for local development -- the app uses
SQLite locally and Azure SQL in production.

**React is the primary UI.** Blazor and Desktop are deprecated.

## Deployment Model

Three environments, two branches, blue-green production:

```text
staging branch ──► GitHub Actions ──► Staging Environment
                                        catan-staging.azurewebsites.net
                                        catan-api-staging.azurewebsites.net
                                        (test here, iterate, verify)
        │
        │  PR to main (when staging looks good)
        ▼
main branch ────► GitHub Actions ──► Production (blue-green)
                                        1. Deploy to INACTIVE slot
                                        2. Swap slots
                                        3. New version serves traffic
                                        4. Old version stays as rollback
```

### Staging

Two ways to deploy to staging:

**Option A: CI/CD (branch-based).** Push to the `staging` branch
triggers GitHub Actions, which builds and deploys automatically.
Use this for the normal dev workflow -- branch off `staging`, make
changes, PR back into `staging`.

**Option B: Script (direct push).** Run the deploy script locally
to push your current working tree to staging without updating the
branch. Use this for quick iteration, debugging, or testing local
changes before committing.

```bash
# Option A: CI/CD -- merge to staging branch, GitHub Actions deploys
git checkout staging
git merge my-feature-branch
git push   # triggers deploy-staging.yml

# Option B: Direct push -- deploy current code without updating the branch
# React UI to staging (defaults to staging GameService)
pwsh ./catan.ps1 azure ui deploy-staging -Force -TraceLevel DEBUG

# GameService to staging (only needed when backend changes)
pwsh ./catan.ps1 azure game-service deploy -Slot staging -Force -TraceLevel DEBUG

# Grant staging slot database access (after fresh slot creation)
pwsh ./catan.ps1 azure database deploy-staging-access -TraceLevel DEBUG
```

- **URL:** `https://catan-staging.azurewebsites.net`
- **GameService:** `https://catan-api-staging.azurewebsites.net`
- **Database:** Shared Azure SQL (same data as production)
- **Purpose:** Verify changes before promoting to production

### Production (Blue-Green)

The `catan` app has two slots. At any given time:

- **Active slot** serves `https://catan.azurewebsites.net` (version N)
- **Inactive slot** holds the previous version (version N-1)

When main deploys:

1. New build (version N+1) deploys to the **inactive** slot
2. `swap-slots` puts N+1 active, N becomes the rollback
3. If something is wrong: `swap-slots` again → N is back, N+1 is
   inactive for debugging

```text
Before deploy:
  Active slot  → version N   (serving traffic)
  Inactive slot → version N-1

After deploy + swap:
  Active slot  → version N+1 (serving traffic)
  Inactive slot → version N   (rollback ready)

Rollback (if needed):
  Active slot  → version N   (restored)
  Inactive slot → version N+1 (for debugging)
```

### Workflow

```text
1. Develop on feature branches
2. Merge to staging → auto-deploy to staging environment
3. Test at https://catan-staging.azurewebsites.net
4. PR staging → main
5. Main deploy → build, deploy to inactive slot, swap
6. Verify at https://catan.azurewebsites.net
7. If broken → swap-slots to rollback
```

## Azure Resources

Configuration stored in `.azure/catan-azure.json`:

| Resource | Name | Type |
| -------- | ---- | ---- |
| Resource Group | `rg-catan` | Resource Group |
| App Service Plan | `asp-catan` | S1 (supports slots) |
| GameService | `catan-api` | App Service (.NET 9) |
| GameService Staging | `catan-api` slot `staging` | Deployment Slot |
| UI | `catan` | App Service |
| UI Staging | `catan` slot `staging` | Deployment Slot (Node.js 22) |
| SQL Server | `sql-catan` | Azure SQL Serverless |
| Database | `catan` | Azure SQL Database |
| Storage | `stcatan` | Storage Account |
| Monitoring | `ai-catan` | App Insights |
| Region | `westus2` | |

### Slot Identity

Each slot gets its **own managed identity** with a different
principal ID. Database access must be granted separately per slot.
App settings and connection strings are NOT inherited.

## Deploy Internals

The scripts use the **Kudu ZIP Deploy API** with Azure AD bearer
tokens (not `az webapp deploy`, which has a
[known hang bug](https://github.com/Azure/azure-cli/issues/29003)).
SCM basic auth is disabled by subscription policy, so bearer tokens
are required. All of this is handled inside `Deploy-KuduZip` in
`catan-azure.ps1` -- developers just run `./catan.ps1` commands.

## Next.js Standalone Packaging

The React UI uses Next.js `output: 'standalone'` mode. The deploy
zip must include:

- `server.js` -- standalone entrypoint
- `.next/` -- compiled pages and server code
- `node_modules/` -- minimal runtime dependencies
- `public/` -- static assets (themes, fonts, images)
- `package.json`

**Critical:** PowerShell's `Compress-Archive -Path "$dir/*"` skips
dotfiles. The `.next` directory must be explicitly included using
`Get-ChildItem -Force`.

App settings to prevent Azure Oryx from overwriting the pre-built
deployment:

- `SCM_DO_BUILD_DURING_DEPLOYMENT=false`
- `ENABLE_ORYX_BUILD=false`

## CI/CD Workflows

### `deploy-staging.yml` -- Staging (React)

| Trigger | Action |
| ------- | ------ |
| Push to `staging` | Deploy React UI to staging slot |
| Manual dispatch | Deploy React UI to staging slot |

Single job: build Next.js standalone, deploy to `catan` staging
slot via Kudu API, verify HTTP 200. Points at staging GameService.

GameService staging is deployed separately via `./catan.ps1` when
backend changes are needed.

### `deploy-azure.yml` -- Production (Blue-Green)

**Status:** Not yet updated. Currently deploys GameService + Blazor
directly to production. Needs to be rewritten to match the
blue-green model below.

| Trigger | Action |
| ------- | ------ |
| Push to `main` | Deploy React to inactive slot, then swap |
| Manual dispatch | Deploy React to inactive slot, then swap |

Steps:

1. Build Next.js standalone
2. Determine which slot is inactive (not serving traffic)
3. Deploy to the inactive slot via Kudu API
4. Verify health on the inactive slot
5. Swap slots -- new version now serves traffic
6. Verify health on the now-active slot

### `deploy-react-staging.yml` -- Legacy

Deploys React to staging on `main` push (react-ui/** changes).
Points at **production** GameService. Will be removed once the
blue-green production workflow is in place.

## Database

| Environment | Database | Provider |
| ----------- | -------- | -------- |
| Local | SQLite | `Data/catan.db` |
| Azure (all slots) | Azure SQL Serverless | Connection string |

All slots share the same database instance. Each slot's managed
identity needs its own access grant via:

```sql
CREATE USER [catan-api/slots/staging] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [catan-api/slots/staging];
ALTER ROLE db_datawriter ADD MEMBER [catan-api/slots/staging];
ALTER ROLE db_ddladmin ADD MEMBER [catan-api/slots/staging];
```

The `./catan.ps1 azure database deploy-staging-access` command
automates this.

## Health Endpoints

| Endpoint | Purpose |
| -------- | ------- |
| `/health` | Service uptime, version, database status |
| `/health?checkDatabase=true` | Full database connectivity check |
| `/api/database/health` | Detailed database diagnostics |

## Scripts

**File:** `.scripts/catan-azure.ps1` (~3400 lines)

Invoked via `./catan.ps1 azure <noun> <verb>`:

| Command | Purpose |
| ------- | ------- |
| `azure game-service install` | Create GameService app + managed identity |
| `azure game-service deploy` | Build and deploy GameService |
| `azure game-service deploy -Slot staging` | Deploy to staging slot |
| `azure database install` | Create SQL Server + database |
| `azure database deploy` | Configure connection strings + DB access |
| `azure database deploy-staging-access` | Grant staging slot DB access |
| `azure ui install` | Create UI app + staging slot |
| `azure ui deploy-staging` | Build and deploy React to staging |
| `azure github install` | Setup GitHub Actions OIDC (app registration, federated credentials, secrets) |
| `azure swap-slots` | Swap active/inactive production slots |
| `azure doctor` | Health check all resources |
| `azure clean` | Remove all Azure resources |

## Recreating from Scratch

If resources get into a bad state, nuke and rebuild. See
[clean-azure-recreation.md](clean-azure-recreation.md).

**Install order** (GameService first): managed identity must exist
before database install can grant it roles.

**Deploy order** (Database first): GameService needs the database
connection on startup.

## Versioning

**Status:** Not yet defined. Currently each deploy stores the git
commit hash and build timestamp as app settings. The `/health`
endpoint reports these as `version.commit` and `version.buildTime`.
A formal versioning scheme (semver tags, build numbers, etc.) has
not been established yet.
