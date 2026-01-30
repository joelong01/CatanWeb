# System Audit Summary

**Date:** Jan 30, 2026
**Auditor:** Gemini (Automated Agent)
**Scope:** Full codebase review, Design Documentation, and Architecture verification.

## 1. Executive Summary

The CatanWeb project is in a **late-stage migration phase** from a Blazor/Desktop hybrid model to a modern React + ASP.NET Core architecture. The core business logic (`Catan3.Shared`) is robust, deterministic, and well-isolated, allowing the backend to serve multiple frontend clients reliably.

However, the repository carries significant "conceptual debt" from the legacy Blazor and Desktop implementations. Documentation was fragmented between the root `.design` folder (legacy) and the newer implementation reality.

**Verdict**: The system is stable and the architecture is sound, but the repository needs house-cleaning to clarify the primary development path (React).

## 2. Key Findings

### ✅ The Good

* **Single Source of Truth**: `GameStateMachine` in `Catan3.Shared` successfully drives logic for all platforms. The decision to make it deterministic enabling "Replay Testing" is a major architectural win.
* **Hybrid Persistence**: The generic `IPersistenceService` backing both SQL/Blob storage (Server) and File storage (Desktop) works seamlessly.
* **Coordinate System**: The Cubic Coordinate implementation is mathematically sound and consistent across C# and TypeScript.
* **Tooling**: `catan.ps1` is a powerful unifying tool that abstracts away the complexity of the multi-project build.

### ⚠️ The Concerns

* **Legacy Code Weight**: The `WebUI` (Blazor WASM) and `WebUI.Server` projects appear to be deprecated in favor of `react-ui`, but are still part of the solution and build process.
* **Hardcoded Logic**:
  * "House Rules" like the "Grief Dodgy" mechanics are compiled into `GameStateMachine` rather than being data-driven configurations.
  * CSS Themes are consistent, but color palettes are duplicated across `app.css` (Blazor) and `globals.css` (React).
* **Security**: The system assumes a trusted environment (local LAN / authenticated via simple ID). There is no robust auth token (JWT) implementation visible for the API yet.

### 🔍 Documentation State

* **Before**: Documentation was scattered across 30+ files in `design_docs/`, mixing aspirational ideas (2025) with legacy implementation details.
* **After**: Validated "As-Built" documentation is now centralized in `.design/gemini/`.

## 3. Recommendations & Action Items

### Priority 1: Cleanup & Consolidation

1. **Tag & Archive Blazor**: If React is the way forward, formally mark `WebUI` and `WebUI.Server` as deprecated. Move them to a `_archive` folder or remove them from the solution filter (`CatanWeb.slnf`) to speed up build times.
2. **Delete Legacy Docs**: The files in `design_docs/` that have been ported to `.design/gemini/` should be deleted to prevent confusion.

### Priority 2: Technical Debt

1. **Extract House Rules**: Refactor `GameStateMachine` to accept a `RulesConfiguration` object. Move "Grief Dodgy" and "Supplemental Build" logic behind feature flags in this configuration.
2. **API Standardization**: Generate an OpenAPI (Swagger) definition for the `GameService`. Currently, the API contract is implied by the C# Controllers and React fetch calls.

### Priority 3: Infrastructure

1. **Containerization**: The current `catan.ps1` reliance is heavy. Adding a `docker-compose.yml` that spins up the GameService, SQL Edge (DB), and React Frontend would standardize the dev environment further and remove "it works on my machine" issues.
2. **HTTPS/Certificates**: Address the SSL friction documented in `troubleshooting.md` by setting up a proper dev certificate for the React proxy.

## 4. Conclusion

The standard of code in `Catan3.Shared` is high quality. The migration to React is succeeding in replicating the rich logic of the Desktop app. The primary risk is the cognitive load of maintaining two parallel UI stacks (Blazor/Desktop vs React) and the associated build complexity.
