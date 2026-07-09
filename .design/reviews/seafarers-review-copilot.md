# Seafarers Design Review - Copilot

Reviewed artifact: `.design/seafarers.md` for epic #200, including the appended
"Review resolutions" section. Supporting context checked: issue #200,
`.design/old/seafarers-2026-02-draft.md`, `.ai/ai-rules.md`,
`.claude/CLAUDE.md`, and the current source tree.

## Verdict

Approve with changes.

The design is materially stronger after accepting most of the first review. The
two rejected points are accepted here: a roll-production hook and setup-allocation
hook are not needed for Heading for New Shores, and permanent `GoldMine` tiles are
already represented by the current model. The remaining risk is not the direction;
it is that several corrected decisions still live only in the appended resolution
table while the main D-sections continue to say the older, wrong thing.

## Resolved Rejections

- Former SF-003 sub-points on roll production and setup allocation are removed.
  New Shores island land tiles are normal `TileModel`s, and the current roll path
  produces resources by iterating highlighted tiles and adjacent buildings.
  Standard main-island setup is enough for this scenario.
- Former SF-012 on gold is removed. `ResourceType.GoldMine` is a permanent tile
  resource, `BuildingModel.Resources` grants 1 or 2 `GoldMine`, and the UI already
  tracks a Gold card/count. The earlier review conflated this with the temporary
  gold house rule.

## Decision Ratings

- D0 composition modules: risky until the accepted `CommandContext` and
  score/victory seam replace the old descriptor contract in the main text.
- D1 islands as tiles: sound if fixed-vs-shuffleable tile classification is
  integrated before the Seafarers template is created.
- D2 scenario profile: sound directionally, but the score/winner contract is still
  underspecified.
- D3 ships reuse `RoadModel`: sound representation, but buildable-edge affordances
  need more model detail than one `Buildable` road state.
- D4 ship movement: risky. The explicit state fields are now acknowledged, but the
  legality and undo/cancel contract still need concrete shape.
- D5 island-aware shuffle: sound after the fixed/shuffleable resolution, but Phase
  1 cannot ship independently unless it includes that protection.
- D6 longest trade route: sound after the `IsRouteSegmentTraversable` correction;
  do not rewrite the traversal.
- D7 new-island bonus VP: sound if implemented as explicit `GameModel` scoring
  state and included in the scenario score contribution.
- D8 backwards compatibility: risky until the hash-version policy is precise.
- D9 client: feasible, but the interaction registry must be stateful and the board
  model must distinguish road-buildable from ship-buildable affordances.

## Findings

### SF2-001 - High - Superseded decisions are not integrated

- Location: D0, D3-D8, Review resolutions.
- What is wrong: The appended resolution table fixes important problems, but the
  main sections still contain stale claims: ship purchase needs no bespoke logic,
  longest route is a one-line helper change, `BuildIndex` can support ships built
  this turn, D5 can wrap the existing shuffle, and D8 gets compatibility from optional
  JSON fields alone. The resolution table says it supersedes those sections, but
  implementation plans are likely to quote the earlier sections.
- Recommended fix: Before approval, fold accepted resolutions into the main
  D-sections and remove or rewrite the stale language. Keep a short changelog if
  desired, but make each decision have one authoritative wording.

### SF2-002 - High - Phase 1 is not independently shippable

- Location: Phased delivery; D1, D5;
  `Catan3.Shared/Extensions/GameModelExtensions.cs:715`, `:724`, `:735`.
- What is wrong: Phase 1 says add a Seafarers template with sea and island tiles
  and make the board render. But new-game creation immediately calls `Shuffle`,
  which currently shuffles resources and numbers across every tile. The accepted
  fixed-vs-shuffleable correction is scheduled for Phase 2, so Phase 1 can corrupt
  the very board it is supposed to display.
- Recommended fix: Move fixed tile classification and shuffle exclusion into
  Phase 1, or make Phase 1 a non-playable template/editor preview that does not
  pass through `GameStateMachine.HandleNewGameAsync`. The first shippable game
  creation phase must preserve sea tiles.

### SF2-003 - High - Dispatch failure semantics are still not concrete

