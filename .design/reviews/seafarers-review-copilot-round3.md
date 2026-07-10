# Seafarers Design Review - Copilot (Round 3)

## Reviewed Artifact + Supporting Context

- Primary artifact read: `.design/seafarers.md`.
- Supporting rules/context read: `.ai/ai-rules.md`, `.claude/CLAUDE.md`.
- Prior review read: `.design/reviews/seafarers-review-copilot.md`.
- Source-tree verification performed for cited locations in:
  - `Catan3.Shared/Extensions/GameModelExtensions.cs`
  - `Catan3.Shared/GameLogic/GameStateMachine.cs`
  - `Catan3.Shared/Utility/HexCoordinates.cs`
  - `Catan3.Shared/Models/*.cs` (targeted files)
  - `Catan3.GameService/Controllers/GameApiController.cs`
  - `Catan3.GameService/Services/AsyncCommandProcessor.cs`
  - `Catan3.GameService/Hubs/GameHub.cs`
  - `react-ui/lib/services/GameServiceProxy.ts`
  - `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs`

## Verdict

Reject.

The direction is promising and most framework-first decisions are sensible, but the document is not currently reliable enough to drive implementation. The highest-risk blocker is citation integrity: several load-bearing code claims are wrong or materially misdescribed (not just off-by-lines), especially around `ComputeGameHash` semantics and turn/dispatch anchors. That must be corrected before design approval because the plan explicitly depends on these references.

## Decision Ratings

- D0: risky - Framework direction is good, but dispatch contract still underspecifies exact failure/recording semantics for descriptor-level validation failures.
- D1: sound - `ShuffleGroup` (author tag) and derived islands (geometry) is the right separation.
- D2: sound - Scenario profile shape is reasonable and composable.
- D3: sound - Reusing `RoadModel`/`RoadState.Ship` is appropriate; avoids duplicate transport models.
- D4: risky - Optional-entitlement seam is good, but edge-state lifecycle and non-blocking-next behavior must map to both UI and server gate paths explicitly.
- D5: risky - Bounded retry/fallback is correct, but concrete termination behavior must be specified for low-entropy groups and random-gold overlap constraints.
- D6: sound - Predicate swap in existing traversal is the right approach; no DFS rewrite.
- D7: risky - Main-island = component containing `(0,0,0)` is acceptable only if template authoring/enforcement guarantees center belongs to intended main island.
- D8: unsound - Hash section is built on incorrect current-code assumptions about what `ComputeGameHash` currently discriminates.
- D9: sound - Data-driven interaction session registry is the correct client seam.
- D10: sound - Shared edge classifier is coherent and reusable.
- D11: risky - One-state robber/pirate model is viable, but no-legal-target and no-victim flows are still underspecified.
- D12: risky - Gold policy direction is fine; loop safety/eligibility bounds need explicit guardrails.
- GameHash section: unsound - Current-state citations and behavior assertions are not accurate enough to base migration policy on.
- Sequencing + per-step gate: risky - Gate is good, but because citation trust is currently low, each step's model/hash review cannot be trusted until references are corrected first.

## Findings

### Design-Level Issues

#### SF3-001

- Severity: High
- Type: Design-level
- Location: D8, GameHash section
- What is wrong: The hash policy assumes the current hash already represents model identity at the granularity described, but the current road/building contributions do not fully discriminate slot identity and do not include road state. This makes the proposed "versioned extension" strategy unsafe because the baseline being extended is mischaracterized.
- Recommended fix: Rewrite D8 against the actual baseline hash inputs first, then specify an explicit v2 field list and invariants (`Regular` unchanged, scenario-on fields included). Include concrete before/after replay-hash tests for Regular and scenario games.

### ComputeGameHash Analysis (Detailed)

This section captures the current behavior of `ComputeGameHash` as implemented today and the specific implications for D8.

#### A. What the current code actually hashes

From `Catan3.Shared/Extensions/GameModelExtensions.cs`, current discriminators are:

- `GameState`
- `CurrentPlayerId` (deterministic string hash)
- `HasSupplementalBuildPhase`
- Every tile's `ResourceTileType` and `Number` (tiles sorted by `Q/R/S`)
- Every harbor's sorted index and harbor type
- Owned roads only: owned-list index (`roadIndex`) and `OwnerId` hash
- Owned buildings only: owned-list index (`buildingIndex`), `BuildingState`, `OwnerId` hash
- For each player: supplemental flags, `Id`, sorted `UnspentEntitlements`
- Robber `Q/R/S` when not default

Notably absent in current hash inputs:

- Road state (`Road`, `Ship`, etc.)
- A direct canonical road-slot key value in the road contribution term
- A direct canonical building-slot key value in the building contribution term

