# React Architecture As-Built

**Status:** As-Built
**Active Directory:** `react-ui/`

## 1. Technology Stack

| Component | Technology | Version | Purpose |
|---|---|---|---|
| **Framework** | Next.js | 16.1.2 | App Router, Server Actions |
| **UI Library** | React | 19.2.3 | Component model |
| **Styling** | Tailwind CSS | 4.0 | Utility-first styling |
| **State** | Zustand | 5.0.10 | Client-side state management |
| **Real-time** | @microsoft/signalr | 10.0.0 | Game state updates |
| **Icons** | FontAwesome | 6.x | UI icons (supplementing Catan font) |
| **Testing** | Vitest | 3.x | Unit and integration testing |

## 2. Directory Structure

```text
react-ui/
├── app/                        # Next.js App Router
│   ├── game/[id]/page.tsx      # MAIN GAME ENTRY POINT
│   ├── globals.css             # Tailwind & CSS Variables
│   └── layout.tsx              # Root layout
├── components/
│   ├── game/
│   │   ├── board/             # GameBoard, Hex, Road, Building renderers
│   │   ├── controls/          # ActionCluster, MeasurementCluster, RollRing
│   │   ├── overlays/          # WinnerDialog, SupplementalOverlay, etc.
│   │   └── panels/            # FloatingPanel, PlayersPanel, ResourcesHeader
│   └── hex-grid/              # SVG Hex geometry engine (cubic coords)
├── lib/
│   ├── api/                   # REST API wrappers (gameApi.ts)
│   ├── constants/             # Assets, mappings (board-assets.ts)
│   ├── hooks/                 # Custom Hooks (useGameConnection.ts)
│   ├── services/              # GameServiceProxy.ts (SignalR/REST hybrid)
│   ├── stores/                # Zustand stores (gameStore.ts, layoutStore.ts)
│   └── utils/                 # Helpers (playerColors.ts)
└── types/
    └── generated/             # TypeScript types generated from C# Models
```

## 3. State Management

The app uses **Zustand** stores as the single source of truth, updated via SignalR.

### A. `gameStore`

Holds the authoritative `GameModel`.

- **State**: `gameModel`, `playerProfiles`, `currentPlayerId`.
- **Updates**: `setGameModel` called by `useGameConnection` when `GameStateUpdated` arrives.
- **Selectors**: Fine-grained selectors (`selectTiles`, `selectActionFlags`) prevent unnecessary re-renders.

### B. `layoutStore`

Manages the windowing system for the game UI.

- **State**: Positions/visibility for all panels (`dice`, `actions`, `players`, etc.).
- **Persistence**: Saved to `localStorage` via Zustand middleware.
- **Layouts**: Supports `regular` vs `expansion` default layouts.

### C. `uiStore`

Ephemeral UI state.

- **State**: `isMenuOpen`, `modalStack`, tooltip states.

## 4. Component Hierarchy (Game Page)

`app/game/[id]/page.tsx` renders the following tree:

```text
GamePage
├── MainLayout
│   └── div (Full Screen Container)
│       ├── GameBoard (The "Ocean" canvas)
│       │   ├── SVG Layer (Hexes, Paths)
│       │   └── HTML Layer (Overlays)
│       │
│       ├── FloatingPanels (Z-Indexed above board)
│       │   ├── RollRing (Dice controls)
│       │   ├── ActionCluster (Purchase/Turn buttons)
│       │   ├── MeasurementCluster (Stats)
│       │   ├── PlayersPanel (Scoreboard)
│       │   └── GameResourcesHeader (Resource tracking)
│       │
│       └── Overlays (Modals)
│           ├── GoFirstOverlay (Turn order roll)
│           ├── SupplementalOverlay (5-6p phase)
│           ├── RobberTargetMenu (Stealing)
│           └── WinnerDialog / Celebration
```

## 5. Data Flow

1. **Server Update**: C# backend broadcasts `GameStateUpdated(GameModel)` via SignalR.
2. **Reception**: `GameServiceProxy` receives event, triggers callback.
3. **Hook**: `useGameConnection` hook receives callback, updates `gameStore`.
4. **State Change**: `gameStore.setGameModel(model)` replaces the entire state tree.
5. **Re-render**: Components subscribed via `useGameStore(selector)` re-render.
    - *Example*: `PlayersPanel` subscribes to `gameModel.players` -> updates scores.
    - *Example*: `GameBoard` subscribes to `gameModel.roads` -> renders new road.

## 6. CSS & Theming

- **Tailwind v4**: Uses `@theme` directive in `globals.css` for custom tokens.
- **Variables**: CSS custom properties (`--game-bg-primary`, `--player-blue-primary`) define the look.
- **Orientation**: `@custom-variant portrait` and `landscape` enable responsive layout logic directly in class names.

```
