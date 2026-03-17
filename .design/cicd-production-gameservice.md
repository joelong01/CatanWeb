# Design: Production CI/CD — GameService + Database Deploy

## Problem

`deploy-azure.yml` (triggered on push to `main`) only deploys the React UI.
It never builds or deploys the ASP.NET GameService or ensures database access.
As a result, the production GameService has been stale since ~Feb 2026 (commit
`21b29d4`) despite many merges to `main`.

`deploy-staging.yml` does the right thing — it uses change detection and deploys
the backend or frontend independently. Production needs the same treatment.

## Current State

| Workflow | Trigger | Deploys |
|---|---|---|
| `deploy-staging.yml` | push to `staging` | GameService → staging API slot (if backend changed), React → staging UI slot (if frontend changed), DB access grant |
| `deploy-azure.yml` | push to `main` | React UI only (staging slot → slot swap to production) |

## Proposed Solution

Extend `deploy-azure.yml` to mirror the structure of `deploy-staging.yml`:

1. **Add a `changes` job** — identical change detection logic (backend = `Catan3.GameService/` or `Catan3.Shared/`; frontend = `react-ui/`; `workflow_dispatch` deploys everything).
2. **Add a `deploy-gameservice` job** — runs `catan-azure.ps1 game-service deploy -Force` (no `-Slot` = production slot). Skips when no backend changes.
3. **Add a `deploy-database` job** — runs `catan-azure.ps1 database deploy` after GameService deploys. This is idempotent — it verifies the connection string and managed identity are configured. Skips when no backend changes.
4. **Keep the React jobs** — gate them on `frontend == 'true'` rather than always running.

## Key Design Decisions

### GameService: direct deploy, no slot swap

The React UI uses a blue-green slot swap (staging slot → production). The
GameService does not have a staging slot in the production pipeline. This is
acceptable because:

- The GameService API is versioned and backwards-compatible by convention
- A direct deploy with restart causes ~30s downtime, acceptable for this project
- Adding a GameService staging slot would require schema migration coordination
  and significant complexity

If zero-downtime GameService deploys become a requirement, that's a separate
design effort.

### Database: idempotent deploy, not install

`database deploy` checks if the connection string and managed identity are
already configured before acting. Running it on every backend deploy is safe.
It should NOT run `database install` (which provisions infrastructure).

`database deploy-staging-access` (used in staging) grants the staging slot
identity access to the shared database. That is staging-specific and not needed
here — production's managed identity already has access from initial install.

### Change detection scope

Backend path filter: `^(Catan3\.GameService|Catan3\.Shared)/`

This is identical to staging. Any change to shared game logic or the service
itself triggers a backend redeploy.

### React deploy: only when frontend changed

Currently `deploy-azure.yml` always deploys React on every push to `main`,
even if only backend files changed. With change detection, a pure backend
change won't trigger a React build/deploy/swap. This is a secondary benefit.

### `workflow_dispatch` forces all

On manual trigger, deploy everything regardless of detected changes.

## Revised Workflow Structure

```
push to main
    │
    ▼
changes (detect backend/frontend)
    │
    ├──► deploy-gameservice  (if backend=true)
    │        └──► deploy-database  (after gameservice)
    │
    ├──► deploy-react  (if frontend=true)
    │        ├── build React
    │        ├── deploy to staging slot
    │        ├── verify staging slot
    │        ├── swap slots
    │        └── verify production
    │
    └──► verify (summary, always runs)
```

## Files to Modify

| File | Change |
|---|---|
| `.github/workflows/deploy-azure.yml` | Add `changes` job, `deploy-gameservice` job, `deploy-database` job; gate React on `frontend=true` |

No changes needed to `.scripts/catan-azure.ps1` — the existing
`game-service deploy` and `database deploy` commands already support production
(no `-Slot` argument = production slot).

## Health Verification

After GameService deploy, poll `https://catan-api.azurewebsites.net/health`
until `status == "healthy"` (same pattern as staging's 10-minute poll loop).

After React slot swap, existing verify-production step is unchanged.

## Risks

- **GameService deploy downtime**: ~30s restart window. Acceptable.
- **`appsettings set` race**: As seen in staging, if two jobs call
  `appsettings set` on the same slot concurrently they can conflict.
  In this design the database job runs sequentially _after_ the GameService
  job so they don't race each other. The React job runs in parallel but
  targets a different Azure resource so no conflict.
- **First run**: The first time this runs, `game-service deploy` will
  deploy the current `main` HEAD, resolving the stale `21b29d4` issue.
