# Versioning Strategy

## Motivation

The `TestAction.Type` property serializes as `"type"` in JSON, which is problematic because
`type` is a TypeScript keyword. Renaming to `ActionType` (serialized as `"actionType"`) is
straightforward in code but requires migrating all stored recordings.

This highlights a broader need: **we have no versioning strategy** for breaking changes to:

1. **API endpoints** - REST routes and SignalR message contracts
2. **Persisted data** - Recordings, saved games, player stats in the database
3. **Model schemas** - The shape of `GameModel`, `TestAction`, etc.

## Current State (No Versioning)

- **API**: Endpoints are unversioned (`/api/game/new`, `/api/recording/{id}`)
- **Database**: No schema version tracking; migrations are implicit
- **Recordings**: No version field; format changes break old recordings
- **Models**: No version indicators; deserialization fails on schema changes

## Proposed Versioning Design

### 1. API Versioning

Route-based versioning for REST endpoints:

```text
/api/v1/game/new
/api/v1/game/{id}
/api/v1/recording/{id}
/api/v1/recordings
```

**Implementation**:

Use simple route-based versioning without additional NuGet packages:

```csharp
[Route("api/v1/[controller]")]
[ApiController]
public class GameController : ControllerBase
{
    // ...
}
```

This avoids the complexity of `Microsoft.AspNetCore.Mvc.Versioning` while still providing
clear version separation. Only add the versioning package if header-based negotiation
becomes necessary.

#### SignalR Hub: No Versioning Needed

SignalR contracts should be treated as "always current" because:

- The React client is served by the same deployment as the API
- If the server updates, the client reloads and gets the new code
- Strict SignalR versioning adds unnecessary complexity

If a breaking SignalR change occurs, handle it at deployment time (rolling restart)
rather than runtime version negotiation.

### 2. Data Versioning (Database Documents)

Add `Version` to the **JSON payload**, not as a SQL column. This avoids database migrations
and keeps version handling in application code:

```csharp
// Version lives INSIDE the JSON Data blob, not as a separate column
public class RecordingEntity
{
    public string Id { get; set; }
    public string Data { get; set; }  // Contains { "version": 1, ... }
    // ...
}

// The version is part of the deserialized data structure
public class RecordingData
{
    public int Version { get; set; } = 1;  // Inside JSON
    public GameModel InitialGameModel { get; set; }
    public List<TestAction> Actions { get; set; }
}
```

**Migration strategy**:

Two approaches depending on whether migration is feasible:

#### Option A: Migrate on Load (Preferred when possible)

- On load: Check `SchemaVersion`, apply migrations to current version
- On save: Always use current version
- Migrations are code-based transformations (not SQL)
- Old versions eventually disappear as data is re-saved

#### Option B: Multi-Version Support (Last Resort)

> **Warning**: This approach accumulates technical debt. Maintaining separate `LoadGameV1`,
> `LoadGameV2` methods and potentially duplicate model classes clutters the codebase.
> **Only use when Option A is mathematically impossible.**

When data can't be automatically migrated (e.g., semantic changes, data loss):

```csharp
public GameModel LoadGame(string json, int version)
{
    return version switch
    {
        1 => LoadGameV1(json),  // Keep V1 loader forever
        2 => LoadGameV2(json),  // V2 has different semantics
        _ => throw new NotSupportedException()
    };
}
```

This results in `if (version == 1) {} else {}` style code, which is acceptable **only** when:

- Migration would lose data that cannot be reconstructed
- Semantics changed in incompatible ways (field means something different)
- Business logic differs fundamentally between versions

**Choosing the approach**:

| Scenario | Approach |
|----------|----------|
| Field renamed (`type` → `actionType`) | Migrate on load |
| Field added with default | Migrate on load |
| Field removed (data loss) | Multi-version support |
| Semantic change (field means something different) | Multi-version support |
| Breaking algorithm change | Multi-version support |

