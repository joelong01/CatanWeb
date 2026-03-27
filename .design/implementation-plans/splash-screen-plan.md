# Splash Screen — Implementation Plan

**Design doc:** `.design/splash-screen.md`
**Issue:** #95

## Overview

Three-phase self-healing splash screen: runtime health check → Azure auth → infrastructure
repair. Implemented as an overlay on the home page that only appears when something is broken.

The Azure infrastructure check/fix logic lives in TypeScript (`react-ui/lib/azure/`), shared
by three consumers:

1. **CLI** — `azureDoctor.cli.ts` (local dev, uses `DefaultAzureCredential` from `az login`)
2. **Web** — Next.js API route (browser auth, forwarded MSAL token)
3. **PowerShell** — `catan.ps1 azure doctor` delegates to the CLI, replacing `az` CLI calls

All Azure doctor operations (`Get-GameServiceDoctor`, `Get-UIDoctor`, `Get-GitHubDoctor`,
`Get-DatabaseDoctor`) are ported to TypeScript. The PowerShell implementations become dead
code and are deleted. `catan.ps1 azure doctor` becomes a single delegation to the TypeScript
CLI. This eliminates duplication and means the splash screen and CLI share one implementation.

## Step 0: Azure Doctor TypeScript Module (DONE)

Core Azure infrastructure check/fix logic using ARM SDKs. Already implemented and tested.

### Files created

| File | Purpose |
|------|---------|
| `react-ui/lib/azure/types.ts` | Shared types, constants (CheckResult, AzureConfig, REQUIRED_CONTAINERS, etc.) |
| `react-ui/lib/azure/azureDoctor.ts` | Core check/fix logic — 5 checks using `@azure/arm-cosmosdb` and `@azure/arm-appservice` |
| `react-ui/lib/azure/azureDoctor.cli.ts` | CLI entry point — `DefaultAzureCredential`, reads `.azure/catan-azure.json` |

### npm packages installed

- `@azure/identity` — `DefaultAzureCredential` for CLI
- `@azure/arm-cosmosdb` — Cosmos account, firewall, containers, SQL RBAC
- `@azure/arm-appservice` — App Service config, managed identity
- `@azure/core-auth` — `TokenCredential` interface

### Checks implemented

| Check | What it verifies | Auto-fix |
|-------|-----------------|----------|
| `cosmosAccount` | Account exists, provisioningState=Succeeded | No (report only) |
| `cosmosFirewall` | publicNetworkAccess=Enabled, ipRules=[] | Yes — PATCH to enable + clear |
| `cosmosContainers` | Database + 5 required containers exist | Yes — create with `/id` partition key |
| `appServiceConfig` | COSMOS_ENDPOINT set, managed identity enabled | No (report only) |
| `cosmosRbac` | Managed identity has Cosmos Data Contributor role | Yes — create SQL role assignment |

### Verified

```bash
AZURE_SUBSCRIPTION_ID=$(az account show --query id -o tsv) \
  npx tsx react-ui/lib/azure/azureDoctor.cli.ts --no-fix
# ✅ All 5 checks pass against production
```

---

## Step 1: Wire catan.ps1 to Delegate to TypeScript

### Files modified

| File | Change |
|------|--------|
| `catan.ps1` | `azure doctor` command calls `npx tsx react-ui/lib/azure/azureDoctor.cli.ts --json` and parses output |

### Integration approach

```powershell
# In the azure doctor handler:
$subId = az account show --query id -o tsv
$env:AZURE_SUBSCRIPTION_ID = $subId
$jsonOutput = npx tsx react-ui/lib/azure/azureDoctor.cli.ts --json $(if ($NoFix) { "--no-fix" })
$result = $jsonOutput | ConvertFrom-Json
# Display using existing Show-DoctorResult formatting
```

### Verification

- `pwsh ./catan.ps1 azure doctor` produces same output as before
- `pwsh ./catan.ps1 azure doctor -Staging` works with staging config

---

## Step 2: Add Count Methods to ICatanDb + CosmosCatanDb

**Prerequisite for the /health endpoint** — currently fetches all player documents just to
count them.

### Files modified

| File | Change |
|------|--------|
| `Catan3.GameService/Abstractions/ICatanDb.cs` | Add `CountPlayersAsync()`, `CountTemplatesAsync()`, `CountRecordingsAsync()` |
| `Catan3.GameService/Abstractions/CosmosCatanDb.cs` | Implement using `SELECT VALUE COUNT(1) FROM c` (same pattern as existing `CountGamesAsync`) |

### CosmosCatanDb implementation pattern

```csharp
public async Task<int> CountPlayersAsync()
{
    var iter = _players.GetItemQueryIterator<int>("SELECT VALUE COUNT(1) FROM c");
    if (iter.HasMoreResults)
    {
        var page = await iter.ReadNextAsync();
        return page.FirstOrDefault();
    }
    return 0;
}
// Same for CountTemplatesAsync (_templates) and CountRecordingsAsync (_recordings)
```

### Verification