#### B. Why deterministic ordering is not enough

Roads are sorted by key, but the key is used only to define iteration order. The road term itself is:

`sum(prime_i * roadIndex + prime_j * ownerHash)` over the owned-only list.

Because `roadIndex` is 0..N-1 over the filtered owned subset, it does not encode absolute board slot identity. Different owned edge sets can still produce the same road contribution when owner multiset and owned counts align.

Equivalent statement:

- Sorting gives determinism.
- Determinism is not the same as full discrimination.

#### C. Collision class (roads)

Two states can differ in owned edge locations but match road contribution:

- Same number of owned roads
- Same owner sequence after sort (often same single owner)
- Different `RoadKey` set

Then `roadIndex` sequence and owner hashes are identical, so the road block collides.

#### D. Collision class (buildings)

Buildings have analogous behavior: owned-only filtered list, sorted order, then hash by list index + building state + owner hash. Slot identity is also indirect and can under-discriminate in analogous owner/state-preserving permutations.

#### E. Implication for D8 (versioned hash policy)

D8 currently assumes a stronger baseline identity contract than the code provides. Therefore, "extend v1 by adding scenario fields" is underspecified unless the baseline itself is explicitly documented as-is (with known under-discrimination classes) or corrected first.

#### F. Safe ways to make road/building ownership fully discriminating

Any one of these is sufficient for slot identity:

- Hash every canonical road slot in sorted full-road order, adding owner/state value per slot (including unowned as sentinel).
- Keep owned-only iteration, but include a deterministic scalar derived from `RoadKey` in each owned-road term.

Same recommendation applies to buildings if full slot discrimination is desired.

#### G. Minimum D8 text that would be accurate

- "Current v1 hashes owned-road and owned-building subsets by sorted owned-list index and owner/state values; it does not hash road state and does not directly hash canonical slot keys in those terms."
- "v2 adds explicit scenario discriminators and should define whether slot-identity strengthening is part of the migration or deferred."

#### H. Required tests for D8 confidence

- Baseline tests that prove current behavior (including at least one owned-road-set permutation case).
- Replay-hash invariance tests for existing Regular/Expansion fixtures.
- Scenario tests proving v2 differentiates required Seafarers states (road vs ship, pirate position, scenario bonus inputs).

#### SF3-002

- Severity: Medium
- Type: Design-level
- Location: D7 (main island definition)
- What is wrong: "Main island = component containing `(0,0,0)`" is acceptable as a convention, but the design treats it as universally true for authored New Shores without defining an authoring validator or runtime guard. A malformed template can silently mis-score island VP.
- Recommended fix: Add a template-validation rule for scenarios using new-island VP: either require `(0,0,0)` to be land in the intended main component, or add explicit `MainIslandRule` metadata fallback.

#### SF3-003

- Severity: Medium
- Type: Design-level
- Location: D11 (pirate flow)
- What is wrong: The design does not fully specify resolution behavior when the active player has no legal pirate destinations, or no eligible victim after selecting a legal sea hex. This is a frequent gameplay edge, not a pathological one.
- Recommended fix: Define deterministic no-target handling for both robber and pirate branches in `MustMoveRobber` (pass-without-steal vs constrained target set vs mandatory piece choice), and include replay assertions.

#### SF3-004

- Severity: Medium
- Type: Design-level
- Location: D5 and D12 (random selection loops)
- What is wrong: The document calls for bounded shuffle retries, but similar boundedness is not explicitly required for all random selection loops that gain new exclusions (e.g., temporary gold eligibility in sea-heavy scenarios).
- Recommended fix: Add explicit "bounded with deterministic fallback" language to every random-pick loop touched by Seafarers constraints, not only tile shuffle.

### Code-Citation Mismatches

#### SF3-005

- Severity: High
- Type: Code-citation mismatch
- Location: GameHash section, `Catan3.Shared/Extensions/GameModelExtensions.cs:208`, `:262`, `:324-329`
- What is wrong: The design claims a specific prime-times-value structure including owned-road owner+position and robber coordinate anchors at cited lines. Current code is materially different: roads hash by sorted index + owner hash (not explicit coordinates/state), and cited line anchors do not match described blocks.
- Recommended fix: Re-audit `ComputeGameHash` and replace all load-bearing claims with exact, current references and pseudocode reflecting current behavior.

#### SF3-006

- Severity: High
- Type: Code-citation mismatch
- Location: D0/D4/D6 line anchors in `Catan3.Shared/GameLogic/GameStateMachine.cs`
- What is wrong: Multiple anchors are stale or misdescribed as authoritative proof points (`AllowNext`, "turn-end gate", purchase/consume lines, recompute chain, route award lines). Some behavior exists but at different sites (`CanTransitionToNext` is the server gate), and some cited line semantics do not match exactly.
- Recommended fix: Update citations to exact methods and include method names in prose rather than relying on fragile line numbers.

