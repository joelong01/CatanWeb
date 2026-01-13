# .design Directory Table of Contents

**Last Updated:** January 12, 2026  
**Purpose:** Authoritative "as built" design documentation for the Catan project

## Quick Reference

| Document | Purpose | Last Updated |
|----------|---------|-------------|
| [README.md](README.md) | Directory overview and maintenance guidelines | Dec 3, 2025 |
| [summary.md](summary.md) | High-level project architecture summary | Dec 3, 2025 |
| [css.md](css.md) | CSS architecture and theming standards | Dec 3, 2025 |
| [portrait-mode.md](portrait-mode.md) | Mobile portrait mode design | Dec 3, 2025 |
| [azure.md](azure.md) | Azure deployment architecture and configuration | Dec 8, 2025 |
| [azure-cosmos-dal.md](azure-cosmos-dal.md) | CosmosDB data access layer design (comprehensive) | Dec 8, 2025 |
| [azure-sql-serverless-alternative.md](azure-sql-serverless-alternative.md) | Azure SQL Serverless alternative analysis (recommended) | Dec 8, 2025 |
| [test-plan.md](test-plan.md) | WebUI test suite with recording/replay infrastructure | Jan 12, 2026 |

## Projects

Architecture and implementation details for each solution project:

- [**cli.md**](projects/cli.md) - CLI tool design and command patterns
- [**desktop-app.md**](projects/desktop-app.md) - WinUI 3 desktop application architecture
- [**game-service.md**](projects/game-service.md) - ASP.NET Core backend service
- [**shared.md**](projects/shared.md) - Shared library components and utilities
- [**webui.md**](projects/webui.md) - Blazor WebAssembly frontend architecture

## Systems

Cross-cutting system designs that span multiple projects:

- [**board-rendering.md**](systems/board-rendering.md) - SVG board generation and rendering pipeline
- [**coordinates.md**](systems/coordinates.md) - Coordinate system and geometric calculations
- [**database.md**](systems/database.md) - Entity Framework Core data layer design
- [**game-service-api.md**](systems/game-service-api.md) - REST API design and SignalR hubs
- [**mvvm-messaging.md**](systems/mvvm-messaging.md) - MVVM pattern and inter-component messaging
- [**save-load.md**](systems/save-load.md) - Game persistence and serialization
- [**settings.md**](systems/settings.md) - Configuration management across platforms
- [**signalr-to-rest-migration.md**](systems/signalr-to-rest-migration.md) - Migration plan for SignalR → REST commands

## User Interface

Component-level design documentation for UI elements:

- [**assets.md**](ui/assets.md) - Icon fonts, images, and visual asset management
- [**board-measurement.md**](ui/board-measurement.md) - Board sizing and coordinate mapping
- [**number-token.md**](ui/number-token.md) - Number token rendering and placement
- [**player-viewmodel.md**](ui/player-viewmodel.md) - Player data presentation patterns
- [**uiscale-design.md**](ui/uiscale-design.md) - Responsive scaling architecture (WebUI)

## Document Categories

### Implementation Reference

Documents that describe current code behavior and patterns:

- All `projects/` documents
- `systems/board-rendering.md`, `systems/database.md`, `systems/game-service-api.md`
- `ui/assets.md`, `ui/number-token.md`

### Architecture Specifications

Documents that define system-wide design patterns:

- `summary.md`, `css.md`
- `systems/coordinates.md`, `systems/mvvm-messaging.md`
- `ui/uiscale-design.md`

### Mobile/Responsive Design

Documents focused on cross-platform UI adaptation:

- `portrait-mode.md`
- `ui/board-measurement.md`
- `ui/uiscale-design.md`

## Usage Guidelines

### For AI Assistants

When working on Catan project tasks:

1. **Discovery Phase**: Start with `summary.md` for project overview
2. **Component Work**: Check relevant `projects/` document for implementation patterns
3. **System Integration**: Reference `systems/` documents for cross-cutting concerns
4. **UI Development**: Consult `ui/` documents for component-specific design
5. **Mobile Support**: Review `portrait-mode.md` and `ui/uiscale-design.md` for responsive requirements

### For Developers

- Use this documentation as the authoritative source for "as built" system behavior
- Update relevant documents when making implementation changes
- Reference legacy `design_docs/` for historical design decisions
- Maintain line lengths under 150 characters for markdown lint compliance

## Relationship to Legacy Documentation

The `.design/` directory supplements the existing `design_docs/` directory:

- **design_docs/**: Historical design decisions and initial specifications
- **.design/**: Current implementation documentation and "as built" architecture

Both directories serve important but different purposes in the project documentation ecosystem.
