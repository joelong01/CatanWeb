# GitHub Copilot – Catan Project Instructions

**Last Updated:** 2026-01-15

## Primary Instructions

Read and follow all rules and guidelines in `.ai/ai-rules.md`.

## Project Architecture

| Project | Purpose |
|---------|---------|
| **Catan3.Shared** | Core game logic, models, GameStateMachine (authoritative) |
| **Catan3.GameService** | ASP.NET Core backend with REST API + SignalR |
| **Catan3.WebUI** | Blazor WebAssembly frontend |
| **WebUI.Server** | Blazor Server host |
| **DesktopApp** | WinUI3 desktop client (Windows-only) |

## Code Review: Things to Flag

### Critical Issues (Block PR)
- Hardcoded colors instead of CSS variables
- String-based commands instead of typed messages (`UndoMessage`, `PurchaseMessage`, etc.)
- State management outside of GameModel
- Missing null checks on SignalR message handlers
- Security issues (SQL injection, XSS, hardcoded secrets)

### Important Issues (Request Changes)
- Hardcoded URLs or configuration values
- Missing error handling in async code
- Breaking changes to public APIs without migration path
- Large methods that should be refactored

### Minor Issues (Comment Only)
- Minor style inconsistencies
- Missing XML documentation on internal methods
- PowerShell script analyzer warnings

## Code Review: Things NOT to Flag

- Using F1 (Free) Azure tier (acceptable for dev)
- Skipped tests with `[Skip]` attribute (intentionally deprecated)
- `= null!` on fields set in constructor via methods (intentional)

## Key Principles

1. **GameModel is single source of truth** - All client state comes from GameModel
2. **Typed messages, not strings** - Use message classes for client-server communication
3. **CSS variables for theming** - Never hardcode colors
4. **GameServiceProxy for communication** - All client-server comms through shared proxy
5. **Minimal changes** - Make surgical modifications, don't refactor surrounding code
6. **Platform-specific icons** - WebUI uses Catan font (`Catan.ttf`) for cross-platform support; DesktopApp uses Segoe MDL2 Assets (Windows-only)

## Build & Test Commands

```bash
# Build (excludes DesktopApp)
dotnet build CatanWeb.slnf -c Release

# Run tests
dotnet test Tests/Shared
dotnet test Tests/GameService

# Full validation
pwsh ./catan.ps1 test

# Deploy to Azure
pwsh ./catan.ps1 azure deploy
```

## Technology Stack

- .NET 9.0 (pinned in `global.json`)
- Blazor WebAssembly + ASP.NET Core + SignalR
- SQLite via Entity Framework Core
- xUnit for testing

For comprehensive details, see `.ai/ai-rules.md`.
