# Catan Design Documentation

**Last updated:** January 30, 2026

## Start Here

| Document | Purpose |
|----------|---------|
| [as-built/](as-built/) | **Verified current implementation (30 docs)** -- read this first |
| [as-built/audit-summary.md](as-built/audit-summary.md) | Full system audit findings and recommendations |
| [as-built/game-play.md](as-built/game-play.md) | How humans play this hybrid Catan game |

## As-Built (Verified Current State)

These documents are verified against the actual code and are the
source of truth for how the system works today.

### Architecture & Communication

| Document | Purpose |
|----------|---------|
| [as-built/message-flow.md](as-built/message-flow.md) | State machine, message types, REST endpoints, SignalR events |
| [as-built/game-service-api.md](as-built/game-service-api.md) | Complete REST API (Game, Recording, Stats) and SignalR hub |
| [as-built/react-architecture.md](as-built/react-architecture.md) | React dependencies, stores, components, data flow |
| [as-built/serialization.md](as-built/serialization.md) | TypeGenRunner pipeline, JsonIgnore removal, enum conversion |

### Game Systems

| Document | Purpose |
|----------|---------|
| [as-built/game-rules-summary.md](as-built/game-rules-summary.md) | Game rules mapped to GameState values |
| [as-built/game-state-ui.md](as-built/game-state-ui.md) | All 33 GameStates to UI mapping, ActionFlags, entitlements |
| [as-built/board-rendering.md](as-built/board-rendering.md) | React HexGrid board rendering system |
| [as-built/coordinates.md](as-built/coordinates.md) | Cube coordinate system and hex math |
| [as-built/settings.md](as-built/settings.md) | House rules configuration |
| [as-built/balance-algorithm.md](as-built/balance-algorithm.md) | Board balance: two-phase shuffle, star parity, clump prevention |
| [as-built/grief-dodgy.md](as-built/grief-dodgy.md) | GriefDodgy feature: tile flip, fake-out, celebration |
| [as-built/devcard-tracking.md](as-built/devcard-tracking.md) | Dev card tracking, VP entry, score formula |

### UI & Layout

| Document | Purpose |
|----------|---------|
| [as-built/floating-panel.md](as-built/floating-panel.md) | FloatingPanel + MinimizedBar, WindowPosition, layoutStore |
| [as-built/css-theming.md](as-built/css-theming.md) | Tailwind v4 tokens, player colors, @utility directives |
| [as-built/portrait-mode.md](as-built/portrait-mode.md) | Portrait layout, Blazor vs React implementation status |
| [as-built/assets.md](as-built/assets.md) | Font sources, theme system, glyph constants |
| [as-built/game-play.md](as-built/game-play.md) | How humans play the hybrid game |

### Data & Persistence

| Document | Purpose |
|----------|---------|
| [as-built/database.md](as-built/database.md) | EF Core schema, all 6 entity tables |
| [as-built/save-load.md](as-built/save-load.md) | Game persistence pipeline |
| [as-built/recording-and-stats.md](as-built/recording-and-stats.md) | Recording infrastructure and lifetime statistics |

### Development & Operations

| Document | Purpose |
|----------|---------|
| [as-built/testing.md](as-built/testing.md) | Replay tests, unit tests, GameHash verification |
| [as-built/cli-tooling.md](as-built/cli-tooling.md) | catan.ps1 verbs, Catan3.CLI project, flags |
| [as-built/troubleshooting.md](as-built/troubleshooting.md) | SSL errors, port conflicts, database locks |
| [as-built/azure-deployment.md](as-built/azure-deployment.md) | Azure resources, CI/CD, deployment scripts |

### Status & Reference

| Document | Purpose |
|----------|---------|
| [as-built/known-issues.md](as-built/known-issues.md) | Bugs, TODOs, gaps, session history |
| [as-built/audit-summary.md](as-built/audit-summary.md) | Full system audit with accuracy findings |
| [as-built/react-porting-status.md](as-built/react-porting-status.md) | All 21 React design docs with implementation status |
| [as-built/proposals.md](as-built/proposals.md) | 7 unimplemented proposals with status |
| [as-built/desktop-reference.md](as-built/desktop-reference.md) | WinUI 3 desktop app (reference, DO NOT MODIFY) |
| [as-built/blazor-legacy.md](as-built/blazor-legacy.md) | Blazor WebUI reference, unported features |

