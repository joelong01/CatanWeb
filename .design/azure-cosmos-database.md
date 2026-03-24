# Design: Azure CosmosDB Database Management

**Status:** Proposed — awaiting approval before implementation

## Overview

The local CosmosDB emulator workflow (Phase 1) is complete. This design covers the Azure side:
replacing the existing Azure SQL Serverless database with an Azure CosmosDB for NoSQL account,
and wiring up the same `ICatanDb` implementation that already runs locally.

Scope: `catan-azure.ps1` database functions, `database.ps1 -Azure` mode,
`.azure/catan-azure.json` config, and `deploy-azure.yml` GitHub Actions workflow.
No changes to the C# runtime code — `CosmosCatanDb.cs` and `CosmosClientFactory` already handle
both local emulator (HTTP, key-based) and Azure (HTTPS, Managed Identity).

---

## Current State

| Component | Today |
|-----------|-------|
| Azure database | Azure SQL Serverless (`sql-catan.database.windows.net`) |
| Auth | Managed Identity → `db_datareader/writer/ddladmin` SQL roles |
| Connection config | `AZURE_SQL_CONNECTION_STRING` app setting on App Service |
| Schema management | EF Core `MigrateAsync()` + `Repair-DatabaseSchema` in PS1 |
| Doctor checks | SQL Server exists, DB online, schema tables, connection string |

---

## Proposed State

| Component | After |
|-----------|-------|
| Azure database | Azure Cosmos DB for NoSQL account (`cosmos-catan`) |
| Auth | Managed Identity → `Cosmos DB Built-in Data Contributor` RBAC role |
| Connection config | `COSMOS_ENDPOINT` app setting on App Service (no key, no password) |
| Schema management | `ICatanDb.InitializeAsync()` (creates DB + containers if absent) |
| Doctor checks | Cosmos account exists, DB + 5 containers exist, app setting, RBAC |

---

## Configuration Changes

### `.azure/catan-azure.json`

Remove `sqlServer` block; add `cosmosDb` block:

```json
{
  "cosmosDb": {
    "accountName": "cosmos-catan",
    "databaseName": "catan",
    "endpoint": "https://cosmos-catan.documents.azure.com:443/"
  }
}
```

The account name follows the existing `{prefix}-{baseName}` naming convention.
The `endpoint` is derived from `accountName` and stored to avoid re-querying.

---

## `catan-azure.ps1` Database Function Replacements

The six SQL-specific functions are replaced with CosmosDB equivalents with the same calling
signatures. The dispatch block (`catan-azure.ps1:3645–3671`) requires no changes.

### `Install-Database` → CosmosDB

1. Create Cosmos account (free tier if available, otherwise **Standard S1 provisioned 400 RU/s**)
   `az cosmosdb create --name cosmos-catan --resource-group rg-catan --kind GlobalDocumentDB`
2. Create database: `az cosmosdb sql database create --name catan`
3. Create 5 containers (players, games, completed-games, templates, recordings) with `/id`
   partition key each: `az cosmosdb sql container create`
4. Save `endpoint` to `catan-azure.json`

Idempotent: all three steps use `--if-none-match *` semantics or check existence first.

**SKU rationale:** Provisioned 400 RU/s (not serverless) keeps backend partitions warm at all
times — eliminates CosmosDB cold-start latency on the first game query after idle.
Cost: ~$23/month.

### `Deploy-Database` → CosmosDB

1. Read `endpoint` from config
2. Set `COSMOS_ENDPOINT` app setting on the App Service:
   `az webapp config appsettings set --settings COSMOS_ENDPOINT=<endpoint>`
3. Get the App Service managed identity principal ID
4. Grant `Cosmos DB Built-in Data Contributor` role via data-plane RBAC:
   `az cosmosdb sql role assignment create`

Role definition ID (built-in): `00000000-0000-0000-0000-000000000002`

