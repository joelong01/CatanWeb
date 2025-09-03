# Catan3 End-to-End CLI Testing Script

## cli_e2e.ps1

A PowerShell script that automates the complete end-to-end testing workflow for Catan3.

### What it does

1. **Builds the solution** - Ensures all projects are compiled and ready
2. **Checks GameService status** - Verifies if GameService is running on port 8080
3. **Starts GameService if needed** - Automatically launches GameService in the background
4. **Runs the CLI test** - Executes the Catan3.CLI with your specified arguments

### Usage Examples

```powershell
# Basic usage - run a regular game with default settings
.\cli_e2e.ps1 regular

# Run a complete end-to-end test with detailed logging
.\cli_e2e.ps1 regular --complete --log-level INFO

# Run until a specific game state and keep the session alive
.\cli_e2e.ps1 expansion --run-to WaitingForRoll --no-exit

# Run with custom player count and specific URI
.\cli_e2e.ps1 regular --player-count 4 --uri http://localhost:8080

# Run expansion game with all options
.\cli_e2e.ps1 expansion --complete --log-level DEBUG --no-exit
```

### Parameters

- **GameType** (Position 0): `regular` or `expansion` (default: `regular`)
- **Arguments** (Remaining): All CLI arguments you want to pass through

### Available CLI Arguments

- `--complete` - Run full end-to-end game progression
- `--run-to <state>` - Stop at specific GameState (e.g., `WaitingForRoll`)
- `--player-count <n>` - Number of players (default: 3 for regular, 5 for expansion)
- `--log-level <level>` - Logging level: DEBUG, TRACE, INFO, WARNING, ERROR
- `--uri <url>` - GameService URL (default: auto-detected)
- `--no-exit` - Keep game state alive after completion

### Smart Features

- **Auto-detection**: Checks if GameService is already running
- **Background management**: Starts GameService only if needed
- **Cleanup**: Manages GameService lifecycle appropriately
- **Status reporting**: Detailed progress and color-coded output
- **Error handling**: Comprehensive error detection and reporting

### Examples for Testing

```powershell
# Quick smoke test
.\cli_e2e.ps1 regular --run-to PickingBoard

# Test allocation phase
.\cli_e2e.ps1 regular --run-to AllocateResourceForward

# Test dice rolling
.\cli_e2e.ps1 regular --run-to WaitingForRoll

# Full game with debug output
.\cli_e2e.ps1 regular --complete --log-level DEBUG

# Test and keep for debugging
.\cli_e2e.ps1 expansion --run-to WaitingForNext --no-exit --log-level INFO
```

### Integration with Development Workflow

The script is designed for:

- **Automated testing** during development
- **Regression testing** before commits
- **Integration testing** of CLI + GameService
- **Demo preparation** with live game states
- **Debugging** with persistent game sessions

### Output

The script provides color-coded, timestamped output showing:

- Build progress and results
- GameService startup status
- CLI execution progress
- Final summary with URLs for further testing

When GameService is started by the script, it remains available at:

- **Companion Interface**: <http://localhost:8080/companion>
- **Game Discovery**: <http://localhost:8080/api/companion/games>
- **Demo Interface**: <http://localhost:8080/demo>
