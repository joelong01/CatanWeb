# Design Review: Storage Service (State Management)

**Reviewer:** GitHub Copilot (Gemini 3 Pro Preview)
**Date:** 2026-01-20
**Target Document:** [.design/systems/state.md](../../systems/state.md)

## Executive Summary

The proposed design correctly identifies the need for environment isolation and decoupling of storage logic from the core game service. The shift to **Azure Cosmos DB** (Serverless) is a strong architectural choice that aligns well with the "ephemeral games, persistent stats" nature of the application. The use of a dedicated Storage Service acts as an effective anti-corruption layer.

However, the decision to implement this service in **Go or Rust** introduces significant operational complexity and "cognitive load" for a team primarily focused on .NET.

## Strengths

1. **Architecture:** The "Single Reader/Writer" pattern correctly isolates data access, simplifying the GameService and preventing schema leakage.
2. **Technology Fit:** Cosmos DB Serverless is cost-effective for mostly idle or bursty workloads like this game, and eliminates the fixed cost of SQL Server.
3. **Isolation Strategy:** Using container prefixes (`prod_`, `staging_`) effectively solves the "shared database" risk identified in previous reviews without requiring multiple expensive database accounts.
4. **API Design:** The move to "App-Semantic" APIs (e.g., `SaveGame` vs `InsertRow`) prevents the internal data structure from leaking to consumers.

## Critical Risks & Concerns

### 1. Language Fragmentation vs. Learning Goals (Trade-off)

The design suggests implementing the Storage Service in **Go or Rust**:

> Recommendation: Start with Go for faster iteration

**Analysis:**

* **For Production Efficiency:** Sticking to .NET is optimal because it allows code sharing (DTOs from `Catan3.Shared`) and unified tooling.
* **For Learning/Experimentation:** Since this project has explicit learning goals (and a planned frontend move to TypeScript/React), implementing the Storage Service in **Go or Rust** is an excellent architectural choice to explore polyglot microservices.

**Updated Recommendation:**
**Proceed with Go or Rust** (aligning with the goal of learning/experimenting), but be aware of the trade-off: **Code Duplication**.

* You cannot use `Catan3.Shared` in Go/Rust.
* You will need to manually sync the JSON DTO definitions between `GameService` (C#) and `StorageService` (Go/Rust).
* **Mitigation:** Consider defining the API contract in OpenAPI (Swagger) and generating the server stubs (or client SDKs), or simply accept the manual maintenance cost as part of the learning exercise.

### 2. Header Trust Model (Moderate)

> "A request with X-Environment: test can never read/write production data"

**Risk:** While acceptable for internal separation, relying *solely* on a client-provided header (`WebUI -> GameService -> StorageService`) is fragile. If a developer accidentally hardcodes headers or a bug in the proxy logic occurs, data could be written to the wrong container.

**Recommendation:**
The `GameService` should potentially **override** or **validate** this header based on its own running environment (`ASPNETCORE_ENVIRONMENT`), rather than blindly trusting the WebUI.

* Production GameService -> Forces `X-Environment: production` (or rejects non-prod).
* Staging GameService -> Forces `X-Environment: staging`.
* Dev/Test -> Allowing client override is fine.

### 3. Local Development Friction (Minor)

> "SQLite via EF Core" -> "Cosmos DB Emulator"

**Risk:** The Cosmos DB Emulator is significantly heavier than SQLite. It requires Docker (on macOS/Linux) or a local install (Windows), and can be slow to start.

**Recommendation:**
Ensure the `catan.ps1` developer scripts handle the emulator lifecycle gracefully. Consider an in-memory mock implementation of the Storage Service for unit tests to avoid requiring the emulator for every test run.

## Detailed verification Items

### 1. Partition Function

The design defines:

* `Players` -> `/id`
* `Stats` -> `/playerId`

**Verify:** Is `Stats` query pattern always "Get stats for player X"? If you ever need "Global High Scores", a partition key of `/playerId` makes that a cross-partition query (expensive). Consider if you need a "GlobalStats" container or a different partitioning strategy for leaderboards.

### 2. ETag / Concurrency

The design mentions `PUT /api/games/{id}`.

**Verify:** Does the API support Optimistic Concurrency Control (ETags)? If two players make a move simultaneously, the Storage Service must reject the second write if the game state has changed. Cosmos DB supports this natively (`_etag`), but the REST API must expose it (`If-Match` header).

## Conclusion

The architecture is sound, but the implementation language choice (**Go/Rust**) is a premature optimization that introduces unnecessary friction. Sticking to **.NET** will allow the team to move much faster by sharing models and tooling.

**Action:**

1. Change implementation language to **.NET 9**.
2. Add **Optimistic Concurrency** (ETag support) to the API specification.
3. Refine the `Stats` container partitioning strategy if leaderboards are a requirement.