Note: This replaces the `Invoke-SqlCmd` path entirely — no SQL drivers or temporary
firewall rules needed.

### `Get-DatabaseDoctor` → CosmosDB

Checks (in order):

1. Cosmos account exists and is `Online`
2. Database `catan` exists
3. All 5 containers exist (players, games, completed-games, templates, recordings)
4. `COSMOS_ENDPOINT` app setting is configured on the App Service
5. Managed identity has `Cosmos DB Built-in Data Contributor` on the account
6. GameService `/health` endpoint responds (existing check, unchanged)

Returns the same hashtable shape as today so `Show-DoctorResult` works unchanged.

### `Fix-Database` → CosmosDB

Handles common drift scenarios:

- Missing containers: re-runs container creation
- Missing `COSMOS_ENDPOINT` setting: re-runs `Deploy-Database`
- Missing RBAC assignment: re-grants the role

### `Test-DatabaseSchema` / `Repair-DatabaseSchema` → Remove

These are SQL-specific (CREATE TABLE statements). With CosmosDB, container creation
is idempotent and schema-free. `Fix-Database` handles missing containers directly.

### `Grant-StagingDatabaseAccess` → CosmosDB

Same logic as `Deploy-Database` but targets the staging slot's managed identity.
The staging slot gets the same `Cosmos DB Built-in Data Contributor` role on the account.

### `Clean-Database` → CosmosDB

`az cosmosdb delete --name cosmos-catan --resource-group rg-catan --yes`

Deletes the entire account (database and containers are children of the account).

---

## `database.ps1 -Azure` Mode

`database.ps1` currently exits with error 1 for `-Azure`. The new behavior
delegates to `catan-azure.ps1` with a clear mapping:

| `database.ps1 -Azure` verb | Delegates to |
|---------------------------|--------------|
| `install` | `catan-azure.ps1 database install` |
| `doctor` / `status` | `catan-azure.ps1 database doctor` |
| `clean` | `catan-azure.ps1 database clean` |
| `seed` | `catan-azure.ps1 database fix` (re-creates missing containers) |
| `start` / `stop` | Not applicable — print informational message |
| `test` | `catan-azure.ps1 database doctor` then run tests against Azure endpoint |

`write-test-params -Azure` writes a params file with the Azure endpoint (no key)
so contract tests can be run against the live Azure account.

---

## GitHub Actions (`deploy-azure.yml`)

The `deploy-database` job currently calls `catan-azure.ps1 database deploy`.
The new job does the same thing — no workflow YAML change needed.

`Deploy-Database` now configures `COSMOS_ENDPOINT` and grants RBAC instead of
configuring a SQL connection string, but the script surface is unchanged.

One new step in `deploy-gameservice`: remove `AZURE_SQL_CONNECTION_STRING`
from App Service settings if it exists (cleanup).

---

## Files to Create / Modify

| File | Change |
|------|--------|
| `.azure/catan-azure.json` | Remove `sqlServer`; add `cosmosDb` block |
| `.scripts/catan-azure.ps1` | Replace 6 SQL database functions with CosmosDB equivalents |
| `.scripts/database.ps1` | Implement `-Azure` mode verbs; add `Invoke-SeedDefaultData` (control plane seeding) |
| `.github/workflows/deploy-azure.yml` | Add cleanup of `AZURE_SQL_CONNECTION_STRING` setting (one line) |
| `Catan3.GameService/Abstractions/CosmosCatanDb.cs` | Add `await _client.OpenAsync()` at end of `InitializeAsync()` |

**C# change required:** Add `await _client.OpenAsync()` at the end of `CosmosCatanDb.InitializeAsync()`
to pre-establish TCP connections on startup, eliminating SDK-level cold-start on the first query.

---

## Control Plane vs Data Plane Separation

### Principle

**Control plane** (scripts, deployment tooling, one-time ops):

