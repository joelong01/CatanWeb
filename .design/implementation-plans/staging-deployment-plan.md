# Staging Deployment Pipeline -- Implementation Plan

**Design doc:** [azure-deployment.md](../azure-deployment.md)

## Context

The `deploy-azure.yml` workflow is broken. Root cause:
`az webapp deploy --async true` does not prevent the Azure CLI from
polling for site startup (known
[bug](https://github.com/Azure/azure-cli/issues/29003)). Each
deploy polls for 10+ minutes waiting for the container to start
(Oryx overhead + cold start = 200-600s). With multiple deploys the
workflow runs 30+ minutes and fails.

We are starting fresh: create a `staging` branch, fix the deploy
scripts to use the Kudu REST API, deploy each component
individually to staging slots, verify each one, and add startup
logging to the React app.

## Key Decision: Reuse existing scripts

The staging workflow calls the same PowerShell scripts
(`catan-azure.ps1`) used by production. We fix the broken `az
webapp deploy` inside the scripts rather than duplicating logic in
workflow YAML. Changes to `catan-azure.ps1`:

1. Add `--Slot` parameter to `Deploy-GameService` and `Deploy-UI`
2. Replace `az webapp deploy --async true` with Kudu ZIP Deploy
   REST API (`/api/zipdeploy?isAsync=true`) in all deploy functions
3. Add a `Deploy-KuduZip` helper function for the Kudu API call +
   status polling

## Files Modified

| File | Action | Purpose |
| ---- | ------ | ------- |
| `.scripts/catan-azure.ps1` | Modify | Add Slot param, Kudu API deploy, DB access for staging identity |
| `.github/workflows/deploy-staging.yml` | New | Staging deployment workflow (3 parallel jobs) |
| `.design/azure-deployment.md` | Update | Staging architecture docs (already done) |
| `react-ui/components/StartupLogger.tsx` | New | Startup connectivity logging component |
| `react-ui/app/layout.tsx` | Modify | Mount StartupLogger in body |
| `react-ui/app/globals.css` | Modify | Startup logger CSS styles |

## Step 1: Create `staging` branch

```bash
git checkout main && git pull origin main
git checkout -b staging
```

Do not push yet -- push after all changes so the first push
triggers a meaningful deploy.

## Step 2: Fix deploy scripts (`catan-azure.ps1`)

### 2a. New helper: `Deploy-KuduZip`

Add a function that replaces `az webapp deploy --async true`:

```powershell
function Deploy-KuduZip {
    param(
        [string]$AppName,
        [string]$ResourceGroup,
        [string]$ZipPath,
        [string]$Slot = $null  # null = production
    )

    # Get publishing credentials
    $slotArgs = if ($Slot) { "--slot $Slot" } else { "" }
    $creds = Invoke-AzCommand "webapp deployment list-publishing-credentials --name $AppName --resource-group $ResourceGroup $slotArgs" -JsonOutput

    $user = $creds.publishingUserName
    $pass = $creds.publishingPassword

    # Determine SCM hostname
    $scmHost = if ($Slot) {
        "$AppName-$Slot.scm.azurewebsites.net"
    } else {
        "$AppName.scm.azurewebsites.net"
    }

    # POST zip to Kudu (truly async)
    $pair = "${user}:${pass}"
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($pair)
    $base64 = [Convert]::ToBase64String($bytes)

    $uri = "https://$scmHost/api/zipdeploy?isAsync=true"
    $response = Invoke-WebRequest -Uri $uri -Method Post `
        -InFile $ZipPath `
        -ContentType "application/zip" `
        -Headers @{ Authorization = "Basic $base64" } `
        -UseBasicParsing

    if ($response.StatusCode -ne 202) {
        Write-Log -Level "ERROR" -Message "Kudu deploy failed: HTTP $($response.StatusCode)"
        return $false
    }

    Write-Log -Level "INFO" -Message "Deploy initiated via Kudu (async)"

    # Poll deployment status
    $statusUri = "https://$scmHost/api/deployments/latest"
    for ($i = 1; $i -le 60; $i++) {
        Start-Sleep -Seconds 10
        try {
            $status = Invoke-RestMethod -Uri $statusUri `
                -Headers @{ Authorization = "Basic $base64" } `
                -TimeoutSec 15
            $code = $status.status
            Write-Log -Level "INFO" -Message "Deploy status ($($i * 10)s): $code"
            if ($code -eq 4) { return $true }   # Success
            if ($code -eq 3) { return $false }   # Failed
        } catch {
            Write-Log -Level "DEBUG" -Message "Status poll failed: $_"
        }
    }

    Write-Log -Level "WARN" -Message "Deploy status polling timed out (10 min)"
    return $true  # Deployment was submitted; site may still be starting
}
```

### 2b. Update `Deploy-GameService` (line ~1837)

Replace:

```powershell
Invoke-AzCommand "webapp deploy --name $appName --resource-group $rgName --src-path `"$zipPath`" --type zip --async true" -SuppressOutput
```

With:

```powershell
$slotParam = if ($Slot) { $Slot } else { $null }
if (-not (Deploy-KuduZip -AppName $appName -ResourceGroup $rgName -ZipPath $zipPath -Slot $slotParam)) {
    Write-Log -Level "ERROR" -Message "GameService deployment failed"
    return $false
}
```

Add `[string]$Slot = $null` to the function's `param()` block.

### 2c. Update `Deploy-UI` (line ~1901)

Same change -- replace `Invoke-AzCommand "webapp deploy ..."` with
`Deploy-KuduZip`.

Add `[string]$Slot = $null` to the function's `param()` block.

### 2d. Update `Deploy-ReactStaging` (line ~2020)

Replace the `Invoke-BackgroundInstaller` call with
`Deploy-KuduZip`. The staging slot name is already known (`staging`).

### 2e. Add staging slot setup to `Install-GameService`

Add logic to create a staging slot on `catan-api` if it doesn't
exist, similar to how `Install-UI` already creates one (line ~1697):

```powershell
# Create staging slot if it doesn't exist
$existingSlots = Invoke-AzCommand "webapp deployment slot list --name $appName --resource-group $rgName --query `"[].name`" -o tsv" -FailOnError $false
if ($existingSlots -notcontains "staging") {
    Invoke-AzCommand "webapp deployment slot create --name $appName --resource-group $rgName --slot staging" -SuppressOutput
    Invoke-AzCommand "webapp identity assign --name $appName --resource-group $rgName --slot staging" -SuppressOutput
    Invoke-AzCommand "webapp config appsettings set --name $appName --resource-group $rgName --slot staging --settings WEBSITES_CONTAINER_START_TIME_LIMIT=600" -SuppressOutput
}
```

### 2f. Add staging identity DB access

Add a function `Grant-StagingDatabaseAccess` that:

1. Gets staging slot principal ID
2. Creates temp firewall rule for current IP
3. Gets Azure AD token
4. Runs idempotent SQL:

   ```sql
   IF NOT EXISTS (SELECT 1 FROM sys.database_principals
                  WHERE name = 'catan-api/slots/staging')
   BEGIN
       CREATE USER [catan-api/slots/staging] FROM EXTERNAL PROVIDER;
   END
   -- Grant roles (idempotent checks omitted for brevity)
   ALTER ROLE db_datareader ADD MEMBER [catan-api/slots/staging];
   ALTER ROLE db_datawriter ADD MEMBER [catan-api/slots/staging];
   ALTER ROLE db_ddladmin ADD MEMBER [catan-api/slots/staging];
   ```

5. Removes temp firewall rule

### 2g. Wire `--Slot` through `catan.ps1`

Add `--Slot` parameter to the `azure deploy` verb path in
`catan.ps1` so it passes through to `catan-azure.ps1`:

```powershell
# In catan.ps1 azure deploy game-service path:
& $azureScript game-service deploy -Force:$Force -NoBuild:$NoBuild -Slot:$Slot -TraceLevel $TraceLevel
```

## Step 3: Create `deploy-staging.yml`

**File:** `.github/workflows/deploy-staging.yml`

```yaml
name: Deploy to Staging

on:
  push:
    branches: [staging]
  workflow_dispatch:

jobs:
  deploy-gameservice:
    name: Deploy GameService (staging)
    runs-on: ubuntu-latest
    permissions:
      id-token: write
      contents: read
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - name: Build GameService
        run: dotnet build Catan3.GameService -c Release
      - name: Deploy GameService to staging
        shell: pwsh
        run: |
          ./.scripts/catan-azure.ps1 game-service deploy -NoBuild -Slot staging -TraceLevel INFO
      - name: Grant staging DB access
        shell: pwsh
        run: |
          ./.scripts/catan-azure.ps1 database deploy-staging-access -TraceLevel INFO
      - name: Verify GameService staging
        run: |
          for i in $(seq 1 30); do
            CODE=$(curl -s -o /dev/null -w "%{http_code}" -m 10 \
              https://catan-api-staging.azurewebsites.net/health)
            echo "Health check ($((i*10))s): HTTP $CODE"
            [ "$CODE" = "200" ] && exit 0
            sleep 10
          done
          echo "GameService staging did not respond in 5 minutes"
          exit 1

  deploy-react:
    name: Deploy React UI (staging)
    runs-on: ubuntu-latest
    permissions:
      id-token: write
      contents: read
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '22'
          cache: 'npm'
          cache-dependency-path: react-ui/package-lock.json
      - name: Install dependencies
        working-directory: react-ui
        run: npm ci
      - name: Build Next.js
        working-directory: react-ui
        env:
          NEXT_PUBLIC_GAME_SERVICE_URL: https://catan-api-staging.azurewebsites.net
        run: npm run build
      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - name: Deploy React to staging
        shell: pwsh
        run: |
          ./.scripts/catan-azure.ps1 ui deploy-staging -TraceLevel INFO
      - name: Verify React staging
        run: |
          for i in $(seq 1 12); do
            CODE=$(curl -s -o /dev/null -w "%{http_code}" -m 10 \
              https://catan-staging.azurewebsites.net)
            echo "React staging ($((i*10))s): HTTP $CODE"
            [ "$CODE" = "200" ] && exit 0
            sleep 10
          done
          echo "React staging did not respond in 2 minutes"
          exit 1

  verify:
    name: Verify Staging
    needs: [deploy-gameservice, deploy-react]
    runs-on: ubuntu-latest
    permissions:
      id-token: write
      contents: read
    steps:
      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - name: Cross-component verification
        run: |
          echo "=== GameService Staging ==="
          curl -s https://catan-api-staging.azurewebsites.net/health | jq .

          echo ""
          echo "=== Database Connectivity ==="
          curl -s "https://catan-api-staging.azurewebsites.net/health?checkDatabase=true" | jq .database

          echo ""
          echo "=== React Staging ==="
          curl -s -o /dev/null -w "HTTP %{http_code}\n" https://catan-staging.azurewebsites.net

          echo ""
          echo "=== Staging URLs ==="
          echo "GameService: https://catan-api-staging.azurewebsites.net"
          echo "React UI:    https://catan-staging.azurewebsites.net"
```

## Step 4: React Startup Logger

### New file: `react-ui/components/StartupLogger.tsx`

Client component (`'use client'`) that:

1. On mount, records `Date.now()` as start time
2. Calls `getServiceUrl()` from `react-ui/lib/config.ts`
3. Always logs to `console.log` with `[Loading Xs]` prefix
   (matching Blazor pattern in `WebUI/wwwroot/index.html:92`)
4. Fetches `{serviceUrl}/health`
5. On success: logs result, renders nothing visible
6. On error: shows a fixed panel in bottom-left with log entries

Console output pattern:

```text
[Loading 0.0s] Page loaded
[Loading 0.0s] GameService URL: https://catan-api-staging.azurewebsites.net
[Loading 0.1s] Checking server health...
[Loading 0.3s] Health: HTTP 200
[Loading 0.3s] Database: connected
[Loading 0.3s] Ready
```

Error case (visible overlay appears):

```text
[Loading 0.0s] Page loaded
[Loading 0.0s] GameService URL: https://catan-api-staging.azurewebsites.net
[Loading 0.1s] Checking server health...
[Loading 30.1s] Health check failed: Failed to fetch
```

### Modify: `react-ui/app/layout.tsx`

Add `<StartupLogger />` to body alongside `<ThemeInitializer />`.

### Modify: `react-ui/app/globals.css`

Add styles for the error overlay:

- Fixed position, bottom-left, `z-index: 9999`
- Dark background (`rgba(0, 0, 0, 0.85)`), rounded corners
- Monospace font, `#8a8` normal text, `#f66` error text
- Max height 200px with overflow scroll
- Fade-in animation

## Step 5: Push and verify

1. Commit all changes on the `staging` branch
2. Push: `git push -u origin staging`
3. Watch `deploy-staging.yml` workflow:
   - Each job should complete in < 15 minutes
   - GameService verify step confirms `/health` returns 200
   - React verify step confirms HTTP 200
4. Manual checks:
   - `curl https://catan-api-staging.azurewebsites.net/health`
   - Open `https://catan-staging.azurewebsites.net` in browser
   - Check browser console for `[Loading Xs]` messages
5. Run `pwsh ./catan.ps1 test` locally (57 tests should pass)

## Risks

| Risk | Mitigation |
| ---- | ---------- |
| Staging managed identity lacks DB access | Workflow calls `database deploy-staging-access` to grant via SQL |
| Kudu credentials unavailable via OIDC | `az webapp deployment list-publishing-credentials` works with OIDC |
| GameService cold start exceeds 5 min | `WEBSITES_CONTAINER_START_TIME_LIMIT=600`; verify retries 5 min |
| Two workflows target same staging slot | Last push wins; they serve different purposes (staging vs main) |
| `Deploy-KuduZip` breaks production deploys | Same Kudu API, just replacing broken `az webapp deploy` call |
