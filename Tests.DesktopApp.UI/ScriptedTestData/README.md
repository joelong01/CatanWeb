# Catan Recording & Replay System

This directory contains the automated UI testing system for the Catan desktop application. The system records user interactions during gameplay and replays them deterministically for automated testing.

## Overview

The recording/replay system captures MVVM messages during gameplay and converts them into a format suitable for UI automation testing. This enables:

- **Deterministic Testing**: Same inputs produce same outputs every time
- **Regression Testing**: Verify that UI changes don't break existing workflows  
- **Cross-Platform Validation**: Ensure game logic works consistently
- **GameHash Validation**: Verify game state integrity throughout replay

## Architecture

### Core Components

1. **Recording System** (`GameRecorder.cs`)
   - Captures MVVM messages during live gameplay
   - Converts messages to `IRecordedMessage` records with GameHash snapshots
   - Saves complete test scenarios as `.catan_test` files

2. **Message Types** (`RecordedMessage.cs`)
   - `ShuffleRecord`: Deterministic board shuffling with seed
   - `RollRecord`: Dice rolls with recorded values
   - `BuildingUpgradeRecord`: Settlement/city placements
   - `RoadPurchaseRecord`: Road construction
   - `MoveRobberRecord`: Robber movement and targeting
   - And more...

3. **Replay Engine** (`FullCyclePackagedUiTests.cs`)
   - Loads `.catan_test` files and extracts recorded actions
   - Validates GameHash before each action to ensure state consistency
   - Executes UI automation to replay user interactions
   - Uses FlaUI to find and interact with UI elements via AutomationId

4. **Game Logic** (`GameController.cs`, `GameFactory.cs`)
   - Handles deterministic game state transitions
   - Uses seeded randomization for reproducible board generation
   - Maintains GameHash integrity throughout state changes

## Recording Flow

### 1. Enable Recording Mode
- Start the desktop application
- Enable recording mode via the UI toggle
- GameRecorder begins capturing all MVVM messages

### 2. Play Through Scenario
- Perform the actions you want to test (shuffle board, place buildings, roll dice, etc.)
- Each action gets recorded with:
  - The MVVM message parameters
  - The GameHash **before** the action (for validation during replay)
  - Timestamp and action metadata

### 3. Stop Recording
- Disable recording mode
- GameRecorder saves the complete scenario to a `.catan_test` file containing:
  ```json
  {
    "gameModel": { /* Initial game state */ },
    "actionStack": [
      {
        "type": "shuffleRecord",
        "gameHash": "A1B2C3D4", 
        "seed": 123456
      },
      {
        "type": "buildingUpgrade", 
        "gameHash": "E5F6G7H8",
        "buildingKey": { /* coordinates and position */ }
      }
      // ... more actions
    ]
  }
  ```

## Replay Flow

### 1. Test Initialization
- `FullCyclePackagedUiTests` loads the specified `.catan_test` file
- `ScenarioLoader` deserializes the JSON into `List<IRecordedMessage>`
- Test validates the scenario structure and action count

### 2. Game State Setup
- Application launches with the recorded GameModel
- Initial GameHash is computed and recorded
- UI automation framework (FlaUI) connects to the application

### 3. Action Execution Loop
For each recorded action:

```csharp
// 1. Validate current game state matches recorded expectation
string currentHash = GetCurrentGameHash();
if (currentHash != recordedMessage.GameHash) {
    throw new InvalidOperationException("Game state mismatch");
}

// 2. Execute the recorded action via UI automation or direct message
switch (recordedMessage) {
    case ShuffleRecord shuffle:
        // Send ShuffleMessage with recorded seed for deterministic result
        WeakReferenceMessenger.Default.Send(new ShuffleMessage(shuffle.Seed));
        break;
    case BuildingUpgradeRecord building:
        // Find UI element and click it
        var element = uiHelper.FindElement(building.BuildingKey.GetAutomationId());
        element.Click();
        break;
    // ... handle other message types
}

// 3. Wait for action to complete and UI to update
WaitForUiUpdate();
```

### 4. Validation & Completion  
- Each action's result is validated against expected game state
- Final game state is compared to expected end state
- Test reports success/failure with detailed logging

## Key Design Principles

### Deterministic Randomization
- **Problem**: Board shuffling uses random numbers, making replay non-deterministic
- **Solution**: Record the random seed with shuffle actions and use UI binding to inject test data

#### **Test Data Injection Pattern**
For actions requiring deterministic behavior during testing, we use hidden UI elements bound to ViewModel properties:

```csharp
// 1. Add hidden UI element with TwoWay binding
<!-- In MainPage.xaml -->
<TextBox AutomationProperties.AutomationId="TestSeedInput"
         Text="{x:Bind MainPageModel.GameViewModel.ShuffleSeed, Mode=TwoWay}"
         Opacity="0.01" Width="5" Height="5" />

// 2. Add property to ViewModel  
[ObservableProperty]
public partial string ShuffleSeed { get; set; } = "";

// 3. Command checks property first, falls back to random
private int GetTestSeedOrRandom()
{
    if (!string.IsNullOrEmpty(ShuffleSeed) && int.TryParse(ShuffleSeed, out int testSeed))
    {
        ShuffleSeed = ""; // Clear after use
        return testSeed;
    }
    return Random.Shared.Next(); // Fallback to random
}

// 4. Test injects data before UI interaction
var testSeedInput = FindByAutomationId("TestSeedInput");
testSeedInput.AsTextBox().Text = shuffle.Seed.ToString();
var shuffleButton = FindByAutomationId("ShuffleButton");
shuffleButton.Click(); // Uses injected seed
```

