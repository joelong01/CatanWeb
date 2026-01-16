# Design: Port CatanWeb to Next.js + TypeScript

**Author:** Gemini 3 Pro (Preview)
**Date:** 2026-01-16
**Status:** DRAFT

## 1. Executive Summary

This design outlines the plan to port the existing Blazor WebAssembly `Catan3.WebUI` application to a modern **React 19 + Next.js 15** application using **TypeScript**. The primary goals are to achieve 100% feature parity, strictly preserve the `Catan3.GameService` and `Catan3.Shared` backend, and simplify the frontend architecture by leveraging the React ecosystem.

The core architecture changes involve:

1. **Frontend**: Blazor/Razor -> Next.js (App Router).
2. **Communication**: Mixed SignalR/REST -> Strict "REST for Actions, SignalR for State" pattern.
3. **Rendering**: C# String-built SVG -> Declarative React SVG Components.
4. **Models**: Automated parity via TypeScript definition generation.

## 2. Architecture & Tech Stack

### 2.1 Core Framework

- **Framework**: **Next.js 15**. Chosen for its robust App Router, built-in optimizations, and strong TypeScript support.
- **Library**: **React 19**. Utilizing Server Components (RSC) where applicable (e.g., lobby/marketing pages) and Client Components for the interactive Game Board.
- **Language**: **TypeScript 5**. Strict mode enabled for maximum safety.

### 2.2 Styling & UI Library

- **Styling**: **Tailwind CSS**. Replaces custom CSS files with utility classes for maintainability and smaller bundle sizes.
- **Components**: **shadcn/ui**. Provides high-quality, accessible, copy-pasteable components (Dialogs, Forms, Dropdowns) built on Radix UI and Tailwind.
- **Icons**: **Lucide React** or **FontAwesome** (via `react-icons`) to replace custom font glyphs where possible, though custom SVG paths (game assets) will be preserved.

### 2.3 State Management (Zustand)

We will adopt a **Zustand-first** approach for high-performance state management, replacing React Context for the high-frequency game loop.

- **Library**: `zustand` + `zustand/middleware` (subscribeWithSelector).
- **Pattern**: The entire `GameModel` is stored in a single store. Components subscribe via **selectors** to only re-render when their specific slice changes.

**Game Store Architecture:**

```typescript
// stores/gameStore.ts
import { create } from 'zustand';
import { subscribeWithSelector } from 'zustand/middleware';
import type { GameModel } from '@/types/generated/api';

interface GameState {
  gameModel: GameModel | null;
  setGame: (model: GameModel) => void;
  // ... optimistic actions ...
}

export const useGameStore = create<GameState>()(
  subscribeWithSelector((set) => ({
    gameModel: null,
    setGame: (model) => set({ gameModel: model }),
  }))
);

// PERFORMANCE CRITICAL: Selectors prevent full-app re-renders on every SignalR pulse
export const useCurrentPlayerId = () => useGameStore((s) => s.gameModel?.currentPlayerId);
export const useTiles = () => useGameStore((s) => s.gameModel?.tiles ?? []);
export const useRoads = () => useGameStore((s) => s.gameModel?.roads ?? []);
```

## 3. Integration & Type Safety

The client must maintain 100% parity with `Catan3.Shared`.

### 3.1 Type Generation (NSwag)

We will use **NSwag** to generate accurate TypeScript interfaces and API clients.

> **Correction to Alternate Design:** The proposed `npm install -g nswag` is actively discouraged for team environments. We will use a **local devDependency** to ensure strict versioning across all developer machines and CI agents.
>
> **Run Mode:** We will generate types from the **Build Artifact (DLL)** or the **OpenAPI Spec Endpoint**, avoiding the fragility of requiring the backend to be running in debug mode during CI builds.

- **Tool**: `nswag` (v14+ for .NET 9 support).
- **Runtime**: `Net90` (Matching `global.json`).
- **Command**: `npx nswag run nswag.json`.

