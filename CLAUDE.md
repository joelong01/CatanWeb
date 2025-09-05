# Project Context for Claude Code

## Current State (2025-09-05)

The Catan3 project is a multi-platform Settlers of Catan game system with:

- **Desktop App** (WinUI3) - Working reference implementation  
- **GameService** (ASP.NET Core) - SignalR + REST API backend
- **Shared Library** - Common models and game logic
- **Test Suite** - Unified test infrastructure with modern ReplayTest approach

## Recent Architecture Changes (September 5, 2025 Session)

### Major Test Infrastructure Reorganization

Successfully completed comprehensive test infrastructure restructuring:

- **Directory Structure**: Moved all test projects from root level to Tests/ 
  subdirectories using `git mv` to preserve history
  - `Tests/Desktop` (formerly Tests.DesktopApp.UI) - UI automation tests
  - `Tests/GameService` - Integration and SignalR ReplayTest infrastructure  
  - `Tests/Shared` - JSON serialization compatibility tests
  - `Tests/Data` - Centralized test scenario files (.catan_test)
- **Build System**: Updated Catan.sln and all .csproj project references 
  for new directory structure
- **CLI Consolidation**: Removed duplicate Tests/Cli project, kept full-featured 
  Catan3.CLI as single command-line interface

### Test Data Architecture Migration

Transitioned from embedded resources to filesystem-based test data loading:

- **TestDataLoader**: Modified to load .catan_test files from Tests/Data 
  directory instead of embedded resources
- **PowerShell Script**: Updated update-test-files.ps1 to copy test files 
  to Tests/Data location
- **Csproj Cleanup**: Removed embedded resource entries from 
  Catan3.Shared.csproj

### Unified Log Implementation

Consolidated Log implementations into single shared approach:

- **Single Log Class**: All projects now use `Catan3.Shared/Utility/Log.cs`
- **Interface Standardization**: `IGameLog` interface used consistently 
  across Desktop and GameService
- **Architecture Cleanup**: Eliminated duplicate logging implementations 
  and over-abstraction patterns

### Modern Testing Approach

Standardized on ReplayTest methodology:

- **ReplayTest Infrastructure**: Clean test approach using .catan_test files 
  for behavioral consistency validation
- **Legacy Cleanup**: Removed old SignalR testing patterns and 
  EndToEndSignalRSession complexity
- **Two Core Tests**: ReplayExpansionTest and ReplayRegularTest using 
  modern ReplayTest pattern

## Important Files

### Core Game Logic

- `Catan3.GameService/Controllers/GameApiController.cs` - REST endpoints for
  game lifecycle
- `Catan3.Shared/GameLogic/GameStateMachine.cs` - Core game logic and state
  management
- `Catan3.GameService/Services/GameStateMachineService.cs` - Service layer coordination

### Test Infrastructure  

- `Tests/GameService/ReplayTests/EndToEndGameTests.cs` - Modern ReplayTest approach
- `Tests/GameService/TestWebApplicationFactory.cs` - Test server setup
- `Tests/GameService/ReplayTest.cs` - Core ReplayTest infrastructure
- `Tests/Data/*.catan_test` - Test scenario files for game validation

### Unified Utilities

- `Catan3.Shared/Utility/Log.cs` - Consolidated logging implementation
- `Catan3.Shared/TestData/TestDataLoader.cs` - Filesystem-based test data loading
- `Catan3.CLI/Program.cs` - Full-featured command-line interface

### Message Types

- `Catan3.Shared/Models/MessageObjects.cs` - Request/response DTOs
- `Catan3.Shared/Interfaces/IGameLog.cs` - Unified logging interface

## Development Workflow

### Build Commands

- **Quick build**: `./build.ps1 -NoTest`
- **Clean build**: `./build.ps1 -NoTest -Clean`
- **Full build with tests**: `./build.ps1`
- **Inner loop**: `/inner_loop` command (build→fix→repeat until clean)

### Testing

- **GameService tests**: `dotnet test Tests/GameService --filter "ReplaySharedExpansionTestFile"`
- **All tests**: `./build.ps1` (includes test run)
- **Shared tests**: `dotnet test Tests/Shared` (45 serialization tests)
- **Test with verbose**: Add `--verbosity normal` to any dotnet test command

### Test Data Management

- **Update test files**: `./update-test-files.ps1` copies .catan_test files to Tests/Data
- **Test data location**: Tests/Data directory contains all .catan_test scenario files

## Current Issues

### GameHash Investigation (Set Aside)

ReplayTest occasionally shows GameHash mismatches, indicating behavioral 
differences between Desktop and GameService implementations. Recent investigation 
found Log consolidation was successful but didn't resolve all differences.
User resolved specific Regular.catan_test issue by rerecording test data.

### Build Status

- **All projects build successfully**
- **ReplayExpansionTest passes**
- **Tests/Shared passes** (45 tests)
- **Architecture is now unified and cleaner**

## Next Session Priorities

1. **Continue GameHash difference investigation** if issues resurface
2. **Look at other behavioral differences** beyond Log implementation
3. **Consider initialization, RNG, or serialization order differences**

## Rules & Patterns

- **Use Tests/ directory structure** - All test projects under Tests/
- **Single CLI tool** - Catan3.CLI for all command-line utilities
- **Filesystem test data** - Tests/Data for all .catan_test files
- **Unified logging** - IGameLog interface with shared Log implementation
- **ReplayTest methodology** - Modern approach for behavioral validation
- **Clean builds required** - Fix all errors, warnings, and lint issues
  before handover
