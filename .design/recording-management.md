# Recording Management Design

**Status:** Implemented
**Created:** 2026-01-14

## Overview

A unified `recording` verb for managing test recordings across local and Azure environments. This replaces the fragmented `database export-tests` and `database import-tests` commands with a cohesive interface.

## Command Structure

```bash
./catan.ps1 recording <subcommand> [options]
```

### Subcommands

| Command | Description |
|---------|-------------|
| `list` | List all recordings |
| `save` | Save recordings to files |
| `load` | Load recordings from files |
| `delete` | Delete recordings |
| `replay` | Replay recordings (existing functionality) |

## Detailed Commands

### List Recordings

```bash
./catan.ps1 recording list [-Local|-Azure] [-Json]
```

**Options:**

- `-Local` - Query local GameService (default, requires running service)
- `-Azure` - Query Azure GameService
- `-Json` - Output as JSON for scripting

**Output (default):**

```text
Recordings (Local)
==================
ID                                    Name                  Actions  Players  Type
------------------------------------  --------------------  -------  -------  ---------
47156021-00d6-483c-9205-78c3df1b3ce3  Balanced / Winner     95       5        Expansion
b7fceb07-76fb-44a3-9ad8-86a6ca3a5593  Full Simulated Game   62       5        Expansion
```

**Output (-Json):**

```json
[
  {
    "id": "47156021-00d6-483c-9205-78c3df1b3ce3",
    "name": "Balanced / Winner",
    "actionCount": 95,
    "playerCount": 5,
    "gameType": "Expansion",
    "createdAt": "2026-01-14T01:08:41.525433"
  }
]
```

### Save Recordings

```bash
./catan.ps1 recording save [-Name <name>|-All] [-Location <path>] [-Local|-Azure]
```

**Options:**

- `-Name <name>` - Save specific recording by name (supports wildcards)
- `-All` - Save all recordings (default)
- `-Location <path>` - Output directory (default: `Catan3.GameService/Default Data/Recordings/`)
- `-Local` - Save from local GameService (default)
- `-Azure` - Save from Azure GameService

**Examples:**

```bash
# Save all local recordings to default location
./catan.ps1 recording save

# Save specific recording
./catan.ps1 recording save -Name "Balanced*"

# Save from Azure to custom location
./catan.ps1 recording save -Azure -Location ./my-recordings/
```

### Load Recordings

```bash
./catan.ps1 recording load [-Name <name>|-All] [-Location <path>] [-Local|-Azure]
```

**Options:**

- `-Name <name>` - Load specific recording by filename (supports wildcards)
- `-All` - Load all recordings from location (default)
- `-Location <path>` - Input directory (default: `Catan3.GameService/Default Data/Recordings/`)
- `-Local` - Load to local GameService (default)
- `-Azure` - Load to Azure GameService

**Behavior:**

- Skips recordings that already exist (idempotent)
- Reports: imported, skipped, failed counts

**Examples:**

```bash
# Load all recordings to local
./catan.ps1 recording load

# Load to Azure
./catan.ps1 recording load -Azure

# Load specific recording
./catan.ps1 recording load -Name "Full-Simulated-Game.json"
```

### Delete Recordings

```bash
./catan.ps1 recording delete -Name <name> [-Local|-Azure] [-Yes]
```

**Options:**

- `-Name <name>` - Recording name or ID to delete (required)
- `-Local` - Delete from local GameService (default)
- `-Azure` - Delete from Azure GameService
- `-Yes` - Skip confirmation prompt

### Replay Recordings

```bash
./catan.ps1 recording replay [-Name <name>|-All] [-Local|-Azure]
```

**Options:**

- `-Name <name>` - Replay specific recording
- `-All` - Replay all recordings (default)
- `-Local` - Replay against local GameService (default)
- `-Azure` - Replay against Azure GameService

This is the existing `./catan.ps1 replay` functionality moved under `recording`.

## File Format

Recordings are stored as JSON files with the following structure:

```json
{
  "id": "guid",
  "name": "Recording Name",
  "createdAt": "2026-01-14T01:08:41.525433",
  "gameType": "Regular|Expansion",
  "playerCount": 5,
  "playerIds": "Joe-001,Ryan-001,...",
  "actionCount": 95,
  "data": "{...serialized RecordingData...}"
}
```

The `data` field contains a JSON string with:

- `initialGameModel` - Complete GameModel at recording start
- `actions[]` - Array of recorded messages with expected game hashes

## API Endpoints

Existing endpoints used by these commands:

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/recordings` | List all recordings (summary) |
| GET | `/api/recording/{id}` | Get full recording with data |
| POST | `/api/recording/import` | Import a recording |
| DELETE | `/api/recording/{id}` | Delete a recording |
| POST | `/api/recording/{id}/replay` | Replay a recording |

## Typical Workflows

### Development: Create and Save Tests

```bash
# 1. Play game in WebUI, use Record button
# 2. Export recordings to files
./catan.ps1 recording save
# 3. Commit JSON files to git
git add "Catan3.GameService/Default Data/Recordings/*.json"
git commit -m "Add test recordings"
```

### CI/CD: Load Tests to Azure

```bash
# After deployment, load test recordings
./catan.ps1 recording load -Azure

# Run replay tests against Azure
./catan.ps1 recording replay -Azure
```

### Database Rebuild: Restore Tests

```bash
# After database reinstall, reload recordings
./catan.ps1 recording load
```

### Sync Between Environments

```bash
# Save from Azure
./catan.ps1 recording save -Azure -Location ./backup/

# Load to local
./catan.ps1 recording load -Location ./backup/
```

## Implementation Notes

### Current State (Partial Implementation)

The following was partially implemented before this design was created:

- `RecordingService.ImportRecordingAsync()` - Added
- `POST /api/recording/import` endpoint - Added
- `./catan.ps1 database export-tests` - Added (to be replaced)
- `./.scripts/catan-azure.ps1 database import-tests` - Added (to be replaced)

### Migration Plan

1. Create unified `recording` verb in `catan.ps1`
2. Move existing `replay` command under `recording replay`
3. Replace `database export-tests` with `recording save`
4. Replace Azure `database import-tests` with `recording load -Azure`
5. Update help text and documentation

### Code Locations

| File | Changes |
|------|---------|
| `catan.ps1` | Add `recording` verb with subcommands |
| `RecordingController.cs` | Already has required endpoints |
| `RecordingService.cs` | Already has `ImportRecordingAsync` |

## Acceptance Criteria

- [x] `./catan.ps1 recording list` shows recordings from local or Azure
- [x] `./catan.ps1 recording save` exports recordings to JSON files
- [x] `./catan.ps1 recording load` imports recordings (idempotent)
- [x] `./catan.ps1 recording replay` runs replay tests
- [x] `-Local` and `-Azure` flags work consistently across all commands
- [x] `-Json` output works for scripting
- [x] Help text is clear and examples are provided
- [x] Existing `./catan.ps1 replay` still works (redirects to new command)
