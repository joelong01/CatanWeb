# System Audit Summary

**Date:** January 30, 2026
**Scope:** Full codebase review, all design documentation, architecture verification

## Executive Summary

The CatanWeb project is in **late-stage migration** from a Blazor/Desktop
hybrid model to React + ASP.NET Core. The core game logic
(`Catan3.Shared/GameStateMachine`) is robust, deterministic, and shared
across all clients. The React UI (`react-ui/`) implements the main game
loop and most UI features using a modern stack (Next.js 16, React 19,
Zustand 5, Tailwind v4).

This audit produced **30 verified as-built documents** to serve as the
definitive reference for the project. Every design document in
`.design/` (63 files across `systems/`, `ui/`, `projects/`, and
`ui/react/`) was read, verified against code, and either absorbed into
an as-built doc or documented as outdated/proposed.

## Key Findings

### Architecture Strengths

- **Single source of truth:** `GameStateMachine` in `Catan3.Shared`
  drives all game logic deterministically. Replay testing validates
  correctness via `GameHash` verification.
- **REST commands + SignalR updates:** The React client intentionally
  uses REST for commands (reliable delivery, no ordering issues on
  mobile) and SignalR only for receiving `GameModel` broadcasts.
- **Coordinate consistency:** Cube coordinates (`Q + R + S = 0`) are
  implemented identically in C# and TypeScript, both flat-top.
- **Board balance algorithm:** Two-phase approach (random search +
  convergence) with star parity and clump prevention produces fair
  boards consistently.
- **Persistence safety:** Auto-save after every command, metadata/blob
  split, completed game archiving.
- **Azure deployment:** Production-ready CI/CD with OIDC authentication,
  Azure SQL Serverless, and intelligent change detection.

### Architecture Concerns

- **No authentication:** All endpoints trust caller-supplied `playerId`.
- **Partial React port:** ~60% of GameState UI requirements implemented.
  Key gaps: allocation phases, TooManyCards, GriefDodgy animations.
- **GriefDodgy default bug:** `HouseRules.GriefDodgy` defaults to `true`
  in C# model; should default to `false`.
- **Legacy code weight:** Blazor and Desktop projects still in solution.
- **Duplicate font files:** Catan.ttf exists in two React locations.
- **Glyph duplication:** `Building.tsx` duplicates constants from
  `catanGlyphs.ts`.

### Documentation State

**Before audit:** 63 design docs scattered across 5 directories. Mix of
current, outdated, aspirational, and Blazor-specific content.

**After audit:** 30 verified as-built documents covering all major
systems, features, infrastructure, and proposals. No content lost from
the original 63 files.

## Accuracy Issues Found

### In Original Design Docs

| Document | Issue |
|----------|-------|
| `systems/game-service-api.md` | Labels `/api/game/action` as "legacy desktop path" -- it is the primary React endpoint |
| `systems/board-rendering.md` | Describes Blazor SVG; React uses DOM-based HexGrid |
| `systems/database.md` | Lists 4 tables; actual schema has 6 |
| `systems/settings.md` | Missing `GriefDodgy` house rule |
| `systems/database-schema.md` | Proposed single-table design was rejected |
| `systems/pane-visibility-system.md` | Proposed, never implemented |
| `systems/versioning.md` | Proposed, never implemented |
| `summary.md` | Says "Next.js 15" -- actual is Next.js 16 |
| `portrait-mode.md` | Blazor-specific; React has infrastructure only |
| `devcard-tracking.md` | Says "30 files to modify" -- actual changes are fewer |

### In Gemini Audit (`.design/gemini/`)

| Document | Issue |
|----------|-------|
| `systems/coordinates-and-rendering.md` | Says "pointy-topped hexes" -- actually flat-top |
| `systems/coordinates-and-rendering.md` | Uses pointy-top formula |
| `systems/css-theming.md` | Says "CSS Modules (React)" -- uses Tailwind v4 |
| `react-architecture.md` | Says "Vitest 3.x" -- actual is 4.0.17 |
| `message-flow.md` | Uses `DECLARE_WINNER` -- actual is `DeclareWinnerMessage` |
| `systems/database-schema.md` | Lists 4 entities -- actual is 6 |
| `systems/game-service-internals.md` | Says games loaded per-request -- they stay in memory |

