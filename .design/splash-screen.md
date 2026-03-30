# Splash Screen — Self-Healing Startup

## Problem

When the app starts — especially after overnight MCAPS reconfiguration in non-prod environments
— users hit a broken home page with no indication of what's wrong. The #1 failure mode:

**MCAPS policy disables "public network access" on CosmosDB overnight**, thinking it means
random internet access. It actually means access from inside pre-prod. This breaks everything.

Other failures:

- GameService cold start (F1/B1 tier takes 30-60s)
- Cosmos containers missing after environment rebuild
- App settings misconfigured after slot swap
- RBAC roles removed by policy sweep
- Managed identity not assigned

The PowerShell `./catan.ps1 azure doctor -Staging` detects and fixes all of these, but it
can't run on an LG WebOS TV or any browser-only device.

## Goal

A **splash screen** that:

1. Runs automatically on app load
2. Performs progressive health checks to identify what's broken
3. If infrastructure is misconfigured, prompts for Azure credentials
4. Uses Azure Management APIs to **diagnose and fix** the infrastructure
5. Only navigates to the home page once everything is verified

After this page runs, the game works. Period.

## Architecture — Three Phases

### Phase 1: Runtime Health Check (no auth required)

React makes a single call to the existing `/health` endpoint (extended with template
and recording counts):

```text
React UI                          GameService
────────                          ────────────
1. fetch /health          →       Version, uptime, Cosmos connectivity,
   (retry 3x for cold start)      playerCount, gameCount, templateCount,
                                   recordingCount
```

One round-trip instead of five. If Cosmos is down, you get one failure instead of four
separate timeouts.

**Retry logic:** 3 retries with exponential backoff (2s, 6s, 18s). Total Phase 1 timeout:
30 seconds. HTTP 503 → "GameService is starting up...". Connection refused → "GameService
is unreachable." After 30s with no success, show the Phase 2 sign-in prompt.

If the health check passes → render home page. Done.

### Phase 2: Azure Authentication (only if Phase 1 fails)

If any check fails (especially Cosmos connectivity), the splash screen shows:

> "Infrastructure issue detected. Sign in with Azure to diagnose and fix."
>
> [Sign in with Microsoft]

Uses **MSAL.js** (`@azure/msal-browser`) to authenticate against the app's Azure AD tenant.
The user needs at minimum Contributor role on the resource group.

**Token scopes needed:**

- `https://management.azure.com/.default` — Azure Resource Manager API access

The browser obtains the token via MSAL, then passes it to Next.js API routes that do the
actual Azure management work server-side.

### Phase 3: Infrastructure Diagnosis and Repair (via Next.js API routes)

The React splash screen passes the Azure token to **Next.js API routes** that use the
official Azure ARM SDKs (`@azure/arm-cosmosdb`, `@azure/arm-appservice`,
`@azure/arm-authorization`) server-side. This avoids browser-compatibility issues with
the Azure SDKs and keeps management logic on the server where the SDKs are fully supported.

```text
React UI          Next.js API Routes (server-side)       Azure Resource Manager
────────          ─────────────────────────────────       ──────────────────────
POST /api/azure/doctor
  (Bearer token)
                  1. CosmosDBManagementClient            → Check publicNetworkAccess
                  2. CosmosDBManagementClient             → Enable public access if OFF
                  3. CosmosDBManagementClient             → Verify database + containers
                  4. WebSiteManagementClient              → Check app settings, identity
                  5. AuthorizationManagementClient         → Check RBAC roles
                  6. AuthorizationManagementClient         → Add role if missing
                  ←──────────────────────────────────────
  { checks: [...], fixes: [...] }
```

**Why Next.js API routes instead of calling ARM REST APIs from the browser:**

