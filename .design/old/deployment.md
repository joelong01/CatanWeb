# Deployment Strategy Design

**Status:** Draft
**Last Updated:** 2026-01-20

## Executive Summary

This document defines the CI/CD deployment strategy for CatanWeb using Azure App Service
deployment slots with branch-based triggers:

- **`main` branch** → Production (catan.azurewebsites.net)
- **`staging` branch** → Staging slot (catan-staging.azurewebsites.net)

## Current State

### CatanWeb Repository

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| CI | push to main, PRs to main | Build & test |
| Deploy to Azure | push to main | Deploy to production |
| CodeQL | various | Security scanning |
| Claude/Copilot | PRs | Code review |

### Catan3 Repository

| Workflow | Trigger | Purpose |
|----------|---------|---------|
| Claude Code Review | PRs | Code review |
| Claude Code | PRs | Code review |

**Note:** Catan3 is the WinUI3 desktop app. It has no deployment workflows - any previous Azure
deployment workflow has been removed. The desktop app is distributed as an MSIX package.
**No changes needed there.**

### Current Azure Resources

```text
Resource Group: rg-catan
├── App Service Plan: asp-catan
├── App Service (UI): catan → https://catan.azurewebsites.net
├── App Service (API): catan-api → https://catan-api.azurewebsites.net
├── SQL Server: sql-catan.database.windows.net
│   └── Database: catan
├── Storage Account: stcatan
└── Application Insights: ai-catan
```

## Proposed Architecture

### Azure Deployment Slots

Azure App Service deployment slots are the recommended approach for staging environments:

**Advantages:**

- Same infrastructure, different URL
- Instant swap between staging and production
- Easy rollback by swapping back
- Staging slot warms up before swap
- Shared App Service Plan (no extra compute cost for Basic+ tiers)

**URL Pattern:**

| Environment | UI URL | API URL |
|-------------|--------|---------|
| Production | catan.azurewebsites.net | catan-api.azurewebsites.net |
| Staging | catan-staging.azurewebsites.net | catan-api-staging.azurewebsites.net |

### Branch Strategy

```text
                    ┌─────────────────────────────────────┐
                    │         Feature Branches            │
                    │   (feat/*, fix/*, refactor/*, etc) │
                    └─────────────┬───────────────────────┘
                                  │ PR
                                  ▼
                    ┌─────────────────────────────────────┐
                    │           staging branch            │
                    │  Auto-deploy to staging slot        │
                    │  Integration testing environment    │
                    └─────────────┬───────────────────────┘
                                  │ PR (after validation)
                                  ▼
                    ┌─────────────────────────────────────┐
                    │            main branch              │
                    │  Auto-deploy to production          │
                    │  Stable, production-ready code      │
                    └─────────────────────────────────────┘
```

### Workflow Triggers

| Branch | CI (Build/Test) | Deploy |
|--------|-----------------|--------|
| Feature branches | On PR to staging or main | None |
| staging | On push | Deploy to staging slot |
| main | On push | Deploy to production |

## Implementation Plan

### Phase 1: Create Azure Deployment Slots

Create staging slots for both App Services:

```bash
# Create staging slot for UI
az webapp deployment slot create \
  --name catan \
  --resource-group rg-catan \
  --slot staging

# Create staging slot for API
az webapp deployment slot create \
  --name catan-api \
  --resource-group rg-catan \
  --slot staging
```

**Configuration for staging slots:**

- Copy production app settings to staging slot
- Update any environment-specific settings (e.g., `ASPNETCORE_ENVIRONMENT=Staging`)
- Configure staging slot to use the same database (or a staging database if desired)

### Phase 2: Update GitHub Actions Workflows

#### 2.1 Modify CI Workflow

Update `.github/workflows/ci.yml` to trigger on PRs to both main and staging:

```yaml
name: CI

on:
  push:
    branches:
      - main
      - staging
  pull_request:
    branches:
      - main
      - staging
```

#### 2.2 Split Deploy Workflow

Create two deployment workflows or parameterize the existing one:

