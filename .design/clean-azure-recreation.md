# Clean Azure Recreation

**Status:** Design review
**Date:** February 12, 2026

## Problem

The Azure staging deployment is broken in multiple ways:

1. **Kudu SCM basic auth disabled** by subscription policy — fixed by
   switching to bearer token auth, but the staging slot is now in a
   bad state (container crash-looping, SCM endpoint unreachable)
2. **Staging slot was created without configuration** — missing
   connection strings, app settings, managed identity DB grants
3. **Deploy order wrong** — GameService was deployed before the
   database was configured, so it crashed on startup trying to
   connect to a DB it couldn't reach
4. **Production also unreachable** — likely due to resource contention
   on the S1 plan with a broken staging slot consuming the single
   worker

Rather than debug accumulated configuration drift, we wipe everything
and recreate from scratch with the correct order and settings.

## Plan

### Step 1: Delete resource group

```bash
az group delete --name rg-catan --yes --no-wait
```

This destroys everything: App Service Plan, both web apps (production

+ staging slots), SQL Server + database, storage account, App
Insights. There is no data worth preserving — the database is
unreachable and all game data can be regenerated from defaults.

Wait for deletion to complete (~2-5 minutes).

### Step 2: Recreate infrastructure (install)

Use the existing scripts in the correct dependency order:

```bash
# 1. Resource group + App Service Plan + GameService web app
#    Creates managed identity — principal ID needed by step 2
./catan.ps1 azure game-service install -TraceLevel DEBUG

# 2. SQL Server + database (creates server, serverless DB, firewall rules)
#    Grants GameService managed identity "SQL Server Contributor" role
./catan.ps1 azure database install -TraceLevel DEBUG

# 3. UI web app + staging slot (upgrades plan B1→S1 for slot support)
./catan.ps1 azure ui install -TraceLevel DEBUG
```

**Why GameService before Database?** Install only creates the
resources — no code is deployed yet. The GameService managed identity
must exist before `Install-Database` can grant it database roles.
The database must be **deployed** (Step 3) before GameService code
is **deployed** — that's where the runtime dependency matters.

### Step 3: Deploy in dependency order

**Database first** — GameService needs the DB connection on startup:

```bash
# 1. Configure connection string + grant managed identity DB access
./catan.ps1 azure database deploy -TraceLevel DEBUG

# 2. Build and deploy GameService (production slot)
./catan.ps1 azure game-service deploy -Force -TraceLevel DEBUG

# 3. Verify GameService health
curl https://catan-api.azurewebsites.net/health
```

### Step 4: Verify production works

Before touching staging, confirm the production stack is healthy:

```bash
curl -sf https://catan-api.azurewebsites.net/health | jq .
curl -sf "https://catan-api.azurewebsites.net/health?checkDatabase=true" | jq .
```

### Step 5: Deploy staging

```bash
# 1. Deploy GameService to staging slot (creates slot, copies config)
./catan.ps1 azure game-service deploy -Slot staging -Force -TraceLevel DEBUG

# 2. Grant staging managed identity DB access
./catan.ps1 azure database deploy-staging-access -TraceLevel DEBUG

# 3. Verify staging GameService
curl https://catan-api-staging.azurewebsites.net/health

# 4. Deploy React UI to staging
./catan.ps1 azure ui deploy-staging -Force -TraceLevel DEBUG \
  -AzureGameServiceUrl https://catan-api-staging.azurewebsites.net

# 5. Verify React staging
curl -s -o /dev/null -w "HTTP %{http_code}\n" https://catan-staging.azurewebsites.net
```

### Step 6: Update deploy-staging.yml workflow

Fix the workflow job ordering. Currently `deploy-gameservice` and
`deploy-react` run in parallel. Change to sequential:

```
deploy-database → deploy-gameservice → deploy-react → verify
```

The `deploy-database` job grants the staging slot's managed identity
access to the database. This must complete before GameService starts.

## Script fixes already applied (uncommitted)

These changes are in the working tree on the `staging` branch:

| File | Change |
|------|--------|
| `.scripts/catan-azure.ps1` | Bearer token auth for Kudu (replaces basic auth) |
| `.scripts/catan-azure.ps1` | `az webapp restart` after Kudu deploy completes |
| `.scripts/catan-azure.ps1` | Staging slot auto-creation with full config |
| `catan.ps1` | `-Slot` and `-AzureGameServiceUrl` params wired through |

## Workflow fix needed

`deploy-staging.yml` currently runs GameService and React deploys in
parallel. The correct order is:

1. **deploy-gameservice** — deploy code + grant DB access (sequential
   within the job)
2. **deploy-react** (needs: deploy-gameservice) — deploy React UI
3. **verify** (needs: deploy-react) — cross-component health checks

This ensures the database is accessible before GameService starts,
and GameService is healthy before React points at it.

## Future: games.ps1

After the infrastructure is stable, create a `games.ps1` script for
managing game data (save/load/delete games between Azure and local).
This enables extracting games for debugging and test data generation.
Separate design doc to follow.

## What's NOT changing

+ Resource names (same `catan-azure.json` config)
+ OIDC federated credentials (already exist on the Azure AD app)
+ GitHub secrets (already configured)
+ Local development (SQLite, unaffected)