#### SF3-007

- Severity: Medium
- Type: Code-citation mismatch
- Location: D1/D7 references to `HexCoordinates.cs:352,521`
- What is wrong: The center/spiral concept is correct, but exact anchor references are stale and do not consistently point to the claimed semantics in current file layout.
- Recommended fix: Cite `GenerateSpiralCoordinates` and constructor usage by symbol name and refresh line anchors.

#### SF3-008

- Severity: Medium
- Type: Code-citation mismatch
- Location: D3/D9 model and enum anchors (`GameEnums.cs`, `RoadModel.cs`, `TileModel.cs`, `GameModels.cs`)
- What is wrong: The design references fields/enums as if already present (`MovableShip`, `MoveShip`, `BuildableKinds`, `ShuffleGroup`, `Scenario`, `MaxShips`) in places where they are not currently present.
- Recommended fix: Mark these explicitly as "proposed additions" and stop citing them as existing evidence.

#### SF3-009

- Severity: Medium
- Type: Code-citation mismatch
- Location: D0 dispatch backbone (`GameApiController.cs`, `AsyncCommandProcessor.cs`)
- What is wrong: The baseline dispatch flow is correctly identified, but line-number assertions for "default throw" and contract details are stale and over-specific.
- Recommended fix: Cite the message-type switch in `ExecuteGameLogicAsync` by symbol and keep exact numbers out of normative text.

#### SF3-010

- Severity: Low
- Type: Code-citation mismatch
- Location: Typegen claim in GameModel/data-model section (`CatanTypeGenSpec.cs`)
- What is wrong: The statement "regeneration picks up new fields" is true for already-registered types, but current text blurs that with non-registered new types/enums that will not appear without explicit `AddInterface`/`AddEnum` changes.
- Recommended fix: Split the statement into two bullets: "existing registered models auto-pick fields" and "new types must be registered."

## Citation Audit

| Citation checked | Status | Notes |
|---|---|---|
| `Catan3.Shared/Extensions/GameModelExtensions.cs:208` | Wrong | Line anchor/description mismatch; implementation differs from prose details. |
| `Catan3.Shared/Extensions/GameModelExtensions.cs:262` | Wrong | Claimed owned-road hash semantics do not match current method behavior. |
| `Catan3.Shared/Extensions/GameModelExtensions.cs:324-329` | Stale | Robber hash exists, but anchor range is stale. |
| `Catan3.Shared/Extensions/GameModelExtensions.cs:715` | Confirmed | `Shuffle` exists and loops on `ValidateGame`. |
| `Catan3.Shared/GameLogic/GameStateMachine.cs:845` | Confirmed | `ValidatePurchase` exists and has no `Ship` case currently. |
| `Catan3.Shared/GameLogic/GameStateMachine.cs:1008` | Confirmed | Production path applies temporary gold -> `GoldMine`. |
| `Catan3.Shared/GameLogic/GameStateMachine.cs:1062` | Confirmed | `SetActionFlags` method location aligns. |
| `Catan3.Shared/GameLogic/GameStateMachine.cs:1079` | Stale | `AllowNext` exists nearby; exact line drifted. |
| `Catan3.Shared/GameLogic/GameStateMachine.cs:1349` | Stale | `UpdateStateOnNextPlayer` exists nearby and clears turn state. |
| `Catan3.Shared/GameLogic/GameStateMachine.cs:1357` | Confirmed | `SetTempGoldTiles` invoked during turn advance. |
| `Catan3.Shared/GameLogic/GameStateMachine.cs:1387/1410` | Stale | `RoadPurchase` entitlement checks/consumption exist, line anchors drifted. |
| `Catan3.Shared/GameLogic/GameStateMachine.cs:1461` | Confirmed | `LogGameModel` recompute pipeline exists. |
| `Catan3.Shared/GameLogic/GameStateMachine.cs:1490` | Confirmed | `UpdatePurchaseUi` exists in recompute path. |
| `Catan3.Shared/GameLogic/GameStateMachine.cs:1912` | Stale | Server-side next-state gate exists (`CanTransitionToNext`) near cited area. |
| `Catan3.Shared/GameLogic/GameStateMachine.cs:1924` | Confirmed | `SetTempGoldTiles` method exists and currently skips previously-gold/desert. |
| `Catan3.Shared/GameLogic/GameStateMachine.cs:2216/2237/2246/2262` | Stale | Longest-road traversal and >=5/tie semantics exist; exact lines have drifted. |
| `Catan3.Shared/Utility/HexCoordinates.cs:352,521` | Stale | Spiral/center semantics exist; anchor values drifted. |
| `Catan3.Shared/Models/GameEnums.cs:31` | Confirmed | `RoadState` includes `Ship`. |
| `Catan3.Shared/Models/GameEnums.cs:115` | Stale | `Entitlement.Ship` exists; line number changed. |
| `Catan3.Shared/Models/GameEnums.cs:5` | Confirmed | `ResourceType` includes `Sea` and `GoldMine`. |
| `Catan3.Shared/Models/RoadModel.cs` | Wrong | `BuildableKinds`/`IsShip` helper not currently present. |
| `Catan3.Shared/Models/TileModel.cs` | Wrong | `ShuffleGroup`/`Fixed` fields not currently present. |
| `Catan3.Shared/Models/PlayerModel.cs` | Wrong | `ScenarioBonusVp` not currently present. |
| `Catan3.Shared/Models/GameModels.cs:47` | Wrong | `MaxEntitlementCount(Ship)` not implemented; returns default path. |
| `Catan3.Shared/Models/HouseRules.cs` | Confirmed | `GoldTiles` default is 1. |
| `Catan3.Shared/Models/BuildingModel.cs:94-95` | Confirmed | `GoldMine` yields 1/2 via settlement/city path. |
| `Catan3.GameService/Controllers/GameApiController.cs:126` | Confirmed | `/api/game/action` endpoint exists. |
| `Catan3.GameService/Services/AsyncCommandProcessor.cs:127/154` | Stale | Message-type switch and unknown default throw exist; anchors drifted. |
| `Catan3.GameService/Hubs/GameHub.cs` (`GameStateUpdated`) | Confirmed | Broadcast event exists. |
| `react-ui/lib/services/GameServiceProxy.ts:476` | Confirmed | `moveRobber` wrapper exists. |
| `react-ui/lib/services/GameServiceProxy.ts:693/708` | Stale | `executeCommand` + `/api/game/action` path exists; anchor drift. |
| `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs` | Confirmed | Existing registrations present; proposed new registrations absent. |

