# Stats Management Design

**Status:** Draft
**Created:** 2026-01-14

## Overview

A unified `stats` verb for managing lifetime player statistics across local and Azure
environments, plus a UI option to disable stats tracking during test games.

## Problem Statement

1. **Test games pollute stats** - When creating test recordings or debugging, games
   affect lifetime statistics
2. **No backup mechanism** - Stats can be lost during deployments or schema changes
3. **No migration path** - If schema changes, no way to export/fix/reimport data
4. **No reset capability** - Can't start fresh without database rebuild

## Requirements

### CLI Commands

```bash
./catan.ps1 stats <subcommand> [options]
```

| Command  | Description                         |
| -------- | ----------------------------------- |
| `list`   | Show all players and their stats    |
| `export` | Export all stats to JSON file       |
| `import` | Import stats from JSON file         |
| `reset`  | Delete all stats (with confirmation)|

### UI Changes

New Game page checkbox:

- **Label:** "Save Lifetime Stats"
- **Default:** Checked (On)
- **Behavior:** When unchecked, winner declaration does NOT update lifetime stats
- **Use case:** Test recordings, debugging, demo games

## Detailed Design

### CLI: `stats list`

```bash
./catan.ps1 stats list [-Local|-Azure] [-Json]
```

**Options:**

- `-Local` - Query local GameService (default)
- `-Azure` - Query Azure GameService
- `-Json` - Output as JSON for scripting

**Output (default):**

```text
Player Statistics (Local)
=========================
Player          Games  Wins  Win%   Avg Score  Best Score
--------------  -----  ----  -----  ---------  ----------
Joe-001            15     8  53.3%       8.2          12
Ryan-001           12     3  25.0%       7.1          10
Adrian-001         10     2  20.0%       6.8           9
...

Total: 5 players, 42 games played
```

### CLI: `stats export`

```bash
./catan.ps1 stats export [-Local|-Azure] [-Location <path>]
```

**Options:**

- `-Local` - Export from local GameService (default)
- `-Azure` - Export from Azure GameService
- `-Location` - Output file path (default: `./player-stats-{timestamp}.json`)

**Output file format:**

```json
{
  "exportedAt": "2026-01-14T11:30:00Z",
  "source": "local",
  "schemaVersion": 2,
  "players": [
    {
      "playerId": "Joe-001",
      "gamesPlayed": 15,
      "gamesWon": 8,
      "totalScore": 123,
      "highScore": 12,
      "lastPlayed": "2026-01-14T10:00:00Z",
      "winsByGameType": {
        "Regular": 5,
        "Expansion": 3
      },
      "gamesByGameType": {
        "Regular": 10,
        "Expansion": 5
      }
    }
  ]
}
```

### CLI: `stats import`

```bash
./catan.ps1 stats import [-Local|-Azure] -File <path> [-Merge|-Replace]
```

**Options:**

- `-Local` - Import to local GameService (default)
- `-Azure` - Import to Azure GameService
- `-File` - Path to JSON file (required)
- `-Merge` - Merge with existing stats (default) - adds to existing values
- `-Replace` - Replace existing stats completely

**Merge behavior:**

- If player exists: Add games/wins/scores to existing
- If player doesn't exist: Create new record
- Preserves higher high scores

**Replace behavior:**

- Deletes all existing stats
- Imports exactly what's in the file

### CLI: `stats reset`

```bash
./catan.ps1 stats reset [-Local|-Azure] [-Yes]
```

**Options:**

- `-Local` - Reset local GameService (default)
- `-Azure` - Reset Azure GameService
- `-Yes` - Skip confirmation prompt

**Behavior:**

- Requires confirmation unless `-Yes` provided
- Deletes ALL player statistics
- Does NOT delete players themselves (if stored separately)

### UI: Save Lifetime Stats Checkbox

**Location:** New Game page, in game settings section

**Component:**

```razor
<div class="setting-row">
    <label>
        <input type="checkbox" @bind="SaveLifetimeStats" />
        Save Lifetime Stats
    </label>
    <span class="setting-hint">Uncheck for test games</span>
</div>
```

**Data flow:**

1. Checkbox state stored in `GameCreationOptions`
2. Passed to `POST /api/game/new` as `saveLifetimeStats: bool`
3. Stored in `GameModel.SaveLifetimeStats`
4. When winner declared, check this flag before updating stats

**Default:** `true` (stats are saved)

## API Endpoints

### New Endpoints

| Method | Endpoint           | Description              |
| ------ | ------------------ | ------------------------ |
| GET    | `/api/stats`       | List all player stats    |
| GET    | `/api/stats/export`| Export all stats as JSON |
| POST   | `/api/stats/import`| Import stats from JSON   |
| DELETE | `/api/stats`       | Reset all stats          |

### Modified Endpoints

| Method | Endpoint        | Change                           |
| ------ | --------------- | -------------------------------- |
| POST   | `/api/game/new` | Add `saveLifetimeStats` parameter|

### Modified Game Logic

**WinnerService.DeclareWinnerAsync():**

```csharp
public async Task DeclareWinnerAsync(GameModel game, string winnerId)
{
    // ... existing winner logic ...

    // Only update lifetime stats if enabled for this game
    if (game.SaveLifetimeStats)
    {
        await _statsService.RecordGameResultAsync(game, winnerId);
    }
}
```

## File Format Versioning

The export format includes `schemaVersion` for future compatibility:

| Version | Changes                                        |
| ------- | ---------------------------------------------- |
| 1       | Initial format (current PlayerStats table)     |
| 2       | Added `winsByGameType`, `gamesByGameType`      |

Import logic should handle schema migrations:

