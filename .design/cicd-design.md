# CI/CD System Design

**Date:** 2026-04-01
**Status:** Audit complete — bugs filed

## Overview

The CI/CD system uses GitHub Actions with Azure OIDC (no stored secrets) to deploy
a .NET GameService + React/Next.js UI to Azure App Service. Two environments:
production (main branch) and staging (staging branch).

## Architecture

```text
Feature branch
    │ (push)
    ▼
CI (ci.yml, windows-latest)
    ├─ Build .NET solution (Release)
    ├─ Run tests (Shared + GameService, excludes CatanDb + Desktop)
    └─ CodeQL security scan
    │
    ▼ (merge to staging)
Deploy to Staging (deploy-staging.yml, ubuntu-latest)
    ├─ Detect changes (backend vs frontend)
    ├─ GameService → staging slot (if backend changed)
    ├─ React → staging slot (if frontend changed)
    └─ Verify health
    │
    ▼ (merge staging → main)
Deploy to Production (deploy-azure.yml, ubuntu-latest)
    ├─ Detect changes
    ├─ GameService → production slot (DIRECT, no blue-green)
    ├─ React → staging slot → SWAP to production (blue-green)
    └─ Verify production health
```

## Workflow Files

| File | Trigger | Purpose |
|------|---------|---------|
| `ci.yml` | Push to main, PRs to main | Build + test (.NET, Windows) |
| `deploy-staging.yml` | Push to staging, manual | Deploy to staging slots |
| `deploy-azure.yml` | Push to main, manual | Deploy to production |
| `deploy-react-staging.yml` | Manual only | Deploy React to staging with prod backend |
| `codeql.yml` | Push to main/staging, PRs | Security scanning (C#, JS) |
| `claude.yml` | @claude mentions | Claude Code integration |

## Authentication

All Azure authentication uses **OIDC federated identity** — no stored credentials.

| Secret | Purpose |
|--------|---------|
| `AZURE_CLIENT_ID` | App registration client ID |
| `AZURE_TENANT_ID` | Azure AD tenant |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription |

**App Registration:** `github-actions-catan-deploy`
**Federated Credentials:** `main` and `staging` branches only
**Role:** Contributor on `rg-catan` resource group
**Setup:** `./catan.ps1 github install -Azure` (also runs during `install -Azure`)

## Deployment Scripts

### Entry Points

| Command | What it does |
|---------|-------------|
| `./catan.ps1 deploy -Azure` | Deploy GameService + React to production |
| `./catan.ps1 deploy -Azure -Staging` | Deploy to staging slots |
| `./catan.ps1 install -Azure` | Create infra + deploy code + configure OIDC |
| `.scripts/catan-cicd.ps1 gameservice` | CI/CD orchestrator for GameService |
| `.scripts/catan-cicd.ps1 react` | CI/CD orchestrator for React |

### Deployment Flow (GameService)

1. Check/create deployment slot (if staging)
2. Configure slot settings (Cosmos endpoint, timeout, etc.)
3. Check if deployment needed (compare `DEPLOY_COMMIT` app setting)
4. `dotnet publish` → zip → Deploy via Kudu ZIP API
5. Poll deployment status (up to 10 minutes)
6. Restart app
7. Store commit hash in app settings

### Deployment Flow (React UI)

1. Configure slot for Node.js 22 (runtime can change after slot swap)
2. `npm ci` → `npm run build` (Next.js standalone)
3. Assemble package (standalone + static + public)
4. Deploy via Kudu ZIP API
5. Poll deployment status
6. Restart
7. Store commit hash

### Kudu ZIP Deploy

All deploys use the Kudu REST API (`/api/zipdeploy?isAsync=true`) instead of
`az webapp deploy` which has a known bug with async polling. The `Deploy-KuduZip`
function in `utility-scripts.psm1` handles:

- Azure AD bearer token auth (works with disabled SCM basic auth)
- Async upload (HTTP 202)
- Status polling every 10s for up to 10 minutes
- Status codes: 0=Pending, 1=Building, 2=Deploying, 3=Failed, 4=Success

## Environments

| Environment | React URL | GameService URL | Deploys from |
|-------------|-----------|-----------------|-------------|
| Production | catan.azurewebsites.net | catan-api.azurewebsites.net | main branch |
| Staging | catan-staging.azurewebsites.net | catan-api-staging.azurewebsites.net | staging branch |

All environments share the same CosmosDB database.

## Blue-Green Deployment

**React UI:** Uses slot swap. New code deploys to staging slot, verified, then
swapped to production. Rollback = swap back.

**GameService:** Deploys directly to production slot. No blue-green. This is
a known gap (see #147).

## Change Detection

Workflows detect what changed between commits to skip unnecessary deploys:

- **Backend:** `Catan3.GameService/`, `Catan3.Shared/`, `.scripts/`, `.azure/`
- **Frontend:** `react-ui/`
- **Manual dispatch:** Deploys both regardless

## Health Verification

After deployment, workflows verify:

- **GameService:** `GET /health` must return `{"status":"healthy",...}`
- **React UI:** `GET /` must return HTTP 200

Polling: 10s intervals, up to 5 minutes for staging, 3 minutes for production.

## Known Issues

See filed bugs below. Key risks:

1. Kudu polling timeout returns success even if deploy still in progress
2. GameService has no blue-green deployment
3. No automatic rollback on failure
4. Hardcoded polling intervals with no exponential backoff
