# Adversarial Review — Bound Undo Depth + Synchronous Save (Plan #197, rev 2)

**Reviewer:** GitHub Copilot  
**Date:** 2026-06-22 (rev 1 initial; rev 2 updated after design/plan revision)  
**Reviewed files:** `Log.cs`, `GameStateMachine.cs`, `GameApiController.cs`,
`DatabasePersistenceService.cs`,  
`.design/remove-compression.md` (rev 2), `.design/implementation-plans/remove-compression-plan.md` (rev 2)

---

## Status of rev-1 findings

| Finding | Original severity | Status |
|---------|------------------|--------|
| 1 — `ReplayGame` silently breaks | BLOCKER | **CLOSED** — D5/Part C reconstructs from current GameModel |
| 2 — `TurnCount` caps at 26 | MAJOR | **CLOSED** — D6/Part D sources from `TotalRolls` |
| 3 — Thread-safety claim wrong; `RemoveAt(0)` race | MAJOR | **CLOSED** — D2/Part B removes the background-save path entirely |
| 4 — `SetActionFlags` one-action lag undocumented | MINOR | **OPEN** — still not mentioned in the semantics audit |
| 5 — `const` on generic class | MINOR | **CLOSED** — `LogConstants` non-generic class added |
| 6 — `turns` in PERF-SAVE trace misleading | MINOR | **OPEN** — see Finding 6 below |
| 7 — Test plan gaps | MINOR | **PARTIALLY CLOSED** — three of five missing tests now present |
| 8 — `if` vs `while` inconsistency | NIT | **CLOSED** — plan uses `while` consistently |

---

## New findings (rev 2 design)

---

### Finding N1 — MAJOR: Synchronous save blocks the client's board update on every Cosmos write

**Location:** Design "Part B — Synchronous save" caveat; plan section B.

**What is wrong:**

Today's flow: `LogGameModel()` → `RequestSave()` (fire-and-forget) → method returns → GameModel returned to hub → SignalR dispatched to clients. The client sees the board update *before* the Cosmos write finishes.

After Part B: `await LogGameModelAsync()` includes `await SaveAsync()` (Cosmos write, ~10 ms average, P99 latency spikes to 50–100 ms on Azure). The method returns only after Cosmos confirms. The hub then dispatches to clients. Every single action now carries the P99 Cosmos latency on the player-visible render path.

The design acknowledges this as an "accepted caveat" but does not quantify it. The original complaint was 50–900 ms stalls for long games. With synchronous save, players will experience a constant ~10–50 ms per-action delay at P50/P99 — not a long-game cliff, but present on every move. Whether this is acceptable depends on whether the Cosmos P99 on the chosen consistency level is below the perceptible threshold (~50 ms).

**This is a known tradeoff, not a bug.** But the plan should state the accepted P99 budget explicitly and confirm it against the target SKU, rather than leaving "~10 ms local" as the only data point.

---

### Finding N2 — MAJOR: Part C reconstruction fidelity is underspecified for implementation

**Location:** Design "Part C — Replay: reconstruct from the current GameModel"; plan section C.

**What is wrong:**

The plan lists the fields to keep and reset, but uses phrases like "per-player progress (resources, dev cards, entitlements, score, stars, knights, badrolls, etc.)" without an exhaustive list. For the base `Regular` game type this may be complete; for the `Expansion` type (larger board, additional building types, metropolis, wall, `HasSupplementalBuildPhase` flag, `FinishedSupplemental`, `ParticipatingInSupplemental`) there are additional fields not named.

Missing a single field in the reset produces a reconstructed state that fails the equivalence test (caught) — or worse, passes the equivalence test because the particular short-game recording used in the test never exercises that field (not caught).

The design says "Reuse existing new-game construction where it already builds a fresh game from a board + roster, to avoid hand-rolling the reset." This is the right approach and dramatically reduces risk, but the plan does not name which existing construction path or method to reuse. "Reuse existing construction" is not an implementation instruction.

**Required fix before implementation:** The plan must either (a) name the specific constructor/factory that already builds a `WaitingForRollForOrder` state from a board + roster, or (b) provide an exhaustive per-field reset list for both `Regular` and `Expansion` game types. The equivalence test should be run against an Expansion game with metropolises and walls present.

