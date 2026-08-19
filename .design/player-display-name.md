# Player Display Names

**Status:** Awaiting approval
**Issue:** [#208](https://github.com/joelong01/CatanWeb/issues/208)
**Branch:** `player-display-name`

## Problem

Display names are produced by string-parsing the player **ID** instead of reading
`PlayerProfile.Name`. The name is computed in `PlayerModel`:

```csharp
// Catan3.Shared/Models/PlayerModel.cs:21
public string Name => ExtractNameFromId(Id);
```

`ExtractNameFromId` splits the ID on `-` and returns the first segment.

| Id | Derived name | |
|---|---|---|
| `Adrian-001` | `Adrian` | right by coincidence |
| `1ffb33af-9316-4870-b7db-32346965ed8b` | `1ffb33af` | wrong |

The `Name-NNN` ID convention exists so a **human can recognize a player by their ID**
in logs, database documents, and saved-game JSON. It is a debugging affordance. It was
never meant to carry the display name. The seeded players were therefore never
"working" -- they coincidentally produced the right string. Any ID that does not begin
with the player's name exposes the defect.

The stored data is correct. `GET /api/players` returns the right name for everyone;
nothing in the `Players` collection needs repair.

## Constraints

**The invariant is no data loss.**

The schema may evolve; stored information may not be destroyed. Concretely:

- Schema changes are **additive**. `PlayerIds` is added *alongside* `PlayerNames`, never
  in place of it
- No existing row is rewritten or cleared. Backfill may only fill in a field that is
  currently absent
- Any field that holds the **only** copy of some information is retained, even once a
  better representation exists

`PlayerNames` is the case that matters. Existing records carry names but no IDs, so
dropping the column would destroy the only record of who played those games. It stays.

There is a second, less obvious reason to keep it: a completed-game record is a
**point-in-time document**, not a cache. "Ty won on 2026-08-19" should remain true even
after Ty renames. A name resolved from the current profile cannot reconstruct what a
player was called at the time; only a stored name can. That makes `PlayerNames` on
completed games a genuine record rather than a denormalization -- which is why it is kept
permanently, not merely for backward compatibility.

Readers therefore prefer `PlayerIds` when present and fall back to `PlayerNames`, and both
representations coexist indefinitely.

## Root cause: a display name is not game state

`GameModel` is the single runtime source of truth for **game state**. A display name is
not game state -- it is identity data that belongs to `PlayerProfile` and changes
independently of any game.

The comment above the (dead) copy in `GameModel.cs:277` records the original intent:

> Rule 7 Compliance: Helper methods for computed fields that GameInfo needs.
> These ensure GameModel is the single source of truth for all game information.

The intent was to honor the GameModel-is-truth invariant. The premise was wrong: a name
is not game information, so deriving it locally to avoid a profile lookup encoded
identity data into the identity field's *format*. That coupling is the bug.

## Current state

There are **three** copies of `ExtractNameFromId`:

| Location | Visibility | Called by | Status |
|---|---|---|---|
| `Catan3.Shared/Models/PlayerModel.cs:187` | private | `PlayerModel.Name` (line 21) | live |
| `Catan3.Shared/Models/GameInfo.cs:92` | private | `NewGameRequest.GetPlayerNames()`, which has no callers | dead |
| `Catan3.Shared/Models/GameModel.cs:277` | public static | nothing | dead |

Server-side reads of `PlayerModel.Name`:

- `GameModel.GetDisplayName()` (line 310) -- default game display name
- `GameModel.GetCurrentPlayerName()` (line 349) -- used by `GameStateMachineRegistry`
- `GameModel.GetPlayerNames()` (line 388) -- no callers
- `GameApiController` `PlayerNames` writes: lines 685, 868, 1711, 1943, 2029, 2043
- `GameApiController` log lines: 595, 624

Persisted records store display text with no ID counterpart:

- `GameMetadata` / `GameSaveData` -- `PlayerNames` as a comma-joined string, no `PlayerIds`
- `CompletedGameRecord` -- `PlayerNames` string, plus `WinnerId` **and** redundant `WinnerName`
- Cosmos list projection selects `c.playerNames` (`CosmosCatanDb.cs:158`)

`GameInfo` is the exception: it already carries `PlayerIds`, and this branch added
`CurrentPlayerId`.

## Design

**The service deals in player IDs. The client resolves names from profiles.**

Identity (the ID) is authoritative and travels with the game. The display name is
looked up at render time from `PlayerProfile`, which is already the only place it is
authored and the only place a rename takes effect.

### Data flow

```text
GameModel.Players[].Id  ──────────────►  client
                                            │
PlayerProfile{Id, Name} ──/api/players──►  playerProfiles: Map<id, profile>
                                            │
                                            ▼
                                     usePlayerName(id) ──► rendered name
```

The client already holds this map. It is populated on mount and refreshed by the
existing `PlayersUpdated` SignalR broadcast, and `usePlayerColors(id)` has resolved
colors through it all along. Names were the asymmetry.

### Changes by layer

#### Catan3.Shared

- Delete `PlayerModel.Name` and `PlayerModel.ExtractNameFromId`
- Delete the dead `GameModel.ExtractNameFromId` and `GameInfo.ExtractNameFromId`
  (with `NewGameRequest.GetPlayerNames()`, its only caller)
- Delete `GameModel.GetPlayerNames()` (no callers) and `GameModel.GetCurrentPlayerName()`
- `GameModel.GetDisplayName()` no longer names a player; use player **count**
- `GameInfo` is a transient API DTO, never persisted -- `PlayerIds` and `CurrentPlayerId`
  are the fields callers should read. `PlayerNames` and `CurrentPlayer` stay for
  compatibility

#### Persistence (additive only)

- `GameMetadata`, `GameSaveData`, `CompletedGameRecord`: **add** `PlayerIds`
  (`List<string>`); `PlayerNames` is retained
- `CompletedGameRecord`: `WinnerName` is retained alongside `WinnerId` as the
  point-in-time record of what the winner was called
- Cosmos: project `c.playerIds` **and** `c.playerNames`; readers prefer the former

Two different write policies, because the two records mean different things:

| Record | Writes | Rationale |
|---|---|---|
| Saved (in-progress) games | `PlayerIds` only | a live view; current names are the correct names |
| Completed games | `PlayerIds` **and** `PlayerNames` / `WinnerName` | a historical document; freeze what they were called |

#### Catan3.GameService

- Replace the nine `PlayerModel.Name` reads: write `PlayerIds` everywhere, and resolve
  names from profiles only on the completed-game path
- Remove the two stopgap lookups on the saved-game and games-list paths; keep the winner
  lookup, which is now the intended behavior

#### react-ui

- `usePlayerName(id)` becomes the single accessor (already added on this branch)
- Load-game resolves names from `PlayerIds` (no fallback needed once step 3 has run)
- Completed-game and stats history display the **stored** name, subject to the repair rule

### Fallback semantics

The client must **never** render a fabricated name. Two distinct cases:

| Case | Render |
|---|---|
| Profiles not yet fetched (transient, normal) | `"Loading..."` |
| No profile exists for the ID (deleted player, bad data) | `"Profile Error"` |

Showing a plausible-but-wrong name is the failure mode that produced this bug; both
strings are obviously not a name, which is the point.

The two cases stay distinct because the loading window is normal and brief -- flashing
`"Profile Error"` on every page load would train people to ignore it. They are
distinguishable in the store: the profile map is empty before the fetch resolves, and
populated-but-missing-this-ID after.

## Backfill

Existing records store names with no IDs. Identity **is** recoverable -- the saved-game
document contains the full compressed `GameModel`, including `Players[].Id` -- but not from
the lightweight list projection.

Under the no-data-loss invariant this is a **backfill, not a migration**: it only fills in
the newly added `PlayerIds`, and never touches `PlayerNames`.

### Where a fallback is actually needed

The two record types read names differently, so they need different things:

| Record | Displays | Needs `PlayerIds` for display? |
|---|---|---|
| Completed games | the stored `PlayerNames` -- a point-in-time record | no |
| Saved (in-progress) games | names resolved live from profiles | yes |

So the fallback applies to exactly one case: **a saved game written before this change**,
which has no `PlayerIds` yet. It is not a permanent feature of the design, and it
disappears the moment that row is backfilled.

**Recommendation: backfill eagerly, and drop the fallback.** The saved-game set is
in-progress games only -- bounded and small by nature -- so a single pass that decompresses
each document and writes `PlayerIds` removes the dual-read path entirely rather than
leaving it in the code indefinitely. Confirm the count before running it. A lazy backfill
(fill on next save) is the alternative if that pass turns out to be larger than expected,
and it is the reason the fallback would need to exist at all.

Completed games are backfilled too, but for identity rather than display: it enables
click-through to a player, and the repair rule below.

### Backfill owner and contract

The backfill is **one named routine**, not a behavior spread across write paths:
`BackfillPlayerIdsAsync` in the persistence layer, invoked once from an admin endpoint or
`catan.ps1` task.

| Property | Contract |
|---|---|
| Input | every document in `games` and `completed-games` |
| Reads | `compressedData`, decompressed to recover `Players[].Id` |
| Writes | `playerIds` only |
| Never writes | `playerNames`, `winnerName`, or any other existing field |
| Idempotent | a document that already has `playerIds` is skipped, not rewritten |
| Reports | documents scanned, updated, skipped, and failed |

A document whose `compressedData` cannot be decompressed is **logged and skipped**, never
partially written. Because the routine is idempotent and additive, it is safe to re-run
after fixing any failures.

### Other consumers of `PlayerNames`

`PlayerNames` is read outside the React client, so it cannot be treated as an internal
field:

- **Seed data** -- all 29 files in `Default Data/Games/` and those in
  `Default Data/CompletedGames/` carry `playerNames` and **no** `playerIds`. They do carry
  `compressedData`, so they are backfillable by the same routine. Until they are, a fresh
  `catan.ps1 database install` produces games whose player lists render empty once the
  client reads `PlayerIds`. **The backfill must cover seed data, or the seed files must be
  regenerated.**
- `.scripts/export-sql.ps1` and `.scripts/transform-to-cosmos.ps1` map `playerNames`
- `WebUI/Pages/Game.razor` (Blazor) reads player names

Because `PlayerNames` is retained permanently, none of these break. They are listed so
that "retained" is understood as a requirement rather than a courtesy.

### Testing

The design is only as good as the regression that pins it:

1. **The bug itself** -- a player whose ID is a bare GUID and whose profile name differs
   from the ID prefix renders the profile name, not the prefix
2. **Legacy IDs unaffected** -- `Joe-001` still renders `Joe`
3. **Repair rule, negative case** -- a player with ID `Ty-<uuid>` who has been renamed
   still shows the **stored** historical name, not the current one. This is the false
   positive described above and is the single most important test here
4. **Repair rule, positive case** -- a bare-GUID player's corrupted history resolves to
   the profile name
5. **Backfill idempotency** -- running it twice changes nothing the second time, and
   `playerNames` is byte-identical before and after
6. **Loading state** -- name renders `"Loading..."` with an empty profile map and
   `"Profile Error"` with a populated map missing that ID

### Repairing the corrupted names

The GUID fragments already written into completed games (Ty, Susie, Dan) are a wrinkle in
the point-in-time argument. That argument is sound for a *real* name -- if Ty later renames
to Tyler, "Ty" is what the record should keep saying. But `1ffb33af` was never anyone's
name. It is a bug artifact, not a historical fact, and preserving it preserves nothing.

The resolution keeps both properties. **Never overwrite `PlayerNames`** -- the raw record
stays exactly as written, satisfying no-data-loss. Instead, history views apply a display
rule that bypasses provably-corrupt values.

An earlier draft of this rule was **unsound**, and the failure is worth recording because
it came from two decisions made separately that interact:

> Show the stored name unless it equals `PlayerId.Split('-')[0]` and does not match the
> profile name.

Since new IDs are minted as `<SanitizedName>-<uuid>`, `Split('-')[0]` *is* the player's
name for every player created after this change. So for a player created as `Ty`
(ID `Ty-<uuid>`) who later renames to `Tyler`, a completed game correctly storing `"Ty"`
satisfies both conditions -- and the rule replaces the correct historical name with the
current one. That is a systematic false positive on **every rename**, and it inverts
exactly the point-in-time property the rule exists to protect.

**The corrected rule scopes by ID shape, not by name comparison.** Only a **bare GUID**
ID can have produced a corrupt name, because only a bare GUID has no name to parse:

| ID shape | Ever corrupt? | Rule applies |
|---|---|---|
| `Joe-001` (seeded) | no -- prefix is the real name | no |
| `Ty-<uuid>` (new) | no -- prefix is the real name | no |
| `1ffb33af-9316-...` (bare GUID) | yes -- prefix is hex | yes |

So: if `PlayerId` matches a bare-GUID pattern **and** the stored name equals its first
segment, resolve from the profile; otherwise show the stored name. The affected population
is exactly the players created between the introduction of GUID IDs and this fix -- Ty,
Susie, and Dan -- and it is closed, because no future ID will be a bare GUID.

This is why completed games get `PlayerIds`: the rule needs the ID to classify the record.
Genuine historical names are never overridden, and nothing is destroyed.

## Considered alternative: compound `name-guid` IDs

Encode the name into the ID (`Ty-1ffb33af...`), migrate existing profiles to the new
format, and leave `ExtractNameFromId` in place. Attractive because it appears to need no
code changes.

**Segment order is decisive.** `ExtractNameFromId` returns `parts[0]`, the *first*
segment. `<guid>-<name>` returns the GUID and stays broken; only `<name>-<guid>` works
with the parser untouched, and it also keeps legacy `Joe-001` working. The `<guid>-<name>`
ordering would require both a parser change and migration of the seeded IDs.

**Rejected for four reasons.**

1. **Migration is a primary-key rewrite.** The `players` container is partitioned on
   `/id` (`CosmosCatanDb.cs:71`) and Cosmos `id` is immutable, so changing a player's ID
   is a delete-and-recreate. It also requires rewriting `imageUri` (which embeds the ID),
   `Players[].Id` **inside every compressed saved-game blob**, and `WinnerId` on completed
   games. Ty has 13 games and 6 wins; skipping the blob rewrite leaves his history still
   rendering GUID fragments. This is strictly more work and more risk than adding a
   `playerIds` field.
2. **Renames become migrations.** With the name inside the key, every rename is another
   primary-key rewrite -- or the UI silently keeps showing the old name. Renames work
   correctly today via the `PlayersUpdated` broadcast; this would regress that.
3. **Names cannot round-trip through a key.** The segment lands in a Cosmos `id` and a URL
   path (`/api/images/{id}`). Apostrophes, spaces, unicode, and `-` itself must be
   sanitized, so the segment is not the real name -- the profile is still required for
   display.
4. **Two sources of truth.** `PlayerProfile.Name` and the ID suffix can drift; any write
   path that updates one and not the other produces inconsistency.

**Adopted in part.** New player IDs are now generated as `<SanitizedName>-<uuid>`. This
restores the human-recognizability in logs and raw JSON that the `Name-NNN` convention
existed for, costs nothing (no migration -- new records only), and is explicitly *not* a
display-name mechanism. Existing players are left alone; profile-based rendering already
fixes them.

## Rollout sequence

**There are no live games until tonight**, so this ships as a single change rather than a
compatible sequence. No dual-read path, no compatibility shim, no intermediate state that
has to be independently correct. The steps below are an implementation order, not a
release plan.

1. Add `PlayerIds` to the three persisted records and to the Cosmos projection (additive)
2. Write `PlayerIds` from every site that currently writes `PlayerNames`; keep writing
   `PlayerNames` on the completed-game path, resolved from the profile
3. Backfill `PlayerIds` on existing saved and completed games -- one pass, additive,
   `PlayerNames` untouched
4. Saved-game list resolves names from `PlayerIds`; history views apply the repair rule
5. Delete `PlayerModel.Name` and all three `ExtractNameFromId` copies

The order still matters within the session -- `PlayerModel.Name` cannot be deleted while
the service reads it, and step 4 assumes step 3 has run -- but nothing needs to survive
being interrupted between steps.

### What must finish before tonight

The window makes breakage cheap, not free. Two things are only safe if the session
completes:

- **Step 3 (backfill).** After step 4 the saved-game list reads `PlayerIds`. Any game not
  backfilled shows an empty player list. Existing in-progress games break if the session
  stops between 3 and 4.
- **Step 5 (deletion).** Leaving `PlayerModel.Name` in place is harmless; deleting it
  half-way through is not. It is last for that reason.

Steps 1 and 2 are additive and safe to leave in place indefinitely.

**The backfill runs against production Cosmos.** It is additive and touches no existing
field, but it is still a write to real data and gets explicit confirmation before running,
along with a count of affected documents.

## Already done on this branch

A rendering-only stopgap, so games are playable before the persistence work lands:

- Added `usePlayerName(id)` to `gameStoreHooks.ts`
- `GoFirstOverlay`, `PlayersPanel`, `phone-control`, robber target menu, and
  `useBoardData` resolve names from profiles
- Added `GameInfo.CurrentPlayerId`
- New player IDs are minted as `<SanitizedName>-<uuid>`
- Three service-side profile lookups. The **winner** lookup is the intended end state and
  stays; the saved-game and games-list lookups are removed by step 2

Six `PlayerNames` writes in `GameApiController` are still derived, so completed games
continue to record GUID fragments until step 2.

## Performance (deferred)

Resolving names from profiles instead of parsing them out of IDs trades string parsing for
data access. That is the correct trade -- but it has costs that this design knowingly
defers rather than solves.

### 1. We are adding database round trips

The stopgap on this branch introduced two:

- `DatabasePersistenceService.SaveAsync` does one point read per player per save
  (up to 6, roughly 1 RU each) on what is a hot path -- previously zero
- `/api/companion/games` calls `LoadPlayersAsync()`, a cross-partition
  `SELECT * FROM c` over the whole players container

Both are **transitional** and are deleted by step 2 of the rollout: once those paths write
`PlayerIds`, the service no longer needs a name, and the client resolves it from the
profile map it already holds in memory.

One lookup is **permanent** by design -- the completed-game path, which must resolve a name
in order to freeze it into the historical record. That is a single lookup per game
completion, not per save, so it is negligible. Net round trips end up below where they
started.

### 2. Fetching every player does not scale

Today every screen that needs a name calls `GET /api/players`, which returns **all**
players with no filter, no projection, and no paging (`CosmosCatanDb.cs:91`):

```csharp
var iter = _players.GetItemQueryIterator<PlayerDoc>("SELECT * FROM c");
```

Three problems compound as the player count grows:

- The container is partitioned on `/id`, so this is inherently cross-partition
- `SELECT *` pulls the whole document, and `PlayerDoc` carries base64 avatar bytes
  (`CosmosCatanDb.cs:138`). Those bytes cross the wire from Cosmos on every call even
  though `DocToProfile` discards them -- `PlayerProfile` has only `ImageUri`
- There is no cache anywhere in the path; every call is a fresh query

#### 2a. Gameplay only needs the players in the game

During a game the client needs at most 6 profiles, and it already knows exactly which:
`GameModel.Players[].Id`. A targeted lookup (`GET /api/players?ids=...`, or a batch POST)
turns a cross-partition scan into a handful of single-partition point reads whose cost is
bounded by table size, not by how many people have ever played.

#### 2b. Other screens need targeting and paging

Edit Players, Stats, and the New Game roster genuinely browse the full set, so they need
different treatment:

- **Projection** -- return only the fields the caller uses. The roster needs
  `id`, `name`, `colors`, `imageUri`; it does not need `LifetimeStats` or image bytes
- **Paging** -- Cosmos continuation tokens, surfaced through the API so the client can
  page rather than materializing every player

### 3. Caching is likely the fix

Player profiles are close to ideal cache material: a small set, rarely written, read
constantly, and -- critically -- there is **already an invalidation signal**. `SavePlayerAsync`
is the only write path, and profile edits already broadcast `PlayersUpdated` to every
connected client.

- **Service side** -- an in-memory profile cache invalidated on `SavePlayerAsync` would
  remove essentially all repeat reads, including the transitional ones in point 1
- **Client side** -- the `playerProfiles` map already *is* a cache; it is refreshed by the
  `PlayersUpdated` broadcast. What it lacks is scoping (point 2a) and a paged path for the
  browse screens (point 2b)

### Status

**All of the above is deferred.** None of it blocks the name-resolution work, and the
current player count makes it invisible. It is recorded here so the scaling limit is a
known, chosen deferral rather than a surprise -- and so that step 2 of the rollout is
understood to remove the round trips this branch temporarily added.

## Open questions

1. **Fallback string** -- `"Profile Error"` as specified, or something shorter for the
   narrow hex tiles in `GoFirstOverlay` / phone-control?
2. **`GetDisplayName()`** -- auto-generated game names currently embed the first player's
   name. Switch to a count (`"Regular - 4 players (17:10)"`), or resolve at the call site?
3. **Non-React clients** -- `DesktopApp/Models/MessageObjects.cs:25` reads `Player?.Name`.
   It is out of the solution and marked do-not-modify; confirm it can stay broken.
4. **Scaling** -- the client fetches *all* profiles, not just the game's. Known, deferred.
