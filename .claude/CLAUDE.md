# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Communication Style

- Be direct and honest, not agreeable
- Don't start responses with "You're right" or similar validation
- If my approach is wrong or suboptimal, say so clearly
- Focus on facts and technical accuracy over politeness
- Skip the pleasantries and get straight to the solution
- Do not declare work complete without building and verifying tests pass

## Build Commands

```bash
# Full build with tests
pwsh ./build.ps1

# Build without tests
pwsh ./build.ps1 -NoTest

# Clean build
pwsh ./build.ps1 -Clean

# WebUI development (starts GameService + WebUI, launches browser)
pwsh ./webui.ps1 run

# WebUI clean including database
pwsh ./webui.ps1 clean database

# Quick build check
dotnet build

# Run specific tests
dotnet test Tests/GameService --filter "TestName"
```

**Important:** Always use `pwsh` (PowerShell 7+), never legacy `powershell`.

### WebUI Development Script

When working on the WebUI, use `./webui.ps1` instead of `./build.ps1`. This script manages
the full development workflow including database setup, hot reload, and service lifecycle.

**Common commands:**

| Command | Purpose |
|---------|---------|
| `./webui.ps1 run` | Build, init database, start GameService + WebUI with hot reload |
| `./webui.ps1 stop` | Stop running services |
| `./webui.ps1 restart` | Stop and restart services |
| `./webui.ps1 update` | Rebuild and restart (when hot reload fails) |
| `./webui.ps1 clean` | Clean build artifacts (preserves database) |
| `./webui.ps1 clean database` | Clean build AND database |
| `./webui.ps1 database doctor` | Diagnose database health |
| `./webui.ps1 database install` | Fresh database install with default data |

**Typical workflow:**

1. `./webui.ps1 run` - Start services (hot reload enabled)
2. Make code changes - Browser auto-refreshes
3. If hot reload fails: `./webui.ps1 update`
4. If database schema changed: `./webui.ps1 database install`

**Service URLs:**

- GameService: `http://localhost:8080`
- WebUI: `http://localhost:5296`

## AI Rules Directory

The `.ai/` directory contains AI-agnostic rules and standards that apply to **all AI assistants**
working on this project (Claude, Copilot, ChatGPT, etc.).

**Required:** Load and follow `.ai/ai-rules.md` at the start of any coding session. This file contains:

- Code quality standards and documentation requirements
- Markdown formatting rules (must be lint-free)
- File and directory conventions
- Build and development workflow
- Architecture patterns and design decisions
- Testing requirements
- Git and version control guidelines

The `.ai/` directory is the **single source of truth** for project standards. Agent-specific
configurations (like this CLAUDE.md file) reference back to `.ai/` content.

## Acknowledgment

When you read this file, output: "I have read CLAUDE.md and will follow its guidelines."

## Project Architecture

**Multi-platform Settlers of Catan game system** with shared core logic:

| Project | Purpose |
|---------|---------|
| **Catan3.Shared** | Core game logic, models, GameStateMachine (2000+ lines), communication interfaces |
| **Catan3.GameService** | ASP.NET Core backend with REST API + SignalR hub |
| **Catan3.WebUI** | Blazor WebAssembly frontend |
| **DesktopApp** | WinUI3 reference implementation - **DO NOT MODIFY** unless explicitly directed |
| **Catan3.CLI** | Testing/automation harness |

### Key Files

- `Catan3.Shared/GameLogic/GameStateMachine.cs` - Authoritative game rules engine
- `Catan3.Shared/Services/GameServiceProxy.cs` - Unified REST + SignalR client interface
- `Catan3.GameService/Hubs/GameHub.cs` - SignalR real-time communication
- `Catan3.GameService/Controllers/GameApiController.cs` - REST endpoints

### Communication Flow

1. **REST API** (game lifecycle): Create/load games via `/api/game/new`, `/api/game/load`
2. **SignalR** (real-time gameplay): Join game → receive `GameStateUpdated` → send typed messages → broadcast updates

### Message Architecture

Use typed messages, not generic string commands:

- `UndoMessage` → `HandleUndoAsync()`
- `RedoMessage` → `HandleRedoAsync()`
- `NextMessage` → `HandleNextAsync()`
- `PurchaseMessage` → `HandlePurchaseAsync()`

## Development Rules

1. **GameModel is single source of truth** - All client state comes from GameModel
2. **Desktop app is reference** - Analyze for behavior, but don't modify without explicit direction
3. **Tests must pass** - Build and verify all tests before completing any task
4. **Minimal changes** - Make surgical modifications, don't refactor surrounding code
5. **Use GameServiceProxy** - All client-server communication through the shared proxy
6. **CSS custom properties** - All theming via CSS variables, never hardcode colors
7. **Catan font** - WebUI uses official `Catan.ttf` for game icons (see `Layout/CatanFont.cs`)

## Technology Stack

- **.NET 9.0** (pinned in `global.json`)
- **Blazor WebAssembly** for WebUI
- **ASP.NET Core** with SignalR
- **SQLite** via Entity Framework Core
- **SVG** for dynamic board rendering
- **xunit** for testing

## Documentation Structure

- `.ai/` - AI-agnostic rules and standards (applies to all AI tools)
- `.design/` - Current "as built" architecture docs (start with `summary.md` and `TOC.md`)
- `design_docs/` - Legacy historical documents

## Testing

- **ReplayTest pattern**: Load `.catan_test` files, verify game progression
- **Test data**: `Tests/Data/*.catan_test` contains recorded game scenarios
- Tests validate: board state, player resources, SignalR communication

## Hot Reload Considerations

- **Browser caching**: Hard refresh (Ctrl+Shift+R) after changes
- **SVG caching**: Create new game or restart GameService
- **Blazor**: Some changes require full rebuild

## Current Build Status

All projects build successfully (verified 2025-12-05)