- Azure ARM SDKs (`@azure/arm-*`) are designed for Node.js — fully supported server-side
- No browser-compatibility issues with `@azure/core-rest-pipeline`
- Bundle size stays small (SDKs don't ship to the client)
- Azure token stays on the server for the duration of the request (passed in Authorization
  header, not stored server-side)

**Progressive updates via SSE:** The `POST /api/azure/doctor` endpoint uses Server-Sent
Events (SSE) to stream check results as they complete. ARM operations (especially
`beginCreateOrUpdateAndWait` for Cosmos account updates) can take 30-60 seconds — SSE
ensures the user sees progress rather than staring at a spinner. Each check result is
sent as an SSE event as it finishes.

```typescript
// SSE event format
event: check
data: {"check":"cosmosPublicAccess","status":"fixing","detail":"Enabling public access..."}

event: check
data: {"check":"cosmosPublicAccess","status":"ok","detail":"Public access enabled","durationMs":12400}
```

Fixes are applied automatically where safe (enabling public access, adding RBAC,
creating missing containers). All containers use `/id` as partition key, which is stable
and well-known — auto-creation is low-risk and makes self-healing truly end-to-end.

## Azure SDK Usage (Server-Side)

The Next.js API routes use the official Azure ARM SDKs. The user's MSAL token is wrapped
in a `TokenCredential` that the SDKs accept.

### Token forwarding

**Security:** The Authorization header MUST NOT be logged. Add middleware that strips it
from error/diagnostic output. The token is held in memory only for the request duration.

```typescript
// Next.js API route receives Bearer token from the browser
const token = req.headers.authorization?.replace('Bearer ', '');

// Parse actual expiration from JWT claims (don't hardcode)
const payload = JSON.parse(Buffer.from(token.split('.')[1], 'base64url').toString());
const expiresOnTimestamp = payload.exp * 1000;

// Wrap in a TokenCredential for the Azure SDKs
const credential: TokenCredential = {
  getToken: async () => ({ token, expiresOnTimestamp })
};

const cosmosClient = new CosmosDBManagementClient(credential, subscriptionId);
const webClient = new WebSiteManagementClient(credential, subscriptionId);
```

### Cosmos Account — Check and Fix Public Access

```typescript
const account = await cosmosClient.databaseAccounts.get(resourceGroup, accountName);

if (account.publicNetworkAccess !== 'Enabled' || account.ipRules?.length) {
  await cosmosClient.databaseAccounts.beginCreateOrUpdateAndWait(resourceGroup, accountName, {
    ...account,
    publicNetworkAccess: 'Enabled',
    ipRules: [],
  });
}
```

### Cosmos Containers — Verify Existence

```typescript
const containers = cosmosClient.sqlResources.listSqlContainers(resourceGroup, accountName, 'CatanDb');
const found = new Set<string>();
for await (const c of containers) {
  found.add(c.resource?.id ?? '');
}
const required = ['players', 'games', 'completed-games', 'templates', 'recordings'];
const missing = required.filter(r => !found.has(r));
```

### App Service Config — Check Settings

```typescript
const settings = await webClient.webApps.listApplicationSettings(resourceGroup, appName);
const hasEndpoint = !!settings.properties?.['COSMOS_ENDPOINT'];

const site = await webClient.webApps.get(resourceGroup, appName);
const hasIdentity = site.identity?.type === 'SystemAssigned';
```

### Cosmos SQL RBAC — Check and Fix Data Contributor

**Important:** Cosmos DB has its own RBAC system separate from Azure RBAC. The "Cosmos DB
Built-in Data Contributor" role is a **Cosmos SQL role**, not an Azure role assignment.
`AuthorizationManagementClient` cannot manage these. Use
`CosmosDBManagementClient.sqlResources` instead (same pattern as `az cosmosdb sql role
assignment create` in `database.ps1`).

```typescript
const dataContributorRole = '00000000-0000-0000-0000-000000000002';
const accountScope = `/subscriptions/${sub}/resourceGroups/${rg}/providers/Microsoft.DocumentDB/databaseAccounts/${account}`;

// Check existing Cosmos SQL role assignments
const assignments = cosmosClient.sqlResources.listSqlRoleAssignments(resourceGroup, accountName);
let hasRole = false;
for await (const a of assignments) {
  if (a.roleDefinitionId?.endsWith(dataContributorRole)
      && a.principalId === managedIdentityPrincipalId) {
    hasRole = true;
  }
}

// Fix: create Cosmos SQL role assignment
if (!hasRole) {
  await cosmosClient.sqlResources.beginCreateUpdateSqlRoleAssignmentAndWait(
    randomUUID(),
    resourceGroup,
    accountName,
    {
      roleDefinitionId: `${accountScope}/sqlRoleDefinitions/${dataContributorRole}`,
      principalId: managedIdentityPrincipalId,
      scope: accountScope,
    }
  );
}
```

## Configuration

The splash screen needs to know which Azure resources to check. These come from environment
variables set at deployment time (already used by `catan.ps1`):

| Variable | Example | Source |
|----------|---------|--------|
| `NEXT_PUBLIC_AZURE_SUBSCRIPTION_ID` | `abc-123-...` | App setting |
| `NEXT_PUBLIC_AZURE_RESOURCE_GROUP` | `rg-catan-staging` | App setting |
| `NEXT_PUBLIC_AZURE_COSMOS_ACCOUNT` | `cosmos-catan-staging` | App setting |
| `NEXT_PUBLIC_AZURE_APP_NAME` | `catan-staging` | App setting |
| `NEXT_PUBLIC_AZURE_TENANT_ID` | `def-456-...` | App setting (for MSAL) |
| `NEXT_PUBLIC_AZURE_CLIENT_ID` | `ghi-789-...` | App setting (MSAL app registration) |

For **local development**, these are not set and Phase 2/3 are skipped — the local emulator
doesn't have these issues.

## UI Design

### Route: `/` (root) — overlay on failure, no redirect

The home page stays at `/`. The health check runs in the background on page load. If it
passes, the home page renders normally with zero delay. If it fails, a full-screen splash
overlay appears on top with diagnostic info and repair controls.

This avoids penalizing every healthy visit and preserves all bookmarks and links.

### Healthy state — no splash visible

The home page renders immediately. A small status indicator (green dot or checkmark in a
corner) confirms the health check passed. No splash screen when things work.

### Failure state — splash overlay

```text
┌─────────────────────────────────────────────────┐
│                                                 │
│                 🎲 Catan                        │
│              (Catan font, large)                │
│                                                 │
│  ┌───────────────────────────────────────────┐  │
│  │  ✅  Game Service         12ms           │  │
│  │  ❌  Database             — FAILED       │  │
│  │      Connection refused (403)             │  │
│  └───────────────────────────────────────────┘  │
│                                                 │
│  Infrastructure issue detected.                 │
│  Sign in to Azure to diagnose and repair.       │
│                                                 │
│  [ Sign in with Microsoft ]    [ Retry ]        │
│                                                 │
└─────────────────────────────────────────────────┘
```

### Phase 3 Layout — Repair in Progress

```text
┌─────────────────────────────────────────────────┐
│                                                 │
│                 🎲 Catan                        │
│           Diagnosing infrastructure...          │
│                                                 │
│  ┌───────────────────────────────────────────┐  │
│  │  ✅  Resource Group       exists         │  │
│  │  ❌  Cosmos Account       public=OFF     │  │
│  │      → Enabling public access...         │  │
│  │  ⏳  Cosmos Containers                   │  │
│  │  ⬜  App Settings                        │  │
│  │  ⬜  RBAC Roles                          │  │
│  └───────────────────────────────────────────┘  │
│                                                 │
└─────────────────────────────────────────────────┘
```

After all fixes applied, automatically re-runs Phase 1 to verify everything works end-to-end.

### States per check

- `⬜` — pending (gray)
- `⏳` — running (animated spinner)
- `✅` — passed (green)
- `🔧` — fixing (amber, with action description)
- `❌` — failed and unfixable (red, with error message)

### Responsive considerations

- LG WebOS TV: large text, high contrast, no hover states, auto-proceeds on success
- Desktop: standard layout
- Works without keyboard/mouse when healthy (auto-navigates)

## Implementation Notes

### New npm dependencies

**Client-side (browser):**

- `@azure/msal-browser` — Azure AD authentication (interactive login)
- `@azure/msal-react` — React hooks for MSAL

**Server-side (Next.js API routes only — not bundled to client):**

- `@azure/arm-cosmosdb` — CosmosDB account management + Cosmos SQL RBAC
- `@azure/arm-appservice` — App Service config
- `@azure/core-auth` — `TokenCredential` interface

### GameService changes

1. **Extend existing `/health` endpoint** — add `templateCount` and `recordingCount` to the
   `databaseDiagnostics` response. No new controller needed. One round-trip covers
   everything the splash screen needs for Phase 1.

2. **No new DoctorController** — the existing `/health` endpoint already queries Cosmos
   and returns `playerCount`, `gameCount`, and `connected`. Just add two more counts.

3. **Add lightweight count methods to `ICatanDb`** — The current `/health` endpoint calls
   `LoadPlayersAsync()` which fetches all documents just to count them. Add
   `CountPlayersAsync()`, `CountTemplatesAsync()`, and `CountRecordingsAsync()` using
   Cosmos `SELECT VALUE COUNT(1)` queries (1 RU each). `CountGamesAsync()` already exists.
   This is a prerequisite — do not call `ListTemplatesAsync().Count` or
   `ListRecordingsAsync().Count` for the health endpoint.

### React UI changes

1. **No route change** — `/` stays as the home page
2. **Health check hook**: `useHealthCheck()` runs on mount, calls `/health`
3. **Splash overlay component**: Full-screen overlay shown only when health check fails
4. **MSAL provider**: Lazy-load via `React.lazy()` + dynamic import — only loaded when
   Phase 1 fails and Phase 2 is needed. MSAL.js adds ~40KB gzipped; no reason to include
   it on healthy page loads
5. **Progressive UI**: State machine driving the check/fix/verify cycle

### Next.js API routes (server-side)

1. **`app/api/azure/doctor/route.ts`** — Main doctor endpoint. Accepts Bearer token,
   runs all ARM SDK checks, streams results via SSE or returns full JSON.
2. **`lib/azure/azureDoctor.ts`** — Server-side check + fix logic using ARM SDKs.
   NOT bundled to client.
3. **`lib/azure/tokenCredential.ts`** — Wraps the forwarded MSAL token in a
   `TokenCredential` for the Azure SDKs.

### File structure

```text
react-ui/
├── app/
│   ├── page.tsx                    # Home page (unchanged route)
│   └── api/
│       └── azure/
│           └── doctor/
│               └── route.ts        # Next.js API route — ARM SDK checks via SSE
├── lib/
│   ├── hooks/
│   │   └── useHealthCheck.ts       # Phase 1 health check hook (calls /health)
│   └── azure/
│       ├── msalConfig.ts           # MSAL configuration from env vars (client-side)
│       ├── azureDoctor.ts          # Check + fix logic using ARM SDKs (server-side only)
│       ├── tokenCredential.ts      # Wrap forwarded token for ARM SDKs (server-side only)
│       └── types.ts                # CheckResult, FixAction types (shared)
└── components/
    └── splash/
        ├── SplashOverlay.tsx       # Full-screen overlay (shown only on failure)
        ├── CheckRow.tsx            # Individual check status row
        └── AzureSignIn.tsx         # Sign-in prompt + MSAL trigger
```

### What the Azure doctor checks (port of PowerShell)

| PowerShell function | Server-side equivalent | Auto-fix? |
|--------------------|-----------------------|-----------|
| `Get-GameServiceDoctor` | Phase 1 health check (client-side) | N/A (retry only) |
| Cosmos `publicNetworkAccess` check | `CosmosDBManagementClient.databaseAccounts.get()` | Yes — update to enable |
| Cosmos `ipRules` check | Same call, check `ipRules` array | Yes — update to clear |
| Cosmos database/containers check | `CosmosDBManagementClient.sqlResources.listSqlContainers()` | Yes — auto-create with `/id` partition key |
| App Service `COSMOS_ENDPOINT` | `WebSiteManagementClient.webApps.listApplicationSettings()` | No — report only |
| Managed identity check | `WebSiteManagementClient.webApps.get()` check `identity` | No — report only |
| Cosmos SQL RBAC role | `CosmosDBManagementClient.sqlResources.listSqlRoleAssignments()` | Yes — create assignment |

### Local development behavior

When `NEXT_PUBLIC_AZURE_TENANT_ID` is not set (local dev), Phase 2/3 are completely skipped.
Phase 1 runs against the local GameService/emulator. If healthy, proceeds immediately.
If not, shows: "Run `./catan.ps1 doctor` for help."

## Security Considerations

- MSAL uses authorization code flow with PKCE (no client secret in the browser)
- Azure token is forwarded to Next.js API routes via Authorization header — never stored
  server-side, never sent to GameService
- The app registration needs `Azure Service Management` API permission (user_impersonation)
- User must have Contributor on the resource group — the app can't escalate privileges
- Phase 3 only runs in non-production environments (gate on environment name)
- Next.js API routes validate the token before using it (check audience, issuer)

## Decisions

1. **App registration**: Create a dedicated app registration for the splash screen (e.g.,
   `catan-doctor-{baseName}`). The existing `github-actions-{baseName}-deploy` uses federated
   credentials for CI/CD, not interactive browser login.

2. **Auto-fix in pre-prod**: Phase 3 auto-fixes without confirmation in non-production
   environments (enabling public access, adding RBAC, creating containers). Production
   environments show confirmation dialogs before applying changes.

3. **Token caching**: MSAL uses sessionStorage by default. No change needed — if the browser
   restarts, Phase 1 re-checks and only prompts for auth if something is broken.

4. **`/api/doctor` in production**: Yes — the checks are read-only and lightweight. Useful
   for debugging production issues from the browser. Phase 3 fixes require confirmation
   in production (see #2).

## Verification Plan

### End-to-end self-healing test

1. **Break it**: Use Azure CLI to reset Cosmos to the MCAPS-default locked-down config:

   ```bash
   az cosmosdb update -n cosmos-catan-staging -g rg-catan-staging \
     --public-network-access DISABLED
   ```

2. **Load the app**: Navigate to the staging URL in a browser.
3. **Phase 1 fails**: Splash overlay appears — health check reports database unreachable.
4. **Sign in**: Click "Sign in with Microsoft", authenticate with a Contributor account.
5. **Phase 3 auto-repairs**: Watch the SSE-driven progress:
   - Detects `publicNetworkAccess=Disabled`
   - Enables public access (PATCH via ARM SDK)
   - Waits for Cosmos account update to complete
   - Verifies containers exist
   - Checks RBAC roles
6. **Phase 1 re-runs**: After fixes, health check passes automatically.
7. **Home page renders**: Overlay dismisses, game is playable.

**Pass criteria**: From broken Cosmos config to playable game with zero terminal commands.

### Additional scenarios to verify

- **Cold start**: Stop GameService, load app → retry logic shows "Starting up..." and
  eventually connects after service wakes
- **Missing containers**: Delete a container, load app → Phase 3 recreates it with `/id`
  partition key
- **Missing RBAC**: Remove Cosmos SQL role assignment, load app → Phase 3 reassigns it
- **Healthy system**: Load app → no overlay, home page renders immediately, green status dot
- **Local dev**: Run locally without Azure env vars → Phase 1 only, failures show
  "Run `./catan.ps1 doctor` for help"
- **LG WebOS TV**: Load on TV → auto-proceeds on healthy, overlay readable on failure
  (large text, high contrast)