## React Porting (Active Design Docs)

Primary design documents for the React migration. See
[as-built/react-porting-status.md](as-built/react-porting-status.md)
for verified implementation status of each.

### Architecture

| Document | Status |
|----------|--------|
| [ui/react/typescript-porting-design.md](ui/react/typescript-porting-design.md) | Partially implemented |
| [ui/react/ts-port-impl-plan.md](ui/react/ts-port-impl-plan.md) | Phases 0-1 complete |
| [ui/react/responsive-design.md](ui/react/responsive-design.md) | Valid standards |

### Game Page

| Document | Status |
|----------|--------|
| [ui/react/game-page-design.md](ui/react/game-page-design.md) | Partially implemented |
| [ui/react/react-game-page.md](ui/react/react-game-page.md) | Substantially implemented |
| [ui/react/game-state-ui.md](ui/react/game-state-ui.md) | Critical reference (~60% implemented) |

### Components

| Document | Status |
|----------|--------|
| [ui/react/hex-grid-component-design.md](ui/react/hex-grid-component-design.md) | Implemented |
| [ui/react/hex-grid-component.md](ui/react/hex-grid-component.md) | Implemented |
| [ui/react/floating-panel-design.md](ui/react/floating-panel-design.md) | Implemented |
| [ui/react/winner-overlay-design.md](ui/react/winner-overlay-design.md) | Implemented |
| [ui/react/home-page-hex.md](ui/react/home-page-hex.md) | Implemented |

### Refactoring

| Document | Status |
|----------|--------|
| [ui/react/react-refactoring-plan.md](ui/react/react-refactoring-plan.md) | Part 1 complete |
| [ui/react/react-refactoring-audit.md](ui/react/react-refactoring-audit.md) | Audit complete |

### Gemini Reviews

| Document | Key Insight |
|----------|-------------|
| [ui/react/arch-review-gemini.md](ui/react/arch-review-gemini.md) | Reference stability risk with SignalR |
| [ui/react/game-page-gemini-review.md](ui/react/game-page-gemini-review.md) | Road geometry validation |
| [ui/react/gemini-review.md](ui/react/gemini-review.md) | Config duplication issue |
| [ui/react/react-game-page-gemini.md](ui/react/react-game-page-gemini.md) | Component divergence from spec |
| [ui/react/react-refactoring-gemini-feedback.md](ui/react/react-refactoring-gemini-feedback.md) | Phase parallelization confirmed |
| [ui/react/type-script-gemini.md](ui/react/type-script-gemini.md) | SVG rationale, visual testing strategy |

## Legacy Documents

The following sections contain original design documents that have
been superseded by the as-built docs above. They are retained for
historical reference.

### Projects

| Document | Purpose |
|----------|---------|
| [projects/cli.md](projects/cli.md) | CLI tool (see [as-built/cli-tooling.md](as-built/cli-tooling.md)) |
| [projects/desktop-app.md](projects/desktop-app.md) | WinUI 3 desktop app (see [as-built/desktop-reference.md](as-built/desktop-reference.md)) |
| [projects/game-service.md](projects/game-service.md) | Backend service (see [as-built/game-service-api.md](as-built/game-service-api.md)) |
| [projects/shared.md](projects/shared.md) | Shared library (see [as-built/message-flow.md](as-built/message-flow.md)) |
| [projects/webui.md](projects/webui.md) | Blazor frontend (see [as-built/blazor-legacy.md](as-built/blazor-legacy.md)) |

### Systems (Superseded)