- v1 to v2: Initialize game type breakdowns from totals (assume Regular)

## Help Text

### Main Help Addition

Add to `./catan.ps1 help` output:

```text
Stats:
  ./catan.ps1 stats list       - Show player statistics summary
  ./catan.ps1 stats export     - Export stats to JSON file
  ./catan.ps1 stats import     - Import stats from JSON file
  ./catan.ps1 stats reset      - Delete all statistics
  ./catan.ps1 stats            - Show detailed stats help
```

### Detailed Stats Help

When running `./catan.ps1 stats` without subcommand:

```text
Stats Management Commands
=========================

Usage: ./catan.ps1 stats <subcommand> [options]

Subcommands:
  list     - Show all player statistics
  export   - Export stats to JSON file
  import   - Import stats from JSON file
  reset    - Delete all statistics

Options:
  -Local        Target local GameService (default)
  -Azure        Target Azure GameService
  -Json         Output as JSON (for list)
  -Location     File path for export (default: ./player-stats-{timestamp}.json)
  -File         File path for import (required for import)
  -Merge        Merge imported stats with existing (default for import)
  -Replace      Replace all stats with imported data
  -Yes          Skip confirmation prompts

Examples:
  ./catan.ps1 stats list                        - List local player stats
  ./catan.ps1 stats list -Azure                 - List Azure player stats
  ./catan.ps1 stats list -Json                  - Output as JSON for scripting

  ./catan.ps1 stats export                      - Export local stats to timestamped file
  ./catan.ps1 stats export -Azure               - Export Azure stats
  ./catan.ps1 stats export -Location ./backup/  - Export to specific directory

  ./catan.ps1 stats import -File stats.json     - Import and merge with local
  ./catan.ps1 stats import -File stats.json -Azure -Replace
                                                - Replace Azure stats completely

  ./catan.ps1 stats reset                       - Reset local stats (prompts)
  ./catan.ps1 stats reset -Azure -Yes           - Reset Azure stats (no prompt)

Typical Workflows:

  Before deployment (backup):
    ./catan.ps1 stats export -Azure -Location ./backup/pre-deploy-stats.json

  After deployment (restore if needed):
    ./catan.ps1 stats import -Azure -File ./backup/pre-deploy-stats.json

  Creating test recordings:
    1. Start new game with "Save Lifetime Stats" unchecked
    2. Play and record the game
    3. Stats remain unchanged

  Fresh start:
    ./catan.ps1 stats reset -Yes
```

## Typical Workflows

### Before Deployment

```bash
# Export current stats as backup
./catan.ps1 stats export -Azure -Location ./backup/stats-pre-deploy.json

# Deploy
./catan.ps1 azure deploy

# Verify stats survived (or import backup if needed)
./catan.ps1 stats list -Azure
```

### Creating Test Recordings

1. Start new game with "Save Lifetime Stats" unchecked
2. Play through game scenarios
3. Record game
4. Winner declaration does NOT affect lifetime stats

### Schema Migration

```bash
# Export from old schema
./catan.ps1 stats export -Azure -Location ./stats-v1.json

# Manually edit JSON to fix/migrate data
# (or write a script to transform)

# Import with replace
./catan.ps1 stats import -Azure -File ./stats-v2.json -Replace
```

### Fresh Start

```bash
# Reset all stats
./catan.ps1 stats reset -Azure -Yes
```

## Implementation Order

### Phase 1: Core Infrastructure

1. **StatsController.cs** - New controller with endpoints
2. **StatsService.cs** - Export/import/reset logic
3. **catan.ps1 stats** - CLI verb with subcommands

### Phase 2: UI Integration

1. **GameModel.SaveLifetimeStats** - Add property
2. **NewGame.razor** - Add checkbox
3. **WinnerService** - Check flag before recording stats

### Phase 3: Testing

1. Create test recordings with stats disabled
2. Test export/import round-trip
3. Test Azure connectivity

## Code Locations

### New Files

| File | Purpose |
| ---- | ------- |
| `Catan3.GameService/Controllers/StatsController.cs` | Stats API endpoints |
| `Catan3.GameService/Services/StatsExportService.cs` | Export/import logic |

### Files to Modify

| File | Changes |
| ---- | ------- |
| `catan.ps1` | Add `stats` verb |
| `Catan3.Shared/Models/GameModel.cs` | Add `SaveLifetimeStats` property |
| `WebUI/Pages/NewGame.razor` | Add checkbox |
| `Catan3.GameService/Services/StatsService.cs` | Conditional recording |

## Acceptance Criteria

- [ ] `./catan.ps1 stats list` shows player stats summary
- [ ] `./catan.ps1 stats export` creates valid JSON file
- [ ] `./catan.ps1 stats import` restores stats from JSON
- [ ] `./catan.ps1 stats reset` clears all stats (with confirmation)
- [ ] `-Azure` flag works for all stats commands
- [ ] New Game page has "Save Lifetime Stats" checkbox
- [ ] Unchecking prevents stats from being saved when winner declared
- [ ] Export/import round-trip preserves all data
- [ ] Schema version in export enables future migrations
- [ ] Help text is clear with examples

## Open Questions

1. **Player deletion?** Should `stats reset` also delete player profiles, or just stats?
   - Recommendation: Just stats, keep player profiles

2. **Per-player export?** Allow exporting/importing single player stats?
   - Recommendation: Start with all-or-nothing, add per-player later if needed

3. **Stats history?** Should we keep per-game records or just aggregates?
   - Recommendation: Aggregates only (current design), per-game in recordings

4. **Merge conflicts?** What if importing stats for a player with different high score?
   - Recommendation: Keep higher value for high scores, sum for totals