## As-Built Documents Produced (30)

### Architecture & Communication (4)

| Document | Content |
|----------|---------|
| `message-flow.md` | State machine, message types, mermaid diagrams, adding new messages, GameStateMachineRegistry |
| `game-service-api.md` | All 3 controllers, SignalR hub, message routing, startup pipeline, auth gap |
| `react-architecture.md` | Dependencies, directory structure, stores, components |
| `serialization.md` | TypeGenRunner pipeline, JsonIgnore removal, enum conversion |

### Game Systems (8)

| Document | Content |
|----------|---------|
| `game-rules-summary.md` | Hybrid play model, all game phases |
| `game-state-ui.md` | All 33 GameStates mapped to UI requirements |
| `board-rendering.md` | React HexGrid, GameBoard, tile/building/road rendering |
| `coordinates.md` | Cube coordinates, hex math, positioning |
| `settings.md` | All 8 house rules with defaults |
| `balance-algorithm.md` | Two-phase shuffle algorithm with star parity |
| `grief-dodgy.md` | Feature design, implementation status, known bugs |
| `devcard-tracking.md` | Dev card entitlement, VP entry, score formula |

### UI & Layout (5)

| Document | Content |
|----------|---------|
| `floating-panel.md` | FloatingPanel + MinimizedBar architecture |
| `css-theming.md` | Tailwind v4 tokens, player colors, @utility directives |
| `portrait-mode.md` | Portrait layout, Blazor vs React status |
| `assets.md` | Font sources, theme system, glyph constants, approval policy |
| `game-play.md` | How humans play the hybrid game |

### Data & Persistence (3)

| Document | Content |
|----------|---------|
| `database.md` | All 6 EF Core entity tables |
| `save-load.md` | Persistence pipeline, auto-save, file format |
| `recording-and-stats.md` | Recording infrastructure, replay, lifetime stats |

### Development & Operations (4)

| Document | Content |
|----------|---------|
| `testing.md` | Replay tests, unit tests, GameHash verification |
| `cli-tooling.md` | catan.ps1 verbs, Catan3.CLI project, TypeGen integration |
| `troubleshooting.md` | SSL, ports, database locks, SignalR |
| `azure-deployment.md` | Azure resources, CI/CD, deployment scripts |

### Status & Reference (6)

| Document | Content |
|----------|---------|
| `known-issues.md` | Bugs, outdated docs, gaps, session history |
| `react-porting-status.md` | All 21 React design docs with status |
| `audit-summary.md` | This document |
| `proposals.md` | 7 unimplemented proposals with status |
| `desktop-reference.md` | WinUI 3 reference (DO NOT MODIFY) |
| `blazor-legacy.md` | Blazor WebUI reference, unported features |

## File Coverage

All 63 legacy files (now in `old/`) are covered:

| Category | Files | Disposition |
|----------|------:|-------------|
| Superseded by as-built | 10 | Content replaced with verified version |
| React design docs | 21 | Audited in `react-porting-status.md` |
| Blazor UI docs | 7 | Consolidated into `blazor-legacy.md` |
| Unique content absorbed | 13 | New as-built docs created |
| Proposals documented | 5 | Consolidated into `proposals.md` |
| Low-value/redundant | 4 | Content merged into existing docs |
| Meta files (README, TOC) | 3 | Replaced with new versions |
| **Total** | **63** | **100% coverage** |

## Recommendations

### Immediate

1. **Use design docs as primary reference.** Update CLAUDE.md and
   `.ai/ai-rules.md` to point to `.design/` first.
2. **Fix GriefDodgy default.** Change `HouseRules.GriefDodgy` from
   `true` to `false` in `HouseRules.cs`.

### Near-Term

1. **Complete WinnerOverlay integration.** Component is built and tested
   on `controls-test` page. Wire into main game page.
2. **Complete resource allocation UI.** `AllocateResourceForward` and
   `AllocateResourceReverse` need settlement/road placement.
3. **Wire portrait mode.** React has store infrastructure; needs
   orientation detection hook and tab UI.

### Longer-Term

1. **Archive Blazor projects.** Formally deprecate `WebUI` and
   `WebUI.Server`.
2. **Add authentication.** Required before any public deployment.
3. **Implement API versioning.** See `proposals.md` for the design.
