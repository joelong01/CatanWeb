# Design Review: Deployment Strategy

**Reviewer:** GitHub Copilot (Gemini 3 Pro Preview)
**Date:** 2026-01-20
**Target Document:** [.design/deployment.md](../../deployment.md)

## Executive Summary

The proposed deployment strategy correctly leverages Azure App Service Deployment Slots to introduce a staging environment with minimal infrastructure cost. The move to a branch-based strategy (`main` vs `staging`) provides a clear promotion path.

However, the proposed workflow for the `main` branch (**Direct Deploy to Production**) underutilizes the primary benefit of deployment slots: **Safety and Zero-Downtime Swaps**.

## Strengths

1. **Resource Efficiency:** Using deployment slots allows for a full staging environment without incurring costs for new App Service Plans (assuming Standard tier or higher).
2. **Clear Branching Model:** The definition of `staging` as an integration branch and `main` as production is intuitive and standard.
3. **Infrastructure Awareness:** The document correctly identifies the limitation of CI permissions (Contributor vs Owner) and the gap in database configuration (`database fix` vs `database deploy`).
4. **Desktop App Isolation:** Correctly identifies that the WinUI3 app is out of scope for this web-based deployment pipeline.

## Critical Findings & Risks

### 1. Direct Deploy vs. Swap Strategy (Major)

The proposed workflow triggers a direct deployment to the production slot when code is pushed to `main`:

> `main` branch -> Auto-deploy to production

**Risk:** This negates the "Zero-Downtime" and "Warm-up" benefits of deployment slots. If the deployment to the production slot fails or the app fails to start, the production site goes down.

**Recommendation:**
Adopt a **"Deploy to Staging, Swap to Production"** pattern for the `main` branch as well.

1. Push to `main`.
2. CI builds and deploys to the **Staging Slot**.
3. (Optional) Smoke tests run against Staging Slot.
4. **Slot Swap** operation promotes Staging to Production.

This ensures that the production slot is never in an invalid state during deployment.

### 2. Shared Database (Critical)

The document asks: *"Should staging use the same database or a separate staging database?"*

**Risk:** CatanWeb uses EF Core. If a feature branch merges to `staging` and applies a migration (e.g., renaming a column), and `staging` shares the production database, **Production will break immediately** because the running code depends on the old schema.

**Recommendation:**
**Staging MUST have a separate database.**
Given the low cost of an additional logical database in Azure SQL (or using SQLite for staging if strict parity isn't required, though SQL Server is preferred for fidelity), the risk of sharing the database is too high for a project with EF Core migrations.

### 3. Build Artifact Consistency

The separate workflows for `main` and `staging` imply rebuilding the application.

**Observation:** Ideally, the *exact binary* tested in staging should be the one promoted to production. However, in .NET, strictly re-building for `Release` on `main` is acceptable practice if the build process is deterministic.

## Detailed Recommendations

### Refined Workflow Proposal

Instead of Option A/B, consider this hybrid approach for `deploy-azure.yml`:

```yaml
on:
  push:
    branches: [ staging, main ]

jobs:
  deploy:
    # ... setup ...
    steps:
      # ... build ...
      
      # Step 1: ALWAYS Deploy to the Staging Slot first
      - name: Deploy to Staging Slot
        run: ./catan.ps1 azure deploy -Slot staging ...

      # Step 2: If this was 'main', Swap to Production
      - name: Swap to Production
        if: github.ref == 'refs/heads/main'
        run: |
           az webapp deployment slot swap --slot staging --target-slot production ...
```

### Database Management

1. **Create `catan-staging` DB:** Provision a second database on the existing SQL Server.
2. **App Settings:**
    * **Production Slot:** `ConnectionStrings:DefaultConnection = ...Initial Catalog=catan...`
    * **Staging Slot:** `ConnectionStrings:DefaultConnection = ...Initial Catalog=catan-staging...` (Use "Deployment Slot Setting" checkbox in Azure Portal to prevent this from swapping).

### CI/CD Gaps

* **Database Deploy:** The finding regarding `database fix` vs `database deploy` is accurate. Implementing this change is a high priority to ensure connection pooling is active.

## Nitpicks

* **Slot Configuration:** Ensure `ASPNETCORE_ENVIRONMENT` is marked as a "Deployment slot setting" (sticky) in Azure, so `Staging` doesn't accidentally become `Production` during a swap.
* **Rollback:** The document mentions "From Staging Issue: Simply revert...". Just to clarify, if a bad build goes to staging, it doesn't affect prod. If a bad swap happens, you swap back.

## Conclusion

The design is solid but needs to strictly enforce database separation to avoid catastrophic schema conflicts. Shifting the production deployment mechanism to use "Swap" rather than "Direct Deploy" provides better resilience.
