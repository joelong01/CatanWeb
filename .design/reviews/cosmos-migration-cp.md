# Review: cosmos-migration.md

**Reviewer:** GitHub Copilot (GPT-5.4)
**Date:** 2026-03-18
**Document reviewed:** `.design/cosmos-migration.md`

This revision is substantially better. The major gaps from the earlier drafts are now covered:

- production migration from Azure SQL is explicitly planned
- raw SQL DDL inside `GameApiController` is called out
- `ICatanDb` lifetime is mostly normalized to scoped
- recording `gameId` is treated as a first-class stored field
- DTO reuse is now discussed instead of blindly inventing duplicates

What remains are a few concrete inconsistencies. Findings below are ordered by severity.

---

## High: Rollback Instructions Use the Wrong Configuration Knob

The production migration track says:

> revert `COSMOS_ENDPOINT` to the Azure SQL connection string and restart

That is not how the app currently selects SQL Server. The existing code uses `ConnectionStrings:AzureSql`
plus provider selection logic in `DatabaseProviderDetector` via `DATABASE_PROVIDER` / Azure hosting
conditions. `COSMOS_ENDPOINT` is a CosmosDB setting, not a valid place to put a SQL connection string.

If someone follows the rollback steps as written, they will misconfigure the app rather than restore
the previous SQL-based deployment.

**Recommendation:** Rewrite rollback to something like:

1. restore the previous `DATABASE_PROVIDER` / provider selection settings
2. restore `ConnectionStrings:AzureSql` if it was changed
3. remove or ignore Cosmos-specific settings
4. restart the app and run doctor/health checks

---

## High: The Plan Still Incorrectly Says `DatabaseBackedPersistenceService` Can Drop `IServiceScopeFactory`

Phase 1 step 8 correctly keeps `DatabaseBackedPersistenceService` using a scope to resolve scoped
services. But the same bullet then says that in Phase 2, once `CosmosCatanDb` is scoped over a
singleton `CosmosClient`, the scope factory pattern can be removed.

That conclusion does not follow from the stated lifetimes:

- `IPersistenceService` is currently registered as **singleton**
- `IGamePersistence` is currently **scoped**
- `ICatanDb` is now planned as **scoped**

As long as `DatabaseBackedPersistenceService` remains singleton, it cannot directly inject scoped
`IGamePersistence` or scoped `ICatanDb`. It still needs a scope factory, regardless of whether the
underlying Cosmos client is singleton.

**Recommendation:** Pick one of these explicitly:

1. keep `DatabaseBackedPersistenceService` singleton and keep the scope factory permanently, or
2. change `IPersistenceService` / `DatabaseBackedPersistenceService` to scoped as part of the migration

The current wording promises a simplification that the described lifetimes do not allow.

---

## Medium: `RecordingSummary` Reuse Is Inconsistent with the New `gameId` Requirement

Phase 1 step 2 says to reuse `RecordingSummary` from `Catan3.Shared/Services/GameServiceProxy.cs`.
That existing type currently contains:

- `Id`
- `Name`
- `CreatedAt`
- `GameType`
- `PlayerCount`
- `ActionCount`

It does **not** contain `GameId`.

But the design now explicitly says recording summaries should stop parsing `gameId` from raw JSON
and instead read it directly from stored data. That implies the summary contract needs `GameId`.

So either:

- the shared proxy `RecordingSummary` type must be expanded to include `GameId`, or
- the plan should stop claiming that the current proxy type can be reused as-is

**Recommendation:** Resolve this explicitly in the design doc to avoid implementation churn across
`RecordingController`, `GameServiceProxy`, and any callers in WebUI/React.

---

## Medium: The Interface Section Still Uses Type Names That Do Not Match the Reuse Guidance

The Phase 1 steps now correctly say to reuse existing shared models where possible, but the
`ICatanDb` interface section still declares:

```csharp
Task<GameTemplate?> LoadTemplateAsync(string id);
Task SaveTemplateAsync(GameTemplate template);
```

There is no established `GameTemplate` shared model in the current codebase; the existing type is
`GameTemplateData`. The same section also leaves other names like `Recording` and `CompletedGame`
underspecified.

This is no longer a conceptual problem, but it is still a document accuracy problem: the interface
block and the implementation notes point in slightly different directions.

**Recommendation:** Update the interface section to use real, agreed type names now. If the intent
is `GameTemplateData`, say that explicitly. If new persistence-facing models are required, name them
consistently here rather than using placeholders.

---

## Summary

| Issue | Severity | Required Before Implementation? |
|---|---|---|
| Rollback instructions use `COSMOS_ENDPOINT` incorrectly for Azure SQL recovery | High | Yes |
| `DatabaseBackedPersistenceService` scope-factory removal is inconsistent with planned lifetimes | High | Yes |
| `RecordingSummary` reuse conflicts with the new `gameId` requirement | Medium | Yes |
| `ICatanDb` interface still uses unresolved/placeholder model names | Medium | Yes |

## Overall Assessment

The design is now close to implementation-ready. The remaining issues are not architectural reversals;
they are precision problems in configuration, service lifetimes, and contract naming. Fix those and
the plan becomes much more reliable as an execution document.
