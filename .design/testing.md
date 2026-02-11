# Testing Strategy

**Last verified:** January 30, 2026

## Methodology

The project prioritizes **regression testing via replay** over granular
unit tests for game logic. Since `GameStateMachine` is complex and
deterministic, replaying a known sequence of moves and verifying the
resulting `GameHash` is the most effective validation approach.

## Test Projects

### 1. Tests.GameService (Integration / Replay)

**Path:** `Tests/GameService/Tests.GameService.csproj`

Tests full game simulation by replaying recorded games against the live
service.

| File | Purpose |
|------|---------|
| `ReplayTests/ReplayTest.cs` | Recording replay test harness |
| `ReplayTests/EndToEndGameTests.cs` | Full game cycle tests |
| `ReplayTests/TestClient.cs` | SignalR test client |
| `TestWebApplicationFactory.cs` | ASP.NET Core test host setup |

**How it works:**

1. Load a `.catan_test` file containing recorded actions
2. Start an in-memory test server via `TestWebApplicationFactory`
3. Replay each action against the `GameStateMachine`
4. Verify `GameHash` matches the expected value at each step
5. Any hash mismatch indicates a regression in game logic

### 2. Tests.Shared (Unit Tests)

**Path:** `Tests/Shared/Tests.Shared.csproj`

Tests stateless utilities and serialization correctness.

| File | Purpose |
|------|---------|
| `Serialization/BidirectionalSerializationTests.cs` | JSON round-trip validation |
| `Serialization/JavaScriptCompatibilityTests.cs` | C# to TypeScript interop |
| `Serialization/SerializationPerformanceTests.cs` | Performance benchmarks |
| `Serialization/SharedSerializationTests.cs` | Core serialization tests |

**Focus:** `HexCoordinates` math, serialization round-trips between C#
and TypeScript type formats, performance benchmarks.

### 3. Tests.Desktop (UI Automation)

**Path:** `Tests/Desktop/Tests.DesktopApp.UI.csproj`

WinUI 3 desktop app UI tests using FlaUI automation.

| File | Purpose |
|------|---------|
| `FullCyclePackagedUiTests.cs` | End-to-end UI tests |
| `ScriptedTestData/` | Action execution framework |
| `TestInfra/UiTestInfrastructure.cs` | FlaUI automation helpers |

**Note:** These test the Desktop reference app, not the React UI.

## Test Data

**Path:** `Tests/Data/`

Contains `.catan_test` files -- recorded game scenarios used by replay
tests. These files are compressed JSON logs of complete game sessions.

## Test Framework

- **Framework:** xUnit
- **React:** Vitest 4.0.17 (in `react-ui/package.json` devDependencies)
- **Desktop:** FlaUI for WinUI 3 automation

## Running Tests

```powershell
# Run all tests (build + unit + replay)
./catan.ps1 test

# Run specific test by name
dotnet test Tests/GameService --filter "TestName"

# Run replay test against live service
./catan.ps1 recording replay
```

## GameHash Verification

The `GameHash` is a deterministic hash computed from:

- Tiles (types and number tokens)
- Players (scores, buildings, roads)
- Harbors
- Roads
- Buildings
- GameState
- CurrentPlayerId
- Robber position

Any change to game logic that alters state transitions will produce a
different hash, causing replay tests to fail. This is the primary
regression detection mechanism.
