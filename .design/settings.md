# Settings & House Rules

**Last verified:** January 30, 2026

## House Rules

**Model:** `Catan3.Shared/Models/HouseRules.cs`

House rules are configurable per game via the `HouseRules` property on
`GameModel`. They can be updated during the `PickingBoard` phase via
`PUT /api/game/{gameId}/houserules`.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `GoldTiles` | int | 1 | Number of gold tiles in board generation |
| `WallsProtectCities` | bool | true | Cities protected by walls (C&K rule) |
| `HideBaronBeforeInvasion` | bool | false | Hide baron position until first move |
| `KnightMovesBaronBeforeRoll` | bool | true | Knight moves baron before dice roll |
| `HideRobberBeforeInvasion` | bool | false | Hide robber position until first move |
| `KnightMovesRobberBeforeRoll` | bool | false | Knight moves robber before dice roll |
| `SupplementalMinPlayers` | int | 5 | Minimum players for supplemental build phase |
| `GriefDodgy` | bool | true | Enable special animations targeting "Dodgy-001" |

### GriefDodgy

When enabled, the UI applies special effects during robber actions:

- Non-Dodgy buildings fade during robber placement
- Celebration animation when Dodgy is targeted by the robber
- Fake-out animation when Dodgy is NOT targeted
- Hardcoded player ID: `"Dodgy-001"`

This is a humorous house rule for in-person play among friends.

### Supplemental Build Phase

When `HasSupplementalBuildPhase` is true (determined by player count >=
`SupplementalMinPlayers`), a special build phase occurs between turns:

1. After current player clicks Next, state moves to
   `PickSupplementalPlayers`
2. Other players opt in/out of supplemental building
3. Each participating player can build (but not trade)
4. After all supplemental builds, the next player's turn begins

This implements the official 5-6 player expansion rule.

## Resource Rules

**Model:** `Catan3.Shared/Models/ResourceRules.cs`

Resource rules define the number of each tile type in the game:

| Resource | Regular Count | Expansion Count |
|----------|:---:|:---:|
| Wheat | 4 | 6 |
| Wood | 4 | 6 |
| Sheep | 4 | 6 |
| Ore | 3 | 5 |
| Brick | 3 | 5 |
| Desert | 1 | 2 |
| Water | 18 | 22 |
| Gold | per HouseRules | per HouseRules |

## Configuration Endpoints

| Method | Route | Purpose |
|--------|-------|---------|
| PUT | `/api/game/{gameId}/houserules` | Update house rules for a game |
| POST | `/api/settings/update` | Update global service settings |

## React UI

House rules are displayed and editable during the `PickingBoard` game
state. The React UI reads house rules from `gameStore` and sends updates
via `GameServiceProxy`.
