# Seafarers Design Review — Instructions for an AI Reviewer (Round 3)

Hand this file to the reviewing AI (e.g. GitHub Copilot Chat in the repo). It has
full access to the source tree, so this is a **verification review**, not just a
read-through: check that the design is correct, internally consistent, feasible,
and that **every claim it makes about the code is actually true**.

## Your role

You are a skeptical senior engineer reviewing a **design document** (no feature code
exists yet). Judge direction, internal consistency, feasibility, backward
compatibility, and — critically — whether the design's `file:line` citations say
what the design claims. Be direct; call out anything wrong, underspecified, or
inconsistent. Do not rubber-stamp.

## What you are reviewing

- **Primary artifact:** [`.design/seafarers.md`](../seafarers.md) — the full design
  for epic #200 (D0–D12, the GameHash section, the "GameModel & data-model changes"
  section, the message-flow swimlanes, the prior-art review, and the sequencing
  plan with its per-step model-review gate).
- **Supporting context (read before judging):**
  - [`.ai/ai-rules.md`](../../.ai/ai-rules.md) and
    [`.claude/CLAUDE.md`](../../.claude/CLAUDE.md) — project standards and the
    design→plan→implement workflow.
  - The two prior rounds:
    [`.design/reviews/seafarers-review-copilot.md`](seafarers-review-copilot.md).
    Do **not** re-raise findings already resolved there unless they are still wrong.
  - The archived draft [`.design/old/seafarers-2026-02-draft.md`](../old/seafarers-2026-02-draft.md)
    for history only (it is superseded — do not review it).
- **Source tree** — verify against the actual code (see "Verify the citations").

## Project ground rules you must enforce

The design must obey these; flag any violation:

1. **Prime rule / framework-first.** The deliverable is a *reusable expansion
   capability*; Seafarers is only its acceptance test. **No expansion name in core
   control flow** — Seafarers appears only as data (template, scenario profile,
   registered rule module). A tempting `if (seafarers)` / `case "ship"` in core is a
   sign the seam is wrong.
2. **GameModel is the single source of truth; the service is authoritative.** Clients
   send typed messages; the engine validates and mutates a copy; one recompute
   pipeline; snapshot-based undo. The client renders GameModel and collects actions —
   it owns no rules and no state, and never sets state itself.
3. **Prefer an enum value over a new top-level `GameModel` field.** State enums
   (`RoadState`, `BuildingState`, `GameState`) exist to drive `GameStateMachine`
   rules and to tell the client how to render. A new top-level field is a new concept
   deserving scrutiny. Confirm the design honors this (e.g. `RoadState.MovableShip`
   instead of a `PendingShipMoveFrom` field) and that `ShipsBuiltThisTurn` is the
   only justified new turn-scoped field.
4. **GameHash is identity.** Different hash ⇒ different game; same hash ⇒ same
   GameModel. Every new field/enum must be classified discriminating (into the hash
   via `hash += nextPrime * value`) or not, and Regular/Expansion must stay
   **hash-neutral** so existing `.catan_test` hashes never change.
5. **Table-assistant, not strict referee.** Co-located players can enforce rare/hard
   cases; don't gold-plate. But don't use this to skip frequent, load-bearing rules.
6. **Do not copy the old UWP code.** The prior implementation
   (`d:\GitHub\old\Catan`) was scanned for lessons only; it is a tracker, not a
   rules engine. Flag any design element that appears to have copied its approach
   (especially its "diffgram" logging).

## Verify the citations (highest-value check)

The design cites specific `file:line` locations. **Open each and confirm it says
what the design claims.** Load-bearing ones to check (verify all, not just these):

- `Catan3.Shared/Extensions/GameModelExtensions.cs` — `ComputeGameHash` (~`:208`)
  really sums `prime * value` per field (`(int)GameState * prime` ~`:217`; robber
  `Q/R/S` ~`:324-329`); the owned-road hash includes owner+position but **not**
  `RoadState` (~`:262`). `Shuffle` (~`:715`) permutes resource+number over all tiles
  and loops on `ValidateGame`.
