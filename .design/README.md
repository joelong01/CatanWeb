# Catan Design Documentation

**Last updated:** February 9, 2026

Verified documentation reflecting the current implementation. These
33 documents are the **source of truth** for how the system works.

Legacy and superseded documents are archived in [old/](old/).

## Architecture & Communication

| Document | Purpose |
|----------|---------|
| [message-flow.md](message-flow.md) | State machine, message types, REST endpoints, SignalR events, adding new messages |
| [game-service-api.md](game-service-api.md) | Complete REST API: Game, Recording, Stats controllers + SignalR hub + startup |
| [react-architecture.md](react-architecture.md) | Dependencies, directory structure, Zustand stores, component hierarchy, data flow |
| [serialization.md](serialization.md) | TypeGenRunner pipeline, JsonIgnore removal, enum-to-union conversion |
| [typescript-model-transforms.md](typescript-model-transforms.md) | Full TypeGen setup: file locations, pipeline steps, covered types, adding new types |

## Game Systems

| Document | Purpose |
|----------|---------|
| [game-rules-summary.md](game-rules-summary.md) | How Catan is played mapped to GameState values, hybrid play model |
| [game-state-ui.md](game-state-ui.md) | All 33 GameStates mapped to UI requirements, ActionFlags, entitlements |
| [board-rendering.md](board-rendering.md) | React HexGrid component system, GameBoard, tile/building/road rendering |
| [coordinates.md](coordinates.md) | Cube coordinate system, hex math, building/road positioning |
| [settings.md](settings.md) | House rules configuration with all properties and defaults |
| [balance-algorithm.md](balance-algorithm.md) | Board balance algorithm: two-phase shuffle, star parity, clump prevention |
| [grief-dodgy.md](grief-dodgy.md) | GriefDodgy house rule: tile flip, fake-out, celebration animations |
| [devcard-tracking.md](devcard-tracking.md) | Development card tracking, VP entry at game end, score formula |

## UI & Layout

| Document | Purpose |
|----------|---------|
| [app-shell.md](app-shell.md) | Three-zone app shell: nav column, header bar, content area with independent zoom |
| [floating-panel.md](floating-panel.md) | FloatingPanel + MinimizedBar architecture, WindowPosition, layoutStore |
| [css-theming.md](css-theming.md) | Tailwind v4 design tokens, player colors, @utility directives, typography |
| [portrait-mode.md](portrait-mode.md) | Portrait layout: tabbed interface, Blazor vs React status |
| [assets.md](assets.md) | Font sources (Catan.ttf, Font Awesome), theme system, glyph constants |
| [game-play.md](game-play.md) | How humans play the hybrid game, trust model, turn flow |

## Data & Persistence

| Document | Purpose |
|----------|---------|
| [database.md](database.md) | Entity Framework Core schema, all 6 entity tables, persistence service |
| [save-load.md](save-load.md) | Game persistence pipeline, file format, auto-save, load sources |
| [recording-and-stats.md](recording-and-stats.md) | Recording infrastructure, replay verification, lifetime player statistics |

## Development & Operations

| Document | Purpose |
|----------|---------|
| [testing.md](testing.md) | Replay tests, unit tests, test projects, GameHash verification |
| [ts-test-strategy.md](ts-test-strategy.md) | React test strategy: truth sets, generated types only, no parallel type system |
| [cli-tooling.md](cli-tooling.md) | catan.ps1 verbs, Catan3.CLI project, flags, subsystems |
| [troubleshooting.md](troubleshooting.md) | SSL errors, port conflicts, database locks, SignalR issues |
| [azure-deployment.md](azure-deployment.md) | Azure resources, CI/CD pipeline, deployment scripts |

## Status & Reference

| Document | Purpose |
|----------|---------|
| [known-issues.md](known-issues.md) | Bugs, TODOs, gaps, components being replaced, session history |
| [audit-summary.md](audit-summary.md) | Full system audit findings, accuracy issues, recommendations |
| [react-porting-status.md](react-porting-status.md) | All 21 React design docs with implementation status and coverage |
| [proposals.md](proposals.md) | 7 unimplemented proposals: versioning, pane visibility, CosmosDB, etc. |
| [desktop-reference.md](desktop-reference.md) | WinUI 3 desktop app reference (DO NOT MODIFY) |
| [blazor-legacy.md](blazor-legacy.md) | Blazor WebUI reference, features not yet ported to React |

## Archived Documents

Legacy and superseded design documents are in [old/](old/). These are
retained for historical reference but may be outdated. The documents
above are the verified source of truth.

| Directory | Contents |
|-----------|----------|
| [old/projects/](old/projects/) | Original project-level docs (5 files) |
| [old/systems/](old/systems/) | Original system docs (11 files) |
| [old/ui/](old/ui/) | Blazor UI docs (7 files) |
| [old/ui/react/](old/ui/react/) | React design docs (21 files) |
| [old/gemini/](old/gemini/) | Gemini parallel audit (18 files) |
| [old/reviews/](old/reviews/) | External reviews |

## Maintenance

- **Code is truth.** When a doc disagrees with code, the code wins.
  Update the doc.
- **Update after changes.** When you modify code behavior, update the
  relevant doc in this directory.
- **Mark uncertainty.** Use `<!-- TODO: verify -->` for anything you
  haven't confirmed against the source.