This pattern allows tests to provide deterministic input while maintaining normal UI interactions.

### GameHash Validation
- **Purpose**: Verify that game state exactly matches recorded state before each action
- **Implementation**: Fast hash computed from all game state (tiles, buildings, roads, player data)
- **Benefits**: Catches state drift, ensures reproducibility, validates game logic

### State-Before-Action Recording
- **Critical**: Record GameHash **before** executing action, not after
- **Reason**: During replay, we validate the pre-action state matches expectation
- **Flow**: `GetCurrentState() → RecordAction() → ExecuteAction() → UpdateGameHash()`

### Type-Safe Message Conversion
- **Pattern**: Extension methods convert MVVM messages to records
  ```csharp
  public static IRecordedMessage ToRecord(this ShuffleMessage msg, string gameHash)
      => new ShuffleRecord(gameHash, msg);
  ```
- **Benefits**: Compile-time safety, easy to extend, consistent serialization

## File Organization

### Test Files
- **`Regular.catan_test`**: Basic 3-player game scenarios
- **`Expansion.catan_test`**: 5-player expansion pack scenarios  
- **Custom files**: Add your own `.catan_test` files for specific test cases

### Utilities
- **`copy-latest-catan-test.ps1`**: Copies most recent recording from Documents folder
- **`ScenarioLoader.cs`**: Loads and validates `.catan_test` files
- **`UIAutomationHelper.cs`**: Helper methods for FlaUI automation

## Usage Examples

### Running Tests

```bash
# Use default test file
dotnet test Tests.DesktopApp.UI

# Use specific test file via environment variable
$env:CATAN_TEST_FILE = "Regular.catan_test"
dotnet test Tests.DesktopApp.UI

# Use command line argument
dotnet test Tests.DesktopApp.UI -- --test-file Regular.catan_test
```

### Creating New Test Scenarios

1. **Record a new scenario**:
   - Launch desktop app
   - Enable recording mode  
   - Play through your test scenario
   - Stop recording (saves to Documents\Catan Saved Games)

2. **Copy to test directory**:
   ```powershell
   .\copy-latest-catan-test.ps1
   ```

3. **Run your test**:
   ```bash
   dotnet test Tests.DesktopApp.UI -- --test-file YourScenario.catan_test
   ```

### Debugging Failed Tests

When a test fails:

1. **Check GameHash mismatch**: Look for "Game state mismatch" errors
2. **Verify AutomationIds**: Ensure UI elements can be found by automation
3. **Check timing**: UI might need more time to update between actions
4. **Validate test file**: Ensure `.catan_test` file isn't corrupted

## Technical Implementation Details

### Message Serialization
Uses System.Text.Json with polymorphic serialization:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ShuffleRecord), "shuffleRecord")]
[JsonDerivedType(typeof(RollRecord), "rollRecord")]
// ... other types
public interface IRecordedMessage
{
    string GameHash { get; }
    string RecordType { get; }
}
```

### AutomationId Patterns
UI elements use consistent AutomationId patterns:

- Buildings: `"Building-{q},{r},{s}-{position}"` (e.g., `"Building-2,-1,-1-TopRight"`)
- Roads: `"Road-{q},{r},{s}-{side}"` (e.g., `"Road-1,0,-1-Bottom"`)
- Dice: `"Roll-{number}"` (e.g., `"Roll-7"`)
- Players: `"GoFirst-{playerId}"`, `"ParticipatingInSupplemental-{playerId}"`

### Error Handling
- **Invalid GameHash**: Test stops immediately with clear error message
- **Missing UI Elements**: Detailed logging shows which AutomationId failed
- **Timeout Issues**: Configurable wait times for UI updates
- **Malformed Data**: JSON deserialization errors with file validation

## Extending the System

### Adding New Message Types

1. **Create Record Class**:
   ```csharp
   public sealed class YourActionRecord : IRecordedMessage
   {
       public const string Discriminator = "yourAction";
       public string GameHash { get; init; } = string.Empty;
       public string RecordType => Discriminator;
       // ... your properties
   }
   ```

2. **Register Type**:
   ```csharp
   [JsonDerivedType(typeof(YourActionRecord), YourActionRecord.Discriminator)]
   ```

3. **Add Extension Method**:
   ```csharp
   public static IRecordedMessage ToRecord(this YourMessage msg, string gameHash)
       => new YourActionRecord(gameHash, msg);
   ```

4. **Handle in Tests**:
   ```csharp
   case YourActionRecord yourAction:
       Execute_YourAction(yourAction);
       break;
   ```

### Performance Considerations

- **GameHash Computation**: O(n) where n = game entities, cached when possible
- **UI Automation**: Minimize element lookups, use efficient selectors
- **File I/O**: Test files are typically <100KB, loaded once per test
- **Memory Usage**: Records are lightweight, minimal object allocation during replay

This system provides a robust foundation for automated UI testing while maintaining the flexibility to test complex game scenarios and catch regressions early in the development cycle.