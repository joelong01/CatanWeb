# Database Doctor Improvements

## Problem

`./catan.ps1 doctor` cannot verify the database schema when the `sqlite3` CLI
isn't installed (common on Windows). It prints `[SKIP] sqlite3 not found` and
reports `UNKNOWN`. The user expects doctor to inspect the schema and report
actionable results like "outdated schema -- run `./catan.ps1 database install`
to fix".

## Design

### Approach: `--check-database` CLI argument on GameService

Add a `--check-database` argument to `Catan3.GameService/Program.cs` (same
pattern as existing `--seed-database`). This uses EF Core's own model as the
source of truth -- no external tools needed.

The check compares the EF Core model's expected tables against the actual
`sqlite_master` tables in the database file. It outputs a JSON result to
stdout and exits.

### Why this approach

- **No external dependencies**: Uses the same `Microsoft.Data.Sqlite` that EF
  Core already uses. No `sqlite3` CLI, no extra NuGet packages.
- **EF Core model is source of truth**: The `CatanDbContext.OnModelCreating()`
  defines the schema. Comparing against it catches missing tables from schema
  changes without needing migrations.
- **Consistent pattern**: Follows the existing `--seed-database` pattern.
- **Works on brand new machine**: Only needs `dotnet` SDK (already a required
  dependency).

### JSON output format

```json
{
  "healthy": true,
  "databaseExists": true,
  "schemaValid": true,
  "hasPlayers": true,
  "hasGames": false,
  "hasTemplates": true,
  "playerCount": 7,
  "gameCount": 0,
  "templateCount": 2,
  "missingTables": [],
  "extraTables": [],
  "action": null
}
```

When problems exist:

```json
{
  "healthy": false,
  "databaseExists": true,
  "schemaValid": false,
  "hasPlayers": false,
  "hasGames": false,
  "hasTemplates": false,
  "playerCount": 0,
  "gameCount": 0,
  "templateCount": 0,
  "missingTables": ["GameTemplates"],
  "extraTables": [],
  "action": "install"
}
```

The `action` field tells the caller what to do:

- `null` -- everything is fine
- `"install"` -- run `./catan.ps1 database install`
- `"create"` -- database file doesn't exist, run install

### PowerShell doctor rewrite

`Invoke-DatabaseDoctor` gets two modes:

1. **API mode** (GameService running): Query
   `GET /api/database/health` -- unchanged.
2. **Offline mode** (GameService not running): Run
   `dotnet run --project Catan3.GameService -- --check-database` and parse the
   JSON output. No `sqlite3` CLI needed.

The function always returns a hashtable with consistent shape:

```powershell
@{
    Healthy       = $true/$false
    SchemaValid   = $true/$false
    HasPlayers    = $true/$false
    HasGames      = $true/$false
    HasTemplates  = $true/$false
    PlayerCount   = 7
    GameCount     = 0
    TemplateCount = 2
    MissingTables = @()
    Action        = $null  # or "install" or "create"
}
```

### `run` command integration

`./catan.ps1 run` currently calls `Initialize-Database` which only checks if
the file exists. After this change it calls `Invoke-DatabaseDoctor` (offline
mode, since it just built) and acts on the `Action` field:

- `"create"` or `"install"` -- automatically run `Install-Database`
- `$null` -- database is good, proceed

### What changes

| File | Change |
|------|--------|
| `Catan3.GameService/Program.cs` | Add `--check-database` handler |
| `catan.ps1` | Rewrite `Invoke-DatabaseDoctor`, update `Initialize-Database`, update `doctor` command display |

### What doesn't change

- `--seed-database` -- untouched
- API health endpoint -- untouched
- Azure doctor in `catan-azure.ps1` -- untouched (uses direct SQL queries)
- `Test-DatabaseSchema` -- removed (was running dotnet test, unreliable)
- `sqlite3` search logic -- removed entirely
