# Session Summary - 2026-02-10 1945

**Session Duration:** ~5 hours (two context windows)
**Build Status:** .NET build clean, TypeScript `tsc --noEmit` clean, Next.js build clean
**Test Status:** All .NET tests pass (57 passed, 2 skipped), TypeScript tests pass
**Branch:** fix/supplemental-overlay-position
**PR:** [#15](https://github.com/joelong01/CatanWeb/pull/15)
**Commit:** `3f1a0ee`

## Work Completed

### Bug Fixes

- **GoFirst and Supplemental overlay positioning** (`react-ui/app/game/[id]/page.tsx`,
  `react-ui/components/game/board/GameBoard.tsx`):
  The GoFirst overlay ("pick who goes first") and Supplemental overlay were not
  centered on hex (0,0,0) of the game board. The previous approach matched the
  board panel position/size, which ignored pan/zoom and assumed hex (0,0,0) was
  at the board panel center (it isn't due to asymmetric harbor layout).
  - Added `hexCenterRef` prop to `GameBoard` — a ref that GameBoard updates with
    the actual screen coordinates of hex (0,0,0), computed from
    `hexGridContentRef.getBoundingClientRect() + HexGrid origin`.
  - Updates whenever pan, zoom, or container size changes.
  - Overlays are now board-sized (preserving user window layout) and positioned so
    their center aligns with the board's hex (0,0,0).
  - FloatingPanels are now always-mounted with visibility controlled by game state
    effects, rather than conditionally rendered.

### Azure Deploy Infrastructure

- **`Deploy-ReactStaging` with skip-if-current logic** (`.scripts/catan-azure.ps1`):
  New function that builds Next.js standalone, packages it, and deploys to the
  staging slot. Tracks `DEPLOY_COMMIT` app setting and skips deploy when the
  staging slot already has the current git commit (unless `-Force`).
  - Suppresses az CLI progress bar with `AZURE_CORE_ONLY_SHOW_ERRORS` env var
  - Uses `Invoke-BackgroundInstaller` for zip deploy with STATUS progress

- **Get-UIDoctor rewritten with parallel execution** (`.scripts/catan-azure.ps1`):
  Doctor was taking 30+ seconds with no output at INFO level. Rewritten to:
  - Use `Start-Job` parallel batches for independent az CLI calls (~15s total)
  - Show `Write-Log -Level "STATUS"` messages that overwrite the current line
  - Batch 1: production config (runtime, identity, app settings, slots)
  - Batch 2: HTTP health checks + staging config
  - Combined multiple sequential calls into single calls where possible

- **Cold start detection** (`.scripts/catan-azure.ps1`):
  Doctor previously reported `needsDeploy` when HTTP timed out, even if
  `DEPLOY_COMMIT` matched. Fixed: `needsDeploy` only set when commit mismatch
  or no code deployed. Added `coldStart` flag when code is deployed but HTTP
  times out. `Show-DoctorResult` shows "cold start -- browse URL to wake"
  instead of "run: deploy".

- **Context-aware command hints** (`.scripts/catan-azure.ps1`):
  `$script:CmdHintPrefix` detects via `Get-PSCallStack` whether called from
  `catan.ps1` or directly, and uses the correct prefix in doctor output
  (e.g., `./catan.ps1 azure ui deploy` vs `.scripts/catan-azure.ps1 ui deploy`).

- **Swap-slots rewritten with doctor integration** (`catan.ps1`):
  Instead of ad-hoc health checks, swap-slots now calls
  `ui doctor -HashTable -TraceLevel` to get the complete system picture, then
  validates using the doctor result (runtime labels, code deployed, site
  responding, cold start retry with warmup).

- **Database doctor connection string fix** (`.scripts/catan-azure.ps1`):
  Azure CLI returns `[{name, value, type}]` array, not keyed object. Changed
  from `$connStrings.AzureSql` to `$connStrings | Where-Object { $_.name -eq 'AzureSql' }`.

- **Deploy orchestration** (`catan.ps1`):
  `$Target` parameter at Position 2 for targeted deploys. Doctor-based skip
  logic for Blazor. Noun-first routing for `ui`, `game-service`, `database`.

- **GitHub Actions staging slot** (`.github/workflows/deploy-react-staging.yml`):
  Added "Ensure staging slot exists" step before deploy.

### Next.js Configuration

- **`images: { unoptimized: true }`** (`react-ui/next.config.ts`):
  Added globally to prevent Next.js from loading sharp at runtime. All `<Image>`
  components already used per-component `unoptimized`. This eliminates the native
  binary dependency that caused cross-platform deploy failures.

- **TypeScript pinned to 5.9.3** (`react-ui/package.json`):
  For build reproducibility.

## Decisions Made

### Architecture Decisions

1. **Overlay positioning via hexCenterRef instead of store computation**
   - Ref prop written by GameBoard avoids re-renders, uses actual DOM measurements
   - Overlay position computed once when it appears; doesn't track subsequent panning

2. **Auto-upgrade App Service Plan for slot support**
   - `Install-UI` auto-upgrades Basic to S1 Standard for staging slot support
   - Cost impact: B1 ~$13/mo -> S1 ~$73/mo

3. **Staging issues don't block production health**
   - Production responding = healthy. Staging reported separately with own actions.

4. **`images: { unoptimized: true }` over Docker or cross-compilation**
   - Investigated sharp native binary issue (`@img/sharp-darwin-arm64` bundled on macOS)
   - Considered: Docker build container, `npm install --os=linux`, global unoptimized
   - Chose global unoptimized since both `<Image>` uses already had per-component flag
   - Docker may still be needed in future if other native dependencies arise

5. **GitHub Actions for deployment (FUTURE -- not yet implemented)**
   - Local macOS builds produce packages with wrong native binaries for Azure Linux
   - Proposed: `./catan.ps1 azure ui deploy -GitHub` triggers GitHub Actions
   - Staging: deploy from any branch; Production: deploy from main only
   - This solves cross-platform issues at the source (build on Linux for Linux)

## Blockers & Issues

### CRITICAL: Azure sites fail to start when deployed from macOS

Both production and staging Azure sites fail after local deploy:

- **Staging (React/Node.js):** 504 Gateway Timeout. Sharp native binaries
  (`@img/sharp-darwin-arm64`) in the bundle crash on Azure Linux. The
  `images: { unoptimized: true }` config should prevent sharp loading at runtime,
  but the site still fails (possibly sharp traces remain, or separate issue).
  **Status: Unresolved.**

- **Production (Blazor/.NET):** "Site failed to start. Time: 656(s)" after sync
  deploy. This is pure managed .NET code — no native binary issue expected.
  GameService health check returns 200 in 2.4s, so the App Service Plan works.
  Blazor app specifically won't start. **Status: Unresolved.**

- **Root cause hypothesis:** Both failures started when deploying from the local
  script. The PR-based deploy via GitHub Actions worked previously. The fix is
  to build on Linux (via GitHub Actions) rather than trying to cross-compile
  from macOS.

### Action items for next session

1. Implement `./catan.ps1 azure ui deploy -GitHub` to trigger GitHub Actions
2. Investigate Blazor production failure (check Azure runtime logs at
   `https://catan.scm.azurewebsites.net/api/logs/docker`)
3. Test that GitHub Actions deploy resolves both staging and production failures

## Next Session Priority

1. **Implement GitHub Actions CLI trigger** (`-GitHub` flag)
   - Push current branch, trigger `deploy-react-staging.yml` from CLI
   - Staging: any branch; Production: main only
   - Monitor workflow status and report result

2. **Investigate production Blazor failure**
   - Check Azure runtime logs for actual error
   - May need to deploy Blazor via GitHub Actions too

3. **Verify overlay positioning end-to-end**
   - Start a game, advance to FinishedRollOrder state
   - Confirm GoFirst overlay is centered on hex (0,0,0)

4. **Update `.ai/ai-rules.md`** with lint-clean-before-checkin requirement
   (requested in previous session, not yet done)

## Key Files Modified

| File | Lines Changed | Purpose |
|------|---------------|---------|
| `react-ui/app/game/[id]/page.tsx` | +/- 122 | Overlay positioning using hexCenterRef |
| `react-ui/components/game/board/GameBoard.tsx` | +26 | hexCenterRef prop and effect |
| `.scripts/catan-azure.ps1` | +/- 454 | Deploy-ReactStaging, doctor parallel/STATUS, cold start, CmdHintPrefix |
| `catan.ps1` | +/- 406 | $Target param, deploy routing, swap-slots doctor, noun-first routing |
| `.github/workflows/deploy-react-staging.yml` | +8 | Ensure staging slot exists |
| `react-ui/next.config.ts` | +3 | `images: { unoptimized: true }` |
| `react-ui/package.json` | +/- 2 | TypeScript pinned to 5.9.3 |
| `react-ui/package-lock.json` | +/- 2 | Lockfile update |
