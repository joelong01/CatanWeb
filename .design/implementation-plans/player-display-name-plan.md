# Implementation Plan: Player Display Names

**Design:** `.design/player-display-name.md`
**Issue:** [#208](https://github.com/joelong01/CatanWeb/issues/208)
**Branch:** `player-display-name`

## Goal

Stop deriving player display names from player IDs; store `PlayerIds` alongside the
retained `PlayerNames`, and resolve names from `PlayerProfile` at render time.

## Assumption

`GameModel.GetDisplayName()` switches from naming the first player to a player **count**
(`"Regular - 4 players (17:10)"`). Recorded as an open question in the design; resolved
this way in the absence of a different instruction.

## Changes

### 1. Persistence models (additive)

| File | Change |
|---|---|
| `Catan3.GameService/Abstractions/GameSaveData.cs` | add `PlayerIds` to `GameSaveData` and `GameSummary` |
| `Catan3.GameService/Abstractions/CompletedGameRecord.cs` | add `PlayerIds` |
| `Catan3.GameService/Abstractions/CosmosCatanDb.cs` | add `playerIds` to `GameDoc` / `CompletedGameDoc`; map in `DocToSaveData`, `DocToSummary`, `SaveDataToDoc`, completed-game mappers; add `c.playerIds` to the `ListGamesAsync` projection |

`PlayerNames` and `WinnerName` are **retained and still written** — nothing is removed.

### 2. Write sites

| File | Change |
|---|---|
| `Catan3.GameService/Services/DatabasePersistenceService.cs` | write `PlayerIds`; **remove** the stopgap per-player profile lookups |
| `Catan3.GameService/Controllers/GameApiController.cs` | write `PlayerIds` at lines ~685, 868, 1711, 1943, 2029, 2043; keep the profile-resolved `WinnerName`/`PlayerNames` on the completed-game path only; **remove** the `LoadPlayersAsync` stopgap in `GetAvailableGames` |

### 3. Backfill

New `BackfillPlayerIdsAsync` on `ICatanDb` / `CosmosCatanDb`, exposed via an admin
endpoint. Contract per the design: decompress `compressedData`, recover `Players[].Id`,
write `playerIds` only, skip documents that already have it, never touch `playerNames`,
report scanned/updated/skipped/failed.

### 4. Remove the derivation

| File | Change |
|---|---|
| `Catan3.Shared/Models/PlayerModel.cs` | delete `Name` and `ExtractNameFromId` |
| `Catan3.Shared/Models/GameModel.cs` | delete `ExtractNameFromId` (dead), `GetPlayerNames()` (dead), `GetCurrentPlayerName()`; `GetDisplayName()` uses player count |
| `Catan3.Shared/Models/GameInfo.cs` | delete `ExtractNameFromId` and `NewGameRequest.GetPlayerNames()` (both dead) |
| `Catan3.GameService/Services/GameStateMachineRegistry.cs` | `CurrentPlayer` from `CurrentPlayerId`; `PlayerNames` no longer derived |
| `Catan3.GameService/Controllers/GameApiController.cs` | log lines ~595, 624 log player **ID** |

### 5. Client

| File | Change |
|---|---|
| `react-ui/lib/stores/gameStoreHooks.ts` | `usePlayerName` returns `"Loading..."` / `"Profile Error"` per design |
| `react-ui/app/load-game/page.tsx` | resolve names from `playerIds`; apply repair rule |
| `react-ui/lib/utils/playerNames.ts` (new) | `resolveHistoricalName` — repair rule scoped to bare-GUID IDs |
| render sites | drop `?? player.name`, which no longer exists |

## Files Modified

| # | File | Type |
|---|---|---|
| 1 | `Catan3.GameService/Abstractions/GameSaveData.cs` | modified |
| 2 | `Catan3.GameService/Abstractions/CompletedGameRecord.cs` | modified |
| 3 | `Catan3.GameService/Abstractions/CosmosCatanDb.cs` | modified |
| 4 | `Catan3.GameService/Abstractions/ICatanDb.cs` | modified |
| 5 | `Catan3.GameService/Services/DatabasePersistenceService.cs` | modified |
| 6 | `Catan3.GameService/Services/GameStateMachineRegistry.cs` | modified |
| 7 | `Catan3.GameService/Controllers/GameApiController.cs` | modified |
| 8 | `Catan3.Shared/Models/PlayerModel.cs` | modified |
| 9 | `Catan3.Shared/Models/GameModel.cs` | modified |
| 10 | `Catan3.Shared/Models/GameInfo.cs` | modified |
| 11 | `react-ui/lib/stores/gameStoreHooks.ts` | modified |
| 12 | `react-ui/lib/utils/playerNames.ts` | new |
| 13 | `react-ui/app/load-game/page.tsx` | modified |
| 14 | render sites (5 files) | modified |
| 15 | `Tests/Shared/PlayerDisplayNameTests.cs` | new |

## Verification

1. `pwsh ./catan.ps1 build` — 0 errors
2. `pwsh ./catan.ps1 test` — all pass, including the new regressions
3. `pwsh ./catan.ps1 lint` — clean except the known `NU1301` NuGet/TLS failure on `WebUI`
4. Backfill reports scanned/updated/skipped; re-running updates 0
5. Manual: new game shows real names in Go First, Players panel, phone control
