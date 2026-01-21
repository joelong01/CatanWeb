# Database Schema Design

## Philosophy

The database is a **hot cache for active game state**, not an analytics store. Complex queries,
historical analysis, and leaderboards belong in Fabric/Kusto. This allows an extremely simple
schema that rarely needs migration.

## Single Table Design

All documents stored in one table with a minimal, stable schema:

```sql
CREATE TABLE Documents (
    PrimaryKey    TEXT NOT NULL PRIMARY KEY,  -- GameId, RecordingId, PlayerId
    DocType       TEXT NOT NULL,              -- 'Game', 'Recording', 'Player'
    DocVersion    INTEGER NOT NULL DEFAULT 1, -- Schema version for migration
    Data          TEXT NOT NULL,              -- JSON payload
    UpdatedAt     TEXT NOT NULL
);

-- Single index for "list by type" queries
CREATE INDEX IX_DocType ON Documents(DocType);
```

**That's the entire schema.** No secondary keys, no complex indices, no joins.

## Document Types

| DocType | PrimaryKey | Data Contains |
|---------|------------|---------------|
| `Game` | GameId (GUID) | Full GameModel JSON |
| `Recording` | RecordingId (GUID) | RecordingData with actions |
| `Player` | PlayerId (string) | Player profile, preferences |

## Access Patterns

All queries are simple primary key lookups or type scans:

| Operation | Query |
|-----------|-------|
| Get game by ID | `WHERE PrimaryKey = @id` |
| Get recording by ID | `WHERE PrimaryKey = @id` |
| Get player by ID | `WHERE PrimaryKey = @id` |
| List all active games | `WHERE DocType = 'Game'` |
| List all recordings | `WHERE DocType = 'Recording'` |

## Versioning

`DocVersion` indicates the schema version of the JSON in `Data`. See
[versioning.md](./versioning.md) for migration strategies.

```csharp
public class Document
{
    public string PrimaryKey { get; set; }
    public string DocType { get; set; }
    public int DocVersion { get; set; }
    public string Data { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

On load:

1. Check `DocVersion`
2. If older than current, migrate JSON and update `DocVersion`
3. On save, always write current version

## Why Not Multiple Tables?

| Approach | Schema Changes | Index Maintenance | Complexity |
|----------|----------------|-------------------|------------|
| Table per entity | High (new table per type) | High (indices per table) | High |
| Single table | Rare (add DocType value) | Minimal (one index) | Low |

Adding a new document type (e.g., `Tournament`) requires:

- **Multi-table**: New migration, new table, new indices, new EF DbSet
- **Single-table**: Just start writing documents with `DocType = 'Tournament'`

## Analytics Pipeline

Complex queries go to Fabric/Kusto, not SQLite:

```text
Game Events → Event Hub → Fabric Lakehouse → Kusto
                                    ↓
                            Power BI / Dashboards
```

**SQLite handles:**

- Active game state during play
- Recording storage for replay tests
- Player authentication/preferences

**Fabric/Kusto handles:**

- Historical game statistics
- Player leaderboards
- Win rate analysis
- Game balance metrics

## Migration from Current Schema

Current schema uses separate tables (`Games`, `Recordings`, etc.). Migration path:

1. Create `Documents` table
2. Copy existing data with appropriate `DocType`
3. Add `DocVersion = 1` to all migrated documents
4. Remove old tables
5. Update EF Core DbContext to use single `Documents` DbSet

## EF Core Implementation

```csharp
public class CatanDbContext : DbContext
{
    public DbSet<Document> Documents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.PrimaryKey);
            entity.HasIndex(e => e.DocType);
        });
    }
}
```

Repository pattern for type-safe access:

```csharp
public class DocumentRepository
{
    public async Task<T?> GetAsync<T>(string id, string docType) where T : class
    {
        var doc = await _context.Documents
            .FirstOrDefaultAsync(d => d.PrimaryKey == id && d.DocType == docType);

        if (doc == null) return null;

        return MigrateAndDeserialize<T>(doc);
    }

    public async Task<List<T>> ListAsync<T>(string docType) where T : class
    {
        var docs = await _context.Documents
            .Where(d => d.DocType == docType)
            .ToListAsync();

        return docs.Select(MigrateAndDeserialize<T>).ToList();
    }

    public async Task SaveAsync<T>(string id, string docType, T data)
    {
        var json = JsonSerializer.Serialize(data);
        var doc = new Document
        {
            PrimaryKey = id,
            DocType = docType,
            DocVersion = Versions.Schema.GetVersion(docType),
            Data = json,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Documents.Update(doc);
        await _context.SaveChangesAsync();
    }
}
```

## Related Documents

- [versioning.md](./versioning.md) - Schema versioning and migration strategies
