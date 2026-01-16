# React + Next.js + TypeScript Migration Design

**Last Updated:** 2026-01-16
**Status:** Design Document
**Target Location:** `ReactUi/` (sibling to Catan3.Shared, Catan3.GameService)
**Reviewed By:** Claude (Opus 4.5), Gemini 3 Pro

## Executive Summary

This document specifies the migration of the Catan Blazor WebAssembly frontend to a React + Next.js +
TypeScript application. The React client uses **REST for commands** and **SignalR for real-time
updates**. Both Blazor and React clients can connect to the same GameService simultaneously.

### Goals

1. **100% Feature Parity** - Every UI feature in the Blazor app must work identically
2. **Compatible Serialization** - TypeScript types must serialize/deserialize identically to C#
3. **Modern Best Practices** - Use React 19, Next.js 15, TypeScript 5, Tailwind CSS
4. **Maintainable** - Auto-generate types from C# to prevent drift
5. **Testable** - Comprehensive unit, visual regression, and E2E test coverage
6. **Multi-Client Support** - Blazor and React clients play together in the same game

### Non-Goals

- Removing SignalR (still used for server→client push)
- Modifying game logic (GameStateMachine)
- Supporting additional platforms beyond web browsers

### Key Architecture Decisions

1. **SVG for Board Rendering** - Not HTML/CSS (see [Section 7.1](#71-svg-vs-html-rationale))
2. **Flat Layer Architecture** - Matching Desktop app's `GameBoardCtrl.xaml` pattern
3. **REST Commands + SignalR Updates** - Commands via REST API, real-time state via SignalR
4. **NSwag for Type Generation** - From OpenAPI spec, not runtime service
5. **Zustand with Selectors** - Optimized re-renders for SignalR state updates

---

## 1. Project Structure

```text
CatanWeb/
├── Catan3.Shared/           # Unchanged - C# models and game logic
├── Catan3.GameService/      # Unchanged - ASP.NET Core + SignalR backend
├── WebUI/                   # Existing Blazor app (kept for reference)
├── ReactUi/                 # NEW - React/Next.js application
│   ├── app/                 # Next.js App Router pages
│   │   ├── layout.tsx       # Root layout (MainLayout equivalent)
│   │   ├── page.tsx         # Home page
│   │   ├── game/
│   │   │   └── [gameId]/
│   │   │       └── page.tsx # Game page (Game.razor equivalent)
│   │   ├── new-game/
│   │   │   └── page.tsx
│   │   ├── load-game/
│   │   │   └── page.tsx
│   │   ├── edit-players/
│   │   │   └── page.tsx
│   │   ├── settings/
│   │   │   └── page.tsx
│   │   └── stats/
│   │       └── page.tsx
│   ├── components/
│   │   ├── board/           # Board rendering components
│   │   │   ├── BoardContainer.tsx
│   │   │   ├── BaseLayer.tsx
│   │   │   ├── TilesLayer.tsx
│   │   │   ├── RoadsLayer.tsx
│   │   │   ├── BuildingsLayer.tsx
│   │   │   ├── RobberLayer.tsx
│   │   │   ├── GoldTilesLayer.tsx
│   │   │   └── SvgDefinitions.tsx
│   │   ├── players/         # Player UI components
│   │   │   ├── PlayersPanel.tsx
│   │   │   ├── PlayerCard.tsx
│   │   │   ├── PlayerTile.tsx
│   │   │   └── GameResourcesHeader.tsx
│   │   ├── resources/       # Resource display components
│   │   │   ├── ResourceTracking.tsx
│   │   │   ├── ResourceCard.tsx
│   │   │   └── StarCounter.tsx
│   │   ├── controls/        # Game control components
│   │   │   ├── PurchaseButton.tsx
│   │   │   ├── RollGrid.tsx
│   │   │   └── GameControls.tsx
│   │   ├── shared/          # Shared UI components
│   │   │   ├── IconButton.tsx
│   │   │   └── Modal.tsx
│   │   ├── layout/          # Layout components
│   │   │   ├── NavMenu.tsx
│   │   │   └── PortraitTabs.tsx
│   │   └── celebrations/    # Animation overlays
│   │       ├── WinnerCelebration.tsx
│   │       └── GriefCelebration.tsx
│   ├── hooks/               # Custom React hooks
│   │   ├── useGameConnection.ts
│   │   ├── useGameState.ts
│   │   ├── useViewportScaler.ts
│   │   └── useOrientation.ts
│   ├── lib/                 # Core utilities
│   │   ├── api/
│   │   │   ├── client.ts    # REST API client
│   │   │   └── endpoints.ts # Typed API endpoints
│   │   ├── signalr/
│   │   │   ├── connection.ts
│   │   │   └── gameHub.ts   # SignalR client wrapper
│   │   ├── geometry/
│   │   │   └── boardGeometry.ts  # Hex coordinate math
│   │   └── utils/
│   │       └── modelUtils.ts     # Ported C# extension methods
│   ├── stores/              # Zustand state stores
│   │   ├── gameStore.ts
│   │   ├── connectionStore.ts
│   │   └── uiStore.ts
│   ├── types/               # TypeScript type definitions
│   │   ├── generated/       # Auto-generated from C# (NSwag)
│   │   │   └── api.ts
│   │   ├── models.ts        # Re-exports and extensions
│   │   ├── enums.ts         # Game enumerations
│   │   └── messages.ts      # SignalR message types
│   ├── styles/              # Global styles
│   │   └── globals.css      # Tailwind base + custom properties
│   ├── public/
│   │   └── fonts/
│   │       └── Catan.ttf    # Custom Catan icon font
│   ├── scripts/
│   │   └── generate-types.ts  # NSwag type generation script
│   ├── stories/             # Storybook component stories
│   │   └── *.stories.tsx
│   ├── test-fixtures/       # Game state JSON for visual testing
│   │   └── *.json
│   ├── tailwind.config.ts
│   ├── next.config.js
│   ├── tsconfig.json
│   ├── nswag.json            # NSwag type generation config (DLL-based)
│   ├── openapi.json          # (Optional) Fallback OpenAPI spec if DLL approach fails
│   ├── .storybook/           # Storybook configuration
│   └── package.json
└── Tests/                   # Existing test projects
```

### NPM Packages

```json
{
  "dependencies": {
    "next": "^15.0.0",
    "react": "^19.0.0",
    "react-dom": "^19.0.0",
    "@microsoft/signalr": "^8.0.0",
    "zustand": "^5.0.0",
    "framer-motion": "^11.0.0",
    "clsx": "^2.1.0",
    "tailwind-merge": "^2.2.0",
    "lucide-react": "^0.469.0",
    "zod": "^3.24.0",
    "class-variance-authority": "^0.7.0"
  },
  "devDependencies": {
    "typescript": "^5.4.0",
    "tailwindcss": "^3.4.0",
    "autoprefixer": "^10.4.0",
    "postcss": "^8.4.0",
    "@radix-ui/react-dialog": "^1.1.0",
    "@radix-ui/react-dropdown-menu": "^2.1.0",
    "@radix-ui/react-tooltip": "^1.1.0",
    "@radix-ui/react-slot": "^1.1.0",
    "vitest": "^2.0.0",
    "@testing-library/react": "^15.0.0",
    "@playwright/test": "^1.42.0",
    "@storybook/react": "^8.0.0",
    "@storybook/nextjs": "^8.0.0",
    "@chromatic-com/storybook": "^1.0.0",
    "nswag": "^14.0.0"
  }
}
```

### Component Library (shadcn/ui Pattern)

This project uses the **shadcn/ui** pattern: unstyled Radix UI primitives styled with Tailwind CSS,
copied directly into the codebase (not installed as npm packages).

**Why shadcn/ui pattern:**

- Full control over component code (no library updates breaking changes)
- Consistent with Tailwind design tokens
- Accessible by default (Radix primitives)
- Only include components actually used

**Setup:**

```bash
npx shadcn-ui@latest init
npx shadcn-ui@latest add button dialog dropdown-menu tooltip
```

**Components to use from shadcn/ui:**

| Component | Purpose |
|-----------|---------|
| `Button` | Undo/Redo/Next, form actions |
| `Dialog` | Winner confirmation, settings modals |
| `DropdownMenu` | NavMenu hamburger, context menus |
| `Tooltip` | Button hints, stat explanations |

**Custom game components (not from shadcn/ui):**

- `PurchaseButton` - 3D flip animation (Framer Motion)
- `PlayerCard` - Complex multi-state flip
- `RollGrid` - 11 buttons in specific layout
- `BoardContainer` - SVG-based (no HTML primitives)

---

## 2. TypeScript Types & Code Generation

### Serialization Compatibility

The C# backend uses these JSON settings (from `Catan3.Shared/Utility/JsonHelper.cs`):

```csharp
PropertyNamingPolicy = JsonNamingPolicy.CamelCase,  // PascalCase → camelCase
Converters = { new JsonStringEnumConverter() }       // Enums as strings
```

TypeScript must match exactly:

- Property names: `camelCase`
- Enums: string literal unions matching C# enum names

### Code Generation with NSwag

**Why NSwag:** Generates TypeScript interfaces directly from compiled .NET assemblies, ensuring type parity.

**Critical Requirement:** The inner dev loop must NOT depend on a running service.

**Approach:** Generate types from the **compiled DLL** (build artifact), not from a running service or
endpoint. This enables:

- `npm run build` works without GameService running
- CI/CD pipelines don't need service orchestration
- Reproducible builds from build artifacts
- No "works on my machine" issues

**Setup:**

```bash
# Install NSwag as LOCAL devDependency (not global!)
npm install -D nswag
```

> **Why local?** Global installs (`npm install -g`) cause version drift across team members and CI
> agents. Local devDependencies ensure everyone uses the exact same version.

**NSwag Configuration (`ReactUi/nswag.json`):**

```json
{
  "runtime": "Net90",
  "documentGenerator": {
    "aspNetCoreToOpenApi": {
      "project": "../Catan3.GameService/Catan3.GameService.csproj",
      "msBuildProjectExtensionsPath": null,
      "configuration": "Release",
      "runtime": null,
      "targetFramework": "net9.0",
      "noBuild": false,
      "verbose": false,
      "workingDirectory": null,
      "aspNetCoreEnvironment": "Development",
      "output": null,
      "newLineBehavior": "Auto"
    }
  },
  "codeGenerators": {
    "openApiToTypeScriptClient": {
      "className": "GameApiClient",
      "moduleName": "",
      "namespace": "",
      "typeStyle": "Interface",
      "enumStyle": "StringLiteral",
      "dateTimeType": "string",
      "nullValue": "Null",
      "generateClientClasses": false,
      "generateClientInterfaces": false,
      "generateDtoTypes": true,
      "output": "types/generated/api.ts"
    }
  }
}
```

**Key difference:** `aspNetCoreToOpenApi` generates the OpenAPI spec by analyzing the compiled
project, NOT by calling a running service endpoint.

**Package.json Scripts:**

```json
{
  "scripts": {
    "generate-types": "npx nswag run nswag.json",
    "prebuild": "npm run generate-types",
    "build": "next build",
    "dev": "next dev"
  }
}
```

**Workflow:**

1. Make C# API changes in `Catan3.GameService` or `Catan3.Shared`
2. Run `pwsh ./catan.ps1 build` (or NSwag builds automatically via `noBuild: false`)
3. Run `npm run generate-types` (analyzes DLL, generates TypeScript)
4. Verify `types/generated/api.ts` updated correctly

#### Alternative: Pre-generated OpenAPI spec

If NSwag's `aspNetCoreToOpenApi` has issues, fall back to a checked-in spec file:

```json
{
  "documentGenerator": {
    "fromDocument": {
      "json": "openapi.json"
    }
  }
}
```

With a one-time export script (requires service running once):

```bash
# Only run when API changes significantly
curl http://localhost:8080/swagger/v1/swagger.json > openapi.json
npm run generate-types
git add openapi.json types/generated/api.ts
```

The key is that `openapi.json` is **committed to source control**, so subsequent builds don't
need the service running.

### Handling C# Extension Methods

The backend relies heavily on C# extension methods in `Catan3.Shared/Extensions/`. These must be
categorized carefully for porting:

#### MUST PORT (Geometry & Rendering Logic)

These are required for correct UI behavior:

| C# Extension | TypeScript Port | Purpose |
|--------------|-----------------|---------|
| `BuildingKey.Aliases()` | `getBuildingAliases(key)` | Identifies that a vertex is shared by 3 hexes. Critical for click detection and deduplication. |
| `TileModelExtensions.AdjacentTiles()` | `getAdjacentTiles(tiles, tile)` | Required for highlighting, "longest road" visuals |
| `BuildingModelExtensions.FindBuildingModel()` | `findBuilding(buildings, key)` | Lookup with alias support |
| `GameModelExtensions.AdjacentRoads()` | `getAdjacentRoads(game, buildingKey)` | Required for road building validation visuals |
| `GameModelExtensions.AdjacentBuildings()` | `getAdjacentBuildings(game, roadKey)` | Required for settlement placement visuals |
| `TileModel.Stars` | `getTileStars(number)` | Star count for probability display |

**Implementation:** Port as **pure functions** in `lib/utils/modelUtils.ts`, receiving the model
as the first argument (not extending TypeScript prototypes):

```typescript
// lib/utils/modelUtils.ts
import type { BuildingKey, HexPosition, Direction, TileModel, BuildingModel } from '@/types';

/** Vertex aliases - identifies which adjacent tiles share this vertex */
export function getBuildingAliases(key: BuildingKey): Array<{ position: HexPosition; direction: Direction }> {
  switch (key.position) {
    case 'TopRight':
      return [
        { position: 'BottomRight', direction: 'North' },
        { position: 'Left', direction: 'NorthEast' },
      ];
    case 'Right':
      return [
        { position: 'BottomLeft', direction: 'NorthEast' },
        { position: 'TopLeft', direction: 'SouthEast' },
      ];
    // ... other cases matching BuildingModelExtensions.Aliases()
    default:
      return [];
  }
}

/** Find building with alias support */
export function findBuilding(
  buildings: BuildingModel[],
  key: BuildingKey
): BuildingModel | undefined {
  // Direct match first
  const direct = buildings.find(b =>
    b.buildingKey.hexCoordinates.q === key.hexCoordinates.q &&
    b.buildingKey.hexCoordinates.r === key.hexCoordinates.r &&
    b.buildingKey.position === key.position
  );
  if (direct) return direct;

  // Check aliases
  for (const alias of getBuildingAliases(key)) {
    const aliasCoords = getAdjacentTile(key.hexCoordinates, alias.direction);
    const aliasBuilding = buildings.find(b =>
      b.buildingKey.hexCoordinates.q === aliasCoords.q &&
      b.buildingKey.hexCoordinates.r === aliasCoords.r &&
      b.buildingKey.position === alias.position
    );
    if (aliasBuilding) return aliasBuilding;
  }
  return undefined;
}
```

#### DO NOT PORT (Game Rules & Factory Logic)

The **server is the authority**. Do NOT reimplement rule verification client-side:

| C# Extension | Why NOT to Port |
|--------------|-----------------|
| `GameModelExtensions.CreateNew()` | Server-side only |
| `GameModelExtensions.Shuffle()` | Server-side only |
| `GameModelExtensions.BalancedShuffle()` | Server-side only |
| `GameModelExtensions.ValidateGame()` | Server validates; client trusts response |
| `GameModelExtensions.ComputeGameHash()` | Server computes hash |
| `PlayerModelExtensions.CanPurchase*()` | Use `ActionFlags` from `GameModel` instead |

**Key principle:** The client relies on `GameModel.ActionFlags` to enable/disable UI buttons,
NOT by reimplementing purchase validation logic. The server sends flags like:

- `actionFlags.canPurchaseSettlement`
- `actionFlags.canPurchaseRoad`
- `actionFlags.canEndTurn`

The React UI simply reads these booleans to control button state.

### Manual Type Extensions

For computed properties and helper functions not in the API:

```typescript
// types/models.ts
import type { PlayerModel as GeneratedPlayerModel } from './generated/api';

// Re-export generated types
export type { GameModel, TileModel, BuildingModel, RoadModel } from './generated/api';

// Extend with computed properties
export interface PlayerModel extends GeneratedPlayerModel {
  // Computed in C# but not serialized
}

// Helper functions for computed values
export function getPlayerName(player: PlayerModel): string {
  if (!player.id) return 'Unknown';
  const dashIndex = player.id.indexOf('-');
  return dashIndex >= 0 ? player.id.substring(0, dashIndex) : player.id;
}

export function getTileStars(number: number): number {
  switch (number) {
    case 2: case 12: return 1;
    case 3: case 11: return 2;
    case 4: case 10: return 3;
    case 5: case 9: return 4;
    case 6: case 8: return 5;
    default: return 0;
  }
}
```

### Enum Definitions

C# enums serialize as strings. Ty
eScript should use string literal unions:

```typescript
// types/enums.ts

export type GameState =
  | 'Uninitialized'
  | 'WaitingForNewGame'
  | 'BeginResourceAllocation'
  | 'WaitingForPlayers'
  | 'PickingBoard'
  | 'WaitingForRollForOrder'
  | 'FinishedRollOrder'
  | 'AllocateResourceForward'
  | 'AllocateResourceReverse'
  | 'DoneResourceAllocation'
  | 'WaitingForRoll'
  | 'WaitingForNext'
  | 'Supplemental'
  | 'TooManyCards'
  | 'MustDestroyCity'
  | 'PickingRandomGoldTiles'
  | 'HandlePirates'
  | 'DoneDestroyingCities'
  | 'MustMoveMerchant'
  | 'DestroyRoad'
  | 'SwapNumbers'
  | 'PickDeserter'
  | 'PlaceDeserterKnight'
  | 'DoneWithDeserter'
  | 'UpgradeToMetro'
  | 'TestCheckpoint'
  | 'MustMoveRobber'
  | 'DisplaceVictimKnight'
  | 'DisplaceKnightMoveVictim'
  | 'ClickOnKnight'
  | 'PickSupplementalPlayers'
  | 'GameOver';

export type GameType = 'Regular' | 'Expansion' | 'Unset' | 'SavedGame';

export type ResourceType =
  | 'Sheep' | 'Wood' | 'Ore' | 'Wheat' | 'Brick'
  | 'GoldMine' | 'Cloth' | 'Paper' | 'Coin'
  | 'Politics' | 'Trade' | 'Science'
  | 'Desert' | 'Back' | 'None' | 'Sea'
  | 'AnyDevCard' | 'VictoryPoint' | 'Invasion' | 'Robber';

export type Entitlement =
  | 'Settlement' | 'City' | 'Road' | 'Soldier' | 'DevCard'
  | 'Ship' | 'Wall' | 'DestroyCity' | 'Metropolis'
  | 'BuyKnight' | 'UpgradeKnight' | 'ActivateKnight' | 'KnightDisplacement'
  | 'Politics' | 'Science' | 'Trade'
  | 'PoliticsUpgrade' | 'ScienceUpgrade' | 'TradeUpgrade'
  | 'Bishop' | 'Deserter' | 'Inventor' | 'Intrigue' | 'Diplomat' | 'Merchant'
  | 'Undefined' | 'RolledSeven' | 'KnightDisplacementMoveKnightOutOfTheWay';

export type BuildingState =
  | 'PossibleSettlement' | 'NotBuildable' | 'Settlement' | 'City' | 'Metropolis' | 'Knight';

export type RoadState = 'Unowned' | 'Road' | 'Ship' | 'Buildable';

export type HarborType = 'Sheep' | 'Wood' | 'Ore' | 'Wheat' | 'Brick' | 'ThreeForOne' | 'None';

export type HexPosition = 'Right' | 'BottomRight' | 'BottomLeft' | 'Left' | 'TopLeft' | 'TopRight';

export type HexSide = 'Top' | 'TopRight' | 'BottomRight' | 'Bottom' | 'BottomLeft' | 'TopLeft';
```

---

## 3. State Management (Zustand)

### Why Zustand

- Minimal boilerplate compared to Redux
- Excellent TypeScript support
- Built-in selector optimization for re-renders
- Simple API that matches React patterns

### Game Store

```typescript
// stores/gameStore.ts
import { create } from 'zustand';
import { subscribeWithSelector } from 'zustand/middleware';
import type { GameModel, PlayerModel } from '@/types/models';

interface GameState {
  // Core state (from server)
  gameModel: GameModel | null;
  playerId: string | null;

  // Connection state
  isConnected: boolean;
  isConnecting: boolean;
  connectionError: string | null;

  // Actions
  setGameModel: (model: GameModel) => void;
  setPlayerId: (id: string) => void;
  setConnectionStatus: (status: Partial<Pick<GameState, 'isConnected' | 'isConnecting' | 'connectionError'>>) => void;
  reset: () => void;
}

export const useGameStore = create<GameState>()(
  subscribeWithSelector((set, get) => ({
    gameModel: null,
    playerId: null,
    isConnected: false,
    isConnecting: false,
    connectionError: null,

    setGameModel: (model) => set({ gameModel: model }),
    setPlayerId: (id) => set({ playerId: id }),
    setConnectionStatus: (status) => set((state) => ({ ...state, ...status })),
    reset: () => set({
      gameModel: null,
      isConnected: false,
      isConnecting: false,
      connectionError: null,
    }),
  }))
);

// Optimized selectors to prevent unnecessary re-renders
export const useCurrentPlayer = () =>
  useGameStore((s) => s.gameModel?.players.find(p => p.id === s.gameModel?.currentPlayerId));

export const useIsMyTurn = () =>
  useGameStore((s) => s.gameModel?.currentPlayerId === s.playerId);

export const useGameState = () =>
  useGameStore((s) => s.gameModel?.gameState);

export const useActionFlags = () =>
  useGameStore((s) => s.gameModel?.actionFlags);

export const useTiles = () =>
  useGameStore((s) => s.gameModel?.tiles ?? []);

export const useBuildings = () =>
  useGameStore((s) => s.gameModel?.buildings ?? []);

export const useRoads = () =>
  useGameStore((s) => s.gameModel?.roads ?? []);

export const useRobber = () =>
  useGameStore((s) => s.gameModel?.robber);

export const usePlayers = () =>
  useGameStore((s) => s.gameModel?.players ?? []);
```

### UI Store

```typescript
// stores/uiStore.ts
import { create } from 'zustand';
import { persist } from 'zustand/middleware';

type PortraitTab = 'board' | 'controls' | 'players';

interface UIState {
  // Layout
  isPortrait: boolean;
  isMobile: boolean;
  activePortraitTab: PortraitTab;
  viewportScale: number;

  // Modals & Overlays
  showWinnerDialog: boolean;
  showWinnerCelebration: boolean;
  winnerName: string | null;
  showGriefCelebration: boolean;
  showRobberMenu: boolean;
  robberMenuPosition: { x: number; y: number } | null;
  robberMenuTargets: string[];

  // Roll state
  lastRolledNumber: number | null;
  rollDimTimeoutId: NodeJS.Timeout | null;

  // Actions
  setOrientation: (isPortrait: boolean, isMobile: boolean) => void;
  setActiveTab: (tab: PortraitTab) => void;
  setViewportScale: (scale: number) => void;
  openRobberMenu: (position: { x: number; y: number }, targets: string[]) => void;
  closeRobberMenu: () => void;
  showWinner: (name: string) => void;
  hideWinner: () => void;
  triggerGriefCelebration: () => void;
  setRolledNumber: (num: number | null) => void;
}

export const useUIStore = create<UIState>()(
  persist(
    (set, get) => ({
      isPortrait: false,
      isMobile: false,
      activePortraitTab: 'board',
      viewportScale: 1,
      showWinnerDialog: false,
      showWinnerCelebration: false,
      winnerName: null,
      showGriefCelebration: false,
      showRobberMenu: false,
      robberMenuPosition: null,
      robberMenuTargets: [],
      lastRolledNumber: null,
      rollDimTimeoutId: null,

      setOrientation: (isPortrait, isMobile) => set({ isPortrait, isMobile }),
      setActiveTab: (tab) => set({ activePortraitTab: tab }),
      setViewportScale: (scale) => set({ viewportScale: scale }),

      openRobberMenu: (position, targets) => set({
        showRobberMenu: true,
        robberMenuPosition: position,
        robberMenuTargets: targets,
      }),

      closeRobberMenu: () => set({
        showRobberMenu: false,
        robberMenuPosition: null,
        robberMenuTargets: [],
      }),

      showWinner: (name) => set({ showWinnerCelebration: true, winnerName: name }),
      hideWinner: () => set({ showWinnerCelebration: false, winnerName: null }),

      triggerGriefCelebration: () => {
        set({ showGriefCelebration: true });
        setTimeout(() => set({ showGriefCelebration: false }), 6000);
      },

      setRolledNumber: (num) => {
        const { rollDimTimeoutId } = get();
        if (rollDimTimeoutId) clearTimeout(rollDimTimeoutId);

        if (num !== null) {
          const timeoutId = setTimeout(() => {
            set({ lastRolledNumber: null, rollDimTimeoutId: null });
          }, 5000);
          set({ lastRolledNumber: num, rollDimTimeoutId: timeoutId });
        } else {
          set({ lastRolledNumber: null, rollDimTimeoutId: null });
        }
      },
    }),
    {
      name: 'catan-ui-state',
      partialize: (state) => ({ activePortraitTab: state.activePortraitTab }),
    }
  )
);
```

---

## 4. Communication Architecture

### 4.1 Overview: REST Commands + SignalR Updates

The React client uses a **hybrid communication pattern**:

- **REST API** (`/api/game/action`) - All game commands (undo, redo, purchase, roll, etc.)
- **SignalR** - Real-time state updates pushed from server to client

This differs from the Blazor client, which uses SignalR for both directions. Both clients can
play in the same game simultaneously because:

1. Commands go through the same `GameStateMachine` regardless of transport
2. State updates are broadcast to ALL clients in the SignalR group
3. The REST endpoint triggers the same SignalR broadcast as the Hub methods

```text
┌─────────────────┐         ┌─────────────────┐         ┌─────────────────┐
│  React Client   │         │  GameService    │         │  Blazor Client  │
│  (TypeScript)   │         │  (ASP.NET)      │         │  (C#/Razor)     │
└────────┬────────┘         └────────┬────────┘         └────────┬────────┘
         │                           │                           │
         │ POST /api/game/action     │                           │
         │ ─────────────────────────>│                           │
         │                           │                           │
         │                           │  SignalR invoke('Undo')   │
         │                           │<──────────────────────────│
         │                           │                           │
         │                           │  GameStateMachine         │
         │                           │  processes command        │
         │                           │                           │
         │  SignalR: GameStateUpdated│  SignalR: GameStateUpdated│
         │<──────────────────────────│──────────────────────────>│
         │                           │                           │
```

### 4.2 SignalR: Real-Time Updates Only

SignalR is used **only** for:

1. **Connection management** - `JoinGame`, `LeaveGame` (group membership)
2. **Receiving server events** - `GameStateUpdated`, `PlayerPresenceChanged`, `CommandCompleted`

**NOT used for:** Sending game commands (those go via REST)

### Connection Factory

```typescript
// lib/signalr/connection.ts
import * as signalR from '@microsoft/signalr';

const GAME_HUB_URL = process.env.NEXT_PUBLIC_API_URL + '/gameHub';

export function createHubConnection(): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(GAME_HUB_URL)
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        // Exponential backoff: 0s, 2s, 10s, 30s, then every 30s
        if (retryContext.previousRetryCount === 0) return 0;
        if (retryContext.previousRetryCount === 1) return 2000;
        if (retryContext.previousRetryCount === 2) return 10000;
        return 30000;
      },
    })
    .configureLogging(signalR.LogLevel.Information)
    .build();
}
```

### SignalR Client (Updates Only)

```typescript
// lib/signalr/gameHub.ts
import type { HubConnection } from '@microsoft/signalr';
import type { GameModel } from '@/types';

/**
 * SignalR client for receiving real-time game updates.
 * NOTE: Game commands are sent via REST API, not SignalR invoke().
 */
export class GameHubClient {
  constructor(private connection: HubConnection) {}

  // Connection management (still uses SignalR invoke for group membership)
  async joinGame(gameId: string, playerId: string): Promise<void> {
    await this.connection.invoke('JoinGame', gameId, playerId);
  }

  async leaveGame(gameId: string, playerId: string): Promise<void> {
    await this.connection.invoke('LeaveGame', gameId, playerId);
  }

  // Event subscriptions (server → client)
  onGameStateUpdated(callback: (gameModel: GameModel) => void): void {
    this.connection.on('GameStateUpdated', callback);
  }

  onCommandCompleted(callback: (commandId: string, success: boolean, message: string) => void): void {
    this.connection.on('CommandCompleted', callback);
  }

  onCommandFailed(callback: (commandId: string, errorInfo: unknown) => void): void {
    this.connection.on('CommandFailed', callback);
  }

  onPlayerPresenceChanged(callback: (playerId: string, isPresent: boolean) => void): void {
    this.connection.on('PlayerPresenceChanged', callback);
  }

  // Cleanup
  removeAllListeners(): void {
    this.connection.off('GameStateUpdated');
    this.connection.off('CommandCompleted');
    this.connection.off('CommandFailed');
    this.connection.off('PlayerPresenceChanged');
  }
}
```

### 4.3 REST API: Game Commands

All game commands are sent via the existing `/api/game/action` endpoint.

**Endpoint:** `POST /api/game/action`

**Request Body:**

```typescript
interface GameActionRequest {
  gameId: string;
  playerId: string;
  messageType: string;  // e.g., "UndoMessage", "PurchaseMessage"
  messageData?: object; // Message-specific payload
}
```

**Supported Message Types (already implemented):**

| messageType | Description | messageData |
|-------------|-------------|-------------|
| `UndoMessage` | Undo last action | (none) |
| `RedoMessage` | Redo undone action | (none) |
| `NextMessage` | End turn / advance | (none) |
| `PurchaseMessage` | Buy settlement/city/road/dev card | `{ entitlement, buildingKey? }` |
| `RoadPurchaseMessage` | Place a road | `{ roadKey }` |
| `BuildingUpgradeMessage` | Upgrade settlement to city | `{ buildingKey }` |
| `MoveRobberMessage` | Move robber and steal | `{ coordinates, targetPlayerId? }` |
| `RollMessage` | Roll dice | `{ diceValue }` |
| `SetPlayerOrderMessage` | Set turn order | `{ playerOrder }` |
| `BalanceBoardMessage` | Balance tile distribution | (none) |
| `GoFirstMessage` | Claim first turn in roll | `{ playerId }` |

**Message Types Requiring New REST Support:**

| messageType | Description | Status |
|-------------|-------------|--------|
| `ShuffleMessage` | Shuffle board tiles | **Needs REST endpoint** |
| `ParticipatingInSupplementalMessage` | Toggle supplemental participation | **Needs REST endpoint** |
| `SwapTileResourcesMessage` | Swap tile resources (expansion) | **Needs REST endpoint** |

### 4.4 GameServiceProxy: Unified Client

The `GameServiceProxy` class provides a unified interface for all service communication, mirroring
the C# `GameServiceProxy.cs` pattern. It:

1. Manages SignalR connection lifecycle (connect, join, leave, reconnect)
2. Sends game commands via REST API
3. Receives real-time updates via SignalR
4. Supports both local development (`localhost:8080`) and deployed service URLs
5. Provides typed methods for all game operations

**Why a Proxy Class?**

- **Single point of configuration** - Service URL configured once
- **Abstraction** - Components don't know/care about REST vs SignalR
- **Testability** - Easy to mock for unit tests
- **Consistency** - Same pattern as the C# client

```typescript
// lib/services/GameServiceProxy.ts
import * as signalR from '@microsoft/signalr';
import type {
  GameModel,
  PurchaseMessage,
  RoadPurchaseMessage,
  BuildingUpgradeMessage,
  MoveRobberMessage,
  RollMessage,
  GoFirstMessage,
  NewGameMessage,
  HouseRules,
} from '@/types';

export interface CommandResult {
  success: boolean;
  commandId: string;
  message: string;
}

export interface GameServiceProxyConfig {
  /** Base URL for REST API (e.g., "http://localhost:8080") */
  serviceUrl: string;
  /** SignalR hub URL (e.g., "http://localhost:8080/gameHub") */
  hubUrl: string;
}

/**
 * Unified client for GameService communication.
 * - REST API for commands and queries
 * - SignalR for real-time state updates
 *
 * Mirrors the C# GameServiceProxy pattern for consistency.
 */
export class GameServiceProxy {
  private connection: signalR.HubConnection | null = null;
  private _gameId: string | null = null;
  private _playerId: string | null = null;
  private _gameModel: GameModel | null = null;

  // Event callbacks
  public onGameStateUpdated: ((model: GameModel) => void) | null = null;
  public onCommandCompleted: ((commandId: string, success: boolean, message: string) => void) | null = null;
  public onCommandFailed: ((commandId: string, error: unknown) => void) | null = null;
  public onPlayerPresenceChanged: ((playerId: string, isPresent: boolean) => void) | null = null;
  public onReconnecting: ((error?: Error) => void) | null = null;
  public onReconnected: ((connectionId?: string) => void) | null = null;
  public onConnectionClosed: ((error?: Error) => void) | null = null;

  constructor(private config: GameServiceProxyConfig) {}

  // --- Getters ---
  get gameId(): string | null { return this._gameId; }
  get playerId(): string | null { return this._playerId; }
  get gameModel(): GameModel | null { return this._gameModel; }
  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  /** Effective player ID - uses current player from GameModel if available */
  private get effectivePlayerId(): string {
    return this._gameModel?.currentPlayerId ?? this._playerId ?? '';
  }

  // --- Connection Management ---

  async connect(playerId: string): Promise<void> {
    this._playerId = playerId;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.config.hubUrl)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) => {
          if (ctx.previousRetryCount === 0) return 0;
          if (ctx.previousRetryCount === 1) return 2000;
          if (ctx.previousRetryCount === 2) return 10000;
          return 30000;
        },
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.setupEventHandlers();
    await this.connection.start();
  }

  async disconnect(): Promise<void> {
    if (this._gameId && this._playerId) {
      await this.leaveGame();
    }
    await this.connection?.stop();
    this.connection = null;
  }

  async joinGame(gameId: string): Promise<void> {
    if (!this.connection || !this._playerId) {
      throw new Error('Must connect before joining a game');
    }
    await this.connection.invoke('JoinGame', gameId, this._playerId);
    this._gameId = gameId;
  }

  async leaveGame(): Promise<void> {
    if (!this.connection || !this._gameId || !this._playerId) return;
    await this.connection.invoke('LeaveGame', this._gameId, this._playerId);
    this._gameId = null;
    this._gameModel = null;
  }

  private setupEventHandlers(): void {
    if (!this.connection) return;

    this.connection.on('GameStateUpdated', (model: GameModel) => {
      this._gameModel = model;
      this.onGameStateUpdated?.(model);
    });

    this.connection.on('CommandCompleted', (commandId: string, success: boolean, message: string) => {
      this.onCommandCompleted?.(commandId, success, message);
    });

    this.connection.on('CommandFailed', (commandId: string, error: unknown) => {
      this.onCommandFailed?.(commandId, error);
    });

    this.connection.on('PlayerPresenceChanged', (playerId: string, isPresent: boolean) => {
      this.onPlayerPresenceChanged?.(playerId, isPresent);
    });

    this.connection.onreconnecting((error) => this.onReconnecting?.(error));
    this.connection.onreconnected((connectionId) => {
      // Re-join game after reconnect
      if (this._gameId) {
        this.connection?.invoke('JoinGame', this._gameId, this._playerId);
      }
      this.onReconnected?.(connectionId);
    });
    this.connection.onclose((error) => this.onConnectionClosed?.(error));
  }

  // --- Game Commands (via REST) ---

  private async sendAction(messageType: string, messageData?: object): Promise<CommandResult> {
    if (!this._gameId) throw new Error('Must join a game before sending commands');

    const response = await fetch(`${this.config.serviceUrl}/api/game/action`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        gameId: this._gameId,
        playerId: this.effectivePlayerId,
        messageType,
        messageData,
      }),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: response.statusText }));
      throw new Error(error.message || 'Command failed');
    }

    return response.json();
  }

  // Basic game flow
  async undo(): Promise<CommandResult> {
    return this.sendAction('UndoMessage');
  }

  async redo(): Promise<CommandResult> {
    return this.sendAction('RedoMessage');
  }

  async next(): Promise<CommandResult> {
    return this.sendAction('NextMessage');
  }

  // Purchases
  async purchase(message: PurchaseMessage): Promise<CommandResult> {
    return this.sendAction('PurchaseMessage', message);
  }

  async purchaseRoad(message: RoadPurchaseMessage): Promise<CommandResult> {
    return this.sendAction('RoadPurchaseMessage', message);
  }

  async upgradeBuilding(message: BuildingUpgradeMessage): Promise<CommandResult> {
    return this.sendAction('BuildingUpgradeMessage', message);
  }

  // Dice and robber
  async roll(message: RollMessage): Promise<CommandResult> {
    return this.sendAction('RollMessage', message);
  }

  async moveRobber(message: MoveRobberMessage): Promise<CommandResult> {
    return this.sendAction('MoveRobberMessage', message);
  }

  // Turn order
  async goFirst(message: GoFirstMessage): Promise<CommandResult> {
    return this.sendAction('GoFirstMessage', message);
  }

  async setPlayerOrder(playerOrder: string[]): Promise<CommandResult> {
    return this.sendAction('SetPlayerOrderMessage', { playerOrder });
  }

  // Board setup
  async balanceBoard(): Promise<CommandResult> {
    return this.sendAction('BalanceBoardMessage');
  }

  // --- REST-only Operations (not via /api/game/action) ---

  async createGame(message: NewGameMessage): Promise<{ success: boolean; gameId: string }> {
    const response = await fetch(`${this.config.serviceUrl}/api/game/new`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(message),
    });
    return response.json();
  }

  async getGameState(gameId: string): Promise<{ gameModel: GameModel }> {
    const response = await fetch(`${this.config.serviceUrl}/api/gamestate/${gameId}`);
    return response.json();
  }

  async shuffle(): Promise<CommandResult> {
    if (!this._gameId) throw new Error('Must join a game first');
    const response = await fetch(`${this.config.serviceUrl}/api/game/${this._gameId}/shuffle`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId: this.effectivePlayerId }),
    });
    return response.json();
  }

  async updateHouseRules(houseRules: HouseRules): Promise<CommandResult> {
    if (!this._gameId) throw new Error('Must join a game first');
    const response = await fetch(`${this.config.serviceUrl}/api/game/${this._gameId}/houserules`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(houseRules),
    });
    return response.json();
  }
}
```

### 4.5 Service Configuration

Support both local development and deployed service URLs:

```typescript
// lib/services/config.ts

export interface ServiceConfig {
  serviceUrl: string;
  hubUrl: string;
}

/**
 * Get service configuration based on environment.
 * - Development: localhost:8080
 * - Production: from NEXT_PUBLIC_API_URL environment variable
 */
export function getServiceConfig(): ServiceConfig {
  const baseUrl = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:8080';

  return {
    serviceUrl: baseUrl,
    hubUrl: `${baseUrl}/gameHub`,
  };
}

// Singleton instance for the app
let proxyInstance: GameServiceProxy | null = null;

export function getGameServiceProxy(): GameServiceProxy {
  if (!proxyInstance) {
    proxyInstance = new GameServiceProxy(getServiceConfig());
  }
  return proxyInstance;
}
```

### 4.6 React Hook (using GameServiceProxy)

```typescript
// hooks/useGameConnection.ts
'use client';

import { useEffect, useRef } from 'react';
import { getGameServiceProxy, GameServiceProxy } from '@/lib/services/GameServiceProxy';
import { useGameStore } from '@/stores/gameStore';

/**
 * React hook for managing game connection via GameServiceProxy.
 * Handles connection lifecycle, event subscriptions, and Zustand store updates.
 */
export function useGameConnection(gameId: string | null, playerId: string | null) {
  const proxyRef = useRef<GameServiceProxy | null>(null);

  const setGameModel = useGameStore((s) => s.setGameModel);
  const setConnectionStatus = useGameStore((s) => s.setConnectionStatus);

  useEffect(() => {
    if (!gameId || !playerId) return;

    const proxy = getGameServiceProxy();
    proxyRef.current = proxy;

    // Wire up event handlers to Zustand store
    proxy.onGameStateUpdated = (model) => {
      console.log('[GameServiceProxy] GameStateUpdated received');
      setGameModel(model);
    };

    proxy.onCommandFailed = (commandId, error) => {
      console.error('[GameServiceProxy] Command failed:', commandId, error);
    };

    proxy.onReconnecting = () => {
      setConnectionStatus({ isConnecting: true, connectionError: 'Reconnecting...' });
    };

    proxy.onReconnected = () => {
      setConnectionStatus({ isConnected: true, isConnecting: false, connectionError: null });
    };

    proxy.onConnectionClosed = (error) => {
      setConnectionStatus({
        isConnected: false,
        isConnecting: false,
        connectionError: error?.message ?? 'Connection closed',
      });
    };

    // Connect and join game
    setConnectionStatus({ isConnecting: true });

    proxy.connect(playerId)
      .then(() => proxy.joinGame(gameId))
      .then(() => {
        setConnectionStatus({ isConnected: true, isConnecting: false, connectionError: null });
      })
      .catch((err) => {
        setConnectionStatus({
          isConnected: false,
          isConnecting: false,
          connectionError: err.message,
        });
      });

    // Cleanup on unmount
    return () => {
      proxy.disconnect();
    };
  }, [gameId, playerId, setGameModel, setConnectionStatus]);

  return {
    proxy: proxyRef.current,
    isConnected: proxyRef.current?.isConnected ?? false,
  };
}
```

**Usage in Components:**

```tsx
// In a game page component
function GamePage({ gameId }: { gameId: string }) {
  const playerId = usePlayerIdFromStorage(); // or from auth context
  const { proxy, isConnected } = useGameConnection(gameId, playerId);
  const gameModel = useGameStore((s) => s.gameModel);

  const handleUndo = async () => {
    await proxy?.undo();
    // GameStateUpdated will update the store automatically
  };

  const handleRoll = async (diceValue: number) => {
    await proxy?.roll({ diceValue });
  };

  // ... render game UI
}
```

---

## 5. REST API Client

### 5.1 Base Client

```typescript
// lib/api/client.ts
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:8080';

export class ApiError extends Error {
  constructor(
    message: string,
    public statusCode: number,
    public errorCode?: string
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const url = `${API_BASE_URL}${endpoint}`;

  const response = await fetch(url, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...options.headers,
    },
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    throw new ApiError(
      errorData.error ?? response.statusText,
      response.status,
      errorData.errorCode
    );
  }

  return response.json();
}
```

### 5.2 Game Commands API

All game commands go through `/api/game/action`. This is the **primary API for gameplay**.

```typescript
// lib/api/gameCommands.ts
import type {
  PurchaseMessage,
  RoadPurchaseMessage,
  BuildingUpgradeMessage,
  MoveRobberMessage,
  RollMessage,
  GoFirstMessage,
} from '@/types';

interface ActionResponse {
  success: boolean;
  commandId: string;
  message: string;
}

/**
 * Send a game command via REST API.
 * The server broadcasts GameStateUpdated to all clients via SignalR.
 */
async function sendGameAction(
  gameId: string,
  playerId: string,
  messageType: string,
  messageData?: object
): Promise<ActionResponse> {
  return request('/api/game/action', {
    method: 'POST',
    body: JSON.stringify({ gameId, playerId, messageType, messageData }),
  });
}

export const gameCommands = {
  // Basic game flow
  undo: (gameId: string, playerId: string) =>
    sendGameAction(gameId, playerId, 'UndoMessage'),

  redo: (gameId: string, playerId: string) =>
    sendGameAction(gameId, playerId, 'RedoMessage'),

  next: (gameId: string, playerId: string) =>
    sendGameAction(gameId, playerId, 'NextMessage'),

  // Purchases
  purchase: (gameId: string, playerId: string, message: PurchaseMessage) =>
    sendGameAction(gameId, playerId, 'PurchaseMessage', message),

  purchaseRoad: (gameId: string, playerId: string, message: RoadPurchaseMessage) =>
    sendGameAction(gameId, playerId, 'RoadPurchaseMessage', message),

  upgradeBuilding: (gameId: string, playerId: string, message: BuildingUpgradeMessage) =>
    sendGameAction(gameId, playerId, 'BuildingUpgradeMessage', message),

  // Dice and robber
  roll: (gameId: string, playerId: string, message: RollMessage) =>
    sendGameAction(gameId, playerId, 'RollMessage', message),

  moveRobber: (gameId: string, playerId: string, message: MoveRobberMessage) =>
    sendGameAction(gameId, playerId, 'MoveRobberMessage', message),

  // Turn order
  goFirst: (gameId: string, playerId: string, message: GoFirstMessage) =>
    sendGameAction(gameId, playerId, 'GoFirstMessage', message),

  setPlayerOrder: (gameId: string, playerId: string, playerOrder: string[]) =>
    sendGameAction(gameId, playerId, 'SetPlayerOrderMessage', { playerOrder }),

  // Board setup
  balanceBoard: (gameId: string, playerId: string) =>
    sendGameAction(gameId, playerId, 'BalanceBoardMessage'),

  // NOTE: These require backend changes (see Section 4.3)
  // shuffle: (gameId: string, playerId: string) =>
  //   sendGameAction(gameId, playerId, 'ShuffleMessage'),
  //
  // participateInSupplemental: (gameId: string, playerId: string, participating: boolean) =>
  //   sendGameAction(gameId, playerId, 'ParticipatingInSupplementalMessage', { participating }),
};
```

### 5.3 Game Lifecycle API

Non-command endpoints for game management.

```typescript
// lib/api/endpoints.ts
import type { GameModel, NewGameMessage, HouseRules, DeclareWinnerRequest } from '@/types';

interface CommandResponse {
  success: boolean;
  message?: string;
  error?: string;
  gameId?: string;
}

interface NewGameResponse {
  success: boolean;
  gameId: string;
}

export const gameApi = {
  // Game lifecycle
  createGame: (message: NewGameMessage): Promise<NewGameResponse> =>
    request('/api/game/new', { method: 'POST', body: JSON.stringify(message) }),

  getGameState: (gameId: string): Promise<{ gameModel: GameModel }> =>
    request(`/api/gamestate/${gameId}`),

  loadGame: (compressedLog: string): Promise<NewGameResponse> =>
    request('/api/game/load', {
      method: 'POST',
      body: JSON.stringify({ compressedLog }),
    }),

  // Game settings
  shuffle: (gameId: string, playerId: string): Promise<CommandResponse> =>
    request(`/api/game/${gameId}/shuffle`, {
      method: 'POST',
      body: JSON.stringify({ playerId }),
    }),

  updateHouseRules: (gameId: string, houseRules: HouseRules): Promise<CommandResponse> =>
    request(`/api/game/${gameId}/houserules`, {
      method: 'PUT',
      body: JSON.stringify(houseRules),
    }),

  declareWinner: (gameId: string, req: DeclareWinnerRequest): Promise<CommandResponse> =>
    request(`/api/game/${gameId}/winner`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  // Player management
  getPlayers: (): Promise<{ players: PlayerProfile[] }> =>
    request('/api/players'),

  // Statistics
  getStats: (): Promise<{ stats: PlayerStatsSummary[] }> =>
    request('/api/stats'),
};
```

---

## 6. Component Mapping

### Complete Blazor → React Mapping

| Blazor Component | React Component | Notes |
|-----------------|-----------------|-------|
| **Pages** | | |
| `Pages/Game.razor` (1789 lines) | `app/game/[gameId]/page.tsx` | Split into smaller components |
| `Pages/Home.razor` | `app/page.tsx` | |
| `Pages/NewGame.razor` | `app/new-game/page.tsx` | |
| `Pages/LoadGame.razor` | `app/load-game/page.tsx` | |
| `Pages/EditPlayers.razor` | `app/edit-players/page.tsx` | |
| `Pages/Settings.razor` | `app/settings/page.tsx` | |
| `Pages/Stats.razor` | `app/stats/page.tsx` | |
| **Layout** | | |
| `Layout/MainLayout.razor` | `app/layout.tsx` | Root layout |
| `Layout/NavMenu.razor` | `components/layout/NavMenu.tsx` | |
| **Board** | | |
| `Components/Board/BoardContainer.razor` | `components/board/BoardContainer.tsx` | |
| `Components/Board/BaseLayer.razor` | `components/board/BaseLayer.tsx` | Static SVG |
| `Components/Board/TileOverlay.razor` | `components/board/TilesLayer.tsx` | Click handling |
| `Components/Board/RoadsLayer.razor` | `components/board/RoadsLayer.tsx` | |
| `Components/Board/RoadOverlay.razor` | (merged into RoadsLayer) | |
| `Components/Board/BuildingsLayer.razor` | `components/board/BuildingsLayer.tsx` | |
| `Components/Board/BuildingOverlay.razor` | (merged into BuildingsLayer) | |
| `Components/Board/RobberLayer.razor` | `components/board/RobberLayer.tsx` | |
| `Components/Board/GoldTilesLayer.razor` | `components/board/GoldTilesLayer.tsx` | |
| `Components/Board/SharedDefinitions.razor` | `components/board/SvgDefinitions.tsx` | Gradients |
| `Components/Board/BoardMeasurement.razor` | `components/board/BoardMeasurement.tsx` | |
| **Players** | | |
| `Components/Players/PlayersPanel.razor` | `components/players/PlayersPanel.tsx` | |
| `Components/Players/PlayerCard.razor` | `components/players/PlayerCard.tsx` | 3D flip |
| `Components/Players/PlayerTile.razor` | `components/players/PlayerTile.tsx` | |
| `Components/Players/GameResourcesHeader.razor` | `components/players/GameResourcesHeader.tsx` | |
| **Resources** | | |
| `Components/Resources/ResourceTracking.razor` | `components/resources/ResourceTracking.tsx` | |
| `Components/Resources/ResourceCard.razor` | `components/resources/ResourceCard.tsx` | |
| `Components/Resources/StarCounter.razor` | `components/resources/StarCounter.tsx` | |
| **Shared** | | |
| `Components/Shared/PurchaseButton.razor` | `components/controls/PurchaseButton.tsx` | 3D flip |
| `Components/Shared/IconButton.razor` | `components/shared/IconButton.tsx` | |

---

## 7. SVG Board Rendering

### 7.1 SVG vs HTML Rationale

**Decision:** Use SVG for all board rendering, not HTML/CSS.

**Why SVG is correct for Catan:**

| Requirement | SVG | HTML/CSS |
|-------------|-----|----------|
| Hexagonal tile geometry | Native polygon/path support | Complex clip-paths, fragile |
| Precise vertex placement | Mathematical coordinate system | Requires JS for positioning |
| Roads along hex edges | SVG `<line>` with exact endpoints | Absolutely positioned divs |
| Robber animation | CSS transform on `<g>` | Same, but coordinate math harder |
| Click-to-coordinate | `getScreenCTM()` → viewport math | Manual offset calculations |
| Scaling | `viewBox` handles everything | Multiple breakpoints required |
| Consistent rendering | Identical across browsers | Flexbox/Grid quirks |

**Key insight:** The Catan board is fundamentally a coordinate system problem. Tiles have Q/R/S
axial coordinates. Buildings sit at tile vertices. Roads connect adjacent vertices. SVG's
mathematical coordinate system maps directly to this model.

HTML/CSS *can* render hexagons (via `clip-path: polygon(...)`) but:

- Click detection requires complex hit testing
- Positioning children (numbers, buildings) requires absolute positioning
- Scaling requires recalculating all positions

The existing Blazor app uses SVG. The Desktop app (WinUI3) uses a Canvas with the same coordinate
math. Both prove this approach works.

### 7.2 Geometry Module

Port `BoardGeometry.cs` exactly to TypeScript:

```typescript
// lib/geometry/boardGeometry.ts
import type { HexCoordinates, HexPosition, HexSide, BuildingKey, RoadKey } from '@/types';

// Constants from BoardSvgConstants.cs
export const BOARD_SVG_CONSTANTS = {
  hexSize: 60,
  centerX: 540,
  centerY: 468,
  viewBoxWidth: 1080,
  viewBoxHeight: 936,
  viewBox: '0 0 1080 936',
  aspectRatio: 1080 / 936,
} as const;

const { hexSize, centerX, centerY } = BOARD_SVG_CONSTANTS;

/** Converts axial hex coordinates to pixel position */
export function axialToPixel(q: number, r: number): { x: number; y: number } {
  const x = hexSize * (3.0 / 2 * q);
  const y = hexSize * (Math.sqrt(3) / 2 * q + Math.sqrt(3) * r);
  return { x: x + centerX, y: y + centerY };
}

/** Converts pixel position to hex coordinates using cube rounding */
export function pixelToHex(px: number, py: number): HexCoordinates {
  const x = px - centerX;
  const y = py - centerY;

  // Fractional axial coordinates
  const q = (2.0 / 3.0) * x / hexSize;
  const r = (-1.0 / 3.0 * x + Math.sqrt(3) / 3.0 * y) / hexSize;

  // Cube coordinates for rounding
  let cubeX = q;
  let cubeZ = r;
  let cubeY = -cubeX - cubeZ;

  // Round
  let rx = Math.round(cubeX);
  let ry = Math.round(cubeY);
  let rz = Math.round(cubeZ);

  // Fix rounding errors
  const xDiff = Math.abs(rx - cubeX);
  const yDiff = Math.abs(ry - cubeY);
  const zDiff = Math.abs(rz - cubeZ);

  if (xDiff > yDiff && xDiff > zDiff) {
    rx = -ry - rz;
  } else if (yDiff > zDiff) {
    ry = -rx - rz;
  } else {
    rz = -rx - ry;
  }

  return { q: rx, r: rz, s: ry };
}

/** Gets hex vertices for a tile at center position */
export function getHexVertices(cx: number, cy: number, size = hexSize): Array<{ x: number; y: number }> {
  const vertices: Array<{ x: number; y: number }> = [];
  for (let i = 0; i < 6; i++) {
    const angle = (Math.PI / 180) * (60 * i);
    vertices.push({
      x: cx + size * Math.cos(angle),
      y: cy + size * Math.sin(angle),
    });
  }
  return vertices;
}

/** Generates SVG path for hexagon */
export function generateHexPath(cx: number, cy: number, size: number): string {
  const vertices = getHexVertices(cx, cy, size);
  const points = vertices.map(v => `${v.x.toFixed(1)},${v.y.toFixed(1)}`);
  return `M ${points[0]} L ${points.slice(1).join(' L ')} Z`;
}

/** Maps HexSide to vertex indices for road endpoints */
export function getEdgeVerticesForSide(side: HexSide): [number, number] {
  const mapping: Record<HexSide, [number, number]> = {
    Top: [4, 5],
    TopRight: [5, 0],
    BottomRight: [0, 1],
    Bottom: [1, 2],
    BottomLeft: [2, 3],
    TopLeft: [3, 4],
  };
  return mapping[side];
}

/** Maps HexPosition to vertex index */
export function getVertexIndex(position: HexPosition): number {
  const mapping: Record<HexPosition, number> = {
    Right: 0,
    BottomRight: 1,
    BottomLeft: 2,
    Left: 3,
    TopLeft: 4,
    TopRight: 5,
  };
  return mapping[position];
}

/** Gets pixel position for a building vertex */
export function getBuildingPosition(key: BuildingKey): { x: number; y: number } {
  const { x: tileX, y: tileY } = axialToPixel(key.hexCoordinates.q, key.hexCoordinates.r);
  const vertices = getHexVertices(tileX, tileY);
  const vertexIndex = getVertexIndex(key.position);
  return vertices[vertexIndex];
}

/** Gets pixel positions for road endpoints */
export function getRoadEndpoints(key: RoadKey): [{ x: number; y: number }, { x: number; y: number }] {
  const { x: tileX, y: tileY } = axialToPixel(key.hexCoordinates.q, key.hexCoordinates.r);
  const vertices = getHexVertices(tileX, tileY);
  const [v1Idx, v2Idx] = getEdgeVerticesForSide(key.side);
  return [vertices[v1Idx], vertices[v2Idx]];
}

/** Converts client coordinates to SVG viewBox coordinates */
export function clientToSvgCoords(
  svgElement: SVGSVGElement,
  clientX: number,
  clientY: number
): { x: number; y: number } | null {
  const point = svgElement.createSVGPoint();
  point.x = clientX;
  point.y = clientY;

  const ctm = svgElement.getScreenCTM();
  if (!ctm) return null;

  const svgPoint = point.matrixTransform(ctm.inverse());
  return { x: svgPoint.x, y: svgPoint.y };
}
```

### Board Component Structure

```tsx
// components/board/BoardContainer.tsx
'use client';

import { useRef, useCallback } from 'react';
import { useTiles, useBuildings, useRoads, useRobber } from '@/stores/gameStore';
import { useUIStore } from '@/stores/uiStore';
import { SvgDefinitions } from './SvgDefinitions';
import { BaseLayer } from './BaseLayer';
import { RoadsLayer } from './RoadsLayer';
import { BuildingsLayer } from './BuildingsLayer';
import { RobberLayer } from './RobberLayer';
import { BOARD_SVG_CONSTANTS, clientToSvgCoords, pixelToHex } from '@/lib/geometry/boardGeometry';
import type { HexCoordinates, BuildingKey, RoadKey } from '@/types';

interface BoardContainerProps {
  onTileClick?: (coordinates: HexCoordinates) => void;
  onBuildingClick?: (key: BuildingKey) => void;
  onRoadClick?: (key: RoadKey) => void;
}

export function BoardContainer({ onTileClick, onBuildingClick, onRoadClick }: BoardContainerProps) {
  const tiles = useTiles();
  const buildings = useBuildings();
  const roads = useRoads();
  const robber = useRobber();
  const lastRolledNumber = useUIStore((s) => s.lastRolledNumber);

  const svgRef = useRef<SVGSVGElement>(null);

  const handleSvgClick = useCallback((event: React.MouseEvent<SVGSVGElement>) => {
    if (!svgRef.current || !onTileClick) return;
    const svgCoords = clientToSvgCoords(svgRef.current, event.clientX, event.clientY);
    if (svgCoords) {
      const hexCoords = pixelToHex(svgCoords.x, svgCoords.y);
      onTileClick(hexCoords);
    }
  }, [onTileClick]);

  return (
    <div className="relative w-full" style={{ aspectRatio: BOARD_SVG_CONSTANTS.aspectRatio }}>
      {/* Static layer - rarely re-renders */}
      <svg
        className="absolute inset-0 w-full h-full"
        viewBox={BOARD_SVG_CONSTANTS.viewBox}
        preserveAspectRatio="xMidYMid meet"
      >
        <SvgDefinitions />
        <BaseLayer tiles={tiles} dimmedNumber={lastRolledNumber} />
      </svg>

      {/* Interactive layer */}
      <svg
        ref={svgRef}
        className="absolute inset-0 w-full h-full"
        viewBox={BOARD_SVG_CONSTANTS.viewBox}
        preserveAspectRatio="xMidYMid meet"
        onClick={handleSvgClick}
      >
        <RoadsLayer roads={roads} onClick={onRoadClick} />
        <BuildingsLayer buildings={buildings} onClick={onBuildingClick} />
        {robber && <RobberLayer robber={robber} />}
      </svg>
    </div>
  );
}
```

---

## 8. Animations (Framer Motion)

### 3D Flip Card (PurchaseButton, PlayerCard)

```tsx
// components/controls/PurchaseButton.tsx
'use client';

import { useState } from 'react';
import { motion } from 'framer-motion';
import type { Entitlement } from '@/types';
import { cn } from '@/lib/utils';

interface PurchaseButtonProps {
  entitlement: Entitlement;
  count: number;
  canPurchase: boolean;
  onClick: () => void;
}

export function PurchaseButton({ entitlement, count, canPurchase, onClick }: PurchaseButtonProps) {
  const [isFlipped, setIsFlipped] = useState(false);

  const handleClick = () => {
    if (!canPurchase) return;
    setIsFlipped(true);
    setTimeout(() => onClick(), 150);
    setTimeout(() => setIsFlipped(false), 600);
  };

  return (
    <div className="perspective-1000 w-20 h-28">
      <motion.div
        className={cn(
          'relative w-full h-full cursor-pointer',
          !canPurchase && 'opacity-50 cursor-not-allowed'
        )}
        style={{ transformStyle: 'preserve-3d' }}
        animate={{ rotateY: isFlipped ? 180 : 0 }}
        transition={{ duration: 0.6, ease: [0.4, 0, 0.2, 1] }}
        onClick={handleClick}
      >
        {/* Front face */}
        <div
          className="absolute inset-0 backface-hidden rounded-lg bg-gradient-to-br from-amber-700 to-amber-900
                     flex flex-col items-center justify-center text-white border-2 border-amber-600"
        >
          <EntitlementIcon entitlement={entitlement} className="w-8 h-8" />
          <span className="text-xs mt-1">{entitlement}</span>
          {count > 0 && (
            <span className="absolute bottom-1 right-1 text-lg font-bold">{count}</span>
          )}
        </div>

        {/* Back face */}
        <div
          className="absolute inset-0 backface-hidden rounded-lg bg-gradient-to-br from-slate-700 to-slate-900
                     flex items-center justify-center text-white border-2 border-slate-600"
          style={{ transform: 'rotateY(180deg)' }}
        >
          <span className="text-3xl font-bold">{count}</span>
        </div>
      </motion.div>
    </div>
  );
}
```

### Winner Celebration

```tsx
// components/celebrations/WinnerCelebration.tsx
'use client';

import { motion, AnimatePresence } from 'framer-motion';
import { useUIStore } from '@/stores/uiStore';

export function WinnerCelebration() {
  const { showWinnerCelebration, winnerName, hideWinner } = useUIStore();

  return (
    <AnimatePresence>
      {showWinnerCelebration && (
        <motion.div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/80"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          onClick={hideWinner}
        >
          {/* Confetti */}
          {Array.from({ length: 100 }).map((_, i) => (
            <motion.div
              key={i}
              className="absolute w-3 h-6"
              style={{
                left: `${Math.random() * 100}%`,
                backgroundColor: ['#FF0000', '#FFD700', '#00FF00', '#0066FF', '#FF00FF'][i % 5],
              }}
              initial={{ y: -20, rotate: 0, opacity: 1 }}
              animate={{ y: '100vh', rotate: 720, opacity: 0 }}
              transition={{
                duration: Math.random() * 2 + 3,
                delay: Math.random() * 2,
                ease: 'linear',
              }}
            />
          ))}

          {/* Trophy and text */}
          <div className="text-center z-10">
            <motion.div
              className="text-8xl mb-4"
              animate={{ scale: [1, 1.1, 1], rotate: [-5, 5, -5] }}
              transition={{ duration: 1, repeat: Infinity }}
            >
              🏆
            </motion.div>
            <motion.div
              className="text-4xl font-bold text-white"
              initial={{ scale: 0 }}
              animate={{ scale: 1 }}
              transition={{ delay: 0.3, type: 'spring' }}
            >
              {winnerName}
            </motion.div>
            <motion.div
              className="text-6xl font-bold text-yellow-400 mt-2"
              initial={{ scale: 0 }}
              animate={{ scale: 1 }}
              transition={{ delay: 0.5, type: 'spring' }}
            >
              WINS!
            </motion.div>
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
```

### Robber Movement (CSS Transition)

```tsx
// components/board/RobberLayer.tsx
'use client';

import { useMemo } from 'react';
import { axialToPixel } from '@/lib/geometry/boardGeometry';
import type { RobberModel } from '@/types';

interface RobberLayerProps {
  robber: RobberModel;
}

export function RobberLayer({ robber }: RobberLayerProps) {
  const position = useMemo(
    () => axialToPixel(robber.coordinates.q, robber.coordinates.r),
    [robber.coordinates]
  );

  return (
    <g
      className="transition-transform duration-[1200ms] ease-[cubic-bezier(0.22,1,0.36,1)]"
      style={{ transform: `translate(${position.x}px, ${position.y}px)` }}
    >
      <circle r={20} fill="#1a1a1a" stroke="#666" strokeWidth={2} />
      <text textAnchor="middle" dy="0.35em" fill="white" fontSize={12} fontWeight="bold">
        🦹
      </text>
    </g>
  );
}
```

---

## 9. Tailwind Configuration

```typescript
// tailwind.config.ts
import type { Config } from 'tailwindcss';

export default {
  content: [
    './app/**/*.{js,ts,jsx,tsx}',
    './components/**/*.{js,ts,jsx,tsx}',
  ],
  theme: {
    extend: {
      // Match CSS custom properties from app.css
      colors: {
        game: {
          'bg-primary': '#222',
          'bg-secondary': '#333',
          'bg-panel': '#2a2a2a',
        },
        text: {
          primary: '#eee',
          secondary: '#ccc',
          muted: '#999',
        },
        accent: {
          primary: '#007bff',
          hover: '#0056b3',
          success: '#4CAF50',
          error: '#f44336',
        },
      },
      fontFamily: {
        catan: ['Catan', 'sans-serif'],
      },
      // For 3D transforms
      perspective: {
        '1000': '1000px',
      },
      backfaceVisibility: {
        hidden: 'hidden',
        visible: 'visible',
      },
    },
  },
  plugins: [
    // Custom plugin for backface-visibility
    function({ addUtilities }: { addUtilities: Function }) {
      addUtilities({
        '.backface-hidden': { 'backface-visibility': 'hidden' },
        '.backface-visible': { 'backface-visibility': 'visible' },
        '.perspective-1000': { perspective: '1000px' },
        '.preserve-3d': { 'transform-style': 'preserve-3d' },
      });
    },
  ],
} satisfies Config;
```

### Global Styles

```css
/* styles/globals.css */
@tailwind base;
@tailwind components;
@tailwind utilities;

@font-face {
  font-family: 'Catan';
  src: url('/fonts/Catan.ttf') format('truetype');
  font-weight: normal;
  font-style: normal;
}

@layer base {
  :root {
    /* Viewport scaling (from viewportScaler.js) */
    --landscape-width: 1920px;
    --landscape-height: 1080px;
    --portrait-width: 1080px;
    --portrait-height: 1920px;

    /* Layout proportions */
    --left-panel-width: 25fr;
    --center-panel-width: 60fr;
    --right-panel-width: 26fr;
  }

  body {
    @apply bg-game-bg-primary text-text-primary;
  }
}

@layer utilities {
  .catan-icon {
    font-family: 'Catan', sans-serif;
  }

  /* Catan building icons */
  .icon-settlement::before { content: '\E926'; }
  .icon-city::before { content: '\E900'; }
}
```

---

## 10. Responsive Design

### 10.1 Layout Diagrams

**Landscape Mode (desktop, tablet landscape):**

```text
┌─────────────────────────────────────────────────────────────────────────────────┐
│                                    NavMenu                                       │
├──────────────────┬──────────────────────────────────────┬───────────────────────┤
│                  │                                      │                       │
│   ResourcePanel  │            BoardContainer            │    PlayersPanel       │
│                  │                                      │                       │
│   ┌───────────┐  │         ╱╲      ╱╲      ╱╲          │   ┌─────────────┐    │
│   │ Wood: 5   │  │       ╱    ╲  ╱    ╲  ╱    ╲        │   │ PlayerTile  │    │
│   │ Brick: 3  │  │      │  8   ││  6   ││  5   │       │   │ Alice: 5pts │    │
│   │ Sheep: 4  │  │       ╲    ╱  ╲    ╱  ╲    ╱        │   └─────────────┘    │
│   │ Wheat: 2  │  │         ╲╱      ╲╱      ╲╱          │   ┌─────────────┐    │
│   │ Ore: 1    │  │       ╱╲      ╱╲      ╱╲            │   │ PlayerTile  │    │
│   └───────────┘  │      │  9   ││ 10  ││  4   │        │   │ Bob: 3pts   │    │
│                  │       ╲    ╱  ╲    ╱  ╲    ╱         │   └─────────────┘    │
│   ┌───────────┐  │         ╲╱      ╲╱      ╲╱          │   ┌─────────────┐    │
│   │ RollGrid  │  │                                      │   │ PlayerTile  │    │
│   │[2-12 btns]│  │                                      │   │ Charlie:2pt │    │
│   └───────────┘  │                                      │   └─────────────┘    │
│                  │                                      │                       │
│   ┌───────────┐  │                                      │                       │
│   │ Purchase  │  │                                      │                       │
│   │ Buttons   │  │                                      │                       │
│   │ (3x2 grid)│  │                                      │                       │
│   └───────────┘  │                                      │                       │
│                  │                                      │                       │
├──────────────────┴──────────────────────────────────────┴───────────────────────┤
│              [Undo]              [Next]              [Redo]                      │
└─────────────────────────────────────────────────────────────────────────────────┘

Width proportions: 25fr / 60fr / 26fr (≈23% / 54% / 23%)
```

**Portrait Mode (phone, tablet portrait):**

```text
┌──────────────────────────────┐
│           NavMenu            │
├──────────────────────────────┤
│                              │
│                              │
│      Active Tab Content      │
│                              │
│   ┌──────────────────────┐   │
│   │ Tab: Board           │   │
│   │ ╱╲     ╱╲     ╱╲     │   │
│   ││ 8 │ │ 6 │ │ 5 │    │   │
│   │ ╲╱     ╲╱     ╲╱     │   │
│   │                      │   │
│   │ (full-width board)   │   │
│   │                      │   │
│   └──────────────────────┘   │
│                              │
│   - OR -                     │
│                              │
│   ┌──────────────────────┐   │
│   │ Tab: Controls        │   │
│   │ Resources + Roll +   │   │
│   │ Purchase Buttons     │   │
│   └──────────────────────┘   │
│                              │
│   - OR -                     │
│                              │
│   ┌──────────────────────┐   │
│   │ Tab: Players         │   │
│   │ Vertical list of     │   │
│   │ PlayerTiles          │   │
│   └──────────────────────┘   │
│                              │
├──────────────────────────────┤
│  🎲 Board │ 🎮 Ctrl │ 👥 Plyr │
└──────────────────────────────┘
        (Portrait Tabs)
```

### 10.2 Viewport Scaler Hook

```typescript
// hooks/useViewportScaler.ts
'use client';

import { useEffect, useCallback, useRef } from 'react';
import { useUIStore } from '@/stores/uiStore';

const LANDSCAPE_WIDTH = 1920;
const LANDSCAPE_HEIGHT = 1080;
const PORTRAIT_WIDTH = 1080;
const PORTRAIT_HEIGHT = 1920;
const PORTRAIT_THRESHOLD = 4 / 3;

export function useViewportScaler() {
  const containerRef = useRef<HTMLDivElement>(null);
  const setOrientation = useUIStore((s) => s.setOrientation);
  const setViewportScale = useUIStore((s) => s.setViewportScale);

  const updateScale = useCallback(() => {
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const viewportAspect = viewportWidth / viewportHeight;

    const isPortrait = viewportAspect < PORTRAIT_THRESHOLD;
    const isMobile = window.matchMedia('(pointer: coarse)').matches || viewportWidth <= 1024;

    const ref = isPortrait
      ? { width: PORTRAIT_WIDTH, height: PORTRAIT_HEIGHT }
      : { width: LANDSCAPE_WIDTH, height: LANDSCAPE_HEIGHT };

    const scaleX = viewportWidth / ref.width;
    const scaleY = viewportHeight / ref.height;
    const scale = isMobile ? scaleX : Math.min(scaleX, scaleY);

    setOrientation(isPortrait, isMobile);
    setViewportScale(scale);
  }, [setOrientation, setViewportScale]);

  useEffect(() => {
    updateScale();
    window.addEventListener('resize', updateScale);
    window.addEventListener('orientationchange', updateScale);
    return () => {
      window.removeEventListener('resize', updateScale);
      window.removeEventListener('orientationchange', updateScale);
    };
  }, [updateScale]);

  return { containerRef, updateScale };
}
```

### Portrait Tab Switching

```tsx
// components/layout/PortraitTabs.tsx
'use client';

import { useUIStore } from '@/stores/uiStore';
import { cn } from '@/lib/utils';

type Tab = 'board' | 'controls' | 'players';

const tabs: { id: Tab; label: string; icon: string }[] = [
  { id: 'board', label: 'Board', icon: '🎲' },
  { id: 'controls', label: 'Controls', icon: '🎮' },
  { id: 'players', label: 'Players', icon: '👥' },
];

export function PortraitTabs() {
  const { activePortraitTab, setActiveTab, isPortrait } = useUIStore();

  if (!isPortrait) return null;

  return (
    <div className="fixed bottom-0 left-0 right-0 h-16 bg-game-bg-secondary border-t border-gray-700
                    flex justify-around items-center z-40">
      {tabs.map((tab) => (
        <button
          key={tab.id}
          onClick={() => setActiveTab(tab.id)}
          className={cn(
            'flex flex-col items-center justify-center w-full h-full transition-colors',
            activePortraitTab === tab.id
              ? 'text-accent-primary bg-game-bg-panel'
              : 'text-text-muted hover:text-text-secondary'
          )}
        >
          <span className="text-2xl">{tab.icon}</span>
          <span className="text-xs mt-1">{tab.label}</span>
        </button>
      ))}
    </div>
  );
}
```

---

## 11. Testing Strategy

### 11.1 Storybook Component Isolation

Storybook enables development and testing of components in isolation, independent of SignalR or
API connections.

**Configuration (`.storybook/main.ts`):**

```typescript
import type { StorybookConfig } from '@storybook/nextjs';

const config: StorybookConfig = {
  stories: ['../stories/**/*.stories.@(js|jsx|ts|tsx)'],
  addons: [
    '@storybook/addon-essentials',
    '@storybook/addon-interactions',
    '@chromatic-com/storybook', // Visual regression
  ],
  framework: {
    name: '@storybook/nextjs',
    options: {},
  },
  staticDirs: ['../public'],
};

export default config;
```

**Example Story:**

```typescript
// stories/PlayerTile.stories.tsx
import type { Meta, StoryObj } from '@storybook/react';
import { PlayerTile } from '@/components/players/PlayerTile';
import playerFixture from '@/test-fixtures/player-alice.json';

const meta: Meta<typeof PlayerTile> = {
  title: 'Players/PlayerTile',
  component: PlayerTile,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof PlayerTile>;

export const Default: Story = {
  args: {
    player: playerFixture,
    isCurrentPlayer: false,
  },
};

export const CurrentPlayer: Story = {
  args: {
    player: playerFixture,
    isCurrentPlayer: true,
  },
};

export const HighScore: Story = {
  args: {
    player: { ...playerFixture, score: 10 },
    isCurrentPlayer: false,
  },
};
```

### 11.2 Replay-Driven Visual Testing

The existing recording system (`Tests/Data/*.json`) provides game state snapshots that can drive
visual regression tests.

**Strategy:**

1. Export game state JSON at key moments (mid-game, robber moved, winner declared)
2. Load fixtures into Storybook stories
3. Use Playwright to capture screenshots
4. Compare against baseline images

**Test Fixture Structure (`test-fixtures/`):**

```text
test-fixtures/
├── game-start.json           # Initial board state
├── mid-game-regular.json     # Typical mid-game with buildings/roads
├── robber-on-wheat.json      # Robber positioned on specific tile
├── winner-declared.json      # GameOver state with winner
├── supplemental-active.json  # Supplemental round state
└── player-alice.json         # Single player model for component tests
```

**Playwright Visual Test:**

```typescript
// tests/visual/board.spec.ts
import { test, expect } from '@playwright/test';
import gameFixture from '../../test-fixtures/mid-game-regular.json';

test.describe('Board Visual Regression', () => {
  test('mid-game board renders correctly', async ({ page }) => {
    // Inject fixture into store (via test route or mocked SignalR)
    await page.goto('/test/visual?fixture=mid-game-regular');

    await expect(page.locator('[data-testid="game-board"]')).toBeVisible();

    // Screenshot comparison
    await expect(page).toHaveScreenshot('mid-game-board.png', {
      maxDiffPixels: 100,
    });
  });

  test('robber renders on correct tile', async ({ page }) => {
    await page.goto('/test/visual?fixture=robber-on-wheat');

    const robber = page.locator('[data-testid="robber"]');
    await expect(robber).toBeVisible();
    await expect(page).toHaveScreenshot('robber-on-wheat.png');
  });
});
```

**Package.json Scripts:**

```json
{
  "scripts": {
    "storybook": "storybook dev -p 6006",
    "build-storybook": "storybook build",
    "test:visual": "playwright test --project=visual",
    "test:visual:update": "playwright test --project=visual --update-snapshots"
  }
}
```

### 11.3 Unit Tests (Vitest + React Testing Library)

```typescript
// Example: lib/geometry/boardGeometry.test.ts
import { describe, it, expect } from 'vitest';
import { axialToPixel, pixelToHex, getHexVertices } from './boardGeometry';

describe('BoardGeometry', () => {
  describe('axialToPixel', () => {
    it('converts center hex (0,0) to center position', () => {
      const { x, y } = axialToPixel(0, 0);
      expect(x).toBe(540);
      expect(y).toBe(468);
    });
  });

  describe('pixelToHex', () => {
    it('is inverse of axialToPixel', () => {
      const testCases = [{ q: 0, r: 0 }, { q: 1, r: 0 }, { q: -1, r: 1 }];
      for (const { q, r } of testCases) {
        const { x, y } = axialToPixel(q, r);
        const result = pixelToHex(x, y);
        expect(result.q).toBe(q);
        expect(result.r).toBe(r);
      }
    });
  });

  describe('getHexVertices', () => {
    it('returns 6 equidistant vertices', () => {
      const vertices = getHexVertices(100, 100, 60);
      expect(vertices).toHaveLength(6);
      for (const v of vertices) {
        const dist = Math.sqrt((v.x - 100) ** 2 + (v.y - 100) ** 2);
        expect(dist).toBeCloseTo(60, 5);
      }
    });
  });
});
```

### E2E Tests (Playwright)

```typescript
// tests/e2e/newGame.spec.ts
import { test, expect } from '@playwright/test';

test.describe('New Game Flow', () => {
  test('creates a new regular game with 3 players', async ({ page }) => {
    await page.goto('/new-game');

    await page.click('[data-testid="game-type-regular"]');
    await page.fill('[data-testid="player-1-name"]', 'Alice');
    await page.fill('[data-testid="player-2-name"]', 'Bob');
    await page.fill('[data-testid="player-3-name"]', 'Charlie');
    await page.click('[data-testid="start-game-button"]');

    await expect(page).toHaveURL(/\/game\/[a-zA-Z0-9-]+/);
    await expect(page.locator('[data-testid="game-board"]')).toBeVisible();
    await expect(page.locator('[data-testid="player-tile"]')).toHaveCount(3);
  });

  test('portrait mode shows tab navigation', async ({ page }) => {
    await page.setViewportSize({ width: 600, height: 900 });
    await page.goto('/game/test-id');

    await expect(page.locator('[data-testid="portrait-tabs"]')).toBeVisible();
  });
});
```

---

## 12. Migration Phases

### Phase 1: Foundation

**Goal:** Project scaffolding and core infrastructure

**Deliverables:**

1. Initialize Next.js 15 project with TypeScript in `ReactUi/`
2. Configure Tailwind CSS with custom theme
3. Set up NSwag type generation pipeline
4. Create folder structure per Section 1
5. Implement Zustand stores (gameStore, uiStore)
6. Create SignalR connection factory and GameHubClient
7. Create REST API client with typed endpoints
8. Port BoardGeometry from C#

**Files Created:**

- `ReactUi/package.json`
- `ReactUi/tsconfig.json`
- `ReactUi/next.config.js`
- `ReactUi/tailwind.config.ts`
- `ReactUi/nswag.json`
- `ReactUi/stores/*.ts`
- `ReactUi/lib/signalr/*.ts`
- `ReactUi/lib/api/*.ts`
- `ReactUi/lib/geometry/boardGeometry.ts`
- `ReactUi/types/*.ts`

**Acceptance Criteria:**

- [ ] `npm run dev` starts Next.js dev server
- [ ] `npm run generate-types` creates TypeScript types from running GameService
- [ ] SignalR connection successfully joins a game
- [ ] REST API can create a new game

---

### Phase 2: Layout & Navigation

**Goal:** App shell with responsive framework

**Deliverables:**

1. Root layout with dark theme
2. NavMenu component (hamburger sidebar)
3. Viewport scaler hook
4. Portrait tabs component
5. Home page
6. New Game page with form

**Files Created:**

- `ReactUi/app/layout.tsx`
- `ReactUi/app/page.tsx`
- `ReactUi/app/new-game/page.tsx`
- `ReactUi/components/layout/NavMenu.tsx`
- `ReactUi/components/layout/PortraitTabs.tsx`
- `ReactUi/hooks/useViewportScaler.ts`
- `ReactUi/styles/globals.css`

**Acceptance Criteria:**

- [ ] Navigation works between pages
- [ ] Hamburger menu opens/closes
- [ ] Layout switches between landscape/portrait
- [ ] New game form submits and redirects to game page

---

### Phase 3: Game Board

**Goal:** Complete SVG board rendering

**Deliverables:**

1. BoardContainer with layered SVGs
2. SvgDefinitions (gradients for player colors)
3. BaseLayer (hexagonal tiles with numbers)
4. TilesLayer (click handling)
5. RoadsLayer (road rendering)
6. BuildingsLayer (settlements/cities)
7. RobberLayer with movement animation
8. GoldTilesLayer
9. Click-to-coordinate conversion

**Files Created:**

- `ReactUi/components/board/BoardContainer.tsx`
- `ReactUi/components/board/SvgDefinitions.tsx`
- `ReactUi/components/board/BaseLayer.tsx`
- `ReactUi/components/board/TilesLayer.tsx`
- `ReactUi/components/board/RoadsLayer.tsx`
- `ReactUi/components/board/BuildingsLayer.tsx`
- `ReactUi/components/board/RobberLayer.tsx`
- `ReactUi/components/board/GoldTilesLayer.tsx`

**Acceptance Criteria:**

- [ ] Board renders correctly for regular game (19 tiles)
- [ ] Tile numbers display correctly
- [ ] Roads render in correct positions
- [ ] Buildings render at vertices
- [ ] Robber animates when moved
- [ ] Clicking tile returns correct coordinates

---

### Phase 4: Player Components

**Goal:** Complete player UI

**Deliverables:**

1. PlayersPanel container
2. PlayerTile (full stats display)
3. PlayerCard with 3D flip (GoFirst, Supplemental, VP modes)
4. GameResourcesHeader
5. ResourceTracking row
6. ResourceCard with flip animation
7. StarCounter slider

**Files Created:**

- `ReactUi/components/players/PlayersPanel.tsx`
- `ReactUi/components/players/PlayerTile.tsx`
- `ReactUi/components/players/PlayerCard.tsx`
- `ReactUi/components/players/GameResourcesHeader.tsx`
- `ReactUi/components/resources/ResourceTracking.tsx`
- `ReactUi/components/resources/ResourceCard.tsx`
- `ReactUi/components/resources/StarCounter.tsx`

**Acceptance Criteria:**

- [ ] All players display with correct colors
- [ ] Player stats update in real-time
- [ ] 3D flip animations work
- [ ] GoFirst selection works
- [ ] Supplemental participation works
- [ ] Victory points entry works

---

### Phase 5: Game Controls

**Goal:** Interactive game elements

**Deliverables:**

1. PurchaseButton with 3D flip
2. RollGrid (2-12 buttons)
3. GameControls (Undo/Next/Redo)
4. BoardMeasurement component
5. Robber target menu
6. Winner confirmation dialog
7. Winner celebration animation
8. Grief celebration animation

**Files Created:**

- `ReactUi/components/controls/PurchaseButton.tsx`
- `ReactUi/components/controls/RollGrid.tsx`
- `ReactUi/components/controls/GameControls.tsx`
- `ReactUi/components/board/BoardMeasurement.tsx`
- `ReactUi/components/shared/RobberMenu.tsx`
- `ReactUi/components/shared/Modal.tsx`
- `ReactUi/components/celebrations/WinnerCelebration.tsx`
- `ReactUi/components/celebrations/GriefCelebration.tsx`

**Acceptance Criteria:**

- [ ] Purchase buttons flip and send commands
- [ ] Roll buttons work
- [ ] Undo/Redo/Next buttons work
- [ ] Robber menu appears on right-click
- [ ] Winner celebration plays with confetti
- [ ] Grief celebration plays for Dodgy targeting

---

### Phase 6: Secondary Pages

**Goal:** Complete all pages

**Deliverables:**

1. Load Game page (list saved games)
2. Edit Players page
3. Settings page
4. Stats page

**Files Created:**

- `ReactUi/app/load-game/page.tsx`
- `ReactUi/app/edit-players/page.tsx`
- `ReactUi/app/settings/page.tsx`
- `ReactUi/app/stats/page.tsx`

**Acceptance Criteria:**

- [ ] Load game lists available games
- [ ] Can load a saved game
- [ ] Can edit player profiles
- [ ] Settings persist to localStorage
- [ ] Stats display lifetime statistics

---

### Phase 7: Polish & Testing

**Goal:** Production readiness

**Deliverables:**

1. Unit tests for geometry, stores, hooks
2. Component tests for critical UI
3. E2E tests for main user flows
4. Performance optimization
5. Cross-browser testing
6. Mobile device testing
7. Documentation

**Files Created:**

- `ReactUi/tests/unit/*.test.ts`
- `ReactUi/tests/e2e/*.spec.ts`
- `ReactUi/README.md`

**Acceptance Criteria:**

- [ ] Test coverage > 70%
- [ ] No console errors in production build
- [ ] Works in Chrome, Firefox, Safari, Edge
- [ ] Works on iOS Safari and Android Chrome
- [ ] Lighthouse score > 90

---

## 13. Implementation Notes

These notes address common pitfalls identified during design review.

### 13.1 Strict "No-Port" Rule Enforcement

When implementing `lib/utils/modelUtils.ts`, developers may be tempted to port "just one more"
helper (e.g., `canPurchaseRoad`, `hasEnoughResources`).

**Rule:** If the valid state is determined by the Server (via `ActionFlags`), **never** port the
logic. Only port geometry/rendering helpers.

```typescript
// WRONG - Don't reimplement server logic
function canPurchaseRoad(player: PlayerModel): boolean {
  return player.resources.wood >= 1 && player.resources.brick >= 1;
}

// RIGHT - Use ActionFlags from server
const canPurchase = useGameStore((s) => s.gameModel?.actionFlags.canPurchaseRoad);
```

### 13.2 SignalR Message Types

The `types/messages.ts` file must strictly align with `Catan3.Shared/Models/MessageObjects.cs`.

**Potential Issue:** NSwag may struggle with polymorphic base classes (common in C# messaging
patterns). If generated types are incomplete:

1. First attempt: Adjust NSwag configuration
2. Fallback: Manually define message types matching C# exactly

```typescript
// types/messages.ts - Manual definitions if NSwag fails
export interface PurchaseMessage {
  entitlement: Entitlement;
  buildingKey?: BuildingKey;
}

export interface RoadPurchaseMessage {
  roadKey: RoadKey;
}

export interface MoveRobberMessage {
  coordinates: HexCoordinates;
  targetPlayerId?: string;
}
```

### 13.3 ESLint Configuration for Generated Code

Generated TypeScript files should be excluded from strict linting rules.

**`.eslintignore`:**

```text
# Generated files
types/generated/
```

**Why:** NSwag-generated code may not conform to project style rules (naming conventions,
explicit return types, etc.). Fighting the generator is counterproductive.

### 13.4 Phase 1 Validation Checklist

Before proceeding to Phase 2, verify:

- [ ] `npm run generate-types` succeeds without GameService running
- [ ] `types/generated/api.ts` contains `GameModel`, `PlayerModel`, `TileModel` interfaces
- [ ] Enum types generate as string literal unions (not numeric)
- [ ] Property names are camelCase (matching C# JsonNamingPolicy)

---

## Appendix: Critical Source Files Reference

| Purpose | C# File | TypeScript Equivalent |
|---------|---------|----------------------|
| SignalR Hub | `Catan3.GameService/Hubs/GameHub.cs` | `lib/signalr/gameHub.ts` |
| Game State | `Catan3.Shared/Models/GameModel.cs` | `types/generated/api.ts` |
| Messages | `Catan3.Shared/Models/MessageObjects.cs` | `types/messages.ts` |
| JSON Config | `Catan3.Shared/Utility/JsonHelper.cs` | (NSwag handles this) |
| Hex Geometry | `WebUI/Services/Rendering/BoardGeometry.cs` | `lib/geometry/boardGeometry.ts` |
| CSS Variables | `WebUI/wwwroot/css/app.css` | `tailwind.config.ts` + `globals.css` |
| Main Game Page | `WebUI/Pages/Game.razor` | `app/game/[gameId]/page.tsx` |