- `pwsh ./catan.ps1 build` passes
- Existing tests still pass

---

## Step 3: Extend /health Endpoint

### Files modified

| File | Change |
|------|--------|
| `Catan3.GameService/Program.cs` | Replace `LoadPlayersAsync().Count` with `CountPlayersAsync()`, add template + recording counts |

### New response shape

```csharp
var playerCount = await db.CountPlayersAsync();
var gameCount = await db.CountGamesAsync();
var templateCount = await db.CountTemplatesAsync();
var recordingCount = await db.CountRecordingsAsync();
response["databaseDiagnostics"] = new
{
    connected = true,
    checkedAt = DateTime.UtcNow,
    playerCount,
    gameCount,
    templateCount,
    recordingCount,
};
```

### Verification

- `curl http://localhost:8080/health` returns all four counts
- Response time should be <50ms (4 count queries at ~1 RU each)

---

## Step 4: Health Check Hook (React)

### Files created

| File | Purpose |
|------|---------|
| `react-ui/lib/hooks/useHealthCheck.ts` | Phase 1 health check with retry logic |

### Hook interface

```typescript
export type HealthStatus = 'checking' | 'healthy' | 'failed' | 'retrying';

export interface HealthResult {
  status: HealthStatus;
  data?: HealthResponse;
  error?: string;
  retryCount: number;
  retry: () => void;
}
```

### Retry logic

- 3 retries, exponential backoff: 2s, 6s, 18s
- Total timeout: 30s
- HTTP 503 → `retrying` with "GameService is starting up..."
- Network error → `retrying` with "GameService is unreachable"
- After all retries exhausted → `failed`
- Success → `healthy`

### Verification

- Hook returns `healthy` when GameService is running
- Hook retries and eventually returns `failed` when GameService is stopped

---

## Step 5: Splash Overlay Components

### Files created/modified

| File | Change |
|------|--------|
| `react-ui/components/splash/SplashOverlay.tsx` | Full-screen overlay with check status rows |
| `react-ui/components/splash/CheckRow.tsx` | Individual check row (pending/running/pass/fail/fixing) |
| `react-ui/components/splash/AzureSignIn.tsx` | Sign-in button + lazy MSAL import |
| `react-ui/app/page.tsx` | Add `useHealthCheck` hook + conditional `SplashOverlay` |

### SplashOverlay.tsx

- Full-screen fixed overlay with dark background, centered content
- Catan branding at top (Catan font)
- List of `CheckRow` components
- Phase 2 sign-in prompt when health check fails and Azure config is present
- Phase 3 repair progress when authenticated
- "Retry" button always visible

### CheckRow.tsx states

- `pending` — gray circle, label only
- `running` — animated spinner, label
- `ok` — green checkmark, label, duration, optional count
- `fixing` — amber wrench icon, label, action description
- `error` — red X, label, error message

### AzureSignIn.tsx

- Lazy-loads `@azure/msal-browser` via `React.lazy()` + dynamic import
- "Sign in with Microsoft" button
- Acquires token with scope `https://management.azure.com/.default`
- Passes token up to parent via callback

### page.tsx changes

```tsx
const health = useHealthCheck();

return (
  <>
    {/* existing home page content unchanged */}
    {health.status === 'failed' && (
      <SplashOverlay health={health} onRetry={health.retry} />
    )}
  </>
);
```

### Verification

- Healthy system: no overlay, home page renders immediately
- Stop GameService: overlay appears with retry animation, shows failure after 30s
- Start GameService while overlay is up: "Retry" succeeds, overlay dismisses

---

## Step 6: Next.js API Route for Azure Doctor (Web)

### Files created

| File | Purpose |
|------|---------|
| `react-ui/app/api/azure/doctor/route.ts` | SSE endpoint — receives Bearer token, streams check results |
| `react-ui/lib/azure/tokenCredential.ts` | Wraps forwarded MSAL token in `TokenCredential` |
| `react-ui/lib/azure/msalConfig.ts` | MSAL configuration from env vars (client-side) |

### New npm dependency

```bash
npm install @azure/msal-browser
```

### route.ts — SSE endpoint

Receives the user's MSAL token via Authorization header. Creates a `TokenCredential`
wrapper, then calls `runAzureDoctor()` (the same function used by the CLI). Streams
results as SSE events.

**Security:** The Authorization header MUST NOT be logged. Parse JWT `exp` claim for
actual expiration instead of hardcoding.

### tokenCredential.ts

```typescript
export function createTokenCredential(token: string): TokenCredential {
  const payload = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString());
  return {
    getToken: async () => ({ token, expiresOnTimestamp: payload.exp * 1000 }),
  };
}
```

### msalConfig.ts (client-side)

```typescript
export const isAzureConfigured = () =>
  !!process.env.NEXT_PUBLIC_AZURE_TENANT_ID && !!process.env.NEXT_PUBLIC_AZURE_CLIENT_ID;
```

### Verification

- Break Cosmos (disable public access), sign in, watch SSE stream fix it
- Verify token is never logged in error paths

---

