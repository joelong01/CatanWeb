# Azure Deployment

**Last verified:** February 11, 2026

## Overview

The application deploys to Azure App Service with Azure SQL
Serverless for persistence. Deployment is managed by GitHub Actions
workflows with OIDC authentication (no stored secrets).

**Zero Azure dependencies** for local development -- the app uses
SQLite locally and Azure SQL in production.

## Architecture

```text
GitHub
  ├── main branch
  │     └── deploy-azure.yml
  │           └── OIDC Authentication
  │                 ├── GameService → catan-api.azurewebsites.net (production)
  │                 ├── Blazor UI  → catan.azurewebsites.net (production)
  │                 └── Azure SQL  → sql-catan / catan
  │
  └── staging branch
        └── deploy-staging.yml
              └── OIDC Authentication
                    ├── GameService → catan-api-staging.azurewebsites.net
                    ├── React UI   → catan-staging.azurewebsites.net
                    └── Azure SQL  → sql-catan / catan (shared)
```

## Azure Resources

Configuration stored in `.azure/catan-azure.json`:

| Resource | Name | Type |
| -------- | ---- | ---- |
| Resource Group | `rg-catan` | Resource Group |
| GameService | `catan-api` | App Service |
| GameService Staging | `catan-api` slot `staging` | Deployment Slot |
| WebUI | `catan` | App Service |
| WebUI Staging | `catan` slot `staging` | Deployment Slot (Node.js) |
| SQL Server | `sql-catan` | Azure SQL Serverless |
| Database | `catan` | Azure SQL Database |
| Storage | `stcatan` | Storage Account |
| Monitoring | `ai-catan` | App Insights |
| Region | `westus2` | |

### Staging Slot URLs

| Component | Production URL | Staging URL |
| --------- | -------------- | ----------- |
| GameService | `https://catan-api.azurewebsites.net` | `https://catan-api-staging.azurewebsites.net` |
| UI (Blazor) | `https://catan.azurewebsites.net` | N/A (production only) |
| UI (React) | N/A (after swap) | `https://catan-staging.azurewebsites.net` |

## Deployment Strategy

### Kudu ZIP Deploy API

All deployments use the Kudu REST API directly instead of
`az webapp deploy`. The Azure CLI's `--async true` flag has a
[known bug](https://github.com/Azure/azure-cli/issues/29003) where
it still polls for site startup, hanging for 10+ minutes per deploy.

The Kudu `/api/zipdeploy?isAsync=true` endpoint genuinely returns
202 immediately. We poll the deployment status ourselves with
controlled timeouts.

```bash
# Get publishing credentials
CREDS=$(az webapp deployment list-publishing-credentials \
  --name $APP --resource-group rg-catan --slot staging -o json)
USER=$(echo $CREDS | jq -r .publishingUserName)
PASS=$(echo $CREDS | jq -r .publishingPassword)

# Truly async deploy
curl -X POST --data-binary @app.zip \
  -H "Content-Type: application/zip" \
  -u "$USER:$PASS" \
  "https://${APP}-staging.scm.azurewebsites.net/api/zipdeploy?isAsync=true"
```

### Change Detection

The PowerShell scripts (`catan-azure.ps1`) store `DEPLOY_COMMIT`
and `DEPLOY_BUILD_TIME` as app settings after each deploy. The
doctor commands compare these against the current git commit to
determine if a deploy is needed.

## CI/CD Workflows

### Production: `deploy-azure.yml`

| Trigger | Action |
| ------- | ------ |
| Push to `main` | Full deploy (GameService + Blazor UI + DB fix) |
| Manual dispatch | Full deploy |

**Steps:**

1. Authenticate via Azure OIDC
2. Build all .NET projects
3. Run `./catan.ps1 azure deploy -NoBuild`
4. Fix database connectivity if needed

### Staging: `deploy-staging.yml`

| Trigger | Action |
| ------- | ------ |
| Push to `staging` | Deploy GameService + React UI to staging slots |
| Manual dispatch | Deploy to staging slots |

**Three parallel jobs:**

1. **deploy-gameservice** -- Publish .NET GameService, deploy to
   `catan-api` staging slot via Kudu API, verify `/health`
2. **deploy-react** -- Build Next.js standalone, deploy to `catan`
   staging slot via Kudu API, verify HTTP 200
3. **verify** (after both complete) -- Cross-component health
   checks, database connectivity, print summary

### React Staging (legacy): `deploy-react-staging.yml`

| Trigger | Action |
| ------- | ------ |
| Push to `main` (react-ui/** changes) | Deploy React to staging slot |

Deploys React UI to the `catan` staging slot pointing at
**production** GameService. This tests React changes against the
production backend before swap.

## Database Strategy

| Environment | Database | Provider |
| ----------- | -------- | -------- |
| Local | SQLite | `Data/catan.db` |
| Azure (all slots) | Azure SQL Serverless | Connection string in App Settings |

Azure SQL Serverless was chosen over CosmosDB for simplicity:

- Same EF Core code works everywhere
- Connection string switching only
- ~$5-15/month with auto-pause
- No complex DAL abstraction needed

The staging GameService slot shares the same database as production.
Connection strings are inherited from the parent app. The staging
slot's managed identity requires separate database access grants
(handled automatically by the staging workflow).

See [proposals.md](proposals.md) for the CosmosDB alternative that
was evaluated and rejected.

## Staging Slot Database Access

Each deployment slot gets its own managed identity with a different
principal ID. The `deploy-staging.yml` workflow grants database
access automatically:

1. Get staging slot principal ID
2. Create temporary firewall rule for GitHub runner IP
3. Acquire Azure AD token for SQL
4. Run idempotent SQL to grant access:

   ```sql
   CREATE USER [catan-api/slots/staging] FROM EXTERNAL PROVIDER;
   ALTER ROLE db_datareader ADD MEMBER [catan-api/slots/staging];
   ALTER ROLE db_datawriter ADD MEMBER [catan-api/slots/staging];
   ALTER ROLE db_ddladmin ADD MEMBER [catan-api/slots/staging];
   ```

5. Remove temporary firewall rule

## Health Endpoints

| Endpoint | Purpose |
| -------- | ------- |
| `/health` | Service uptime, version, database status |
| `/health?checkDatabase=true` | Full database connectivity check |
| `/api/database/health` | Detailed database diagnostics |

Used by `azure doctor`, provisioning scripts, and the staging
verification job.

## React Startup Logging

The React UI includes a startup logger (matching the Blazor app's
pattern in `WebUI/wwwroot/index.html` lines 77-209):

- Always logs to `console.log` with `[Loading Xs]` prefix
- Shows visible error overlay only when GameService is unreachable
- Checks `/health` endpoint on page load
- Reports GameService URL, health status, database connectivity

## Scripts

### Primary Script

**File:** `.scripts/catan-azure.ps1` (~3200 lines)

Invoked via `./catan.ps1 azure <verb>`:

| Command | Purpose |
| ------- | ------- |
| `azure install` | Create all Azure resources (idempotent) |
| `azure deploy` | Build and deploy GameService + WebUI |
| `azure doctor` | Health check all resources |
| `azure clean` | Remove all Azure resources (with confirmation) |
| `azure swap-slots` | Swap staging and production UI slots |

### Setup Script

**File:** `.scripts/setup-github-actions-azure.ps1`

Creates Azure AD App Registration with OIDC federated credentials
for GitHub Actions. Eliminates need for stored secrets.

## What's Not Implemented

- **Infrastructure as Code** (Bicep/Terraform) -- planned future
- **CDN/load balancing** -- not needed at current scale
