# Seafarers Expansion

**Status:** Draft  
**Date:** 2026-02-12

## Summary

- Support **Seafarers Core** (two islands, ships, pirate) and **Seafarers Full** (scenario set).
- Extend metadata schema: islands, sea tiles w/ `seaKind` (buildable/blocked), face-down tiles, island discovery bonuses.
- Extend GameModel/GameStateMachine for ships, pirate, island-aware shuffle/balance, longest route across roads + ships.
- Seed two templates; ensure metadata-driven creation works end-to-end.

## Rules Highlights (from official Seafarers)

- **Ships**: cost 1 lumber + 1 wool; build on **buildable sea** adjacent to own settlement/city/ship/road; only **last ship** in open route can move; cannot move if built this turn; cannot disconnect route.
- **Pirate**: like robber for sea; blocks ship build/move on its hex; steal from adjacent ships; move pirate OR robber on 7/knight.
- **Gold fields**: land hex; yields resource of choice per adjacent settlement (2 for city).
- **Islands**: scenarios define multiple islands; bonus VP for first settlement on a new island; exploration may reveal face-down tiles.
- **Victory**: scenario-dependent; typically 10 VP plus island bonuses; Longest Trade Route includes roads + ships.

## Metadata Extensions

- `tiles`: `islandId`, `type` (`land|sea`), `seaKind` (`buildable|blocked`), `faceDown` (for exploration), optional `resource`/`number` (face-down revealed later), `tags` (`gold`, `starter`, etc.).
- `islands`: `id`, `name`, `shuffleGroup` (per-island shuffle/balance), optional `discoveryVp`.
- `rules`: `entitlements` map (`ship` included) with `cost`, `icon` (font/image front/back), `title`, `description`, `purchaseType`; `pirateEnabled`, `longestRouteIncludesShips`, `islandDiscoveryBonus`, optional `explorationRewards`.

## GameModel Additions (Shared)

- **Ships**: model similar to roads (owner, path across hex sides), stored separately (`List<ShipModel>`).
- **Pirate**: position on sea tile; included in GameModel; messages to move pirate.
- **Island bookkeeping**: map of tile → islandId; derived from metadata; used for discovery VP.
- **Longest Route**: computation includes roads + ships (respecting blocked edges by pirate).

## GameStateMachine Changes

- **HandlePurchaseAsync**: allow `PurchaseType.Ship` when rules permit; place ship on buildable sea; adjacency rules.
- **Ship Movement**: new message `MoveShipMessage` to move last ship in a route; validate movement rules and pirate blocking.
- **Pirate**: `MovePirateMessage` (or reuse robber message with type); blocks ship build/move on its tile; steals from adjacent ships.
- **Shuffle/BalancedShuffle**: per-island shuffle groups; enforce no adjacent 6/8 within island; maintain resource distribution per island; handle `faceDown` tiles (leave numbers/resources unset until revealed).
- **Island Discovery**: on first settlement on an island, award VP per rules and reveal face-down tiles in that island (if applicable).

## GameService & API

- `GameTemplate` seeds: `seafarers-core`, `seafarers-full`.
- `/api/game/new` works with templates (already covered in builder doc).
- Messages: add typed `PurchaseShipMessage`, `MoveShipMessage`, `MovePirateMessage`.

## React UI Changes

- Board renders **sea tiles** (buildable vs blocked styling), **ships**, **pirate**, **gold** tiles.
- **PurchasePanel** data-driven HexRing: entitlements from metadata; buttons with font/image icons; front/back info with cost/details.
- Tooling to **place ships** and show adjacency hints; disable actions when pirate blocks.
- Display **island discovery** events and bonus VP.
- New icons via Catan font for ship/pirate if available; otherwise vector.

## Testing

- **Unit**: ship placement/movement validation; pirate blocking; per-island shuffle; island discovery VP; longest route with ships.
- **Integration**: GameService new game for seafarers templates; replay tests covering ships/pirate.
- **UI**: React tests for rendering ships/pirate and blocking states; PurchasePanel entitlements render and enable/disable correctly.

## Risks

- Route calculation complexity with ships + roads; ensure performance.
- Pirate and robber coexistence; ensure messages distinguish targets.

## Open Questions

- Exploration revealing face-down tiles: do we implement now or stub? (MVP: no fog-of-war, but support `faceDown` flag for future.)
- Do we need additional entitlements (e.g., ship movement cost)? (MVP: movement free, rules enforced.)

## Milestones

1. Shared models + messages + per-island shuffle.
2. Seed templates; GameService uses templates.
3. React UI rendering/update for ships/pirate.
4. Tests (unit, integration, UI) green.