## Step 7: Wire Phase 2 + Phase 3 into Splash Overlay

### Files modified

| File | Change |
|------|--------|
| `react-ui/components/splash/SplashOverlay.tsx` | Add Phase 2 (sign-in) and Phase 3 (SSE doctor) flow |
| `react-ui/components/splash/AzureSignIn.tsx` | Lazy MSAL initialization, token acquisition |

### Flow

1. Phase 1 fails → overlay appears
2. If `isAzureConfigured()`: show "Sign in with Microsoft" button
3. If not configured (local dev): show "Run `./catan.ps1 doctor` for help"
4. User signs in → token acquired
5. `POST /api/azure/doctor` with Bearer token → SSE stream
6. Each SSE event updates a check row in the overlay
7. After all checks/fixes complete → auto-retry Phase 1
8. Phase 1 passes → overlay dismisses

### Verification

- Full end-to-end test per verification plan in design doc
- Local dev: no Azure sign-in prompt, just "Run `./catan.ps1 doctor` for help"

---

## Step 8: Environment Variables + App Registration

### Files modified

| File | Change |
|------|--------|
| `.scripts/catan-azure.ps1` | Add app settings during deployment |

### New app settings (staging/production)

```text
NEXT_PUBLIC_AZURE_SUBSCRIPTION_ID
NEXT_PUBLIC_AZURE_RESOURCE_GROUP
NEXT_PUBLIC_AZURE_COSMOS_ACCOUNT
NEXT_PUBLIC_AZURE_APP_NAME
NEXT_PUBLIC_AZURE_TENANT_ID
NEXT_PUBLIC_AZURE_CLIENT_ID
```

### App registration (manual)

Create `catan-doctor-{baseName}` in Azure AD:

1. Authentication: Single-page application, redirect URI = staging URL
2. API permissions: `Azure Service Management` → `user_impersonation` (delegated)
3. Supported account types: Single tenant
4. Set `NEXT_PUBLIC_AZURE_CLIENT_ID` to the registration's client ID

---

## Files Summary

| File | Step | Change |
|------|------|--------|
| `react-ui/lib/azure/types.ts` | 0 | Done — shared types and constants |
| `react-ui/lib/azure/azureDoctor.ts` | 0 | Done — core check/fix logic |
| `react-ui/lib/azure/azureDoctor.cli.ts` | 0 | Done — CLI entry point |
| `catan.ps1` | 1 | Delegate `azure doctor` to TypeScript CLI |
| `Catan3.GameService/Abstractions/ICatanDb.cs` | 2 | Add 3 count methods |
| `Catan3.GameService/Abstractions/CosmosCatanDb.cs` | 2 | Implement count methods |
| `Catan3.GameService/Program.cs` | 3 | Extend /health with all counts |
| `react-ui/lib/hooks/useHealthCheck.ts` | 4 | New — Phase 1 health check hook |
| `react-ui/components/splash/SplashOverlay.tsx` | 5, 7 | New — overlay component |
| `react-ui/components/splash/CheckRow.tsx` | 5 | New — check status row |
| `react-ui/components/splash/AzureSignIn.tsx` | 5, 7 | New — lazy MSAL sign-in |
| `react-ui/app/page.tsx` | 5 | Add health check + conditional overlay |
| `react-ui/app/api/azure/doctor/route.ts` | 6 | New — SSE endpoint for ARM SDK |
| `react-ui/lib/azure/tokenCredential.ts` | 6 | New — token wrapper |
| `react-ui/lib/azure/msalConfig.ts` | 6 | New — MSAL config |
| `react-ui/package.json` | 6 | Add `@azure/msal-browser` |
| `.scripts/catan-azure.ps1` | 8 | Add app settings to deployment |

## Implementation Order

Step 0 is done. Steps 1, 2-3, and 4 can run in parallel. Step 5 depends on Step 4.
Step 6 is independent of Steps 4-5. Step 7 wires Steps 5+6 together. Step 8 is
deployment/config.

```text
Step 0 (azureDoctor.ts — DONE) ──→ Step 1 (catan.ps1 delegation) ──┐
                                                                     │
Step 2 (ICatanDb counts) ──→ Step 3 (/health) ──┐                   │
                                                  ├→ Step 7 (wire) → Step 8
Step 4 (useHealthCheck) ──→ Step 5 (overlay) ────┘                   │
                                                                     │
Step 6 (API route + MSAL) ──────────────────────────────────────────┘
```

## Build + Test Verification

After all steps:

1. `pwsh ./catan.ps1 build` — all projects build
2. `pwsh ./catan.ps1 test` — all tests pass
3. `pwsh ./catan.ps1 azure doctor` — delegates to TypeScript, same output
4. Manual: load app with healthy system → no overlay
5. Manual: stop GameService → overlay appears with retries
6. Manual: disable Cosmos public access → overlay, sign in, auto-fix, overlay dismisses
7. Manual: delete a Cosmos container → overlay, sign in, auto-creates container
8. Manual: load on LG WebOS TV → readable, auto-proceeds when healthy