## Round 3 — Resolutions (design author)

Applied to `.design/seafarers.md`. Accept/reject per finding:

- **SF3-001 / SF3-005 (GameHash baseline) — ACCEPTED.** Rewrote the GameHash
  section and D8 to state the *actual* v1 baseline: owned roads/buildings hash
  `sortedOwnedIndex + owner(+state)`, **not** the canonical slot key, and **not**
  `RoadState`. D8 now specifies v2 (scenario-opted) adding a `RoadKey`/`BuildingKey`
  slot scalar (**the #205 fix, scoped to scenario games**) + `RoadState` + scenario
  discriminators, with Regular frozen at v1. Confirmed **bug #205** is real.
- **ComputeGameHash A–H analysis — ACCEPTED** as accurate; folded into D8 + the
  required-tests list.
- **SF3-002 (main-island `(0,0,0)` guard) — ACCEPTED.** D7 now requires a
  template-load/create validator: for `NewIslandBonusVp` scenarios, `(0,0,0)` must
  exist and be land; else fail the create.
- **SF3-003 (pirate no-target) — ACCEPTED.** D11 now specifies nullable victim
  (mirroring `MoveRobber`): move-without-steal; the robber-or-pirate choice always
  leaves a resolvable option.
- **SF3-004 (bounded random-gold loop) — ACCEPTED.** D12 now clamps the temp-gold
  selection to `min(GoldTiles, eligibleTileCount)` so the added `Sea` exclusion
  can't spin forever on sea-heavy boards.
- **SF3-006 / SF3-007 / SF3-009 (stale anchors) — PARTIAL.** Rejected "the facts are
  wrong" (re-verified: `AllowNext` ~1068, `CanTransitionToNext` ~1900,
  `ExecuteGameLogicAsync` default-throw at 154, spiral center all confirmed).
  Accepted the recommendation: cite by **symbol name** (`AllowNext`,
  `CanTransitionToNext`, `ExecuteGameLogicAsync`, `GenerateSpiral`,
  `ComputeGameHash`) — swept through the doc.
- **SF3-008 (proposed fields cited as existing) — REJECTED.** The model section
  marks every one as **add/new**, and "What already exists" lists only real
  members. Proposing a field is not claiming it exists.
- **SF3-010 (typegen wording) — ACCEPTED.** Split into "new types must be
  registered" vs "already-registered types auto-pick new fields."

Open decision recorded: strengthen the **Regular v1** hash for #205 too (needs a
version bump + regenerated baselines) — **deferred** unless a standalone hardening
PR is warranted.
