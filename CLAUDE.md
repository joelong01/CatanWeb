# Project Context for Claude Code

## Current State (2025-09-13)

The Catan3 project is a multi-platform Settlers of Catan game system with:

- **Desktop App** (WinUI3) - Working reference implementation with service mode capability
- **GameService** (ASP.NET Core) - SignalR + REST API backend
- **Shared Library** - Common models and game logic
- **Test Suite** - Unified test infrastructure with modern ReplayTest approach

## Latest Session (September 13, 2025)

### VS Code Symbol Resolution Issues

Attempted to resolve VS Code C# extension symbol resolution issues where Desktop app couldn't resolve symbols from Shared project:

- **Problem Analysis**: VS Code C# extension has limited WinUI3 support compared to Visual Studio 2022
- **Configuration Updates**: Updated .vscode/settings.json, omnisharp.json, created global.json
- **Platform Consistency**: Fixed x64/Any CPU mismatches between VS Code and OmniSharp configurations
- **Resolution**: Confirmed as VS Code limitation - Visual Studio 2022 works correctly

### Branch Management and Code Quality

Successfully completed major branch consolidation and code quality improvements:

- **Branch Hierarchy**: Corrected merge path from Desktop-Service → DotNet-9 → master
- **CoPilot Improvements**: Ported mathematical constant improvements from master branch
- **Magic Number Removal**: Replaced hardcoded values with named constants in Harbor and BoardLayout classes
- **Branch Cleanup**: Deleted all merged branches, leaving only master and new jdl-test-cleanup branch

### Build Status

- **Current Status**: All projects build successfully without errors or warnings
- **Previous Issues**: Outdated build errors in documentation were resolved during branch merges
- **Clean State**: Solution compiles cleanly with `dotnet build`

## Previous Architecture Changes (September 7, 2025 - Session 2)

### Critical GameService Fixes

Fixed missing GameModel processing in service mode that was causing UI display issues:

- **GameService New Game Processing**: Added missing `InitializeLoggingState` and `HandleNewGameAsync` calls to GameService's NewGame endpoint that were preventing stars from showing on buildings and Next button from enabling
- **SignalR Threading Fix**: Resolved `RPC_E_WRONG_THREAD` COM exception by using MainWindow's DispatcherQueue for thread marshaling instead of GetForCurrentThread()
- **Async Method Consistency**: Fixed all async method naming conventions and return types throughout codebase (GameApiController, GameMessageService)
- **Build Error Resolution**: Eliminated all compilation errors and warnings - solution builds cleanly with `./build.ps1 -NoTest`

### Root Cause Analysis

The issue where new games from service didn't show stars on buildings or enable Next button was caused by:
- GameService creating GameModel but not calling `HandleNewGameAsync` for display state processing
- Local version calls: `CreateNew` → `InitializeLoggingState` → `HandleNewGameAsync` → shows stars/enables buttons  
- Service version was missing: `HandleNewGameAsync` call after GameModel creation
- Shuffle worked because it calls proper GameModel processing methods that new game creation was missing

## Previous Architecture Changes (September 7, 2025 - Session 1)

### Service Game Mode Implementation

Completed comprehensive architecture for Desktop app to delegate game logic to remote GameService:

- **ServiceGame Setting**: Added checkbox in settings.json (default: true) to toggle between local and service execution
- **Conditional Handler Registration**: GameMessageService dynamically registers local vs service handlers based on ServiceGame setting
- **Partial Classes**: Split GameMessageService into main class and GameMessageServiceProxy.cs for service handlers
- **GameServiceProxy Integration**: Enhanced existing proxy with EndGame, PersistGame, and UpdateSettings API endpoints
- **Duplicate Handler Prevention**: Implemented proper unregistration to avoid `InvalidOperationException` on setting changes

### Settings Architecture Overhaul

Replaced direct App.Settings access with proper MVVM messaging-based async architecture:

- **SettingsService**: New dedicated service handles all settings persistence and MVVM messaging
- **Async Settings API**: `SettingsModel.GetAsync()` static method provides clean async access via messaging
- **Decoupled Architecture**: SettingsService is private to App, accessed only through messaging to reduce coupling
- **Message-Based Persistence**: UpdateSettings message triggers automatic saving in SettingsService
- **Proper Cleanup**: Registration/unregistration with TaskCompletionSource prevents memory leaks

### Performance & UI Improvements

