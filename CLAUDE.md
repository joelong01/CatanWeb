# Project Context for Claude Code

## Current State (2025-09-14)

The Catan3 project is a multi-platform Settlers of Catan game system with:

- **Desktop App** (WinUI3) - Working reference implementation with service mode capability
- **GameService** (ASP.NET Core) - SignalR + REST API backend
- **Shared Library** - Common models and game logic
- **Test Suite** - Unified test infrastructure with modern ReplayTest approach

## Latest Session (September 14, 2025 - Missing Stars Bug Fix & Architecture Consolidation)

### Missing Stars on Buildings Bug Resolution

Successfully identified and fixed critical bug where local Desktop games weren't
showing buildable building indicators (stars) or enabling Next button:

- **Root Cause Analysis**: Local NewGame handler was missing `LogGameModel()` call
  that marks buildable buildings with stars and enables UI buttons
- **Architecture Inconsistency**: Desktop implementation was missing GameModel
  processing that GameService had, causing display state initialization failure
- **Single Source of Truth**: Consolidated all game initialization logic into
  `GameStateMachine.HandleNewGameAsync()` method to prevent future divergence

### Unified Game Creation Architecture

Implemented architectural consolidation to ensure consistency between local and
service modes:

- **GameStateMachine.HandleNewGameAsync()**: New unified method containing all
  game initialization including `InitializeLoggingState()` and `LogGameModel()`
- **Both Implementations Converged**: Desktop and GameService now delegate to
  same authoritative method for game creation
- **Code Duplication Eliminated**: Removed obsolete helper methods and moved
  CreateNew logic into shared GameStateMachine method
- **File Path Pattern Preserved**: GameService uses temporary paths, Desktop
  uses proper save file paths, both through same unified interface

### Comprehensive Handler Coverage Analysis

Conducted thorough analysis of message handlers across Desktop and GameService:

- **Complete Coverage Confirmed**: All GameService SignalR handlers have
  corresponding Desktop MVVM handlers
- **Handler Comparison**: Verified all 14 GameService handlers match Desktop
  implementations
- **Architecture Consistency**: Both systems use same GameStateMachine methods
  for all game logic operations
- **No Missing Handlers**: LoadGameMessage determined to be GameService-specific
  for REST API, not needed in Desktop app

### Testing Validation

Verified fix through comprehensive testing:

- **ReplayExpansionTest Passed**: Confirms expansion game logic works correctly
  with consolidated architecture
- **Build Success**: All projects compile without errors or warnings
- **Architecture Validation**: Single source of truth prevents future bugs

### Technical Implementation

- **Method Signature**: `HandleNewGameAsync(IGameMetadata, IList<string>, string)`
  provides unified interface for all game creation
- **Proper Initialization Sequence**: Method calls `InitializeLoggingState()` →
  `LogGameModel()` → returns fully initialized GameModel
- **Compilation Cleanup**: Removed obsolete CreateNewGameAsync helper that
  caused method signature conflicts
- **Anti-pattern Resolution**: Eliminated temporary GameModel creation by using
  proper file path generation

### Build Status

- **Current Status**: All projects build successfully without errors or warnings
- **Architecture Complete**: Missing stars bug resolved, consolidation implemented
- **Testing Verified**: ReplayExpansionTest and build validation confirm success
- **Ready for Production**: No blockers, architecture is consistent and working

## Previous Session (September 13, 2025 - Settings Validation & ServiceGame Handler Fix)

### Settings Dialog UX Critical Fix

Implemented comprehensive settings validation system to fix critical UX issue where nested ContentDialogs were causing WinUI exceptions:

- **Problem Analysis**: Users unchecking "Use GameService" got validation errors in nested ContentDialogs, which WinUI doesn't support
- **Architecture Solution**: Replaced nested dialogs with real-time validation using red borders, disabled Save button, and inline error messages
- **Conditional Validation**: SaveFileLocation only required when ServiceGame=false, with proper dependency logic
- **GameService Connectivity**: Added HTTP reachability checks with 3-second timeout for service URLs

### Real-time Validation Implementation

Successfully implemented MVVM-based validation architecture:

- **SettingItemViewModel**: Per-setting validation logic with UI thread-safe updates using DispatcherQueue
- **ObservableProperty Pattern**: Automatic change notifications throughout validation chain
- **Data-driven Tooltips**: Enhanced settings.json with tooltip and errorTooltip properties
- **SettingsModel.ValidateAsync()**: Centralized validation with conditional logic and service connectivity checks
- **Visual Feedback**: Red borders, disabled Save button, and inline error tooltips for invalid settings

### Automatic ServiceGame Handler Re-registration

Fixed critical issue where ServiceGame setting changes required app restart to take effect:

- **ObservableProperty Integration**: CurrentSettings property in GameMessageService with automatic change detection
- **PropertyChanged Subscription**: Direct subscription to individual ServiceGame SettingItem for immediate response
- **Recursion Prevention**: Flag-based protection against infinite registration loops during handler switching
- **Proper Cleanup Order**: EndGame message sent before UnregisterGameMessages to ensure handlers process cleanup
- **Thread Safety**: All registration operations properly marshaled to background thread

### Version Pinning for VS 2026 Compatibility

Resolved auto-upgrade issues from Visual Studio 2026 Insiders:

- **global.json**: Pin .NET SDK to 9.0.305 with latestPatch rollForward to prevent unwanted upgrades
- **Directory.Build.props**: Pin Windows SDK to stable 10.0.22621.3233, enable package lock files
- **Dependency Stability**: Package lock files prevent SDK upgrade cascading to dependencies

### Technical Decisions

- **ObservableProperty over manual tracking**: Leverages CommunityToolkit.Mvvm for automatic change notifications
- **PropertyChanged subscription to individual SettingItem**: More precise than SettingsModel-level change detection
- **Data-driven validation approach**: Settings metadata in JSON drives UI behavior and validation rules
- **Recursion prevention over complex state management**: Simple boolean flag prevents infinite loops

### Build Status

- **Current Status**: All projects build successfully without errors or warnings
- **Validation Work**: 19 files modified with comprehensive settings validation and handler architecture
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

1. **Fix Desktop UI test failures** - Default MainGameModel shows longer before new game loads, causing test failures. Need to prevent showing default MainPage game model and update tests for new game latency
2. **Verify ServiceGame setting propagation works end-to-end** - Test that UI changes immediately affect game creation behavior without requiring app restart
3. **Address TaskCompletionSource race condition** - Implement proper synchronization in SettingsRequestRecipient when multiple UpdateSettings messages are sent

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
- remember: build using the build script: ./build.ps1 -NoTest