### 3.2 Communication Pattern

We will separate **Write** (Actions) and **Read** (State Updates).

**A. Write Actions (Client -> Server)**
All user actions (Purchase, Build, End Turn) will use **REST API** calls.

- **Method**: `POST /api/game/{gameId}/{action}`
- **Body**: JSON-serialized command messages (e.g., `RoadPurchaseMessage`).
- **Response**: `CommandResult` (Success/Failure).

**B. Read State (Server -> Client)**
We will use **SignalR** exclusively for pushing state updates to the client.

- **Connection**: `@microsoft/signalr` package.
- **Events**: `OnUpdateGame(gameModel: GameModel)`, `OnGameJoined`.

### 3.3 Handling C# Extension Methods

The backend relies heavily on C# extension methods (`Catan3.Shared/Extensions/`). We will strictly categorize these for porting:

1. **Geometry & Rendering Logic (MUST PORT)**:
    - **`BuildingModelExtensions.Aliases`**: Critical for identifying that a vertex is shared by 3 hexes. Required for correct click detection.
    - **`TileModelExtensions.AdjacentTiles`**: Required for "longest road" or client-side highlighting.
    - **Implementation**: These will be ported as **pure functions** in `lib/utils/modelUtils.ts` (e.g., `getBuildingAliases(key)`), receiving the model as the first argument, rather than trying to extend the TypeScript prototypes.

2. **Game Rules & Factory Logic (DO NOT PORT)**:
    - **`GameModelExtensions.CreateNew`**: Server-side only.
    - **`GameModelExtensions.CanPurchase*`**: The server is the authority. The client will rely on the `ActionFlags` property properties already present in `GameModel` to enable/disable UI buttons, rather than reimplementing rule verification logic client-side.

## 4. Component Architecture & Rendering

### 4.1 The Board (Rendering Simplification)

The current solution uses `BoardSvgGenerator.cs` to build massive SVG strings. React allows us to break this down into declarative components.

**Structure:**

```tsx
<GameBoard model={gameModel}>
  <BoardDefs /> {/* Patterns, Gradients */}
  <LayerTiles tiles={model.tiles} />
  <LayerHarbors harbors={model.harbors} />
  <LayerRoads roads={model.roads} />
  <LayerBuildings buildings={model.buildings} />
  <LayerRobber position={model.robber} />
  <LayerOverlays /> {/* Click targets for interactive mode */}
</GameBoard>
```

**Benefits:**