##### Option A: Single workflow with matrix (Recommended)

```yaml
name: Deploy to Azure

on:
  push:
    branches:
      - main
      - staging

jobs:
  deploy:
    runs-on: ubuntu-latest
    permissions:
      id-token: write
      contents: read

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 9
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Login to Azure
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Set deployment target
        id: target
        run: |
          if [ "${{ github.ref_name }}" = "main" ]; then
            echo "slot=production" >> $GITHUB_OUTPUT
            echo "environment=production" >> $GITHUB_OUTPUT
          else
            echo "slot=staging" >> $GITHUB_OUTPUT
            echo "environment=staging" >> $GITHUB_OUTPUT
          fi

      - name: Build web projects
        run: dotnet build CatanWeb.slnf -c Release

      - name: Deploy to Azure (${{ steps.target.outputs.environment }})
        shell: pwsh
        run: |
          ./catan.ps1 azure deploy -NoBuild -TraceLevel INFO -Slot ${{ steps.target.outputs.slot }}
```

##### Option B: Separate workflows

- `.github/workflows/deploy-production.yml` - triggers on main
- `.github/workflows/deploy-staging.yml` - triggers on staging

### Phase 3: Update Deployment Scripts

Modify `.scripts/catan-azure.ps1` to support deployment slots:

1. Add `-Slot` parameter to `Deploy-GameService` and `Deploy-UI` functions
2. Update `az webapp deploy` commands to include `--slot` when specified
3. Update deployment tracking (DEPLOY_COMMIT setting) for slots

Example changes:

```powershell
function Deploy-UI {
    param(
        [hashtable]$Config,
        [switch]$Force,
        [string]$Slot = "production"  # New parameter
    )

    $slotArg = if ($Slot -ne "production") { "--slot $Slot" } else { "" }

    # Deploy command
    Invoke-AzCommand "webapp deploy --name $appName --resource-group $rgName $slotArg ..."
}
```

### Phase 4: Create Staging Branch

```bash
# Create staging branch from main
git checkout main
git checkout -b staging
git push -u origin staging

# Protect the staging branch (via GitHub UI or CLI)
gh api repos/joelong01/CatanWeb/branches/staging/protection -X PUT -f ...
```

### Phase 5: Update Documentation

1. Update CLAUDE.md with new branch strategy
2. Update .ai/ai-rules.md with staging workflow
3. Add deployment slot swap instructions for promoting staging to production

## Deployment Slot Swap (Production Promotion)

When staging is validated and ready for production, use slot swap:

```bash
# Swap staging slot to production (zero-downtime)
az webapp deployment slot swap \
  --name catan \
  --resource-group rg-catan \
  --slot staging \
  --target-slot production

az webapp deployment slot swap \
  --name catan-api \
  --resource-group rg-catan \
  --slot staging \
  --target-slot production
```

**Note:** Slot swap is instant and maintains the previous production version in the staging slot,
enabling quick rollback if needed.

## Alternative: Separate Staging Environment

If deployment slots don't meet requirements (e.g., need completely isolated staging database),
create separate Azure resources:

```text
Production:                          Staging:
├── catan.azurewebsites.net         ├── catan-stg.azurewebsites.net
├── catan-api.azurewebsites.net     ├── catan-api-stg.azurewebsites.net
└── sql-catan (database: catan)     └── sql-catan (database: catan-staging)
```

This requires:

- Separate App Services (additional compute cost)
- Separate storage account or container
- Potentially separate database
- More complex configuration management

**Recommendation:** Start with deployment slots. They're simpler, cheaper, and sufficient for most
staging needs. Move to separate environments only if isolation requirements demand it.

## Security Considerations

1. **Staging slot access:** Consider adding IP restrictions or authentication to staging slot
2. **Database access:** Staging uses same database by default; consider read-only or separate DB
3. **Secrets:** Both slots share the same Key Vault/secrets by default

## Cost Impact

| Approach | Additional Cost |
|----------|-----------------|
| Deployment slots (Basic+ tier) | Free (included in App Service Plan) |
| Separate App Services | ~2x App Service cost |