- Create / delete infrastructure (accounts, databases, containers)
- Seed default data into a fresh database
- Doctor / health checks
- Grant RBAC and configure App Service settings

**Data plane** (GameService runtime, `ICatanDb`):

- Everything a player or game does at runtime
- CRUD on players, games, templates, recordings during normal operation

These must not be mixed. Scripts do not call GameService REST APIs to set up the database.
The GameService does not shell out or call infrastructure APIs at runtime.

### Default Data Seeding

Seeding is a control plane operation. It runs once during `database.ps1 install` (local and Azure),
before or alongside app deployment — not at GameService startup.

**Implementation:** `Invoke-SeedDefaultData` in `database.ps1` writes documents directly to
CosmosDB using the same REST helpers already used for container creation. It does not require the
GameService to be running.

Documents written match the internal `PlayerDoc` / `RecordingDoc` shapes defined in
`CosmosCatanDb.cs`. The coupling is intentional and local: both files live in this repo and change
together. If the document shape changes, the seed function must be updated at the same time.

**What gets seeded:**

| Data | Source | Container |
|------|--------|-----------|
| Default players (Joe, Dodgy, Doug, Ryan, Adrian, Chris, Guest) | Hardcoded in script + `Default Data/Players/*.jpg` images as base64 | `players` |
| Recordings | `Default Data/Recordings/*.json` | `recordings` |
| Games | `Default Data/Games/*.catan` (if present) | `games` — deferred; `.catan` format requires game engine to parse |

Idempotent: each document upsert skips items that already exist (409 Conflict = OK).

**Existing `DatabaseSeeder.cs`** (EF Core) remains untouched in Phase 1. It is removed in Phase 2
when the GameService is ported to `ICatanDb`.

### Troubleshoot / Control-Plane REST Endpoints

The GameService currently exposes `/api/database/health` and related endpoints to support a
"Troubleshoot" flow from the web app. The original motivation was Azure SQL Serverless pausing and
firewall access issues that scripts alone could not self-heal.

**These problems do not exist with CosmosDB + Managed Identity:**

- CosmosDB provisioned throughput does not pause
- No firewall rules or connection strings to misconfigure
- RBAC is set once by `database.ps1 deploy -Azure` and is durable

**Decision:** Do not expand control-plane REST endpoints. Existing `/api/database/health` is
deprecated — remove it as part of Phase 2 cleanup. The `doctor` script verb is the authoritative
health check.

---

## Not In Scope

- Migrating existing production data from Azure SQL to CosmosDB (separate task)
- Removing EF Core from the project (Phase 2 of the overall CosmosDB migration)
- Creating the Azure Cosmos account manually — `install` handles it

---

## Performance Budget

Target: no perceptible cold-start; snappy gameplay. Budget: $100/month for the full app.

| Resource | SKU | Monthly cost | Rationale |
|----------|-----|-------------|-----------|
| App Service | Standard S1, Always On | ~$56 | Keeps process alive; staging slot support |
| CosmosDB | 400 RU/s provisioned | ~$23 | Partitions stay warm; no serverless cold-start |
| **Total** | | **~$79** | Within $100 budget |

Always On prevents the App Service process from suspending after 20 min idle.
Provisioned CosmosDB eliminates the cold-start on the first Cosmos query after a quiet period.
`OpenAsync()` in `InitializeAsync()` pre-warms SDK TCP connections so the first game query is fast.

---

## Open Questions

1. **Free tier**: `Install-Database` attempts free tier first and falls back to provisioned
   400 RU/s if the subscription quota is used. **Resolved: fall back to provisioned, not serverless.**
2. **Throughput**: 400 RU/s shared across all 5 containers. Sufficient for ≤10 concurrent players
   on a weekly game night. Each container can be scaled independently later if needed.
3. **Staging slot**: Should the staging slot share the production CosmosDB account
   (different database name like `catan-staging`) or a separate CosmosDB account?
