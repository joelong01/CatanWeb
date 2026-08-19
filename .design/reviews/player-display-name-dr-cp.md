# Design Review: player-display-name

**Design:** `.design/player-display-name.md`
**Reviewed:** 2026-08-19
**Reviewer:** GitHub Copilot
**Stage:** Design Doc

## Summary

This is a strong design and a correct diagnosis of the root cause. The design accurately identifies that display names are being derived from the player ID instead of being read from `PlayerProfile.Name`, and it correctly ties the bug to the shared model (`PlayerModel.Name`) rather than to any single UI component. The proposed solution is consistent with the architecture invariants: `GameModel` remains the authoritative runtime state, IDs stay authoritative, and display names are resolved from the profile data that already owns the name.

The design also correctly distinguishes the current stopgap patch from the final end state: the staged code resolves names from profiles in a few render paths, but that is a tactical compatibility fix rather than the permanent architecture. This is a useful review and should proceed with a few clarifying items around backfill ownership and the temporary fallback contract.

## Critical Issues

_None._

## Important Issues

### 1. Backfill ownership and execution contract should be explicit

**Section:** Backfill / Fallback semantics
**Issue:** The design gives a good recommendation but does not yet name the service boundary that will actually perform the one-time `PlayerIds` backfill. Without an owner, it is easy for the codebase to end up with multiple partial backfill routines or a lazy write in one place and a manual script in another.
**Recommendation:** Add a single, named backfill step (for example, `BackfillPlayerIdsAsync` in the persistence layer or an admin script) and document its pre/post conditions: count of docs scanned, count of docs updated, no change to `PlayerNames`, and a safe no-op when the field already exists.

### 2. The fallback contract should remain explicitly temporary

**Section:** Fallback semantics
**Issue:** The design is right that the fallback is time-bounded, but the code should not gradually normalize it into a permanent feature. If a compatibility path is kept for legacy saved games, the temporary nature must be visible in the API contract and in tests.
**Recommendation:** State clearly that the fallback exists only until all pre-change saved games are backfilled, and that the final code should remove the fallback branch once the backfill completes.

## Suggestions

- Capture the exact files touched by the temporary stopgap so they can be explicitly reversed later: `DatabasePersistenceService.cs`, `GameApiController.cs`, and the React player-name readers.
- Add a short note that `PlayerModel.Name` is safe to remove only after proving no remaining runtime callers depend on it for display decisions.
- Consider adding one regression test for the case where a player ID is a GUID and the profile name differs from the ID prefix, because that is the bug this design is fixing.
- Document the exact saved-game and completed-game read paths separately, because they have different display semantics and should not be treated as the same code path.

## Questions

1. Should the backfill be a one-time admin job or an eagerly triggered migration on first read/save?
2. Are there any existing external clients or scripts that read `GameInfo.PlayerNames` and expect the old behavior?
3. How do we want to handle partially backfilled rows if an old save has `PlayerNames` but no `PlayerIds` and the profile has since been renamed?

## Verification

### 1. `PlayerModel.Name` derives from the ID

**Design says:** "Display names are produced by string-parsing the player ID instead of reading `PlayerProfile.Name`. The name is computed in `PlayerModel`..."
**Actual code:** `Catan3.Shared/Models/PlayerModel.cs:12-21`
**Status:** Verified

### 2. There are multiple copies of the ID-parsing helper

**Design says:** There are three copies of `ExtractNameFromId`.
**Actual code:** `Catan3.Shared/Models/PlayerModel.cs:187+`, `Catan3.Shared/Models/GameInfo.cs:82-106`, `Catan3.Shared/Models/GameModel.cs:277+`
**Status:** Verified

### 3. `GameInfo` already carries `PlayerIds` and `CurrentPlayerId`

**Design says:** "`GameInfo` is the exception: it already carries `PlayerIds`, and this branch added `CurrentPlayerId`."
**Actual code:** `Catan3.Shared/Models/GameInfo.cs:5-24`
**Status:** Verified

### 4. Saved-game and completed-game records currently store names but not IDs

**Design says:** persisted records store display text with no ID counterpart.
**Actual code:** `Catan3.GameService/Abstractions/GameSaveData.cs:3-18`, `Catan3.GameService/Abstractions/CompletedGameRecord.cs:3-17`
**Status:** Verified

### 5. Cosmos uses `/id` as the player partition key and stores profile records with image data

**Design says:** the players container is partitioned on `/id`, and the profile shape includes image bytes and content type.
**Actual code:** `Catan3.GameService/Abstractions/CosmosCatanDb.cs:46-70`, `Catan3.GameService/Abstractions/CosmosCatanDb.cs:78-90`, `Catan3.GameService/Abstractions/CosmosCatanDb.cs:120-148`, `Catan3.GameService/Abstractions/CosmosCatanDb.cs:527-538`
**Status:** Verified

### 6. The staged patch is indeed a render-time stopgap, not the end-state design

**Design says:** the current patch resolves player names from profiles in a few service and UI paths, but it is not the final architecture.
**Actual code:** `Catan3.GameService/Services/DatabasePersistenceService.cs:67-100`, `Catan3.GameService/Controllers/GameApiController.cs` winner/metadata paths, and the React profile-driven display logic in the client
**Status:** Verified

### 7. The design is consistent with the architecture invariants

**Design says:** `GameModel` is runtime truth; `PlayerProfile` owns display names; identity is a separate concern from display.
**Actual code:** `.ai/architecture-invariants.md:15-63`, `Catan3.Shared/Models/PlayerModel.cs:12-21`, `Catan3.Shared/Models/GameInfo.cs:5-24`
**Status:** Verified

## Praise

- The root-cause analysis is clear and correct: the bug is not a UI rendering issue alone, it is a model-level conflation of identity with display name.
- The document is disciplined about not throwing away data. The no-data-loss rule is the right constraint for this project, and the distinction between `PlayerNames` as historical record vs. `PlayerIds` as current identity is well framed.
- The design is careful to separate live game state from point-in-time historical snapshots, which is exactly the correct boundary for completed-game records.
- The rejection of name-in-ID migration is persuasive and well reasoned, especially given the Cosmos partition-key and image-ID coupling.

## Follow-Up Actions

- [ ] Define and document the single backfill owner for `PlayerIds`.
- [ ] Add an explicit temporary-fallback test to ensure the compatibility path is bounded.
- [ ] Keep the staged stopgap patch isolated and identify exactly which files are meant to be removed or rewritten once the design lands.
- [ ] Confirm that no runtime callers depend on `PlayerModel.Name` before deleting it.