## Rollback Procedures

### From Production Issue

```bash
# Swap production back to previous version (now in staging slot)
az webapp deployment slot swap \
  --name catan \
  --resource-group rg-catan \
  --slot staging \
  --target-slot production
```

### From Staging Issue

Simply revert the staging branch or deploy a fixed version.

## Current CI/CD Gap: Database Configuration

**Issue:** The current `deploy-azure.yml` workflow calls `database fix` but not `database deploy`.

| Command           | Configures                                           |
|-------------------|------------------------------------------------------|
| `database fix`    | Network access, firewall rules, schema repair        |
| `database deploy` | All of the above PLUS connection string with pooling |

The `doctor` command flags `Connection String` and `Connection Pooling` as "MISSING (run: deploy)" because
the CI/CD doesn't configure them. The connection string was set manually during initial setup.

**Fix:** Replace `database fix` with `database deploy` in `.github/workflows/deploy-azure.yml`:

```yaml
      - name: Configure database connectivity
        shell: pwsh
        run: |
          ./.scripts/catan-azure.ps1 database deploy -TraceLevel INFO
```

`database deploy` is idempotent - it checks the doctor first and skips if already configured.

## Implementation Checklist

- [ ] **Fix database CI/CD gap** - Replace `database fix` with `database deploy` in workflow
- [ ] Create deployment slots in Azure (UI and API)
- [ ] Configure staging slot settings
- [ ] Update deploy-azure.yml workflow for branch-based deployment
- [ ] Update ci.yml to trigger on staging branch
- [ ] Update catan-azure.ps1 to support -Slot parameter
- [ ] Add `./catan.ps1 azure swap` command - promote staging to production (zero-downtime)
- [ ] Add `./catan.ps1 azure rollback` command - swap back to last known good build
- [ ] Create staging branch from main
- [ ] Configure branch protection rules
- [ ] Test staging deployment
- [ ] Test production deployment from main
- [ ] Test swap and rollback commands
- [ ] Update project documentation

## Infrastructure vs Code Deployment

**Important distinction:**

| Type           | What                         | How                                   | When                               |
|----------------|------------------------------|---------------------------------------|------------------------------------|
| Infrastructure | Resources, roles, networking | `./catan.ps1 azure install` (manual)  | One-time setup, disaster recovery  |
| Code           | Application builds           | CI/CD (`deploy-azure.yml`)            | Every push to main/staging         |

### Why Not Full CI/CD for Infrastructure?

Role assignments require **Owner** or **User Access Administrator** permissions. The GitHub Actions
service principal has **Contributor** role (cannot assign roles). This is intentional - giving CI/CD
Owner permissions is a security risk.

### Required Roles for Troubleshoot Feature

The GameService managed identity needs these roles for self-healing (Troubleshoot button):

| Role                        | Scope                  | Purpose                                    |
|-----------------------------|------------------------|--------------------------------------------|
| Reader                      | Resource Group         | Azure Resource Graph queries               |
| SQL Server Contributor      | SQL Server             | Enable public access, manage firewall      |

These are granted by `./catan.ps1 azure install` (game-service and database install commands).

### Future: Infrastructure as Code (IaC)

For fully automated infrastructure management, consider migrating to:

- **Bicep/ARM templates** - Native Azure IaC
- **Terraform** - Multi-cloud IaC
- **Pulumi** - IaC with real programming languages

Benefits of IaC:

- Version-controlled infrastructure
- Reproducible environments (dev/staging/prod)
- Drift detection
- Pull request reviews for infrastructure changes

The current PowerShell scripts serve as a working reference implementation that could be translated
to Bicep or Terraform in the future.

## Questions for Review

1. **Database isolation:** Should staging use the same database or a separate staging database?
2. **Slot swap vs direct deploy:** Should main deploy directly to production, or should we always
   go staging → swap to production?
3. **Branch protection:** What CI checks should be required before merging to staging and main?
4. **Access control:** Should staging slot require authentication or IP restrictions?