| Document | As-Built Replacement |
|----------|---------------------|
| [systems/game-service-api.md](systems/game-service-api.md) | [as-built/game-service-api.md](as-built/game-service-api.md) |
| [systems/board-rendering.md](systems/board-rendering.md) | [as-built/board-rendering.md](as-built/board-rendering.md) |
| [systems/coordinates.md](systems/coordinates.md) | [as-built/coordinates.md](as-built/coordinates.md) |
| [systems/database.md](systems/database.md) | [as-built/database.md](as-built/database.md) |
| [systems/save-load.md](systems/save-load.md) | [as-built/save-load.md](as-built/save-load.md) |
| [systems/settings.md](systems/settings.md) | [as-built/settings.md](as-built/settings.md) |
| [systems/mvvm-messaging.md](systems/mvvm-messaging.md) | [as-built/message-flow.md](as-built/message-flow.md) |
| [systems/model-jsonignore-to-dto.md](systems/model-jsonignore-to-dto.md) | [as-built/serialization.md](as-built/serialization.md) |
| [systems/database-schema.md](systems/database-schema.md) | [as-built/proposals.md](as-built/proposals.md) (rejected) |
| [systems/pane-visibility-system.md](systems/pane-visibility-system.md) | [as-built/proposals.md](as-built/proposals.md) (proposed) |
| [systems/versioning.md](systems/versioning.md) | [as-built/proposals.md](as-built/proposals.md) (proposed) |

### UI (Blazor-Specific)

See [as-built/blazor-legacy.md](as-built/blazor-legacy.md) for
consolidated Blazor reference.

| Document | Purpose |
|----------|---------|
| [ui/assets.md](ui/assets.md) | Icon fonts, images (see [as-built/assets.md](as-built/assets.md)) |
| [ui/board-measurement.md](ui/board-measurement.md) | Blazor board sizing |
| [ui/game-play-design.md](ui/game-play-design.md) | State machine UI transitions |
| [ui/number-token.md](ui/number-token.md) | Number token rendering |
| [ui/player-viewmodel.md](ui/player-viewmodel.md) | Blazor player data |
| [ui/uiscale-design.md](ui/uiscale-design.md) | Blazor responsive scaling |
| [ui/winning.md](ui/winning.md) | Victory conditions |

### Other

| Document | As-Built Reference |
|----------|-------------------|
| [game-play.md](game-play.md) | [as-built/game-play.md](as-built/game-play.md) |
| [balance-design.md](balance-design.md) | [as-built/balance-algorithm.md](as-built/balance-algorithm.md) |
| [grief-dodgy.md](grief-dodgy.md) | [as-built/grief-dodgy.md](as-built/grief-dodgy.md) |
| [grief-dodgy-design.md](grief-dodgy-design.md) | [as-built/grief-dodgy.md](as-built/grief-dodgy.md) |
| [devcard-tracking.md](devcard-tracking.md) | [as-built/devcard-tracking.md](as-built/devcard-tracking.md) |
| [portrait-mode.md](portrait-mode.md) | [as-built/portrait-mode.md](as-built/portrait-mode.md) |
| [azure.md](azure.md) | [as-built/azure-deployment.md](as-built/azure-deployment.md) |
| [deployment.md](deployment.md) | [as-built/azure-deployment.md](as-built/azure-deployment.md) |
| [azure-cosmos-dal.md](azure-cosmos-dal.md) | [as-built/proposals.md](as-built/proposals.md) |
| [azure-sql-serverless-alternative.md](azure-sql-serverless-alternative.md) | [as-built/proposals.md](as-built/proposals.md) |
| [css.md](css.md) | [as-built/css-theming.md](as-built/css-theming.md) |
| [test-plan.md](test-plan.md) | [as-built/testing.md](as-built/testing.md) |
| [recording-management.md](recording-management.md) | [as-built/recording-and-stats.md](as-built/recording-and-stats.md) |
| [stats-management.md](stats-management.md) | [as-built/proposals.md](as-built/proposals.md) |
| [reduce-redundancy.md](reduce-redundancy.md) | [as-built/proposals.md](as-built/proposals.md) |
| [summary.md](summary.md) | Superseded by as-built README |
| [TODO-mobile-ui.md](TODO-mobile-ui.md) | [as-built/portrait-mode.md](as-built/portrait-mode.md) |

## Parallel Audits

| Directory | Purpose |
|-----------|---------|
| [gemini/](gemini/) | Gemini's parallel audit (18 docs) for comparison |

## Maintenance

- **As-built docs** are the source of truth for current behavior
- **All other docs** are retained for historical reference but may
  be outdated -- check the as-built replacement column
- Update as-built docs when implementation changes
- Keep lines under 120 characters
