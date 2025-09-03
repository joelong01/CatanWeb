# Project Context for Claude Code

## Current State (2025-08-30)

The Catan3 project is a multi-platform Settlers of Catan game system with:

- **Desktop App** (WinUI3) - Working reference implementation
- **GameService** (ASP.NET Core) - SignalR + REST API backend
- **Shared Library** - Common models and game logic
- **Test Suite** - End-to-end tests for both UI and service

## Recent Architecture Changes (Current Session)

### Major Refactoring: Eliminated Lambda Patterns

Removed lambda-based architecture in favor of clean message passing:

- **Removed**: `GameStateMachineWrapper.cs` and `GameServiceFactoryAdapter.cs` -
  unnecessary wrapper layers
- **Pattern Change**: Old lambda approach
  `ExecuteAction(gameId, gsm => gsm.DoSomething())` → Direct method calls
- **New Structure**:
  - `CreateNewGameAsync(NewGameMessage)` - for new games via GameFactory
  - `LoadExistingGameAsync(LoadGameModelMessage)` - for loading existing GameModels
  - Shared internal handler `HandleLoadGameModelInternalAsync(GameModel, bool isTest)`

### GameFactory Static Pattern

- **Made GameFactory static** - no interface needed, direct static method calls
- **Added GameName parameter** to `NewGameMessage` and `GameFactory.CreateGame`
- **Centralized game creation logic** - GameFactory handles GameId,
  GameName, CreatedTime
- **Extension methods moved** to `GameModelExtensions.cs`: `Shuffle()`, `SaveFileName()`, `Validate()`

### Two Distinct Loading Paths

1. **Path 1: From SerializableLog** (compressed .catan files) → Load via GameServiceLogAdapter.LoadFromSerializableLog
2. **Path 2: From GameModel JSON** → LoadGameModelMessage → HandleLoadGameModelInternalAsync

### IsTest Parameter Pattern

Added `IsTest` boolean throughout the stack:

- **LoadGameModelMessage.IsTest** - distinguishes test vs production scenarios
- **Log constructor** - controls file path generation (empty for tests, temp path for production)
- **Service layer** - passed through to control filesystem behavior

## Known Issues

### Build Errors (IN PROGRESS)

Current build failures that need fixing before commit:

- **GameApiController.cs:182** - Still using old lambda pattern for
  LoadExistingGameAsync
- **Missing method** - Need LoadFromCompressedLogAsync or similar for
  SerializableLog path
- **Pattern confusion** - LoadGameMessage (CompressedLog) vs
  LoadGameModelMessage (GameModelJson)

### Architecture Cleanup Needed

- **Two loading paths need clarification** - SerializableLog vs GameModel paths
- **GameApiController needs proper method** for LoadGameMessage handling
- **Remove all lambda patterns** - some may still exist

## Important Files

### Core Game Loading

- `Catan3.GameService/Controllers/GameApiController.cs` - REST endpoints for
  game lifecycle
- `Catan3.Shared/GameLogic/GameStateMachine.cs` - Core game logic and state
  management
- `Catan3.GameService/Services/GameStateMachineService.cs` - Service layer coordination

### Test Infrastructure  

- `Tests.GameService/SignalR/EndToEndGameTests.cs` - Failing multiplayer tests
- `Tests.GameService/TestWebApplicationFactory.cs` - Test server setup with verbose logging

### Message Types

- `Catan3.Shared/Models/MessageObjects.cs` - Request/response DTOs
- `Catan3.Shared/Interfaces/IGameStateMachine.cs` - Service interface definitions

## Development Workflow

### Build Commands

- **Quick build**: `./build.ps1 -NoTest`
- **Clean build**: `./build.ps1 -NoTest -Clean`
- **Inner loop**: `/inner_loop` command (build→fix→repeat until clean)

### Testing

- **Specific test**: `dotnet test [project] --filter [name]`  
- **All tests**: `./build.ps1` (includes test run)
- **Focus area**: GameService SignalR integration tests

## Next Session Priorities

1. **Fix GameService test timeouts** - Primary blocking issue
2. **Investigate SignalR group membership** - Why commands don't reach GameHub
3. **Complete end-to-end test validation** - Ensure multiplayer flow works

## Rules & Patterns

- **No temporary GameStateMachine creation** - Always invalid pattern
- **Direct domain object passing** - Controllers deserialize, services take domain objects
- **JSON string pattern** - Use for ASP.NET validation bypass
- **Clean builds required** - Fix all errors, warnings, and lint issues
  before handover