- `Catan3.Shared/GameLogic/GameStateMachine.cs` — `AllowNext` (~`:1079`) and the
  turn-end gate (~`:1912`) block Next while `UnspentEntitlements.Count > 0`;
  `ValidatePurchase` (~`:845`) counts limits and has no Ship case; `RoadPurchase`
  (~`:1381`) requires then consumes the held entitlement (`ThrowIfNoEntitlement`
  ~`:1387`, `ConsumeEntitlement` ~`:1410`/`:1640`); `UpdateStateOnNextPlayer`
  (~`:1349`) clears `SpentEntitlementsThisTurn` and calls `SetTempGoldTiles`
  (~`:1357`); `SetActionFlags` (~`:1062`); the post-move pipeline `LogGameModel`
  (~`:1461`) → `UpdateScore` (~`:1463`) → `CalculateLongestRoad` (~`:2216`/`:2237`);
  the longest-road award logic enforces **≥5** (~`:2246`) and
  **current-holder-keeps-ties** (~`:2262`); `SetTempGoldTiles` (~`:1924`) selects
  `HouseRules.GoldTiles` tiles, skipping previously-gold (~`:1955-1959`) and desert
  (~`:1961`); temp-gold produces via `:1008`.
- `Catan3.Shared/Utility/HexCoordinates.cs` — cube coords (`Q+R+S=0`); `(0,0,0)` is
  the board center (spiral ring 0, ~`:352`/`:521`); `GetAllNeighbors` (~`:323`).
- `Catan3.Shared/Models/` — `GameEnums.cs` (`RoadState` has `Ship` ~`:31`,
  `Entitlement.Ship` ~`:115`, `ResourceType.Sea`/`GoldMine` ~`:5`), `RoadModel.cs`,
  `TileModel.cs` (`TemporarilyGold`), `PlayerModel.cs` (`GoldRolls`,
  `IslandsPlayed`, `LongestRoad`), `GameModels.cs` (`ResourceRules`,
  `MaxEntitlementCount` ~`:47`), `GameTemplateData.cs`, `HouseRules.cs`
  (`GoldTiles` default 1), `RobberModel.cs`, `BuildingModel.cs` (gold ~`:94-95`).
- Dispatch backbone — `Catan3.GameService/Controllers/GameApiController.cs`
  (`/api/game/action` ~`:126`) → `AsyncCommandProcessor` (switch on messageType;
  throwing default the design wants to replace, cited ~`:127`/`:154`);
  `Catan3.GameService/Hubs/GameHub.cs` (broadcast `GameStateUpdated`);
  `react-ui/lib/services/GameServiceProxy.ts` (`executeCommand` → `/api/game/action`
  ~`:693`/`:708`; `moveRobber` wrapper ~`:476`).