---

### Finding N3 — MINOR: Synchronous `SaveAsync` failures are now action-path exceptions with no recovery contract

**Location:** Design "Part B — Synchronous save"; plan section B; `Log.SaveAsync()` current implementation.

**What is wrong:**

With background saves, a transient Cosmos error is silently swallowed (the background task fails, but the in-memory state is consistent and the next action triggers another save). With synchronous saves, a Cosmos timeout or transient error throws out of `LogGameModelAsync`, propagating through the `Handle*Async` method and up to the SignalR hub or REST controller. The in-memory `DoneStack` already has the new state pushed; the client receives an error; on reconnect, `LoadAsync` returns the previous committed state. The two sides are now out of sync for the duration of the outage.

**Required before implementation:** The plan must specify the error contract: does `LogGameModelAsync` swallow `SaveAsync` exceptions (preserving today's silent behavior) or let them propagate? If propagating, what does the hub return to the client and how does the client recover? If swallowing, the comment block must say so explicitly and note the in-memory-ahead-of-Cosmos window.

---

### Finding N4 — NIT: `DatabasePersistenceService:153` calls `GetSerializableLog()` purely for `DoneCount`

**Location:** `DatabasePersistenceService.cs` line 153; plan Part D.

After Part D, line 153 changes from `gameStateMachine.GetSerializableLog().DoneCount` to `gameStateMachine.GetCurrentState().RollModel.GameRollModel.TotalRolls`. The plan correctly identifies this site. However, the current code calls `GetSerializableLog()` — which serializes the entire stack to JSON strings — just to read one integer. After the cap this is bounded (≤ 26 entries), so it is not a perf issue. The plan's change fixes both the semantic bug and eliminates the gratuitous serialization. No action beyond what the plan already says; calling it out so the implementer is not confused about why `GetSerializableLog()` is being removed from this call site.

---

### Finding 4 (from rev 1 — still open): `SetActionFlags` lag undocumented

**Location:** Design "Semantics audit" table, row "`SetActionFlags` before `Done()` ordering — unchanged."

The semantic consequence — that each saved snapshot's `UndoEnabled` flag is one action behind — is pre-existing and unchanged, but the audit table still says only "unchanged" without explaining *what* is unchanged. Add: *"SetActionFlags reads `CanUndo` before the push, so each saved snapshot's `UndoEnabled` lags by one action. Pre-existing and intentional."*

---

### Finding 6 (from rev 1 — still open): PERF-SAVE trace `turns` field remains `DoneCount`

**Location:** `Log.cs` line 661 (`[PERF-SAVE]` trace); plan "Verification" step 3.

After Part B, PERF-SAVE appears on the request thread — good. But `turns=uncompressedLog.DoneCount` is still ≤ 26 at all times after cap is active. Verification step 3 says "confirm `jsonSize`/`serialize` flat across `turns`" — with `turns` perpetually ≤ 26, this tells you nothing about game length. The `[PERF-SAVE]` line should emit `TotalRolls` (from the current GameModel, available at save time) alongside or instead of `turns` so the perf harness retains diagnostic value.

---

### Finding 7 (from rev 1 — partially open): Remaining test gaps

Three of the five originally flagged missing tests are now included:
- `ReplayGame_AfterCapExceeded_Succeeds` ✓
- `Undo_RestoresReplayableRandomState` ✓
- `Reconstruct_EqualsHistoricalAnchor` ✓

Still missing:

| Missing test | Why it matters |
|---|---|
| `Done_FullWindow_UndoThenNewAction_CorrectCounts` | Stack at 26 → undo 5 → push 6 new actions. Verifies eviction restarts cleanly after a partial undo window and `CanRedo` is false after each new push. |
| `EnforceUndoLimit_NeverEvictsCurrentState` | After 100 pushes, assert `CurrentState()` equals the 100th pushed value. Guards the "current state is never evicted" invariant. |
| `Load_WithRedoStack_TrimDoesNotCorruptRedoBranch` | Load a legacy save with 100 done + 5 redo entries. Assert `RedoCount == 5` after trim and all redo entries are the correct (most-recent) ones. |
| `Reconstruct_Expansion_EqualsHistoricalAnchor` | Same as `Reconstruct_EqualsHistoricalAnchor` but against an Expansion game with metropolises and walls present. Guards N2 field-reset completeness. |

---

## Verdict

**APPROVE-WITH-CHANGES**

The revision has resolved all three original high-severity findings. The new design is substantially stronger: the thread-safety race is eliminated by construction (synchronous save), the `TurnCount` data quality bug is fixed, and the Replay blocker is replaced with a cleaner reconstruction approach.

**Biggest remaining risk:** Finding N2 — Part C reconstruction fidelity. "Reuse existing construction" must be made concrete before implementation begins. If the named construction path cannot be cleanly isolated, the stored-anchor fallback (rev-1 Finding 1 Option A) is still available and avoids the reset-enumeration problem entirely.

**Required before implementation:**
1. **N2:** Name the specific construction path/method that builds `WaitingForRollForOrder` from board + roster, or provide an exhaustive field list for both game types.
2. **N3:** Specify the `SaveAsync` error-handling contract (propagate or swallow; recovery path on Cosmos transient failure).

**Recommended before merge (not blocking):**
- N1: Document the accepted Cosmos P99 latency budget.
- Finding 6: Emit `TotalRolls` (not `DoneCount`) in the PERF-SAVE trace.
- Finding 7: Add the four missing tests listed above.
- Finding 4: Add the one-line clarification to the semantics audit.

---

## Rev-1 original findings (archived for reference)

<details>
<summary>Click to expand rev-1 findings (superseded)</summary>

### Finding 1 — BLOCKER: `ReplayGame` silently breaks for all games longer than ~26 moves

**Location:** `GameApiController.cs` lines 794–828; design doc "Semantics audit" table.

**What is wrong:**

`ReplayGame` finds the replay anchor by scanning all entries in the serializable DoneStack for the first `WaitingForRollForOrder` snapshot:

```csharp
// DoneStack[0] = most recent, DoneStack[Count-1] = oldest.
for (int i = sourceLog.DoneStack.Count - 1; i >= 0; i--)
{
    var gm = JsonHelper.Deserialize<GameModel>(sourceLog.DoneStack[i]);
    if (gm?.GameState == GameState.WaitingForRollForOrder)
    {
        replayIndex = i;
        break;
    }
}
if (replayIndex < 0)
    return UnprocessableEntity("No WaitingForRollForOrder state found");
```

`WaitingForRollForOrder` is one of the first two or three logged states — logged at game start, before any tiles are placed or players roll. It lives at the oldest end of the DoneStack (high index in the serialized list, or low index in the in-memory stack). Once a game accumulates > 26 moves and eviction begins, these oldest entries are the first ones removed. For any game past the horizon, `replayIndex` will be `-1` and the endpoint returns HTTP 422.

This is a direct contradiction of "the only game semantic that changes is undo depth." Replay is a visible user-facing feature, and it will silently stop working the moment a game exceeds the cap — with no error message that mentions the cap.

The design's semantics audit table does not mention Replay at all. That is the oversight that allowed this to slip through.

**Concrete fix options (pick one):**

- **(a) Anchor field (cleanest):** Add `public string? AnchorState { get; set; }` to `SerializableLog`. In `Done()`, before eviction, if `DoneStack[0]` (oldest) contains a `WaitingForRollForOrder` snapshot and a `RemoveAt(0)` is about to delete it, copy it to `AnchorState` first. `ReplayGame` checks `AnchorState` before scanning the DoneStack.

- **(b) Never evict index 0 — smallest code change:** Change the eviction policy from "evict oldest" to "evict oldest *except the anchor*": keep `DoneStack[0]` (the game-start snapshot) permanently; evict from index 1. Effective window becomes current + 24 prior + anchor = 26 total, but the anchor is always preserved. Change the `while` condition in `EnforceUndoLimit` to `while (DoneStack.Count > MaxUndoDepth + 1) DoneStack.RemoveAt(1)`.

Option (b) requires a single line change, preserves the anchor for Replay, and needs one additional test. Option (a) is more explicit but requires a format change and migration.

---

## Finding 2 — MAJOR: `TurnCount` silently caps at 26, corrupting game-list metadata for long games

**Location:** `GameApiController.cs` lines 683, 861, 1662, 1689, 1734, 1893, 1921, 2007, 2021 (nine call sites use `DoneCount` as `TurnCount`); `Log.cs` line 661 (`[PERF-SAVE]` trace `turns=uncompressedLog.DoneCount`).

**What is wrong:**

`DoneCount` is the computed property `DoneStack.Count`, which after the cap is perpetually ≤ 26. Every API response that surfaces `TurnCount` will show "26" for any long game after the first save post-fix. The game list in the UI will display every game beyond move 26 as "26 turns." This is a data quality bug that silently misleads players.

The `[PERF-SAVE]` trace's `turns` field has the same issue. The plan's measurement formula `S ≈ jsonSize / turns` is numerically correct for its purpose (both `jsonSize` and `turns` plateau together at cap, giving the right per-snapshot average). However, after cap is active `turns` will always be ~26 regardless of how long the game has been played — the trace can no longer be used as a game-length diagnostic.

**Fix:** Add `public int TotalMoveCount { get; set; }` to `SerializableLog` and increment it in `Done()` unconditionally. Use `TotalMoveCount` for `TurnCount` metadata. Keep `DoneCount` meaning "current undo depth." No migration needed — missing field defaults to 0 for old saves (or fall back to `DoneCount`).

---

## Finding 3 — MAJOR: The thread-safety claim in the plan is factually wrong; `RemoveAt(0)` amplifies the existing race

**Location:** Plan "Files-modified table", risk column ("Low — additive; one new call on the hot path"); `Log.cs` lines 44–48 (existing thread-safety comment).

**What is wrong:**

The plan and existing comment state: *"new actions don't arrive until the current action's `Done()` + `RequestSave()` completes."* This is incorrect. `RequestSave()` calls `Task.Run(RunSaveLoopAsync)`, which starts a background thread that calls `GetSerializableLog()`. The next HTTP request for the same game can arrive immediately after `RequestSave()` returns — well before `GetSerializableLog()` finishes — and will call `Done()` on the ASP.NET Core request-processing thread.

`GetSerializableLog()` uses index-based iteration (`for (int i = DoneStack.Count - 1; i >= 0; i--)`):

- **Existing `Push` (adds at end):** safe for a high-to-low loop — the new item at position `Count` won't be revisited.
- **New `RemoveAt(0)` (removes from front):** shifts all indices down by 1. If the background loop is at index `i` and `RemoveAt(0)` is called concurrently, the item formerly at index `i` is now at `i-1`. The loop decrements to `i-1` and reads it again — **producing a duplicate entry in the serialized log**. The next coalesced save will overwrite this, but if the process crashes between the bad save and the next good save, Cosmos holds a corrupted game state.

The risk is real but low-probability in practice (requires precise interleaving). The plan's "Low" risk rating is misleading; it should say "low-probability race, pre-existing and slightly amplified."

**Fix:** Add `private readonly object _stackLock = new();` to `Log<T>`. Wrap all stack mutations (`Done`, `Undo`, `Redo`, `EnforceUndoLimit`, both load methods, `InitializeWithGameModel`) and `GetSerializableLog()` in `lock (_stackLock)`. Since these are all short critical sections and the calls are infrequent, the lock will never contend.

If accepting the existing race is intentional, the plan comment must accurately describe it.

---

## Finding 4 — MINOR: `SetActionFlags` one-action lag on `UndoEnabled` is unchanged but not documented

**Location:** Design doc semantics audit table row "`SetActionFlags` before `Done()` ordering — unchanged."

**What is wrong:**

Not a bug, but the design's claim of "unchanged" is correct only in the mechanical sense. The semantic consequence — that the `UndoEnabled` flag stored in any GameModel always reflects the pre-push state — means the model pushed in `Done()` says `UndoEnabled = false` for the first action of a resumed game (even though undo will immediately become available). This is pre-existing and unrelated to the cap, but it should be acknowledged rather than just saying "unchanged."

**No fix needed.** Add to semantics audit: *"SetActionFlags reads `CanUndo` before the push, so each saved snapshot's `UndoEnabled` lags by one action. This is intentional and unchanged."*

---

## Finding 5 — MINOR: `public const MaxUndoDepth` on a generic class is awkward for non-generic callers

**Location:** Plan section 1a, proposed constant location.

**What is wrong:**

`public const int MaxUndoDepth = 25` on `Log<T>` requires callers to write `Log<string>.MaxUndoDepth`. For any fix to `ReplayGame` or future controller code that needs to reference this cap, the caller must pick an arbitrary type argument just to read a constant. The constant has no relationship to `T`.

**Fix:** Either `public static class LogConstants { public const int MaxUndoDepth = 25; }` (new file, zero dependency), or move it to a non-generic `Log` static class.

---

## Finding 6 — MINOR: `turns` in the PERF-SAVE trace is misleading after cap is active

**Location:** Plan "Cosmos DB size budget", formula `S ≈ jsonSize / turns`.

After the cap, the `[PERF-SAVE]` trace logs `turns=uncompressedLog.DoneCount` which will be ≤ 26. The formula `jsonSize / 26` gives correct per-snapshot size for the one-time measurement. However, `turns` can no longer be used as game-length in ongoing performance analysis.

**Fix:** The plan should explicitly note: *"After cap is active, `turns` in the trace reflects the capped stack depth (≤ 26), not total game moves. `S = jsonSize / turns` is valid for the size budget measurement only."* If Finding 2 is fixed (adding `TotalMoveCount`), log that instead.

---

## Finding 7 — Test plan gaps

The five proposed tests are necessary but insufficient. Missing cases:

| Missing test | Why it matters |
|---|---|
| `ReplayGame_AfterCapIsActive_ReturnsSuccess` | Direct regression test for the blocker (Finding 1). Without this, Replay breakage goes undetected in CI. |
| `Done_FullWindow_UndoThenNewAction_CorrectCounts` | Stack at 26 → undo 5 (DoneStack=21, RedoStack=5) → push 6 new actions. Verifies eviction restarts correctly and `CanRedo` is false after each new action. |
| `EnforceUndoLimit_NeverEvictsCurrentState` | After 100 pushes, assert `CurrentState()` equals the 100th pushed value, not anything evicted. |
| `TurnCount_LongGame_PlateausAt26` | Documents (and regression-locks) the `DoneCount` cap behavior so it is a known decision, not a silent bug. Can be `[Skip]`-tagged if Finding 2 is fixed. |
| `Load_WithRedoStack_TrimDoesNotCorruptRedoBranch` | Load a legacy save with 100 done + 5 redo entries. Assert that after trim, `RedoCount == 5` and all 5 redo entries are the correct ones (not shifted). |

---

## Finding 8 — NIT: Design uses `if` for eviction, plan uses `while`; documents disagree

**Location:** Design doc "Implementation — Profile A" code block vs. plan `EnforceUndoLimit()`.

The design shows a single `if` (sufficient for the live path where exactly one item is added per `Done()` call). The plan uses `while` (needed for load-time trimming). Use `while` everywhere and add a comment: *"`while` is needed for the load path; on the live path it acts as `if` since at most one item is added per call."*

---

## Verdict (rev 1 — superseded)

**REJECT (APPROVE-WITH-CHANGES pending blocker resolution)**

**Biggest single risk:** Finding 1. The `ReplayGame` endpoint silently returns HTTP 422 for every game with more than ~26 moves. The design's "only undo depth changes" invariant is false — a user-visible, named feature is broken. This must be fixed in the plan before any implementation code is written. Option (b) above (`RemoveAt(1)` instead of `RemoveAt(0)`) is a one-line code change that fixes it with minimal scope creep.

Findings 2 and 3 are major but non-blocking for the correctness of the core undo/perf fix; they should be tracked as follow-up issues or addressed before merge.

</details>