- Location: D0 dispatch; `Catan3.GameService/Controllers/GameApiController.cs:126`,
  `:163`; `Catan3.GameService/Services/AsyncCommandProcessor.cs:38`, `:154`.
- What is wrong: The resolution correctly adds `CommandContext`, caller validation,
  and replay surfaces. It still does not define the exact descriptor signature,
  whether module handlers call `LogGameModel` or return a pre-log model, how
  exceptions map to async REST acceptance plus SignalR failure, or how command IDs
  correlate with recorded module failures.
- Recommended fix: Replace the old `Handle(GameModel, JsonElement)` contract with
  a concrete async descriptor API, including input validation, model mutation,
  replay record creation, logging ownership, and error recording. Add one example
  descriptor for `ShipPurchaseMessage`.

### SF2-004 - High - Score/victory seam is accepted but not specified

- Location: D2, D7, Review resolutions;
  `Catan3.Shared/GameLogic/GameStateMachine.cs:628`, `:649`, `:1718`, `:1720`.
- What is wrong: The design now accepts a score/victory contribution seam, but the
  module contract and extension-point table still have no hook or model shape for
  it. Current scoring adds settlements, cities, longest road, largest army, and VP
  cards. `HandleDeclareWinnerAsync` accepts a current-player winner and manual VP
  card counts; it does not read a scenario target or bonus VP state.
- Recommended fix: Define either `OnScore(GameModel, PlayerModel, ScoreBreakdown)`
  or explicit `GameModel` score contribution fields for scenario bonus VP. State
  where `VictoryPointTarget` is displayed and whether winner declaration enforces
  it or allows a table-assistant override.

### SF2-005 - High - One `RoadState.Buildable` cannot express road vs ship choices

- Location: D3, D9, SF-006 resolution;
  `Catan3.Shared/Models/GameEnums.cs:31`,
  `react-ui/components/game/board/GameBoard.tsx:1005`, `:1356`, `:1384`.
- What is wrong: The accepted edge classifier says a coastal edge may offer either
  a road or a ship, and D9 now requires distinct road-buildable vs ship-buildable
  affordances. The current board model has one `RoadModel` per edge and one
  `RoadState.Buildable`. That cannot represent "road-buildable", "ship-buildable",
  and "both" without either overwriting state or relying on hidden client mode.
- Recommended fix: Add a buildable edge affordance model, for example
  `RoadModel.BuildableKinds`, a separate `BuildableEdgeModel`, or distinct
  placement-mode overlays computed from server state. Use it in rendering,
  hit-testing, purchase validation, and replay tests.

### SF2-006 - Medium - Ship movement needs an explicit undo/cancel contract

- Location: D4; `Catan3.Shared/GameLogic/GameStateMachine.cs:775`, `:1127`,
  `:1349`, `:1775`; `Catan3.Shared/Models/RoadModel.cs:34`.
- What is wrong: The resolution correctly rejects `BuildIndex` as built-this-turn
  state and adds explicit pending move state. It still leaves key semantics open:
  whether `BeginMoveShip` creates a replayed log state, whether cancel restores
  `PreviousGameState`, whether undo after begin differs from undo after move, and
  how supplemental turns reset "moved a ship this turn".
- Recommended fix: Define the state machine for `BeginMoveShip`, `CancelMoveShip`,
  and `MoveShip` in D4, then require tests for undo after begin, undo after move,
  cancel, turn reset, and supplemental-turn reset.

### SF2-007 - Medium - Open-route legality is still too vague

- Location: D4, D6; table-assistant principle.
- What is wrong: The table-assistant principle is valid for rare pathological
  cases, but the frequent Seafarers rule is not optional: only an open ship, not
  one connecting two of the player's buildings and not one built this turn, may
  move. The design does not define the minimum algorithm for open-end detection
  or disconnection prevention.
- Recommended fix: Specify a bounded server check: movable ships must have at
  least one endpoint that is not connected to another owned ship/road through the
  same route and must not be the only segment connecting two owned buildings.
  Explicitly list which rare graph cases remain table-enforced.

### SF2-008 - Medium - Hash migration policy needs a versioned contract