### 3. Model Versioning

Add version to serialized model roots:

```csharp
public class RecordingData
{
    public int Version { get; set; } = 1;
    public GameModel InitialGameModel { get; set; }
    public List<TestAction> Actions { get; set; }
}

public class TestAction
{
    public ActionType ActionType { get; set; }  // Renamed from Type
    // ...
}
```

**Deserialization**:

```csharp
public RecordingData LoadRecording(string json)
{
    var doc = JsonDocument.Parse(json);
    var version = doc.RootElement.GetProperty("version").GetInt32();

    return version switch
    {
        1 => MigrateV1ToV2(JsonSerializer.Deserialize<RecordingDataV1>(json)),
        2 => JsonSerializer.Deserialize<RecordingData>(json),
        _ => throw new NotSupportedException($"Unknown version: {version}")
    };
}
```

### 4. Version Constants

Centralized version definitions:

```csharp
public static class Versions
{
    public static class Api
    {
        public const string Current = "1.0";
        public static readonly string[] Supported = ["1.0"];
    }

    public static class Schema
    {
        public const int Recording = 2;
        public const int SavedGame = 1;
        public const int PlayerStats = 1;
    }
}
```

## Migration Path for `Type` → `ActionType`

With versioning in place, the migration would be:

1. **Bump schema version**: `Recording.SchemaVersion` from 1 to 2
2. **Add migration code**:

   ```csharp
   private RecordingData MigrateV1ToV2(RecordingDataV1 v1)
   {
       // Transform "type" to "actionType" in actions
       var actions = v1.Actions.Select(a => new TestAction
       {
           ActionType = a.Type,  // Old field -> new field
           // ... copy other fields
       }).ToList();

       return new RecordingData
       {
           Version = 2,
           InitialGameModel = v1.InitialGameModel,
           Actions = actions
       };
   }
   ```

3. **Update recording load code** to check version and migrate
4. **New recordings** are saved with version 2

## Implementation Phases

### Phase 1: Add Version Fields (Non-Breaking)

- Add `SchemaVersion` to entities (default to 1)
- Add `Version` to `RecordingData` (default to 1)
- No behavior changes yet

### Phase 2: API Versioning (Non-Breaking)

- Add `/api/v1/` routes alongside existing routes
- Deprecate unversioned routes (log warnings)
- Update clients to use versioned routes

### Phase 3: Enable Migrations

- Implement migration framework
- Add first migration (`Type` → `ActionType`)
- Test with existing recordings

### Phase 4: Remove Legacy Support

- Remove unversioned API routes
- Remove v1 migration code (after all data migrated)

## Design Considerations

### Backward Compatibility Window

- Support N-1 versions minimum (current + previous)
- Deprecation period: 2 major releases before removal
- Automated migration on read (lazy migration)

### Version Mismatch Handling

```csharp
if (entity.SchemaVersion > Versions.Schema.Recording)
{
    throw new IncompatibleVersionException(
        $"Recording version {entity.SchemaVersion} is newer than " +
        $"supported version {Versions.Schema.Recording}. Update the application.");
}
```

### Client Version Negotiation

SignalR connection should negotiate compatible version:

```typescript
const connection = new HubConnectionBuilder()
    .withUrl("/gamehub", {
        headers: { "X-Api-Version": "1.0" }
    })
    .build();
```

## Files to Modify (Future Implementation)

| Area | Files |
|------|-------|
| API versioning | `Program.cs`, all controllers |
| Entity versioning | `RecordingEntity.cs`, `GameEntity.cs`, etc. |
| Model versioning | `RecordingData.cs`, `TestAction.cs` |
| Migration framework | New: `Migrations/` folder |
| Version constants | New: `Versions.cs` |

## Related Documents

- `.design/systems/model-jsonignore-to-dto.md` - TypeGen handling of model properties
- `.design/ts-port-impl-plan.md` - TypeScript port architecture
