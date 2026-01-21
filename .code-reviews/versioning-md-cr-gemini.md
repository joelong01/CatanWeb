# Code Review: Versioning Strategy

**File:** `.design/systems/versioning.md`
**Reviewed:** 2026-01-21
**Reviewer:** Gemini

## Summary

The document proposes a comprehensive versioning strategy for API endpoints, persisted data sequences (recordings), and serialization models. This is primarily motivated by the need to resolve a TypeScript keyword conflict (`Type` vs `type`) by renaming a property to `ActionType`, which constitutes a breaking change for existing serialized JSON data.

## Critical Issues

None. The proposed strategy is sound and addresses the immediate problem while establishing a pattern for future breaking changes.

## Important Issues

### 1. Maintainability of "Option B: Multi-Version Support"

**Location:** Section "2. Data Versioning (Database Documents)"
The document presents "Option B" (keeping V1 loaders forever) as a viable alternative.
**Risk:** This approach accumulates technical debt rapidly. Maintaining separate `LoadGameV1`, `LoadGameV2` methods and potentially duplicate model classes (`GameModelV1`, `GameModelV2`) clutters the codebase.
**Recommendation:** Explicitly prefer **Option A (Migrate on Load)**. Transform older data into the current model format immediately upon retrieval. Only fall back to Option B if a transformation is mathematically impossible (data loss).

### 2. API Versioning Complexity

**Location:** Section "1. API Versioning"
**Risk:** Introducing `[ApiVersion("1.0")]` usually requires the `Microsoft.AspNetCore.Mvc.Versioning` NuGet package, which is not currently in `Catan3.GameService.csproj`. Adding a dependency for header-based version negotiation might be overkill if simple URL path versioning (`/api/v1/recording/...`) suffices.
**Recommendation:** Clarify if `Microsoft.AspNetCore.Mvc.Versioning` is to be added. If so, document it as a required implementation step. If not, implement simple route-based versioning manually (e.g., `[Route("api/v1/[controller]")]`).

## Suggestions

### 1. Lightweight Alternative for `Type` Property

**Location:** "Migration Path for `Type` -> `ActionType`"
**Suggestion:** While full schema versioning is robust, a custom `JsonConverter` or `[JsonPropertyName]` attribute could handle the `Type` -> `ActionType` rename without a full migration framework if that is the *only* current blocker.

```csharp
public class TestAction
{
    // Serialize as "actionType", but deserialize from "type" OR "actionType"
    [JsonPropertyName("actionType")]
    public ActionType ActionType { get; set; }
}
```

*Note:* A custom converter would be needed to accept both keys during deserialization. The proposed "Migrate on Load" strategy is cleaner architecturally but requires more boilerplate.

### 2. SignalR Message Versioning

**Location:** Section "1. API Versioning" -> "SignalR Hub"
**Suggestion:** SignalR clients and servers must be tightly coupled in this architecture. Instead of version negotiation, consider treating SignalR contracts as "always current". The client (React App) is served by the same host (or coordinated deployment) as the API. If the server updates, the client should reload. Strict versioning here may add unnecessary overhead.

## Questions

### 1. Version Storage in SQLite

**Location:** Section "2. Data Versioning"
**Question:** Where does `SchemaVersion` live in the current database structure? Is it a column on the `Recordings` table, or a property inside the JSON blob?
**Context:** `RecordingEntity` in `Versioning.md` shows it as a C# property. If using EF Core with SQLite, this requires a database migration (SQL) to add the column, or it must be part of the serialized JSON payload.

## Praise

### 1. Lazy Migration Pattern

The "Migrate on Load" approach ("Old versions eventually disappear as data is re-saved") is excellent. It avoids the need for massive "stop-the-world" database update scripts and allows the data to heal itself naturally over time.
