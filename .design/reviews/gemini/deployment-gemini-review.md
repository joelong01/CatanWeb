# Design Review: Deployment Strategy (v2)

**Reviewer:** GitHub Copilot (Gemini 3 Pro Preview)
**Date:** 2026-01-20
**Target Document:** [.design/systems/deployment.md](../../systems/deployment.md)

## Executive Summary

The revised deployment strategy is **significantly improved** and now represents a robust, production-ready design. The adoption of the "Deploy-then-Swap" pattern for the main branch addresses the critical availability risks identified in the previous review.

The strategy for database isolation has shifted from "Separate Databases" to "Table Prefixes". While this reduces infrastructure costs, it introduces application-level complexity that must be managed carefully.

## Status: **APPROVED** (with verification items)

## Improvements & Resolved Issues

### 1. Zero-Downtime Deployment (Adopted)

The design now correctly specifies a **Deploy → Swap** workflow for the `main` branch:
> Step 1: ALWAYS deploy to staging slot first
> Step 2: If main branch, swap staging to production

This ensures:

* Production is never in an invalid state.
* The exact artifact verified in the staging slot is promoted.
* Rollback is instantaneous.

### 2. Slot Configuration (Addressed)

The inclusion of **"Slot-sticky settings (critical)"** section and explicitly calling out `ASPNETCORE_ENVIRONMENT` and `X-Environment` as sticky settings is excellent. This prevents configuration drift during swaps.

### 3. CI/CD Gaps (Addressed)

The plan to replace `database fix` with `database deploy` resolves the connection pooling configuration issue.

## Remaining Risks & Verification Items

### 1. Database Isolation via Table Prefixes (Complexity Risk)

The design notes:
> Same database, isolated via table prefixes... This avoids EF Core migration conflicts while sharing infrastructure.

**Caution:** Implementing table prefixes with EF Core Migrations is non-trivial.

* **Migrations History:** You must ensure that the `__EFMigrationsHistory` table is *also* prefixed or separated (e.g., using `IGameService.HistoryRepository`), otherwise the Staging environment might mark a migration as "applied" in the shared history, preventing Production from applying it (or vice versa).
* **Runtime Mapping:** Dynamic table renaming in `OnModelCreating` based on `X-Environment` requires careful testing to ensure no cross-talk.

**Action Item:** Verify that the `state.md` design includes provisions for isolating the `__EFMigrationsHistory` table.

### 2. Header Routing for Isolation

The reliance on `X-Environment` header for routing storage logic implies that the "Staging Slot" and "Production Slot" are essentially the same running code, just behaving differently based on config/headers.

* **Verify:** Ensure that background services (which don't have HTTP request headers) correctly pick up the environment context (e.g., from `ASPNETCORE_ENVIRONMENT` environment variable) to use the correct table prefix.

## Implementation Checklist Review

The checklist is comprehensive. I recommend adding one item:

* [ ] **Verify Database Isolation:** Automated test to ensure Staging CRUD operations do not touch Production tables (and vice-versa).

## Conclusion

The design changes have addressed the major architectural concerns. The deployment workflow is now safe and standard. The chosen database isolation strategy is valid but pushes complexity into the application layer; the implementation team should treat this as a high-risk area during development.