- **Performance**: React only re-renders changed components (e.g., adding a road doesn't re-render the hex tiles).
- **Interactivity**: `onClick` handlers attached directly to SVG elements (`<circle>`, `<polygon>`) instead of calculating hit-testing or overlay maps.

### 4.2 SVG Management

- **Game Assets**: Existing assets (cards, icons) will be moved to `public/assets`.
- **Dynamic Assets**: Player-colored elements (cities, settlements) will use SVG `mask` or `currentColor` techniques with Tailwind colors instead of generating unique gradients for every player in C#.

### 4.3 Deep Dive: Game Page Layout & Responsiveness

To address the challenge of making the board fill the parent size robustly across devices, we will use a **CSS Grid** layout with a specialized **SVG Container pattern**.

#### 4.3.1 Detailed Component Map

We will structure the specifics components as follows:

```text
ReactUi/
├── app/
│   ├── layout.tsx       # Root MainLayout
│   └── game/[id]/       # Game Page
├── components/
│   ├── board/
│   │   ├── BoardContainer.tsx  # The SVG Root
│   │   ├── BaseLayer.tsx       # Background/Water
│   │   ├── TilesLayer.tsx      # Hexes (Resources)
│   │   ├── RoadsLayer.tsx      # Edges
│   │   ├── BuildingsLayer.tsx  # Vertices (Towns)
│   │   └── RobberLayer.tsx     # Animated overlay
│   ├── players/
│   │   └── PlayersPanel.tsx
│   └── controls/
│       └── GameControls.tsx
├── lib/
│   └── geometry/
│       └── boardGeometry.ts    # Ported axialToPixel logic
└── stores/
    └── gameStore.ts            # Zustand State
```

#### 4.3.2 ASCII Layout Visualization

##### Landscape (Desktop/Tablet)

```text
┌───────────────────┬───────────────────────────────────────┬───────────────────┐
│     COLUMN 1      │               COLUMN 2                │     COLUMN 3      │
│     (Actions)     │              (The Board)              │     (Players)     │
├───────────────────┼───────────────────────────────────────┼───────────────────┤
│ [Game Name]       │                                       │                   │
│                   │              (Automatic)              │  [Player Tiles]   │
│ [Undo/Redo]       │            Aspect Ratio               │    (Grid 1x4)     │
│                   │             Preserved                 │                   │
│ [Purchase Grid]   │                                       │                   │
│  [Rd][St][Cy]     │         <svg viewBox="...">           │  [Resource Card]  │
│  [Kn][Dv]         │                                       │  [Resource Card]  │
│                   │                                       │                   │
│ [Dice Roll]       │                                       │                   │
└───────────────────┴───────────────────────────────────────┴───────────────────┘
```

##### Portrait (Phone)

```text
┌───────────────────────────────────────┐
│ [Tabs: Board | Controls | Players]    │
├───────────────────────────────────────┤
│                                       │
│          (The Board)                  │
│     Fills Available Viewport          │
│                                       │
├───────────────────────────────────────┤
│ [Active Tab Content]                  │
│ (e.g., Controls overlaid at bottom    │
│  or in a drawer)                      │
└───────────────────────────────────────┘
```

#### 4.3.2 The "FixFit" Strategy

The core issue with SVGs in Flex/Grid parents is that SVGs often default to `300x150` or collapse if not constrained.

**Solution:**

1. **Grid Container**: The middle column is `flex-grow` or `1fr`.
2. **Relative Wrapper**: A `div` with `position: relative; width: 100%; height: 100%`.
3. **Absolute SVG**: The SVG is `absolute` within the wrapper to detach it from flow layout quirks.
4. **ViewBox**: The SVG uses `viewBox` (min-x, min-y, width, height) to define its logical coordinate system, while CSS handles the physical pixels.

**Implementation:**

```tsx
// GameLayout.tsx (Landscape)
<div className="grid h-screen w-full grid-cols-[20rem_1fr_20rem] overflow-hidden bg-slate-100">
  <aside className="border-r bg-white p-4">
    {/* Column 1: Controls */}
  </aside>

  <main className="relative h-full w-full p-4">
      {/* Column 2: Board */}
      {/* The Wrapper */}
      <div className="relative h-full w-full"> 
         <GameBoard model={gameModel} />
      </div>
  </main>

  <aside className="border-l bg-white p-4">
    {/* Column 3: Players */}
  </aside>
</div>

// GameBoard.tsx
export const GameBoard = ({ model }) => {
   // Calculate logical bounds
   const { minX, minY, width, height } = calculateBoardBounds(model);
   
   return (
     <svg 
       viewBox={`${minX} ${minY} ${width} ${height}`}
       preserveAspectRatio="xMidYMid meet" // CRITICAL: Centers and fits w/o distortion
       className="absolute inset-0 h-full w-full touch-none select-none" // Tailwind for 100% fill
     >
        {/* Layers... */}
     </svg>
   );
};
```

#### 4.3.3 The Board Layers (Z-Order)

To ensure interactivity and correct visual stacking:

1. **Background Layer**: Water/Ocean pattern.
2. **Grid/Debug Layer** (Optional): Hex outlines for debugging.
3. **Tiles Layer**: The Hexagons (Resources).
4. **Harbors Layer**: Port icons on the coast.
5. **Roads Layer**: Roads lie *between* tiles.
6. **Buildings Layer**: Cities/Settlements lie *on top* of corners (Vertices).
7. **Robber Layer**: The Bandit token.
8. **Effects Layer**: Dice roll animations, "Number Token" overlays.
9. **Interaction Layer**:
    - Invisible circles at every vertex (for clicking to build settlements).
    - Invisible rectangles at every edge (for clicking to build roads).
    - This layer is only rendered when `InteractionState` is active (e.g., "Place Road").

### 4.4 Component Architecture: Flat Layers (Adopted) vs. Hierarchy

Analysis of the Catan Desktop App (`GameBoardCtrl.xaml`) reveals a **Flat Layered Architecture**. The board is composed of multiple `ItemsControl` elements sharing a common `Canvas` container, ordered by Z-Index.

**Desktop Reference (`GameBoardCtrl.xaml`):**

- `IC_Harbors` (Z: -1)
- `IC_Tiles` (Z: 10)
- `IC_Roads` (Z: 20)
- `IC_Buildings` (Z: 30)

This implies that logical relationships (e.g., "This road is on the edge of this tile") are **not** represented in the Visual Tree. Instead, all elements are siblings positioned via absolute coordinates.

**Decision:**
We will mirror this **Flat Layered Architecture** in React. It offers significant performance benefits over a nested hierarchy (e.g., `<Tile><Road /></Tile>`) because:

1. **Z-Indexing is trivial**: We don't need complex `z-index` management to make a road appear "above" two adjacent tiles if it's just in a higher layer container.
2. **Hit-Testing is cleaner**: Interaction layers can sit on top of everything without being obscured by tile content.
3. **Change Detection**: Adding a road only re-renders the `RoadLayer`, not the `TileLayer`.

**React Component Structure:**

```tsx
<div className="absolute inset-0">
  {/* Layer 1: Tiles */}
  <div style={{ zIndex: 10 }}>
    {tiles.map(t => <Tile key={t.id} data={t} />)}
  </div>
  
  {/* Layer 2: Roads */}
  <div style={{ zIndex: 20 }}>
    {roads.map(r => <Road key={r.id} data={r} />)}
  </div>
  
  {/* ... other layers ... */}
</div>
```

This confirms the question of "Layers vs. Geometries": We manage **Layers** of Components, where each Component renders its specific **Geometry**.

### 5. Architectural Decision: SVG vs. HTML-based Rendering

**Question:** Should we abandon SVG and render the board using standard HTML `<div>` elements and CSS shapes to reduce complexity?

**Decision: NO. We must retain SVG.**

**Reasoning:**
While HTML/CSS is simpler for rectangular layouts, it is significantly **more complex** and fragile for hexagonal grids.

1. **Geometry Complexity**:
    - **SVG**: Drawing a hexagon is one line: `<polygon points="..." />`.
    - **HTML**: Requires complex `clip-path` polygon rules or the "border hack" (overlapping triangles), which is difficult to border-stroke correctly.
2. **Seam Issues**: HTML elements positioned with percentage or pixel math often suffer from sub-pixel rendering gaps ("seams") between tiles. SVG coordinates are mathematically precise vectors.
3. **Coordinate System**:
    - The backend logic (and `HexCoordinates.cs`) outputs Cartesian `(x,y)` coordinates.
    - Mapping these to an SVG `viewBox` is 1:1.
    - Mapping these to HTML `top/left` requires constant re-calculation based on container size or fighting with CSS transforms.
4. **Responsive Scaling**: SVG's `viewBox` + `preserveAspectRatio` handles scaling automatically (the "FixFit" strategy). Achieving the same "zoom to fit" behavior with HTML elements requires manual `transform: scale()` calculations.
5. **Hit Testing**: Detecting a click on a *specific* triangular vertex or road edge is trivial in SVG (the click target is the shape itself). in HTML, rectangular bounding boxes of adjacent elements would overlap, requiring complex `pointer-events` management or ray-casting.

**Conclusion:** The perception that HTML is "simpler" is a false economy for non-rectangular geometry. SVG is the correct tool for this specific domain.

## 6. Development Plan

### Phase 1: Setup & Parity Pipeline

1. Initialize Next.js repo `catan-web-next`.
2. Set up `Reinforced.Typings` in `Catan3.Shared` to output `contracts.d.ts` to the frontend repo.
3. Port `HexCoordinates` and `BoardSvgConstants` math to TypeScript.

### Phase 2: Game Service Integration

1. Implement **GameService** (TypeScript class/hook).
    - Initialize SignalR connection.
    - Expose methods for REST actions (`buildRoad(...)`, `endTurn(...)`).
2. Create `useGameContext` to expose the `GameModel` to components.

### Phase 3: The Board (Rendering)

1. Create standard SVG atomic components: `<Hex />`, `<Road />`, `<Settlement />`.
2. Implement the Layout Engine (converting Hex coordinates to Screen X/Y).
3. Assemble the full `GameBoard` component.

### Phase 4: Interactive Gameplay

1. Implement the "Game Loop" UI:
    - **Controls**: Dice Roll, End Turn buttons (using `shadcn/ui`).
    - **Resources**: Player resource cards/stats.
    - **Purchasing**: Implement the "Buy" panel using the new grid layout.
2. Connect UI actions to the REST API.
3. Wire up SignalR updates to trigger re-renders.

### Phase 5: Polish & Parity Verify

1. Verify exact parity with C# logic (e.g., verifying `GameHash`).
2. Implement Animations using `framer-motion` (dice rolls, card flips).
3. Replicate exact "House Rules" and "Lobby" behavior.

## 6. Recommended NPM Packages

| Package | Purpose |
| :--- | :--- |
| `next` | Core React Framework |
| `@microsoft/signalr` | WebSocket communication |
| `axios` | REST API Client (or built-in `fetch`) |
| `tanstack/react-query` | Server state management & caching |
| `zustand` | Client state management |
| `clsx`, `tailwind-merge` | CSS class manipulation |
| `lucide-react` | Icons |
| `framer-motion` | Complex animations (Dice, Cards) |
| `zod` | Schema validation (optional, for form inputs) |

## 7. Migration Risks & Mitigations

| Risk | Mitigation |
| :--- | :--- |
| **Serialization Mismatch** | Automated Type Generation (`Reinforced.Typings`) eliminates manual error. |
| **Logic Duplication** | Keep client logic strictly to *rendering* and *input*. Do not duplicate rules engine; rely on Server responses. |
| **Asset Fidelity** | Reuse exact SVG paths from current `Catan3.GameService` assets folder. |

## 8. Testing Strategy

We explicitly split testing into **Unit Logic** vs **Visual Regression**, refining the generic approach.

### 8.1 Unit Testing (Vitest)

For `boardGeometry.ts` and other math-heavy logic, we need fast, headless unit tests.

- **Framework**: `Vitest` (faster than Jest, native ESM support).
- **Scope**: Coordinate conversion, Hex math, State selectors.

### 8.2 Visual Regression (Playwright)

"Replay-Driven Visual Testing" remains the gold standard for full board verification.

- **Framework**: **Playwright**.
- **Visual Comparison**: **Pixelmatch**.
- **Methodology**: Replay recorded game logs into the React frontend and assert the pixel output matches the "Golden Master".

## 9. Appendix: Component Map (Reference)

The following structure is adopted as the concrete implementation target:

```text
ReactUi/
├── app/
│   ├── layout.tsx         # [MainLayout.razor]
│   ├── page.tsx           # [Home.razor]
│   └── game/[id]/page.tsx # [Game.razor]
├── components/
│   ├── board/
│   │   ├── BoardContainer.tsx
│   │   ├── BaseLayer.tsx
│   │   ├── TilesLayer.tsx      # Interactive Hexes
│   │   ├── RoadsLayer.tsx
│   │   ├── BuildingsLayer.tsx
│   │   └── RobberLayer.tsx
│   ├── players/
│   │   └── PlayersPanel.tsx
│   └── controls/
│       └── GameControls.tsx
├── lib/
│   └── geometry/
│       └── boardGeometry.ts
```

### 8.3 The Pipeline

1. **Replay Generation**:
    - The C# Test Runner iterates through known Game Replays.
    - For key events (e.g., `LogGameModel`), it dumps the full JSON `GameModel` state to a `test-fixtures/` directory in the frontend repo.
2. **Storybook Fixtures**:
    - We create a generic `<GameSnapshotView model={loadedJson} />` story in Storybook.
    - Storybook automatically generates a story for each JSON fixture found in `test-fixtures/`.
3. **Visual Snapshot**:
    - Playwright visits each Storybook story.
    - Takes a screenshot.
    - Compares against the "Golden Master" screenshot.
    - Fails if the pixel difference > 0.1%.

### 8.4 Benefits

- **Zero-Effort Test Cases**: Every game played by humans can instantly become a regression test suite by saving the replay log.
- **Deterministic**: Since `GameModel` is the single source of truth, injecting the same JSON always yields the same UI (unlike testing manually clicking buttons which can be flaky).
- **Component Isolation**: We can also snapshot individual components (e.g., a specific "Robber on Desert" Hex) using the same data injection strategy.

### 8.5 Implementation Examples

#### C# Backend: Exporting Game States

We extend the existing `ReplayTest` to save states instead of just asserting.

```csharp
[Fact]
public void ExportReplayToFixtures()
{
    var log = GameLog.FromFile("Tests/Data/ExpansionReplayTest.json");
    var stateMachine = new GameStateMachine(log, ...);
    
    // Fast forward to end or specific turns
    var finalState = stateMachine.ReplayAll();
    
    // Serialize to frontend fixtures folder
    var json = JsonSerializer.Serialize(finalState, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText("../catan-web-next/src/test-fixtures/expansion-endgame.json", json);
}
```

#### Storybook: Automating Fixture Loading

We can automatically generate stories for every JSON file in the fixtures folder.

```tsx
// src/stories/GameReplay.stories.tsx
import type { Meta, StoryObj } from '@storybook/react';
import { GameBoard } from '../components/GameBoard';
import { GameModel } from '../types/catan-models';

// Import all JSON fixtures
const fixtures = import.meta.glob('../test-fixtures/*.json', { eager: true });

const meta: Meta<typeof GameBoard> = {
  component: GameBoard,
  title: 'Replays/snapshots',
};

export default meta;

// Generate a story for each fixture
export const Stories = Object.entries(fixtures).map(([path, data]) => {
  const name = path.split('/').pop()?.replace('.json', '') || 'Unknown';
  return {
    name,
    args: {
        gameModel: (data as any).default as GameModel
    }
  };
});
```

#### Playwright: Visual Regression Test

```typescript
// tests/visual-regression.spec.ts
import { test, expect } from '@playwright/test';
import fixtures from '../src/test-fixtures/manifest.json'; // List of generated files

test.describe('Game State Visual Parity', () => {
  for (const fixtureName of fixtures) {
    test(`should match snapshot for ${fixtureName}`, async ({ page }) => {
      // Navigate to the specific Storybook story
      await page.goto(`http://localhost:6006/iframe.html?id=replays-snapshots--${fixtureName}&viewMode=story`);
      
      // Wait for board to be idle/rendered
      await page.waitForSelector('[data-testid="game-board-rendered"]');
      
      // Take screenshot and compare
      await expect(page).toHaveScreenshot(`${fixtureName}.png`, {
          maxDiffPixelRatio: 0.01 
      });
    });
  }
});
```