- `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs` — which models/enums are already
  registered (so the design's "regeneration picks up new fields" claim holds) and
  which additions it lists are needed.

**Report any citation that is wrong, stale, or misdescribed** — that is a
high-severity finding because the plan will be built from it.

## Review checklist (by area)

Rate each and give findings:

- **D0 modules + `CommandContext` dispatch.** Is the module contract (5 hooks +
  descriptor dispatch) sufficient and truly generic? Does reusing `api/game/action`
  with a module-descriptor fallback preserve the existing caller-validation,
  logging, and replay contract? Is Regular guaranteed byte-identical (empty module
  list ⇒ no-op loops)?
- **D1/D5/D7 islands + shuffle + island VP.** Is the split correct — `ShuffleGroup`
  (tag, pools tiles that shuffle together) vs **derived** island identity
  (`ComputeIslands` flood-fill; sea fixed ⇒ components invariant)? Is
  "main island = the component containing `(0,0,0)`" sound (is `(0,0,0)` always a
  land tile on the authored main island; what if it isn't)? Is the pool/target
  consistency fix (exclude Sea+Fixed from both) correct? Bounded retry + fallback?
  Harbor-`Type` shuffle?
- **D2 scenario profile + score/winner.** Are the `Scenario` flags the right set?
  Is per-player `ScenarioBonusVp` stored + hashed + recomputed deterministically?
- **D3/D4 ships (buy vs move).** Two entitlements: `Ship` (buy+place, hold-then-
  consume like Road) vs **optional** `MoveShip` (auto-granted, consumed once/turn).
  Is the generic `Entitlement.IsOptional` change to `AllowNext`/`:1912` safe (does it
  break any existing "unspent blocks Next" case)? Is `RoadState.MovableShip` the
  right model, and is a `RoadModel.IsShip => Ship or MovableShip` helper applied at
  **every** `RoadState == Ship` site (render, `MaxShips` count, route)? Are the four
  movability predicates well-defined? Is `ShipsBuiltThisTurn` justified/minimal?
- **D6 longest trade route.** Is "swap only the adjacency predicate
  (`IsRouteSegmentTraversable`), do NOT rewrite the DFS" correct, and does it truly
  preserve the ≥5 + tie logic? Is the road↔ship **through-your-own-building**
  junction rule specified precisely enough to implement?
- **D8 hash policy.** Does `GameHashVersion` reliably keep Regular/Expansion hashes
  unchanged while scenario games hash the new state? Would any existing Regular
  replay test's hash change?
- **D9 client.** Data-driven render + `GameState → stateful interaction session`
  registry; no `gameType` branching; entitlement→button mapping generic.
- **D10 edge classifier.** Land/Coastal/Sea from adjacent tiles; roads exclude Sea;
  ships on Sea/Coastal; is this the single source feeding `BuildableKinds` and the
  island flood-fill adjacency?
- **D11 pirate.** One state (reuse `MustMoveRobber`, accept `MovePirateMessage` when
  `PirateEnabled`), **one piece moved per 7/Soldier**; `GameModel.Pirate` reuses the
  `RobberModel` shape; continuous "no ship build/move adjacent to the pirate" in
  recompute; steal from an adjacent-ship owner; template `PirateStart`. Any hole?
- **D12 gold.** Fixed `GoldMine` (authored) + random house-rule kept; are the
  `SetTempGoldTiles` tweaks (also exclude `Sea`; allow re-picking an already-gold
  tile by dropping the `:1955-1959` guard) correct and complete?
- **GameModel & data-model changes section.** Are the new fields/enums correct,
  minimal, defaulted for backward compat, and each correctly classified in the
  "Hashed?" column? Are the typegen registrations complete?
- **Swimlanes.** Do the five message flows match the real dispatch path
  (`executeCommand` → `/api/game/action` → `AsyncCommandProcessor` →
  core handler *or* module descriptor → `LogGameModel` → broadcast +
  `CommandCompleted`/`CommandFailed`)?
- **Sequencing + per-step gate.** Are steps 1–11 independently verifiable and in a
  sound order (no hidden dependency; every game-creating step preserves sea)? Does
  the "each plan opens with a Model & template changes + GameHash review" gate make
  sense? Is Arc A truly standard-playable before any ship mechanic?

## Adversarial checks (specifically try to break these)

- A `RoadState == Ship` reader that the design forgot to switch to `IsShip` (would
  mis-count `MaxShips`, mis-render, or drop a `MovableShip` from a route).
- The optional-entitlement `AllowNext` change letting a player skip a genuinely
  required placement, OR conversely still blocking Next on a stray `MoveShip`.
- `(0,0,0)` not being on the main island (or being Sea) for the authored New Shores
  board, and what island scoring does then.
- Any path where `RoadState.MovableShip` persists past the turn / across undo and
  corrupts `MaxShips`, longest route, or the hash.
- Pirate on a 7 when the player has **no** legal target or declines — does the flow
  still resolve? Does moving the pirate vs robber leave the *other* piece correct?
- A scenario/undo case where derived island VP or derived islands disagree with the
  hash (should not, since islands derive from already-hashed tile positions —
  confirm).
- Backward compat: adding `GameModel.Pirate`, `Scenario`, `ShipsBuiltThisTurn`,
  `ShuffleGroup`, etc. — do old saves and existing `.catan_test` files still
  deserialize and hash identically for Regular/Expansion?

## What NOT to do

- Do not request code or propose an implementation — this is a design review.
- Do not re-litigate decisions already marked **Decided** unless they are actually
  wrong (island scoring rule, ship-move-as-optional-entitlement, gold, harbor
  shuffle, longest-road ties, `MovableShip` vs a field, main-island = `(0,0,0)`).
- Do not suggest copying the old UWP project.
- Do not restyle prose; focus on substance.

## How to report

Write your review to a **new file**:
`.design/reviews/seafarers-review-copilot-round3.md`, matching the format of the
existing round in this folder:

1. **Reviewed artifact + supporting context** (list what you actually read).
2. **Verdict** — one of `Approve`, `Approve with changes`, `Reject` — with a 2–3
   sentence rationale.
3. **Decision ratings** — one line per D0–D12 (plus the GameHash section and the
   sequencing gate): `sound` / `risky` / `unsound`, with the reason.
4. **Findings** — each with a stable id (continue the series: `SF3-001`, `SF3-002`,
   …), **Severity** (High / Medium / Low), **Location** (D-section and/or
   `file:line`), **What is wrong**, and **Recommended fix**. Separate
   **design-level** issues from **code-citation mismatches** (label each).
5. **Citation audit** — a short table of every cited `file:line` you checked and
   whether it was Confirmed / Wrong / Stale.

Keep findings concrete and actionable. Markdown must be lint-clean
(`pwsh ./catan.ps1 lint`).
