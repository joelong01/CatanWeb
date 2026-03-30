# Implementation Plan: Production CI/CD — GameService + Database

**Design doc:** `.design/cicd-production-gameservice.md`

## Files Modified

| File | Change |
|---|---|
| `.github/workflows/deploy-azure.yml` | Full rewrite — add `changes`, `deploy-gameservice`, `deploy-database` jobs; gate React on `frontend=true`; add `verify` summary job |

## Per-Job Changes

### New: `changes` job

Identical to `deploy-staging.yml` `changes` job.
Outputs: `backend`, `frontend` booleans.
Path filters:

- backend: `^(Catan3\.GameService|Catan3\.Shared)/`
- frontend: `^react-ui/`
- `workflow_dispatch` → force both true.

### New: `deploy-gameservice` job

- `needs: changes`, `if: needs.changes.outputs.backend == 'true'`
- Requires `.NET 9`, Azure login
- Step: `catan-azure.ps1 game-service deploy -Force -TraceLevel INFO` (no `-Slot` = production)
- Step: poll `https://catan-api.azurewebsites.net/health` for `status == "healthy"` — 60 × 10s (10 min max), same pattern as staging

### New: `deploy-database` job

- `needs: [changes, deploy-gameservice]`, `if: needs.changes.outputs.backend == 'true'`
- Requires Azure login only (no .NET)
- Step: `catan-azure.ps1 database deploy -TraceLevel INFO`
- No health poll needed — `database deploy` is synchronous and self-verifying

### Modified: React deploy job (renamed `deploy` → `deploy-react`)

- Add `needs: changes`, `if: needs.changes.outputs.frontend == 'true'`
- All existing steps unchanged (build → staging slot deploy → verify staging → swap → verify production)

### New: `verify` summary job

- `needs: [changes, deploy-gameservice, deploy-react, deploy-database]`
- `if: always() && !cancelled()`
- Prints what was deployed, hits both health endpoints, prints rollback command

## Verification Steps

1. Merge a backend-only change to `main` → only `deploy-gameservice` + `deploy-database` should run; React job should be skipped.
2. Merge a frontend-only change to `main` → only `deploy-react` should run; GameService jobs should be skipped.
3. `workflow_dispatch` → all three deploy jobs should run.
4. After step 1, `pwsh ./catan.ps1 azure doctor` should show game-service at current commit, status OK.