- Location: D8; `Catan3.Shared/Extensions/GameModelExtensions.cs:208`, `:255`.
- What is wrong: The accepted policy says include Seafarers semantic state only
  for scenario-opted games and keep Regular/Expansion hash-neutral. That is the
  right intent, but the design does not define the discriminator, hash version,
  or how loaded legacy Seafarers-in-progress games would be treated if saved
  before all fields exist.
- Recommended fix: Add a `GameHashVersion` or scenario-gated hash section with
  explicit included fields. Require tests for unchanged Regular/Expansion replay
  hashes and distinct Seafarers hashes for road vs ship, island bonus, and pending
  ship move state.

### SF2-009 - Medium - Editor support remains broader than `Sea` plus group

- Location: D1, D5, Phase 2; `react-ui/app/templates/[id]/page.tsx:74`, `:239`;
  `react-ui/components/templates/TileContextMenu.tsx:9`.
- What is wrong: The resolution adds editor support for `Sea`, `IslandGroup`, and
  fixed coordinates. That is necessary, but not sufficient for "a human can build
  the board and it just works." The editor also needs to preserve separated
  coordinates across layout changes, show fixed vs shuffleable status, and prevent
  invalid combinations such as numbered sea or shuffleable sea.
- Recommended fix: Add editor acceptance criteria to Phase 2: can create a
  separated island, tag groups, mark sea/fixed tiles, save/load the template,
  create a game from it, and verify shuffle preserves sea and group membership.

### SF2-010 - Low - Remaining citation drift should be cleaned before approval

- Location: D4, D5, D9, extension table.
- What is wrong: Some line anchors remain stale or imprecise after the resolution
  edits. `SetActionFlags` starts at `GameStateMachine.cs:1062`, not `:1073`.
  `executeCommand` starts at `react-ui/lib/services/GameServiceProxy.ts:693`, not
  `:694`. D5 still cites `GameModelExtensions.cs:715-782` without the actual
  `Catan3.Shared/Extensions/` path. The road purchase path still reads like a
  "ship template" even though it sets `RoadState.Road` at `GameStateMachine.cs:1408`.
- Recommended fix: Do a final citation pass after integrating the resolution table
  into the main text. Keep only anchors that are likely to survive implementation
  planning.

## Dimension Coverage

- Composition hooks: sufficient for New Shores only after adding the accepted
  score/victory seam and invoking `OnTurnAdvanced` at the actual turn-reset site.
  Roll production and setup-allocation hooks are not required for this scenario.
- Generic dispatch: still risky until the `CommandContext` descriptor API and
  async failure semantics are concrete.
- Islands as tiles and `IslandGroup`: sound with fixed/shuffleable tile
  classification; unsafe if Phase 1 creates games before that work lands.
- Ships reuse `RoadModel`: sound for owned pieces, but buildable affordance state
  needs more than `RoadState.Buildable`.
- Ship movement: promising robber-style flow, but undo/cancel and minimum legality
  rules need explicit state transitions.
- Longest Trade Route: the corrected predicate approach is sound and preserves the
  non-DFS traversal.
- Backwards compatibility: JSON defaults are not the hard part; hash versioning
  and replay compatibility are.
- Client: rendering is data-driven enough; interaction must become a stateful
  registry and must visually distinguish road vs ship placement options.
- Scenario framework: flags are adequate for New Shores if score contribution,
  VP target display, and winner policy are specified.
- Phasing: not yet independently shippable because Phase 1 depends on fixed sea
  shuffle behavior.
- Testing and tooling: add tests for fixed sea shuffle, impossible group fallback,
  road-vs-ship hash, coastal edge dual affordance, route junctions, ship movement
  undo/cancel, React replay, generated types, and editor-created Seafarers boards.
- Omissions: buildable-edge affordance model, exact descriptor API, score seam
  signature, ship movement state chart, hash versioning, and editor acceptance
  criteria.

## Top 3 Must-Fix Items

1. Integrate the accepted review resolutions into the main design sections so
   implementation plans do not follow stale claims.
2. Move fixed/shuffleable tile handling into the first Seafarers game-creation
   phase, or make Phase 1 explicitly non-game-creating.
3. Define the road-vs-ship buildable affordance model and the concrete
   `CommandContext` expansion dispatch API.
