# Design: CI/CD Robustness — Revision 2 (accepted)

Tracking issue: #177 (epic). Design proposal + adversarial review +
accepted plan: #179. Branch: `cicd-robustness`.

This supersedes Revision 1. It incorporates the adversarial review of #177
and #179 (findings A–I) and the developer-accepted direction recorded at
<https://github.com/joelong01/CatanWeb/issues/179#issuecomment-4491829246>.

## Problem (verified)

`deploy-azure.yml` left production split on the #175 promotion (run
26066071823): GameService deployed straight to prod and succeeded; React
blue-green failed because its `staging` slot was missing and the script
hard-fails instead of creating it. OIDC/#146 is moot (login succeeds on the
retail "Visual Studio Enterprise" subscription).

Additional defect found while reconciling prod (deploy log 2026-05-19):
`deploy-azure.yml` hardcodes `--name catan --resource-group rg-catan` and
verifies `catan*.azurewebsites.net`, but the real resources are
**`catanweb` / `catanweb-api` / `rg-catanweb`** (derived from
`.azure/catan-azure.json` `baseName`). The scripts derive names correctly;
the YAML does not (Finding I).

Production status: reconciled out-of-band to `89bba4d` via
`./catan.ps1 deploy -Azure` (direct-to-prod, no slots). That is a manual
stopgap; the durable CI fix is this work.

## Keystone decision

Use **Azure swap-with-preview** as the swap spine; shrink the bespoke
controller to the minimum cross-app sequencing needs.

- `az webapp deployment slot swap --action preview` — applies the production
  slot's config to the staging slot and restarts it **under prod config,
  without flipping hostnames**. Surfaces Key Vault / per-slot managed
  identity / Cosmos RBAC / sticky-setting failures **before any prod impact**.
- verify the previewed slot (versioned + pairing health).
- `--action swap` — completes the hostname flip only; slot already warm.
- `--action reset` — cancels a preview cleanly. A distinct verb, so rollback
  is never confused with a stale retry.

Retry/rollback safety comes from this Azure primitive plus reading the
active slot's `DEPLOY_COMMIT`/`releaseId`, **not** a hand-built decision
table or per-attempt blob manifest.

## Honest guarantees

Two separate App Services cannot swap atomically. Guarantee: production is a
**consistent, fully-deployed snapshot of some `main` commit — never a
GS/React mix beyond a bounded, validated, smoke-tested transient window** —
and converges to `main` on the next green run. Not "prod == main HEAD even
on a failed deploy."

## Finding resolutions

| # | Resolution |
|---|------------|
| A | preview/swap/reset is the swap primitive; keep only a small phase record (preview vs swapped) for resume/compensation. |
| B | Keep existing `/health` (`Program.cs:255-282` already returns `version.commit` + always checks DB); add only `releaseId`. No `?checkDatabase`, no re-plumb. |
| C | Feed the existing `window.__CATAN_SERVICE_URL__` hook (`react-ui/lib/config.ts:18-39`) from one slot-sticky app setting; add a small React `/api/health` (commit+releaseId). `NEXT_PUBLIC_*` is build-inlined (`next.config.ts:36-37`) so the post-build app setting is inert — runtime hook is required. |
| D | GitHub `concurrency: {group: production-deploy, cancel-in-progress: false}` + a script lock with specified TTL/renewal, fail-closed before swap, not released while `manual-intervention-required`. |
| E | Verify the new-React ↔ new-GS pair in the previewed staging env before phase-2; require GS back-compatible with the previous React for the brief inter-app flip, enforced by a smoke test; incompatible release auto-selects combined-release fallback. |
| F | Preview phase-1 restart-under-prod-config is the enforcement; add sticky/swappable enumeration per app + staging-identity RBAC check before preview. |
| G | Order: ship slot/health/runtime bits via backend path first → one-time forced reconcile (done manually for #175) → then normal `-Service auto`. |
| H | The above is the trim; drop the bespoke blob-manifest state machine. |
| I | Workflow must carry **no** hardcoded resource names/URLs — derive all identity from `.azure/catan-azure.json` via the scripts. |

## Incremental rollout

Each step independently lowers risk and is shippable. Detailed per-file
plan for steps 1–2: `.design/implementation-plans/cicd-robustness-plan.md`.

1. Idempotent React staging-slot creation in the CI path (fixes the literal
   #175 hard-fail; reuses `catan-azure.ps1` `Install-UI` slot logic).
2. Remove hardcoded resource names/URLs from `deploy-azure.yml`; swap +
   verify via a config-derived script entry point (Finding I).
3. `releaseId` on `/health`; React `/api/health`;
   `window.__CATAN_SERVICE_URL__` via slot-sticky setting (B, C).
4. `Ensure-ServiceProdState` on preview/swap/reset + slot-state read (A).
5. GitHub `concurrency` + specified script lock (D).
6. Versioned + pairing health gates + previous-React smoke test (E, F).
7. Minimal cross-app phase record + compensation (reverse completed phase-2).
8. Workflow restructure: script-first, phases exposed, prod environment,
   summary; fail on un-reconciled release (H).

Steps 1–2 are highest value / lowest risk and ship before the controller.

## Out of scope

- #151 BYOS portable deployment (separate epic; informs only).
- Merging `fix/build-version-pr-link` (OIDC obsolete; reference only).
- Node 20→24 GitHub action-deprecation warnings (track separately).
