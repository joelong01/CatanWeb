# Scripted Test Data

This directory contains `.catan_test` files used for automated UI testing of the Catan desktop application.

## Test File Format

Each `.catan_test` file contains:
- **GameModel**: The initial game state
- **ActionStack**: Array of actions to execute during the test

## Running Tests with Custom Files

You can specify which test file to run using either of these methods:

### Method 1: Environment Variable
```bash
# Windows PowerShell
$env:CATAN_TEST_FILE = "RegularGameTest.catan_test"
dotnet test Tests.DesktopApp.UI

# Windows Command Prompt
set CATAN_TEST_FILE=RegularGameTest.catan_test
dotnet test Tests.DesktopApp.UI

# Linux/Mac
export CATAN_TEST_FILE=RegularGameTest.catan_test
dotnet test Tests.DesktopApp.UI
```

### Method 2: Command Line Arguments
```bash
dotnet test Tests.DesktopApp.UI -- --test-file RegularGameTest.catan_test

# Or short form
dotnet test Tests.DesktopApp.UI -- -t RegularGameTest.catan_test
```

### Default Behavior
If no test file is specified, the test will use `Expansion-Test.catan_test` by default.

## Available Test Files

- **Expansion-Test.catan_test**: Default test with expansion pack, 5 players
- **RegularGameTest.catan_test**: Regular game test scenario
- Add your own `.catan_test` files here for custom scenarios

## Creating New Test Files

1. Enable recording mode in the desktop app
2. Play through your scenario
3. Use the recording output to create a new `.catan_test` file
4. Place the file in this directory
5. Run the test with your new file using the methods above

## Notes

- Test files are automatically copied to the output directory during build
- The test creates a temporary copy to avoid modifying the original
- Test execution logs will indicate which file is being used