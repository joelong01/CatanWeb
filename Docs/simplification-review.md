# Catan Codebase Simplification Review

**Date:** 2026-03-13
**Scope:** Full codebase deep dive — identify redundant, dead, or unnecessarily complex code
**Goal:** Simplify without breaking gameplay or degrading board performance

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Dead Code — Safe to Delete](#2-dead-code--safe-to-delete)
3. [Redundant / Duplicate Code](#3-redundant--duplicate-code)
4. [Overly Complex Code That Can Be Simplified](#4-overly-complex-code-that-can-be-simplified)
5. [Abandoned Infrastructure](#5-abandoned-infrastructure)
6. [WebUI Duplication (Game.razor)](#6-webui-duplication-gamerazor)
7. [Cross-Cutting Concerns](#7-cross-cutting-concerns)
8. [Complexity That Should Be Protected](#8-complexity-that-should-be-protected)
9. [Strategic Questions](#9-strategic-questions)
10. [Prioritized Action Plan](#10-prioritized-action-plan)

---

## 1. Executive Summary

The codebase is in **solid architectural shape** overall. The core game logic in `Catan3.Shared` is
well-designed with proper MVVM support. However, the codebase has accumulated technical debt from
incomplete migrations (hanging GET → SignalR), duplicate implementations across layers, and
test/debug code left in production paths.

**Key metrics from analysis:**

| Area | Files Analyzed | Clean | Minor Issues | Major Issues |
|------|---------------|-------|-------------|-------------|
| Models (Shared) | 27 | 15 (56%) | 8 (30%) | 4 (14%) |
| GameStateMachine | 2 | 1 | 0 | 1 |
| Extensions/Utility | 25+ | 18 | 5 | 2 |
| GameService backend | 15+ | 8 | 4 | 3 |
| WebUI frontend | 20+ | 15 | 3 | 2 |
| Tests/DesktopApp/CLI | 15+ | 8 | 4 | 3 |

**Estimated removable code:** ~2,000–3,000 lines across the solution.

---

## 2. Dead Code — Safe to Delete

These items are confirmed unused and can be removed with confidence.

### 2.1 Files to Delete

| File | Reason |
|------|--------|
| `Catan3.GameService/Services/ClientNotificationService.cs` | Abandoned hanging-GET notification service. Never instantiated; `Program.cs` only registers `SignalRNotificationService`. |
| `Catan3.GameService/Models/ErrorViewModel.cs` | MVC boilerplate. REST API uses JSON error responses (`CommandResponse`), not Razor views. |
| `Tests/Desktop/ScriptedTestData/RecordedAction.cs` | Empty stub — code moved to `Catan3.Shared.Models`. Comment confirms it. |
| `Tests/Desktop/ScriptedTestData/TestAction.cs` | Empty stub — code moved to `Catan3.Shared.Models`. Comment confirms it. |

### 2.2 Dead Methods and Members

| Location | Item | Why It's Dead |
|----------|------|---------------|
| `GameStateMachine.cs:802-842` | `BalanceBoard()` method | Never called. Replaced by `HandleBalanceBoardAsync()` which uses `BalancedShuffle()`. |
| `GameStateMachine.cs:1921-1930` | `CanTransitionToNext()` method | Body is entirely commented out. Always returns `true`. Called once at line 1159 but provides zero validation. |
| `GameStateMachine.cs:2143-2150` | Debug variable `test` | Hardcoded `BuildingKey` with commented-out `Debug.Assert(false)`. Left from a debugging session. |
| `HomeController.cs:32-35` | `Privacy()` action | No `Privacy.cshtml` view exists. Template artifact from ASP.NET MVC scaffold. |
| `GameApiController.cs:271-277` | `POST /api/game/register` | Explicitly deprecated — returns `400 Bad Request` to all callers. |
| `TileModelExtensions.cs:97-125` | `ToResourceCardType()` | Identity function — maps every `ResourceType` value to itself. Used 0 times externally. |
| `GameApiController.cs` | `GameApiOptions` config class | Registered in DI with empty body. Comment says "No hanging GET timeout needed." Artifact from old architecture. |

### 2.3 Dead Enum Members

| File | Member | Reason |
|------|--------|--------|
| `GameEnums.cs:85-86` | `GameState.TestCheckpoint` | Test-only enum value in production code. Should be removed or moved to test assembly. |

### 2.4 Skipped Tests With Dead Data

| Item | Location |
|------|----------|
| `ReplayExpansionTest()` | `EndToEndGameTests.cs:79` — Skip reason: "Deprecated: Test file hashes no longer match" |
| `ReplayRegularTest()` | `EndToEndGameTests.cs:90` — Same skip reason |
| `Expansion.catan_test` | `Tests/Data/` — Only referenced by skipped test |
| `Regular.catan_test` | `Tests/Data/` — Only referenced by skipped test |
| `TestDataLoader` class | `Catan3.Shared/TestData/` — Loads the unused `.catan_test` files |

---

## 3. Redundant / Duplicate Code

### 3.1 CRITICAL: `ExtractNameFromId()` Triplicated

Three **identical** implementations of the same string-parsing method:

| Location | Visibility |
|----------|------------|
| `GameModel.cs:277-292` | `public static` |
| `PlayerModel.cs:187-202` | `private static` |
| `GameInfo.cs:86-101` | `private static` |

**Fix:** Extract to a single shared utility method. All three callers should use one implementation.

### 3.2 GameServiceProxy — Duplicate Null Checks

Every command method in `GameServiceProxy.cs` contains the **same null-check block twice**:

```csharp
if (string.IsNullOrEmpty(_gameId))
    throw new InvalidOperationException("Must join a game...");

if (string.IsNullOrEmpty(_gameId))   // EXACT DUPLICATE
    throw new InvalidOperationException("Must join a game...");
```

**Affected methods** (at minimum): `ExecuteUndoAsync` (211-215), `ExecuteRedoAsync` (234-238),
`ExecuteNextAsync` (257-261), `ExecuteBalanceAsync` (300-304).

**Fix:** Remove the duplicate check in each method. Optionally extract to a `ValidateGameId()` helper.

### 3.3 Harbor Ownership Tracked in Two Places

- `PlayerModel.OwnedHarbors` (`List<HarborKey>`) — only 1 usage in `GameStateMachine`
- `HarborModel.Owner` (`PlayerModel?`) — the canonical source

These track the same relationship from opposite directions. Risk of inconsistency if one is
updated without the other.

**Fix:** Remove `PlayerModel.OwnedHarbors` or make it a computed property derived from
`HarborModel.Owner`.

### 3.4 Robber Coordinate Clearing — Two Maintenance Points

```csharp
// In OnRoll(), lines 994-996:
gameModel.Robber.FakeOutCoordinates = null;
gameModel.Robber.PreviousCoordinates = null;

// In NextState() WaitingForNext case, lines 1215-1217:
gameModel.Robber.FakeOutCoordinates = null;
gameModel.Robber.PreviousCoordinates = null;
```

**Fix:** Extract to `RobberModel.ClearTemporaryCoordinates()`.

### 3.5 Supplemental Phase — Nearly Identical Logic Blocks

`GameStateMachine.cs` lines 1239-1294 (`PickSupplementalPlayers` case) and lines 1295-1337
(`Supplemental` case) contain nearly identical loop patterns for finding participating players.

**Fix:** Consolidate into a shared `FindNextSupplementalPlayer()` helper.

### 3.6 `PlayerFromId()` — Redundant Overload

`PlayerModelExtensions.cs` has two overloads:
- `PlayerFromId(this IList<PlayerModel>, string)` — lines 13-16
- `PlayerFromId(this IEnumerable<PlayerModel>, string)` — lines 18-21

The `IList<>` overload is unnecessary because `IList<T>` implements `IEnumerable<T>`.

**Fix:** Remove the `IList<>` overload.

### 3.7 Shuffle Endpoint — Exists in Both REST and SignalR

- REST: `POST /api/game/{gameId}/shuffle` in `GameApiController.cs`
- SignalR: `Shuffle(gameId, playerId)` in `GameHub.cs`

Both perform the same operation. This is a leftover from the REST→SignalR migration.

**Fix:** Remove the REST shuffle endpoint if all clients use SignalR.

### 3.8 DesktopApp GameMessageServiceProxy — 40+ Boilerplate Methods

`DesktopApp/GameState/GameMessageServiceProxy.cs` (21 KB) contains 40+ handler methods, each
following an identical pattern:

```csharp
if (_gameServiceProxy == null)
    SendErrorMessage(...);
else
    await _gameServiceProxy.ExecuteXxxAsync(...);
```

**Fix:** Consolidate into a generic dispatcher method.

---

## 4. Overly Complex Code That Can Be Simplified

### 4.1 `ResourcesModel.CountForResource()` — 56-Line Switch

**File:** `ResourcesModel.cs:79-134`

A 15-case switch statement mapping each `ResourceType` enum value to its corresponding property.

**Simplified to:**

```csharp
public int CountForResource(ResourceType type) => type switch
{
    ResourceType.Sheep => Sheep,
    ResourceType.Wood => Wood,
    ResourceType.Ore => Ore,
    ResourceType.Wheat => Wheat,
    ResourceType.Brick => Brick,
    // ... remaining cases
    _ => 0
};
```

### 4.2 `ResourceRules.MaxEntitlementCount()` — 27 Cases, 24 Empty

**File:** `GameModels.cs:29-89`

An 88-line switch with 27 `Entitlement` cases, but only 3 have actual values (Settlement, City,
Road). The other 24 just `break` and return 0.

**Simplified to:**

```csharp
public int MaxEntitlementCount(Entitlement entitlement) => entitlement switch
{
    Entitlement.Settlement => MaxSettlements,
    Entitlement.City => MaxCities,
    Entitlement.Road => MaxRoads,
    _ => 0
};
```

### 4.3 `NextState()` — 220 Lines With 18 Empty Cases

**File:** `GameStateMachine.cs:1156-1376`

The central state transition method has 18 game states that have empty `case` blocks (just
`break;` with no logic). These states cannot transition forward when hit.

**States with empty cases:** `MustMoveRobber`, `TooManyCards`, `MustDestroyCity`,
`PickingRandomGoldTiles`, `HandlePirates`, `DoneDestroyingCities`, `MustMoveMerchant`,
`DestroyRoad`, `SwapNumbers`, `PickDeserter`, `PlaceDeserterKnight`, `DoneWithDeserter`,
`UpgradeToMetro`, `TestCheckpoint`, `DisplaceVictimKnight`, `DisplaceKnightMoveVictim`,
`ClickOnKnight`.

**Fix:** Either implement these state transitions (if they're meant for expansion features)
or remove them and use a `default` case. At minimum, add a comment explaining they're
placeholder expansion states so future developers don't mistake them for bugs.

### 4.4 Handler Boilerplate in GameStateMachine — 20+ Repetitions

Every `Handle*Async()` method follows this pattern:

```csharp
public Task<GameModel> HandleXxxAsync(XxxMessage message)
{
    var gameModel = _gameLog.CopyCurrent();
    _logger.Trace(...);
    _recorder?.RecordAction(message.ToRecord(gameModel));

    gameModel = PrivateXxxMethod(message);
    LogGameModel(gameModel);
    return Task.FromResult(gameModel);
}
```

This pattern repeats 20+ times with only the private method call changing.

**Fix:** Create a generic handler method:

```csharp
private Task<GameModel> ExecuteHandler<TMessage>(
    TMessage message, Func<TMessage, GameModel> handler)
    where TMessage : IRecordableMessage { ... }
```

### 4.5 Null-Check Anti-Pattern — `new T() ?? throw`

**Locations:** `BuildingModel.cs:76`, `GameModel.cs:158`

```csharp
var resources = new ResourcesModel() ?? throw new GameException("allocation must succeed");
```

`new T()` can **never** return null in C#. The throw is unreachable.

**Fix:** Remove the null-coalescing throw.

### 4.6 Display Methods in GameModel — Separation of Concerns Violation

**File:** `GameModel.cs:273-389`

Seven formatting/display methods live in the core model:
`GetDisplayName()`, `GetFormattedGameState()`, `GetCurrentPlayerName()`,
`GetCreatedTimeDisplay()`, `GetIsActive()`, `GetSummary()`, `GetPlayerNames()`.

`GetPlayerNames()` is just `return Players.Select(p => p.Name).ToList();` — a trivial wrapper.

**Fix:** Move to a `GameModelDisplayExtensions` class in the UI layer where they belong.

---

## 5. Abandoned Infrastructure

### 5.1 Hanging GET Notification System (Fully Abandoned)

The codebase migrated from hanging GET (long-polling) to SignalR but left behind:

| Artifact | Location | Status |
|----------|----------|--------|
| `ClientNotificationService.cs` | GameService/Services/ | Never instantiated |
| `WaitForNotificationAsync()` | `SignalRNotificationService.cs:47-50` | Throws `NotSupportedException` — exists only for interface compliance |
| `GameApiOptions.HangingGetTimeout` | GameService/Controllers/ | Configured with empty body |

**Fix:** Delete `ClientNotificationService.cs`. Remove `WaitForNotificationAsync()` from the
`IClientNotification` interface. Delete `GameApiOptions`.

### 5.2 MVC View Infrastructure in a REST API

| Artifact | Location |
|----------|----------|
| `HomeController.Privacy()` | Returns View but no view file exists |
| `HomeController.Error()` | Returns `ErrorViewModel` but API uses JSON |
| `ErrorViewModel.cs` | MVC boilerplate with no consumers |
| `AddControllersWithViews()` | Program.cs — could be `AddControllers()` |

**Fix:** Remove MVC artifacts. Switch to `AddControllers()` if no Razor views are served.

### 5.3 Recording/Replay Endpoints in Production

`RecordingController.cs` exposes test recording infrastructure at `/api/recording/*`:
- `POST /api/recording/start/{gameId}`
- `POST /api/recording/stop/{gameId}`
- `POST /api/recording/replay`
- `GET /api/recordings`

These use in-memory `ConcurrentDictionary` (no persistence across restarts) and are explicitly
for test purposes.

**Fix:** Gate behind `if (!env.IsProduction())` or move to a separate admin/test controller
with authorization.

---

## 6. WebUI Duplication (Game.razor)

`Game.razor` is ~1,800 lines — a monolith handling layout, game interaction, animations,
recording, robber placement, and victory display.

### 6.1 Roll Grid — Rendered 3 Times

The same roll entry UI (for loop from 2-12 with click handlers) appears at:
- Landscape mode: lines 148-165
- Portrait mode bottom: lines 251-266
- Portrait mode center: lines 252-262

**Fix:** Extract into `RollGrid.razor` component.

### 6.2 Purchase Controls — Rendered 2 Times

Five `PurchaseButton` components are declared in both:
- Landscape mode: lines 97-145
- Portrait mode bottom: lines 269-318

**Fix:** Extract into `PurchaseGrid.razor` component.

### 6.3 Board Measurements — Rendered 2 Times

Identical `BoardMeasurement` component with identical parameters at:
- Landscape mode: lines 167-178
- Portrait mode bottom: lines 236-246

**Fix:** Extract layout decision into a `GameLayout.razor` component.

### 6.4 Scattered StateHasChanged() Calls

Manual `StateHasChanged()` at lines 500, 520, 559, 578, 704, 759, 1763, 1768 suggests
potential state synchronization issues.

**Fix:** Use Blazor parameter binding and cascading values where possible to reduce manual
refresh triggers.

### 6.5 NavMenu — Unreachable Methods

`NavMenu.razor` contains two TODO methods that are never called from any UI element:
- `OnSaveGame()` (line 311) — `// TODO: Implement game save via GameConnection`
- `OnShowDebugWindow()` (line 424) — `// TODO: Implement debug window`

**Fix:** Remove these methods until they're actually needed.

---

## 7. Cross-Cutting Concerns

### 7.1 Three Competing Logging Abstractions

| Abstraction | Location | Usage |
|-------------|----------|-------|
| `ILogger<T>` | Microsoft.Extensions.Logging | GameService (standard .NET) |
| `ICatanDebugTrace` | Catan3.Shared/Interfaces/ | Custom game logging (optional in `Log<T>`) |
| `Console.WriteLine()` | GameService/Program.cs | 21+ calls for startup diagnostics |

Additionally, `GameApiController.cs` mixes `Console.WriteLine` with `_logger.LogEvent()` for
database save operations (lines 125-154).

**Fix:** Standardize on `ILogger<T>`. Remove `ICatanDebugTrace` — it duplicates `ILogger`
functionality. Replace `Console.WriteLine` with structured logging.

### 7.2 Mixed Event Patterns

- `Action<T>` delegates — 8 events in `GameServiceProxy.cs` (data-carrying)
- `EventHandler` — 2 events in `GameStateService.cs` (legacy pattern)
- SignalR hub methods — direct push notifications

**Fix:** Standardize on `Action<T>` for local events (already dominant pattern).

### 7.3 Interface Methods Not in IGameStateMachine

Three public handler methods exist in `GameStateMachine` but are **not** declared in the
`IGameStateMachine` interface:

- `HandleUpdateHouseRulesAsync()` (line 197)
- `HandleDeclareWinnerAsync()` (line 628)
- `HandleSwapResourcesAsync()` (line 705)

**Fix:** Add these to the interface, or if they shouldn't be public, make them internal.

### 7.4 Board Info Factory — Three Creation Paths

Board metadata can be created via:
1. Hardcoded singletons (`RegularBoardInfo.Default`, `ExpansionBoardInfo.Default`)
2. JSON templates via `BoardInfoJsonAdapter`
3. Fallback logic when templates aren't seeded

**Fix:** Once template seeding is reliable, remove the hardcoded singletons.

---

## 8. Complexity That Should Be Protected

The following areas are complex but that complexity is **justified and necessary**. Do not
simplify these.

### 8.1 HexCoordinates System ✅ KEEP

`Catan3.Shared/Utility/HexCoordinates.cs` (290+ lines)

- Cube coordinate system enforcing Q+R+S=0 invariant
- Pre-computed direction lookups (O(1) neighbor access)
- Arithmetic operators for coordinate math
- Geometry methods for SVG rendering (MidPoint, Corner calculations)

**Why protected:** Board rendering performance is critical. Pre-computed lookups and operator
overloads enable fast coordinate math that would be slower with method calls or lookups.

### 8.2 CalculateLongestRoad ✅ KEEP

`GameStateMachine.cs:2286-2371` (85+ lines, recursive)

Complex recursive algorithm with fork handling. This is inherently complex because longest road
calculation in Catan is a graph traversal problem with fork detection.

**Why protected:** Algorithmic complexity matches problem complexity. Simplifying would risk
correctness.

### 8.3 MarkBuildableBuildings ✅ KEEP

`GameStateMachine.cs:2129-2213` (84 lines, nested loops)

Validates which building spots are legal for placement based on adjacency rules.

**Why protected:** Validates game rules in real-time for the UI. Incorrect simplification
would allow illegal placements.

### 8.4 GameRecorder ✅ KEEP

`Catan3.Shared/GameLogic/GameRecorder.cs` (173 lines)

Well-designed, single-responsibility class. No dead code, no unused methods, clean state
management.

**Why protected:** Already simple and correct.

### 8.5 JsonHelper ✅ KEEP

`Catan3.Shared/Utility/JsonHelper.cs` (138 lines)

Centralized JSON serialization with consistent options. All serialization goes through this
single source of truth, including compression via Brotli.

**Why protected:** This is a good pattern. Centralizing serialization prevents inconsistency.

### 8.6 StreamDeck Plugin ✅ KEEP

`streamdeck/` — 15 files, ~600 lines

Specialized Elgato Stream Deck integration for streaming control. Focused scope, not redundant
with any other component.

---

## 9. Strategic Questions

These require product decisions before code changes:

### 9.1 React-UI vs. Blazor WebUI

Both `react-ui/` (Next.js 16 + React 19) and `WebUI/` (Blazor WASM) connect to the same
GameService backend. Maintaining two frontends doubles the UI maintenance burden.

**Question:** Is React-UI intended to replace Blazor, or are both permanent? If one is
exploratory, the other should be marked as canonical.

### 9.2 PlayerProfile Classes — Future or Dead?

`Catan3.Shared/PlayerProfile/` contains 4 well-designed classes (`PlayerProfile.cs`,
`PlayerColors.cs`, `LifetimeStats.cs`, `GameStats.cs`) that are not actively used in current
game logic.

**Question:** Are these being integrated into persistence, or should they be removed?

### 9.3 Expansion State Placeholders

17 game states in `NextState()` have empty case blocks. These appear to be placeholders for
Catan expansion features (Cities & Knights, Seafarers).

**Question:** Are these expansion features actively being developed? If not, the empty cases
should be removed and re-added when the expansion code is written.

### 9.4 `BoardLayout.cs` — Empty Partial Class

`Catan3.Shared/Models/BoardLayout.cs` is 11 lines with only a comment: "all properties and
methods are defined in the consuming project partial classes."

**Question:** Is there a consuming project that actually defines the partial? If not, delete it.

---

## 10. Prioritized Action Plan

### Tier 1: Quick Wins (< 30 minutes each, high impact)

| # | Action | Files | Impact |
|---|--------|-------|--------|
| 1 | Delete `ClientNotificationService.cs` | 1 file | Remove abandoned code |
| 2 | Delete `ErrorViewModel.cs` | 1 file | Remove MVC artifact |
| 3 | Delete `RecordedAction.cs` and `TestAction.cs` stubs | 2 files | Remove empty stubs |
| 4 | Remove deprecated `/api/game/register` endpoint | `GameApiController.cs` | Remove dead endpoint |
| 5 | Remove `Privacy()` from `HomeController` | `HomeController.cs` | Remove dead endpoint |
| 6 | Remove `BalanceBoard()` dead method | `GameStateMachine.cs` | Remove dead code |
| 7 | Remove `CanTransitionToNext()` or implement it | `GameStateMachine.cs` | Remove no-op method |
| 8 | Remove debug variable at line 2143 | `GameStateMachine.cs` | Remove debug leftover |
| 9 | Fix duplicate null checks in `GameServiceProxy` | `GameServiceProxy.cs` | Remove copy-paste errors |
| 10 | Remove `ToResourceCardType()` identity method | `TileModelExtensions.cs` | Remove dead code |
| 11 | Remove `new T() ?? throw` anti-pattern | `BuildingModel.cs`, `GameModel.cs` | Fix unreachable code |
| 12 | Delete `GameApiOptions` empty config class | Controllers + Program.cs | Remove abandoned config |

### Tier 2: Moderate Effort (1-2 hours each, medium impact)

| # | Action | Files | Impact |
|---|--------|-------|--------|
| 13 | Consolidate `ExtractNameFromId()` to one location | 3 files | Eliminate triplication |
| 14 | Simplify `CountForResource()` switch | `ResourcesModel.cs` | -40 lines |
| 15 | Simplify `MaxEntitlementCount()` switch | `GameModels.cs` | -60 lines |
| 16 | Remove `WaitForNotificationAsync()` from interface | `IClientNotification.cs` | Clean interface |
| 17 | Remove or compute `PlayerModel.OwnedHarbors` | `PlayerModel.cs` | Single source of truth |
| 18 | Remove skipped tests and unused test data | `EndToEndGameTests.cs`, `Tests/Data/` | Remove dead tests |
| 19 | Remove redundant `PlayerFromId()` overload | `PlayerModelExtensions.cs` | Cleaner API |
| 20 | Add missing methods to `IGameStateMachine` interface | `IGameStateMachine.cs` | Interface completeness |
| 21 | Remove NavMenu TODO methods | `NavMenu.razor` | Remove dead UI code |

### Tier 3: Larger Refactors (2-6 hours each, high long-term impact)

| # | Action | Files | Impact |
|---|--------|-------|--------|
| 22 | Extract `RollGrid.razor` and `PurchaseGrid.razor` from `Game.razor` | WebUI/Pages, Components | Eliminate 3x/2x duplication |
| 23 | Move GameModel display methods to extension class | `GameModel.cs` → new ext class | Separation of concerns |
| 24 | Create generic handler in `GameStateMachine` to reduce boilerplate | `GameStateMachine.cs` | -200 lines of boilerplate |
| 25 | Consolidate supplemental phase logic | `GameStateMachine.cs` | Reduce ~100 lines |
| 26 | Consolidate DesktopApp `GameMessageServiceProxy` | `GameMessageServiceProxy.cs` | Reduce 40+ boilerplate methods |
| 27 | Standardize logging to `ILogger<T>` only | Multiple files | Remove competing abstractions |
| 28 | Gate `RecordingController` behind environment check | `RecordingController.cs` | Protect prod from test endpoints |

---

*This document is a reference for prioritizing simplification work. Items in Tier 1 are
low-risk, high-confidence removals. Tier 2 requires light verification. Tier 3 items need
careful testing after changes.*
