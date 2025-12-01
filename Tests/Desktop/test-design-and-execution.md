# Desktop App Test Design and Execution Guide

This document describes the test infrastructure for the Catan Desktop App, including how to record new
test scenarios, manage test files, and execute tests.

## Overview

The Desktop App tests use **UI Automation** (FlaUI) to drive the packaged MSIX app and verify game
functionality end-to-end. Tests are "replay tests" that:

1. Load an initial game state from a `.catan` file
2. Execute a sequence of recorded user actions
3. Verify the game state hash matches expected values after each action

## File Types and Locations

| File Type | Extension | Location | Purpose |
|-----------|-----------|----------|---------|
| Game Save | `.catan` | `DesktopApp/Assets/Test Files/` | Initial game state for starting tests |
| Test Scenario | `.catan_test` | `Tests/Data/` | Complete test: initial state + action stack |
| Test Code | `.cs` | `Tests/Desktop/` | Test execution and validation logic |

### Key Files

- **`Tests/Data/Regular.catan_test`** - Regular game (3-4 players) test scenario
- **`Tests/Data/Expansion.catan_test`** - Expansion game (5-6 players) test scenario
- **`DesktopApp/Assets/Test Files/Expansion-Test.catan`** - Source game file for Expansion tests
- **`Tests/Desktop/FullCyclePackagedUiTests.cs`** - Main test class

## Test Scenario Structure

A `.catan_test` file is JSON containing:

```json
{
  "gameModel": {
    // Complete GameModel representing initial state
    "gameHash": "669B9FED",
    "gameState": "FinishedRollOrder",
    // ... all game properties
  },
  "actionStack": [
    {
      "recordType": "Next",
      "expectedGameHash": "ABC12345",
      "expectedGameState": "PlacingSettlement"
    },
    {
      "recordType": "BuildingUpgrade",
      "buildingKey": { "hexCoordinates": {...}, "position": "Right" },
      "expectedGameHash": "DEF67890",
      "expectedGameState": "PlacingRoad"
    }
    // ... more actions
  ]
}
```

### Action Types

| RecordType | Description | Key Properties |
|------------|-------------|----------------|
| `Next` | Click the Next button | - |
| `Undo` | Click the Undo button | - |
| `Redo` | Click the Redo button | - |
| `Shuffle` | Shuffle the board | - |
| `Purchase` | Purchase an entitlement | `entitlement` |
| `BuildingUpgrade` | Place/upgrade a building | `buildingKey` |
| `RoadPurchase` | Place a road | `roadKey` |
| `MoveRobber` | Move the robber | `coordinates` |
| `Roll` | Roll dice | `roll` (dice value) |

## Recording a New Test Scenario

### Step 1: Enable Recording Mode

In `DesktopApp/App.xaml.cs`, set:

```csharp
public static bool RecordMode { get; set; } = true;
```

Or toggle it at runtime using the "Record Mode" button in the app's command bar (video icon).

### Step 2: Prepare the Initial Game State

1. Create or load a game that represents your desired starting state
2. Save the game as a `.catan` file in `DesktopApp/Assets/Test Files/`
3. The recording will capture the current GameModel when recording starts

### Step 3: Start Recording

1. Build and run the Desktop app: `./build.ps1 -NoTest`
2. Load your `.catan` file
3. Open the Debug Trace window (Menu → "Show Debug Trace")
4. Enable Recording Mode if not already enabled
5. The recording captures:
   - Initial GameModel state
   - Each action with its AutomationId (e.g., `Building-(-3,3,0)-Right`)
   - Expected game hash BEFORE each action

### Step 4: Play Through Your Scenario

Perform all the actions you want to test:

- Click Next button to advance game state
- Place settlements and roads
- Roll dice
- Move robber
- Etc.

Each action is automatically recorded with its:

- `recordType` - The type of action
- `expectedGameHash` - The hash to validate before this action
- `expectedGameState` - The game state to validate before this action
- Action-specific data (building coordinates, road keys, etc.)

### Step 5: Stop Recording and Save

1. Toggle Recording Mode off (or close the app)
2. The `.catan_test` file is saved to `Documents/Catan Saved Games/` with the same name as the log file
3. Copy the generated file to `Tests/Data/` using one of the helper scripts (see below)

### Step 6: Verify the Test

```powershell
# Run just your new test
dotnet test Tests/Desktop/Tests.DesktopApp.UI.csproj --filter "YourTestName"

# Run all Desktop tests
dotnet test Tests/Desktop/Tests.DesktopApp.UI.csproj
```

### Step 7: Disable Recording Mode

In `DesktopApp/App.xaml.cs`, set:

```csharp
public static bool RecordMode { get; set; } = false;
```

## File Management Workflow

### Creating a New Test from Scratch

```text
1. DesktopApp: Create game → Save as .catan
   ↓
2. Copy .catan to: DesktopApp/Assets/Test Files/
   ↓
3. Enable RecordMode → Load .catan → Play through scenario
   ↓
4. Recording saves to: Documents/Catan Saved Games/*.catan_test
   ↓
5. Copy .catan_test to: Tests/Data/
   ↓
6. Add test method in FullCyclePackagedUiTests.cs calling DoFullTestWithScriptedActions("YourTest.catan_test")
```

### Updating an Existing Test

When game logic changes and hashes no longer match:

1. Enable RecordMode
2. Load the original `.catan` file from `DesktopApp/Assets/Test Files/`
3. Play through the same scenario
4. Replace the old `.catan_test` in `Tests/Data/` with the new recording

## Helper Scripts

Two PowerShell scripts automate copying recorded test files:

### `update-test-files.ps1` (Repository Root)

Copies the latest `Regular-*.catan_test` and `Expansion-*.catan_test` files from your saved games
directory to `Tests/Data/`.

```powershell
# From repository root
./update-test-files.ps1

# With custom test data path
./update-test-files.ps1 -TestDataPath "Tests\Data"
```

**Features**:

- Finds saved games in `Documents/Catan Saved Games/` (or `CATAN_DOCUMENTS_PATH` env var)
- Auto-detects Regular vs Expansion based on filename prefix
- Validates JSON structure before copying
- Checks for compression/encoding issues

### `copy-latest-catan-test.ps1` (Tests/Desktop/ScriptedTestData/)

Copies the single most recent `.catan_test` file to the ScriptedTestData folder.

```powershell
cd Tests/Desktop/ScriptedTestData
./copy-latest-catan-test.ps1
```

**Features**:

- Finds the most recent `.catan_test` file by modification date
- Auto-detects game type from JSON content (`gameModel.gameType`)
- Renames to `Regular.catan_test` or `Expansion.catan_test` based on type

### Typical Workflow After Recording

```powershell
# After finishing a recording session:

# Option 1: Update both Regular and Expansion tests
./update-test-files.ps1

# Option 2: Copy just the latest recording
cd Tests/Desktop/ScriptedTestData
./copy-latest-catan-test.ps1
```

### Important Notes

- **Hash Sensitivity**: The `gameHash` is computed from the entire GameModel. Any change to the model
  structure (even adding a field) will change all hashes.
- **Robber Coordinates**: The robber position affects the hash. Use valid coordinates that satisfy
  `Q + R + S = 0`. The default sentinel is `(-99, 99, 0)`.
- **AutomationIds**: The tests use AutomationIds like `Building-(-3,3,0)-Right` to find UI elements.
  These are deterministic based on game coordinates.

## Running Tests

### Prerequisites

1. Desktop app must be deployed as MSIX package
2. Build the solution: `./build.ps1 -NoTest`

### Commands

```powershell
# Run all Desktop UI tests
dotnet test Tests/Desktop/Tests.DesktopApp.UI.csproj

# Run specific test
dotnet test Tests/Desktop/Tests.DesktopApp.UI.csproj --filter "Regular_End_To_End_Test"

# Run with verbose output
dotnet test Tests/Desktop/Tests.DesktopApp.UI.csproj --verbosity normal
```

### Test Execution Flow

1. Test locates the `.catan_test` file in `Tests/Data/`
2. Launches the packaged app with command-line args to auto-load the game
3. Waits for game board to load
4. Caches UI automation elements (buttons, game board, etc.)
5. For each action in the `actionStack`:
   - Validates current game hash matches `expectedGameHash`
   - Executes the action via UI automation
   - Waits for game state to update
6. Test passes if all actions complete without hash mismatches

## Troubleshooting

### Test Fails with Hash Mismatch

```text
Game state mismatch: [Expected GameHash=ABC123][Current Hash=DEF456]...
```

**Cause**: The game model has diverged from the recorded scenario.

**Solutions**:

1. Re-record the test scenario with current game logic
2. Check if game logic changes affected the hash computation
3. Verify the `.catan_test` file wasn't corrupted

### Test Fails to Find UI Element

```text
AutomationId 'Building-(-3,3,0)-Right' not found
```

**Cause**: UI element doesn't exist or has different AutomationId.

**Solutions**:

1. Verify the game state allows that action (e.g., can't place building if not in PlacingSettlement state)
2. Check if UI element naming changed
3. Add delays if UI is slow to render

### App Doesn't Launch

```text
App package is not installed
```

**Solutions**:

1. Deploy the MSIX package: Build in Visual Studio with Deploy option
2. Verify package is registered: `Get-AppxPackage *Catan*`

## Architecture

### Key Classes

| Class | Location | Purpose |
|-------|----------|---------|
| `FullCyclePackagedUiTests` | `Tests/Desktop/` | Main test class |
| `UIAutomationHelper` | `Tests/Desktop/ScriptedTestData/` | UI automation utilities |
| `ScenarioLoader` | `Tests/Desktop/ScriptedTestData/` | Loads `.catan_test` files |
| `ActionExecutor` | `Tests/Desktop/ScriptedTestData/` | Executes recorded actions |
| `GameRecorder` | `DesktopApp/GameState/` | Records actions during gameplay |
| `GameRecorder` | `Catan3.Shared/GameLogic/` | Shared recording logic |

### Recording Flow

```text
User Action in UI
    ↓
GameStateMachine processes action
    ↓
GameRecorder.RecordAction(IRecordedMessage)
    ↓
Action added to internal list with current hash
    ↓
On StopRecording: Write .catan_test file
```

### Playback Flow

```text
Test loads .catan_test
    ↓
App launches with initial GameModel
    ↓
For each recorded action:
    ├─ ValidateGameState (check hash matches)
    ├─ ExecuteRecordedMessage (UI automation)
    └─ Wait for state change
    ↓
Test passes if all actions succeed
```
