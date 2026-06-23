# Implementation Plan: CI/CD Robustness (epic #177, design Rev 2 / #179)

Branch: `cicd-robustness`. Design: `.design/cicd-robustness.md`.

## Goal

Make production deploys reliable: no straight-to-prod, no half-deploy beyond
a bounded validated window, unambiguous retry/rollback, serialized runs,
correct resource identity — shipped incrementally. This plan details
**Steps 1–2** (immediately shippable, unblock the pipeline) per-file and
roadmaps Steps 3–8.

## Step 1 — Idempotent React staging slot in the CI path

**Problem:** `deploy-azure.yml` → `catan-azure.ps1 ui deploy-staging` →
`Deploy-ReactStaging` hard-fails when the `staging` slot is absent
(`.scripts/catan-azure.ps1:2009-2013`). The idempotent create-if-missing
logic already exists in `Install-UI` (`:1503-1518`) but is not reused.

**Change — `.scripts/catan-azure.ps1`:**

1. Add helper `Install-StagingSlot`:
   - `param([string]$AppName, [string]$RgName)`; returns `$true/$false`.
   - Body = the existing block at `:1503-1518` (list `staging` slot; if
     missing → query plan SKU, upgrade plan to `S1` if not Standard+,
     `webapp deployment slot create`; else log "exists").
   - Derive the plan internally so callers pass only app+rg:
     `appServicePlanId = az webapp show --name $AppName --resource-group
     $RgName --query appServicePlanId -o tsv`; `planName = Split-Path …
     -Leaf`.
2. `Install-UI` (`:1459`): replace inline `:1503-1518` with
   `Install-StagingSlot -AppName $appName -RgName $rgName`. Keep the Node
   slot-config lines (`:1520-1524`) unchanged (they run after, idempotent).
3. `Deploy-ReactStaging` (`:1991`): replace the hard-fail at `:2009-2013`
   with:
   `if (-not (Install-StagingSlot -AppName $appName -RgName $rgName)) {`
   `Write-Log -Level ERROR -Message "Failed to ensure staging slot for`
   `$appName"; return $false }`. Keep the existing Node reconfig at
   `:2018-2020` (idempotent).

**Accepted behavior (documented):** `Install-StagingSlot` auto-upgrades a
Basic plan to S1 (already current behavior). Within budget per design
(shared S1 ≈ $70/mo). The explicit `-AllowSkuUpgrade` budget gate is
deferred to Step 5/8.

## Step 2 — Remove hardcoded resource names/URLs from the workflow (Finding I)

**Problem:** `deploy-azure.yml` hardcodes `--name catan --resource-group
rg-catan` (`:126-130`) and verifies `catan-staging|catan|catan-api`
.azurewebsites.net (`:114,:139,:165`). Real resources are
`catanweb`/`catanweb-api`/`rg-catanweb` (deploy log 2026-05-19), derived
from `.azure/catan-azure.json` `baseName` via `Get-AzureResourceNames`
(`catan-azure.ps1:239-260`). The swap had no script entry point.

**Change — `.scripts/catan-azure.ps1`:** add a `ui swap` action (extend the
existing `ui` area `ValidateSet` at `:38` with `swap`):

- Derive names from config (`$Config.ui.appName`, `$Config.resourceGroup`).
- `az webapp deployment slot swap --name $uiApp --resource-group $rg
  --slot staging --target-slot production` (plain swap for Step 2; the
  `--action preview` two-phase form is Step 4).
- Post-swap prod verify (HTTP for now; versioned health is Step 3/6) using
  the config-derived prod URL.
- Return non-zero on failure.

**Change — `.github/workflows/deploy-azure.yml`:**

- Replace the inline `az webapp deployment slot swap …` step with
  `./.scripts/catan-azure.ps1 ui swap -TraceLevel INFO`.
- Replace the hardcoded "Verify staging slot" / "Verify production" curls
  with script-side verification (or a `ui verify -Slot staging|production`
  action) — **no hardcoded hostnames**.
- Replace `-GameServiceUrl https://catan-api.azurewebsites.net` (`:107`)
  with the config-derived URL (script derives it).
- Net: the workflow passes **zero** resource names/URLs.

## Files modified (Steps 1–2)

| File | Change |
|------|--------|
| `.scripts/catan-azure.ps1` | New `Install-StagingSlot` helper (extract from `Install-UI:1503-1518`); call from `Install-UI` and `Deploy-ReactStaging` (replace hard-fail `:2009-2013`); new `ui swap` (+`ui verify`) config-derived action |
| `.github/workflows/deploy-azure.yml` | Remove all hardcoded `catan*`/`rg-catan`; staging-deploy/swap/verify call the script with no resource identity |
| `cspell.json` | `catanweb` added (done) |
| `.design/cicd-robustness.md` | Synced to Rev 2 (done) |

## Verification (Steps 1–2)

1. `./catan.ps1 lint ps1` and `./catan.ps1 lint` (md/spell) clean.
2. `grep -nE 'catan(-api|-staging)?\b|rg-catan\b' .github/workflows/deploy-azure.yml`
   → no hardcoded names remain.
3. PSScriptAnalyzer clean for `catan-azure.ps1`; functions parse
   (`pwsh -NoProfile -Command "& { . ./.scripts/catan-azure.ps1 -WhatIf }"`
   or syntax check).
4. Post-merge to `staging`: `workflow_dispatch deploy-azure.yml` →
   React job **creates the slot when absent** (idempotent), deploy proceeds,
   swap targets `catanweb`, prod verify hits `catanweb.azurewebsites.net`.
5. Re-run with slot present → "Staging slot exists", create is a no-op.
6. Cannot fully execute Azure calls locally (no Azure credentials; CI uses
   OIDC) — functional proof is the dispatched staging run.

## Roadmap — Steps 3–8 (own per-file plan when reached)

- **Step 3 — Versioned health + runtime React URL:** add `releaseId` to
  `Program.cs` `/health`; add React `/api/health` (commit+releaseId);
  inject `window.__CATAN_SERVICE_URL__` from a slot-sticky app setting.
  Files: `Catan3.GameService/Program.cs`, `react-ui/app/api/health/*`,
  `react-ui` server entry, `catan-azure.ps1` (slot-sticky setting).
- **Step 4 — State-checked swap:** `Ensure-ServiceProdState` on
  `swap --action preview|swap|reset` + active-slot `DEPLOY_COMMIT` read.
  Files: `catan-cicd.ps1`, `catan-azure.ps1`.
- **Step 5 — Serialization:** GitHub `concurrency` + script Azure lock
  (specified TTL/renewal, fail-closed). Files: `deploy-azure.yml`,
  `catan-cicd.ps1`.
- **Step 6 — Gates:** pairing health + previous-React smoke; sticky vs
  swappable + staging-identity RBAC checks. Files: `catan-cicd.ps1`,
  `catan-azure.ps1`.
- **Step 7 — Cross-app phase record + compensation** (reverse completed
  phase-2). Files: `catan-cicd.ps1`.
- **Step 8 — Workflow restructure:** script-first jobs, prod environment,
  summary, fail on un-reconciled release. Files: `deploy-azure.yml`,
  `catan-cicd.ps1`.

## Out of scope

- #151 BYOS; merging `fix/build-version-pr-link`; Node 20→24 action
  deprecation warnings.
- Steps 3–8 per-file detail (each gated; planned when its turn comes).
