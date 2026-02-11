# Proposals (Not Yet Implemented)

**Last verified:** January 30, 2026

## Overview

This document collects design proposals that have been written but
not yet implemented. Each section describes the proposal, its
rationale, and current status.

---

## 1. API and Data Versioning

**Source:** `systems/versioning.md`

**Problem:** No versioning exists. API endpoints are unversioned
(`/api/game/new`), the database has no schema tracking, and
recordings break on format changes.

### Proposed Approach

| Layer | Strategy |
|-------|----------|
| REST API | Route-based versioning (`/api/v1/game/new`) |
| SignalR | No versioning (client always served with matching API) |
| Persisted data | Version inside JSON payload, not SQL column |

### Migration Strategies

- **Migrate on Load** (preferred): Check version field, apply
  code-based transformations, re-save at current version
- **Multi-Version Support** (last resort): Maintain separate
  loaders for incompatible versions

### Centralized Constants

```csharp
public static class Versions
{
    public const string Api = "1.0";
    public const int RecordingSchema = 1;
    public const int SavedGameSchema = 1;
    public const int PlayerStatsSchema = 1;
}
```

### Status

**Not implemented.** The motivating example was renaming
`TestAction.type` to `actionType`, which currently requires manual
data fixes. This proposal is a prerequisite for safe schema
evolution.

---

## 2. Pane Visibility System

**Source:** `systems/pane-visibility-system.md`

**Problem:** Blazor `Game.razor` has 1500+ lines with scattered
`@if` conditions controlling which UI panels are visible in each
game state. Logic is duplicated between landscape and portrait
modes, making it error-prone to modify.

### Proposed Architecture

```
UiPane enum → PaneVisibilityConfig → PaneVisibilityService
                                          ↓
                              Game.razor reads visibility
```

### UiPane Enum

Defines all UI regions: GameName, GameControls, Purchase, RollEntry,
BoardMeasurement, GameBoard, ResourceTracking, PlayersPanel,
GoFirst, SupplementalSelect, PlayerStats, plus portrait tabs and
overlays.

### State-to-Visibility Mapping

Detailed tables define which panes are visible in each GameState
for both landscape and portrait modes. State groups (allocation,
main game, player selection) reduce duplication.

### Implementation Steps

1. Create `UiPane` enum
2. Define `PaneVisibilityConfig` data structure
3. Create `PaneVisibilityService` with state-to-visibility logic
4. Migrate existing conditions from `Game.razor`
5. Handle special cases (transitions, animations)
6. Add portrait mode support

### Status

**Not implemented.** Ready to implement. High impact on `Game.razor`
readability. Primarily a Blazor concern -- the React implementation
uses floating panels with `layoutStore` instead.

---

## 3. CosmosDB Data Access Layer

**Source:** `azure-cosmos-dal.md`

**Problem:** Local development uses SQLite, and Azure deployment
needs a cloud database. The original proposal was a comprehensive
DAL abstraction supporting both.

### Proposed Interfaces

```csharp
interface IDataRepository
{
    Task<T?> GetAsync<T>(string id);
    Task SaveAsync<T>(string id, T document);
    Task DeleteAsync(string id);
    Task<IEnumerable<T>> QueryAsync<T>(GameStateFilter filter);
}
```

### CosmosDB Design

- Serverless with partition key by document type
- Zero-config: auto-detect Azure App Service environment
- Managed Identity for authentication
- Documents: Player, GameMetadata, GameData, Image

### Status

**Rejected** in favor of Azure SQL Serverless (see below). The DAL
abstraction was deemed too complex for the actual usage pattern.

---

## 4. Azure SQL Serverless (Approved Alternative)

**Source:** `azure-sql-serverless-alternative.md`

**Problem:** Same as #3, but proposes a simpler solution.

### Approach

- Same EF Core code everywhere -- connection string switching only
- No DAL abstraction needed
- `DatabaseProviderDetector` pattern: localhost -> SQLite,
  Azure -> SQL Server

### Cost

~$5-15/month with auto-pause (serverless tier).

### Status

**Implemented.** Azure SQL Serverless is the current production
database. See [azure-deployment.md](azure-deployment.md) for
deployment details.

---

## 5. Single-Table Database Schema

**Source:** `systems/database-schema.md`

**Problem:** The current 6-table schema requires EF Core migrations
for any schema change.

### Proposed Schema

Single `Documents` table:

| Column | Type | Purpose |
|--------|------|---------|
| PrimaryKey | string | GUID or composite key |
| DocType | string | `'Game'`, `'Recording'`, `'Player'` |
| DocVersion | int | Schema version for migration |
| Data | string | Full JSON document |
| UpdatedAt | DateTime | Timestamp |

### Rationale

- Adding new document types requires no schema changes
- Migration happens in application code via `DocVersion`
- Complex analytics offloaded to Fabric/Kusto
- SQLite only handles active game state

### Status

**Not implemented.** Represents a possible future direction away
from the current multi-table schema. The current 6-table schema
works well and doesn't create migration pressure.

---

## 6. Blazor Code Consolidation

**Source:** `reduce-redundancy.md`

**Problem:** Blazor WebUI has duplicated helper methods across
rendering code.

### Consolidation Targets

1. Pattern ID helpers (`GetPatternId`, `GetHarborPatternId`)
2. Road edge vertex calculation
3. Building vertex position
4. ViewBox bounds computation
5. Pattern assets path mapping
6. Command handler error patterns

### Status

**Not implemented.** Low-risk refactoring with specific code
examples provided. However, the React port makes this less relevant
since the Blazor rendering pipeline is being replaced.

---

## 7. Stats Management CLI

**Source:** `stats-management.md`

**Problem:** No way to backup/restore/reset lifetime player
statistics. Test games pollute real statistics.

### Proposed CLI

```powershell
./catan.ps1 stats list
./catan.ps1 stats export
./catan.ps1 stats import [--mode merge|replace]
./catan.ps1 stats reset
```

### UI Addition

New Game page checkbox: "Save Lifetime Stats" (default checked).
When unchecked, the game does not update lifetime statistics.

### Status

**Partially implemented.** The Stats API endpoints exist
(`GET /api/stats`, `POST /api/stats/import`, etc.) but the CLI
verbs and the "Save Lifetime Stats" checkbox are not implemented.
