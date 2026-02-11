# React Porting Status

**Last verified:** January 30, 2026

## Implementation Phases

From `old/ui/react/ts-port-impl-plan.md`:

| Phase | Description | Status |
|-------|-------------|--------|
| 0 | Foundation (stores, proxy, types) | Complete |
| 1 | Home + Navigation | Complete |
| 2 | New Game Page | In Progress |
| 3 | Game Page (board, controls) | Substantially Complete |
| 4 | Load Game Page | Complete |
| 5 | Edit Players Page | Not Started |
| 6 | Settings Page | Not Started |
| 7 | Stats Page | Not Started |
| 8 | Portrait/Mobile Layout | Partial |
| 9 | Testing | Partial (replay tests exist) |
| 10 | Polish | Not Started |

## React Design Documents Audit

### Core Architecture

| Document | Status | Notes |
|----------|--------|-------|
| `typescript-porting-design.md` | **Partially Implemented** | Migration spec. Phase 0-1 complete, later phases in progress. Some proposed patterns (NSwag type generation, Storybook) not yet active. Actual dependencies differ from spec (Next 16 not 15, Tailwind v4 not v3, React 19, SignalR 10 not 8). |
| `ts-port-impl-plan.md` | **Phase 0-1 Complete** | Multi-phase plan. Phase 0 (foundation) and Phase 1 (home/nav) done. Phases 2-10 at various stages. |
| `responsive-design.md` | **Valid Standards** | Guidelines for form pages. Correctly notes Tailwind v4 custom variant support for portrait/landscape. Game page uses different architecture (floating panels, not responsive grid). |

### Game Page

| Document | Status | Notes |
|----------|--------|-------|
| `game-page-design.md` | **Partially Implemented** | Ambitious "infinite ocean" design. Basic game page works but full infinite hex grid with dynamic water tile rendering not realized. Z-index layering and floating panels implemented. |
| `react-game-page.md` | **Substantially Implemented** | Detailed game page architecture. Board components, control clusters, floating panels all implemented. Some reference implementations (controls-test page) are more complete than production components. |
| `game-state-ui.md` | **Critical Reference** | Maps all 33 GameStates to UI requirements. ~60% implemented in React. Key gaps: allocation phases, TooManyCards, expansion states. Contains detailed rendering specs (dimensions, colors, indexes) not documented elsewhere. |

### Components

| Document | Status | Notes |
|----------|--------|-------|
| `hex-grid-component-design.md` | **Implemented** | Original design using axial coordinates. Led to hex-grid-component.md refinement. |
| `hex-grid-component.md` | **Implemented** | Refined design using cubic coordinates. Two-pass rendering, declarative API, predefined layouts (CLUSTER_7, CLUSTER_19, CLUSTER_30) all implemented. 37 unit tests passing. |
| `hex-grid-TODO.md` | **All Critical Items Done** | All high-priority items checked off. Only low-priority items (keyboard nav, ARIA) remain. |
| `floating-panel-design.md` | **Implemented** | WindowPosition model, FloatingPanel + MinimizedBar separation, layoutStore persistence all working. |
| `winner-overlay-design.md` | **Implemented** | Three-phase WinnerOverlay built and working on controls-test page. Integration into main game page pending (replaces WinnerDialog + WinnerCelebration + VictoryPointsOverlay). |
| `home-page-hex.md` | **Implemented** | Hex-based home page with CLUSTER_7 layout, CenterHex, MenuHex, WaterHex components. |

### Refactoring

| Document | Status | Notes |
|----------|--------|-------|
| `react-refactoring-plan.md` | **Part 1 Complete** | Extension library (playerExtensions, buildingExtensions, etc.) with 413 passing tests. Parts 2-3 (server-driven UI, props standardization) planned but not started. |
| `react-refactoring-audit.md` | **Audit Complete** | Identified 9 refactoring issues (color prop drilling, duplicate calculations, entitlement checks). Work units defined for Phase 3. |

### Gemini Reviews (Secondary Sources)

| Document | Key Insight |
|----------|------------|
| `arch-review-gemini.md` | Reference stability risk with full GameModel deserialization from SignalR |
| `game-page-gemini-review.md` | Road "bow-tie" geometry validated; missing FakeOut animation state |
| `gemini-review.md` | Config duplication between `lib/config.ts` and `lib/services/config.ts` |
| `home-page-hex-design-gemini.md` | Two-layer hex border rendering technique (scaled content approach) |
| `react-game-page-gemini.md` | MeasurementCluster uses rectangular grid instead of specified nested hex clusters |
| `react-refactoring-gemini-feedback.md` | Confirms Phase 1-3 parallelizable with no backend dependencies |
| `type-script-gemini.md` | Original port spec; SVG vs HTML decision rationale; replay visual testing strategy |

## GameState Implementation Coverage

From `game-state-ui.md` audit against actual game page:

| GameState | React UI | Notes |
|-----------|----------|-------|
| `Uninitialized` | N/A | Technical state |
| `WaitingForNewGame` | N/A | Handled by new-game page |
| `PickingBoard` | Implemented | Shuffle, Balance, Swap controls |
| `WaitingForRollForOrder` | Implemented | Roll ring active |
| `FinishedRollOrder` | Implemented | GoFirst overlay |
| `BeginResourceAllocation` | Partial | Transition state |
| `AllocateResourceForward` | Partial | Settlement/road placement needs work |
| `AllocateResourceReverse` | Partial | Settlement/road placement needs work |
| `DoneResourceAllocation` | Implemented | Transition to main loop |
| `WaitingForRoll` | Implemented | Roll ring, tile dimming |
| `WaitingForNext` | Implemented | Full purchase, build, next controls |
| `MustMoveRobber` | Implemented | Tile click + target player menu |
| `PickSupplementalPlayers` | Implemented | SupplementalOverlay |
| `Supplemental` | Implemented | Limited build controls |
| `TooManyCards` | Not Implemented | Discard UI needed |
| `GameOver` | Implemented | WinnerOverlay (on controls-test) |
| Expansion states | Not Implemented | C&K states defined but no React UI |

## Key Technical Patterns

### Two-Hex Border Pattern

Used throughout the HexGrid system: outer hex provides border color,
inner hex (scaled ~0.91) provides content area. This avoids CSS
`clip-path` incompatibility with borders.

### Reconciliation Layer

`GameServiceProxy` receives full `GameModel` from SignalR.
`reconcileGameModel()` preserves array item references via structural
sharing to prevent unnecessary React re-renders when items haven't
changed.

### Selector-Based Rendering

All game components use fine-grained Zustand selectors
(`useGameTiles()`, `useGameBuildings()`, etc.) rather than subscribing
to the full store. Shallow equality comparison prevents re-renders when
unrelated state changes.

### Extension Library

C# extension methods ported to TypeScript as pure functions in
`react-ui/lib/utils/`:

| File | Coverage |
|------|----------|
| `playerExtensions.ts` | Player lookups, name formatting |
| `buildingExtensions.ts` | Building adjacency, aliases, vertex math |
| `roadExtensions.ts` | Road adjacency, edge positioning |
| `tileExtensions.ts` | Tile operations, star calculations |
| `gameModelExtensions.ts` | Composite game model queries |

All extensions tested (413 tests passing as of Jan 28).