- **Settings Performance**: Removed slow environment variable registry writes (eliminated 10-second save delay)
- **Hamburger Menu**: Added to NewGamePage with Settings and Manage Players options following MainPage pattern
- **Centered Dialogs**: Fixed Settings dialog positioning by using `this.Content.XamlRoot`
- **Navigation Safety**: Proper back navigation handling when NewGamePage is initial page

### API Enhancements

Added new GameService REST endpoints:
- `POST /api/game/end` - Proper server-side game cleanup and resource disposal
- `POST /api/settings/update` - Settings synchronization between client and server

## Previous Architecture Changes (September 5, 2025 Session)

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

### Service Mode Architecture

- `DesktopApp/Services/SettingsService.cs` - New service for settings management via MVVM messaging
- `DesktopApp/GameState/GameMessageService.cs` - Main service with conditional handler registration
- `DesktopApp/GameState/GameMessageServiceProxy.cs` - Partial class containing service handlers
- `Catan3.Shared/Models/SettingsModel.cs` - Enhanced with GetAsync() static method
- `Catan3.Shared/Models/MessageObjects.cs` - Added GetSettingsMessage for settings requests

### Updated UI

- `DesktopApp/Game/NewGame/NewGamePage.xaml` - Added SplitView hamburger menu
- `DesktopApp/Game/NewGame/NewGamePage.xaml.cs` - Menu handlers and dialog positioning fixes
- `DesktopApp/Game/NewGame/NewGameViewModel.cs` - Added ShowMenu property for menu state
- `DesktopApp/Settings/SettingsViewModel.cs` - Updated to use async settings API

### Enhanced GameService

- `Catan3.GameService/Controllers/GameApiController.cs` - Added game end and settings sync endpoints
- `Catan3.Shared/Services/GameServiceProxy.cs` - Enhanced with new API methods

### Core Game Logic

- `Catan3.Shared/GameLogic/GameStateMachine.cs` - Core game logic and state management
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
- `Catan3.Shared/Interfaces/IGameLog.cs` - Unified logging interface

## Development Workflow

### Build Commands

- **Quick build**: `dotnet build` (should succeed with no errors)
- **Clean build**: `./build.ps1 -NoTest -Clean`
- **Full build with tests**: `./build.ps1`
- **Inner loop**: `/inner_loop` command (build→fix→repeat until clean)

### Testing

- **GameService tests**: `dotnet test Tests/GameService --filter "ReplaySharedExpansionTestFile"`
- **All tests**: `./build.ps1` (includes test run)
- **Shared tests**: `dotnet test Tests/Shared` (45 serialization tests)
- **Test with verbose**: Add `--verbosity normal` to any dotnet test command

### Settings Testing

- Open Settings dialog from hamburger menu in NewGamePage
- Toggle ServiceGame setting to test handler registration switching
- Verify save performance is fast (no 10-second delay)

## Current Issues

### Build Status

- **All projects build successfully** - All compilation errors resolved
- **Architecture is complete** - Service mode implementation ready for testing
- **No active blockers** - Ready for integration testing

## Next Session Priorities

1. **Complete Service Integration Testing** - Test end-to-end ServiceGame mode switching and verify UI behavior is identical between local and service execution
2. **Update Client Logging** - Implement ILog model for unified logging between service and client
3. **Verify Settings Synchronization** - Ensure settings properly sync to GameService when in service mode

## Rules & Patterns

- **Service Mode Architecture** - Conditional message handler registration based on ServiceGame setting
- **Settings via Messaging** - All settings access through `SettingsModel.GetAsync()` static method
- **Private Services** - Services accessed only through MVVM messaging to reduce coupling
- **Clean API Design** - Intuitive async methods with proper resource cleanup
- **Handler Management** - Always unregister before registering to prevent duplicates
- **Recording Mode** - Start/Stop Recording handlers remain local (not delegated to service)
- **Use Tests/ directory structure** - All test projects under Tests/
- **Single CLI tool** - Catan3.CLI for all command-line utilities
- **Filesystem test data** - Tests/Data for all .catan_test files
- **Unified logging** - IGameLog interface with shared Log implementation
- **ReplayTest methodology** - Modern approach for behavioral validation
- **Clean builds required** - Fix all errors, warnings, and lint issues before handover

## Important Context

- **Default Service Mode**: New installations use ServiceGame=true, so service mode is the primary execution path
- **Message Flow**: UI Action → MVVM Message → Service Handler → GameServiceProxy → GameService → GameStateUpdated Event → UpdateGameModel Message → UI Update
- **Async Initialization**: GameMessageService now properly waits for settings during startup via `await SettingsModel.GetAsync()`
- **Performance**: Environment variable registry writes removed from settings save for better user experience
