# TypeScript React Port - Implementation Plan

**Last Updated:** 2026-01-22
**Design Document:** `typescript-porting-design.md`
**Target Location:** `react-ui/`

## Current Status Summary

| Phase | Status | Notes |
|-------|--------|-------|
| Phase 0: Foundation | ⚠️ NEEDS REFACTORING | Infrastructure in place, but stores need multi-game architecture update |
| Phase 1: Home + Navigation | ✅ COMPLETE | All pages have navigation, placeholders in place |
| Phase 2: New Game Page | ⬜ IN PROGRESS | Components exist, needs responsive layout + PlayerProfilesStore integration |
| Phase 3-10 | ⬜ NOT STARTED | |

## Architecture Updates (2026-01-22)

**Major Changes:**

1. **Config as Code** (Phase 0.1.1) - No environment variables, all config in `config/index.ts`
2. **Multi-Game Store** (Phase 0.4) - GameStore keyed by `gameId` for reusability
3. **Multi-Viewport Store** (Phase 0.4) - ViewportStore keyed by `viewportId`
4. **PlayerProfilesStore** (Phase 0.4) - Player identity (colors, avatars) separate from game state
5. **Infinite Ocean Architecture** (Phase 3-4) - Dynamic hex ocean, board reveal animation
6. **Tailwind-First** - All styling uses Tailwind utilities, CSS orientation variants

## Overview

This document provides a detailed, page-by-page implementation plan for porting the Blazor WebAssembly
frontend to React + Next.js + TypeScript. Each phase has testable milestones to verify correctness
before proceeding.

The plan follows a "vertical slice" approach: complete one page fully before moving to the next,
rather than building all components horizontally.

---

## Coding Standards

All code must adhere to these standards. Every new file must pass linting before being committed.

### Documentation Requirements

**TSDoc Comments (Required for all exports):**

```typescript
/**
 * Converts axial hex coordinates to pixel coordinates for SVG rendering.
 *
 * @param q - The q coordinate in axial hex system
 * @param r - The r coordinate in axial hex system
 * @param size - The size of each hex in pixels
 * @returns The pixel coordinates as {x, y}
 */
export function axialToPixel(q: number, r: number, size: number): Point {
  // ...
}
```

**Comment Guidelines:**

- Comments describe WHAT the code does, not HOW we got here
- Never use comments as change logs (that's what git is for)
- Avoid obvious comments like `// increment counter` for `counter++`
- Use comments for:
  - Complex algorithms or non-obvious logic
  - Business rules that aren't self-evident
  - Why a particular approach was chosen (when non-obvious)
  - TODO items with context

**Bad Comments (avoid):**

```typescript
// Changed from foo to bar per PR #123
// Fixed bug where x was wrong
// Joe added this on 2024-01-15
```

**Good Comments:**

```typescript
// Use pointy-top orientation for hex grid (industry standard for Catan)
// Early exit: robber cannot be placed on desert during setup phase
```

### Linting Requirements

**Every new file must be lint-clean before commit.** Use the unified lint command:

```bash
# Run all linters and formatters (formats first, then lints)
pwsh ./catan.ps1 lint
```

This command:

1. Lints PowerShell scripts with PSScriptAnalyzer
2. Formats TypeScript/JavaScript with Prettier
3. Runs ESLint with auto-fix, then reports remaining issues
4. Fixes Markdown auto-fixable issues, then reports remaining issues
5. Checks spelling with cspell (uses `cspell.json` dictionary)

For manual control during development:

```bash
# TypeScript/JavaScript only
cd react-ui && npm run format      # Format with Prettier
cd react-ui && npm run lint:fix    # Fix ESLint auto-fixable issues
cd react-ui && npm run lint        # Check for remaining issues

# Markdown only (from repo root)
npx markdownlint-cli "**/*.md" --ignore node_modules --ignore react-ui/node_modules --fix
```

**ESLint Configuration:** The project uses `eslint-config-next` with these expectations:

- No unused variables (prefix with `_` if intentionally unused)
- No `any` types (use `unknown` and narrow, or define proper types)
- Prefer `const` over `let` when variable isn't reassigned
- Use explicit return types on exported functions
- No console.log in production code (use proper logging)

**TypeScript Strict Mode:** `tsconfig.json` enforces:

- `strict: true`
- `noImplicitAny: true`
- `strictNullChecks: true`

### File Organization

```text
react-ui/
├── app/                    # Next.js App Router pages
├── components/             # React components (PascalCase.tsx)
├── lib/                    # Utilities, services, non-React code
│   ├── api/               # REST client code
│   ├── geometry/          # Board geometry calculations
│   ├── services/          # GameServiceProxy, etc.
│   ├── stores/            # Zustand stores
│   └── utils/             # Pure utility functions
├── types/                  # TypeScript type definitions
│   └── generated/
│       └── models/        # TypeGen-generated types (do not edit)
└── hooks/                  # Custom React hooks (useCamelCase.ts)

Catan3.Shared/
├── TypeScript/
│   ├── CatanTypeGenSpec.cs    # Defines which C# types to export
│   └── TypeGenRunner/         # Console app that runs TypeGen 7.0.0
│       ├── TypeGenRunner.csproj
│       └── Program.cs
```

**Naming Conventions:**

- Components: `PascalCase.tsx` (e.g., `PlayerTile.tsx`)
- Hooks: `useCamelCase.ts` (e.g., `useGameConnection.ts`)
- Utilities: `camelCase.ts` (e.g., `boardGeometry.ts`)
- Types: `PascalCase` for interfaces/types, `SCREAMING_SNAKE` for constants
- Generated files: Never edit, regenerate with `pwsh ./catan.ps1 generate-types`

### Pre-Commit Checklist

Before committing any file:

1. [ ] File passes `npm run lint` with no errors
2. [ ] All exported functions have TSDoc comments
3. [ ] No `// TODO` without explanation
4. [ ] No commented-out code blocks
5. [ ] Markdown files pass `npx markdownlint-cli`
6. [ ] `npm run build` succeeds
7. [ ] Any new tests pass with `npm run test:run`

---

## Phase 0: Foundation (Infrastructure)

**Goal:** Establish the development environment and core infrastructure before any UI work.

### 0.1 Project Scaffolding ✅ COMPLETED

**Tasks:**

1. Create `react-ui/` directory
2. Initialize Next.js 15 project with TypeScript:

   ```bash
   npx create-next-app@latest react-ui --typescript --tailwind --eslint --app --no-src-dir
   ```

3. Configure `tsconfig.json` with path aliases (`@/`)
4. Set up `.env.local` with `NEXT_PUBLIC_API_URL=http://localhost:8080`

**Actual Implementation:**

- Next.js 16.1.2 (newer than planned)
- Uses `lib/config.ts` for service URL instead of `.env.local`
- React 19.2.3, TypeScript 5.x

**Test Milestone:** ✅

- [x] `cd react-ui && npm run dev` starts dev server on port 3000
- [x] Browser shows Next.js default page at `http://localhost:3000`

### 0.1.1 Configuration as Code (ConfigService) ⬜ NOT STARTED

**Philosophy:** Config as code with dynamic environment discovery. Use **ONE** environment variable
(`CATAN_HOSTING_ENVIRONMENT`) to select between version-controlled config objects. All values are
explicit, changes require code commit and deployment.

**Tasks:**

1. Create `config/index.ts` with ConfigService pattern:

   ```typescript
   interface Config {
     gameServiceUrl: string;
     apiTimeout: number;
     signalr: {
       reconnectDelay: number;
       maxReconnectAttempts: number;
     };
     board: {
       minZoom: number;
       maxZoom: number;
       defaultZoom: number;
       oceanBufferHexes: number;
     };
     animations: {
       tileFlipDuration: number;
       tileRevealStagger: number;
       robberMoveDuration: number;
     };
   }

   const DEV_CONFIG: Config = {
     gameServiceUrl: 'http://localhost:8080',
     apiTimeout: 30000,
     signalr: {
       reconnectDelay: 5000,
       maxReconnectAttempts: 10,
     },
     board: {
       minZoom: 0.25,
       maxZoom: 3.0,
       defaultZoom: 1.0,
       oceanBufferHexes: 2,
     },
     animations: {
       tileFlipDuration: 400,
       tileRevealStagger: 50,
       robberMoveDuration: 1200,
     },
   };

   const STAGING_CONFIG: Config = {
     ...DEV_CONFIG,
     gameServiceUrl: 'https://staging-catan.example.com',
   };

   const PRODUCTION_CONFIG: Config = {
     ...DEV_CONFIG,
     gameServiceUrl: 'https://catan.example.com',
     apiTimeout: 60000,  // Longer timeout for prod
   };

   type Environment = 'DEV' | 'STAGING' | 'PRODUCTION';

   class ConfigService {
     private static getEnvironment(): Environment {
       const env = process.env.CATAN_HOSTING_ENVIRONMENT as Environment;
       return env || 'DEV';
     }

     private static getConfig(): Config {
       const env = this.getEnvironment();
       switch (env) {
         case 'STAGING': return STAGING_CONFIG;
         case 'PRODUCTION': return PRODUCTION_CONFIG;
         default: return DEV_CONFIG;
       }
     }

     static gameServiceUrl(): string {
       return this.getConfig().gameServiceUrl;
     }

     static apiTimeout(): number {
       return this.getConfig().apiTimeout;
     }

     static signalrConfig() {
       return this.getConfig().signalr;
     }

     static boardConfig() {
       return this.getConfig().board;
     }

     static animationConfig() {
       return this.getConfig().animations;
     }
   }

   export { ConfigService };
   ```

2. Delete `lib/config.ts` and `lib/services/config.ts` (consolidate into config/index.ts)
3. Update all imports to use `import { ConfigService } from '@/config'`
4. Update all usage to call methods: `ConfigService.gameServiceUrl()`

**Deployment:**

```bash
# Dev (default)
npm run build

# Staging
CATAN_HOSTING_ENVIRONMENT=STAGING npm run build

# Production
CATAN_HOSTING_ENVIRONMENT=PRODUCTION npm run build
```

**Test Milestone:**

- [ ] All services use `ConfigService.gameServiceUrl()`
- [ ] Only ONE env var needed (`CATAN_HOSTING_ENVIRONMENT`)
- [ ] Config changes require code commit (version controlled)
- [ ] Dev build works without setting any env vars

### 0.2 Tailwind Configuration ⚠️ PARTIAL

**Tasks:**

1. Create `tailwind.config.ts` with game colors, Catan font, 3D utilities
2. Create `styles/globals.css` with CSS custom properties from Blazor app
3. Copy `Catan.ttf` to `public/fonts/`
4. Define Tailwind utilities for `backface-hidden`, `perspective-1000`, `preserve-3d`
5. Add font preload in `app/layout.tsx` to prevent FOUT
6. **Add portrait/landscape orientation variants** for responsive design

**Actual Implementation:**

- Using **Tailwind v4** with CSS-based `@theme` directive (no `tailwind.config.ts` needed)
- All theme colors defined in `app/globals.css` with `@theme inline { ... }`
- Player colors, resource colors, game backgrounds all configured
- Font preload in `app/layout.tsx` with `next/font/local`

**TODO:**

1. ⚠️ **3D utilities** (backface-hidden, perspective, preserve-3d) - needed for card flips
2. ⚠️ **Orientation variants** - needed for responsive layouts

Add to `app/globals.css`:

```css
/* After @import 'tailwindcss'; */

/* Orientation variants for responsive design */
@custom-variant portrait (orientation: portrait);
@custom-variant landscape (orientation: landscape);

/* 3D transform utilities for card flips and animations */
@layer utilities {
  .backface-hidden {
    backface-visibility: hidden;
  }

  .preserve-3d {
    transform-style: preserve-3d;
  }

  .perspective-1000 {
    perspective: 1000px;
  }

  .rotate-y-180 {
    transform: rotateY(180deg);
  }
}
```

**Usage Examples:**

```tsx
// Responsive layout
<div className="portrait:flex-col landscape:flex-row">...</div>

// Card flip animation
<div className="preserve-3d">
  <div className="backface-hidden rotate-y-180">...</div>
</div>
```

**Test Milestone:**

- [x] Create test page with `<span className="font-catan">ABC</span>`
- [x] Catan font renders correctly (no flash of unstyled text)
- [x] Game background color (`bg-game-bg-primary`) applies
- [ ] Portrait/landscape variants work (test by rotating device/resizing window)
- [ ] 3D utilities work for card flip animations

### 0.3 Type Generation Pipeline (TypeGen) ✅ COMPLETED

**Approach:** Use TypeGen 7.0.0 via a custom runner console app to generate TypeScript interfaces
directly from C# model classes. This is superior to OpenAPI/NSwag because:

- Core game models (GameModel, PlayerModel, etc.) are sent over SignalR as JSON, not REST endpoints
- TypeGen generates types directly from C# classes with full fidelity
- No running server required for generation

**Full Documentation:** See [typegen-design.md](../Catan3.Shared/TypeScript/TypeGenRunner/typegen-design.md)
for complete architecture, configuration, and extension guide.

**Implementation:**

1. **TypeGenRunner console app** at `Catan3.Shared/TypeScript/TypeGenRunner/`
   - References TypeGen 7.0.0 NuGet package
   - Programmatically invokes generator with CatanTypeGenSpec
   - Outputs to `react-ui/types/generated/models/`
   - Generates enum description mappings for UI text

2. **CatanTypeGenSpec** at `Catan3.Shared/TypeScript/CatanTypeGenSpec.cs`
   - Defines all types to export (GameModel, PlayerModel, enums, messages)
   - Uses `AddInterface<T>()` and `AddEnum<T>()` methods

3. **Generated output** at `react-ui/types/generated/models/`
   - 57+ TypeScript files with full type definitions
   - `index.ts` barrel export for easy imports
   - `enum-descriptions.ts` with UI display text from `[Description]` attributes
   - camelCase property names (via PascalCaseToCamelCaseConverter)
   - String literal enums (e.g., `GameState.WaitingForRoll`)

**Usage:**

```bash
pwsh ./catan.ps1 generate-types
```

**Test Milestone:** ✅

- [x] `pwsh ./catan.ps1 generate-types` succeeds
- [x] `types/generated/models/game-model.ts` contains full GameModel interface
- [x] Enum types are string literal unions (e.g., `GameState = 'WaitingForRoll'`)
- [x] Property names are camelCase
- [x] All 57 model files generated with proper imports
- [x] Serialization tests pass (`react-ui/lib/serialization.test.ts`)
- [x] `enum-descriptions.ts` generated with UI text from `[Description]` attributes

### 0.4 Zustand Stores ⚠️ NEEDS UPDATE

**Architecture Principle:** Zustand is the single source of truth for ALL rendering state.
Components subscribe directly to stores using selectors for fine-grained reactivity.

**Store Design:**

1. **GameStore** - Multi-game state keyed by gameId with reference counting

   ```typescript
   interface GameStoreState {
     games: Record<string, GameModel>;      // Multiple games can coexist
     refCounts: Record<string, number>;     // Track component subscriptions

     loadGame: (gameId: string, model: GameModel) => void;
     updateGame: (gameId: string, model: GameModel) => void;
     clearGame: (gameId: string) => void;
   }

   // Implementation with reference counting (prevents race conditions)
   const useGameStore = create<GameStoreState>((set) => ({
     games: {},
     refCounts: {},

     loadGame: (gameId, model) => set(state => ({
       games: { ...state.games, [gameId]: model },
       refCounts: { ...state.refCounts, [gameId]: (state.refCounts[gameId] || 0) + 1 }
     })),

     clearGame: (gameId) => set(state => {
       const newCount = (state.refCounts[gameId] || 1) - 1;
       if (newCount <= 0) {
         // Safe to delete - no more subscribers
         const { [gameId]: _, ...remainingGames } = state.games;
         const { [gameId]: __, ...remainingCounts } = state.refCounts;
         return { games: remainingGames, refCounts: remainingCounts };
       }
       // Still in use by other components
       return { refCounts: { ...state.refCounts, [gameId]: newCount } };
     })
   }));

   // Usage: Only re-renders when specific game's tiles change
   const tiles = useGameStore(state => state.games[gameId]?.tiles ?? []);
   ```

2. **ViewportStore** - Multi-viewport state keyed by viewportId

   ```typescript
   interface ViewportStoreState {
     viewports: Record<string, ViewportState>;
     setViewport: (id: string, viewport: ViewportState) => void;
     pan: (id: string, deltaQ: number, deltaR: number) => void;
     zoom: (id: string, level: number, focalPoint?: Point) => void;
   }

   // Usage: Game page uses gameId, thumbnails use thumb-${gameId}
   const viewport = useViewportStore(state => state.viewports[viewportId] ?? defaultViewport);
   ```

   **Viewport ID Naming Convention:**
   - Main game view: Use `gameId` directly (e.g., `"game-abc123"`)
   - Thumbnails/previews: Use `thumb-${gameId}` pattern (e.g., `"thumb-game-abc123"`)
   - **Critical:** Never reuse viewport IDs across multiple components
   - Each visual instance must have isolated pan/zoom state

   ```tsx
   // Example: Load Game page with preview boards
   <Board gameId="saved-game-123" viewportId="thumb-saved-game-123" readonly />
   <Board gameId="saved-game-456" viewportId="thumb-saved-game-456" readonly />
   // Each thumbnail has independent pan/zoom state
   ```

3. **PlayerProfilesStore** - Player identity (colors, avatars, names)

   ```typescript
   interface PlayerProfilesStoreState {
     profiles: Record<string, PlayerProfile>;  // Keyed by playerId
     isLoading: boolean;
     loadProfiles: () => Promise<void>;
     updateProfile: (playerId: string, updates: Partial<PlayerProfile>) => void;
     createProfile: (profile: PlayerProfile) => void;
     deleteProfile: (playerId: string) => void;
   }

   interface PlayerProfile {
     id: string;
     name: string;
     color: string;        // Hex color
     avatar?: string;      // URL or emoji
     preferences?: { soundEnabled: boolean };
   }

   // Usage: Change color once, updates everywhere (buildings, roads, player tiles)
   const color = usePlayerProfilesStore(state => state.profiles[playerId]?.color ?? '#888');
   ```

4. **UIStore** - UI state (persist middleware)
5. **ConnectionStore** - SignalR connection state

**The Rule:** "One logical game context per component tree" - each page loads its game(s)
into the store with unique IDs and cleans up on unmount.

**Component Pattern:**

```tsx
// Board subscribes directly to stores (NOT passed as props)
function Board({ gameId, viewportId, readonly, onTileClick }: BoardProps) {
  const tiles = useGameStore(state => state.games[gameId]?.tiles ?? []);
  const viewport = useViewportStore(state => state.viewports[viewportId ?? gameId]);
  return <svg>...</svg>;
}
// ...
```

### 0.4.1 State Reconciliation (Structural Sharing) 🆕 ADDED

**Problem:** SignalR deserializes a new object reference for every update.
`newModel.tiles !== oldModel.tiles` even if the data hasn't changed.
This breaks `useGameStore(state => state.games[id].tiles)` optimizations.

**Solution:** Implement a "Reconciliation Layer" in the store update action.
Compare new arrays against old arrays using custom equality functions.
If contents are identical, keep the **OLD** array reference.

**Equality Rules (Operator== Implementation):**

1. **HexCoordinates:**

    ```typescript
    (a, b) => a.q === b.q && a.r === b.r && a.s === b.s
    ```

2. **Tiles (`TileModel`):**

    ```typescript
    (a, b) =>
      coordsEqual(a.tileKey, b.tileKey) &&
      a.resourceTileType === b.resourceTileType &&
      a.number === b.number &&
      a.highlighted === b.highlighted &&
      a.temporarilyGold === b.temporarilyGold
    ```

3. **Roads (`RoadModel`):**

    ```typescript
    (a, b) =>
      coordsEqual(a.roadKey.a, b.roadKey.a) && // Start hex
      coordsEqual(a.roadKey.b, b.roadKey.b) && // End hex
      a.roadState === b.roadState &&
      a.ownerId === b.ownerId
    ```

4. **Buildings (`BuildingModel`):**

    ```typescript
    (a, b) =>
      coordsEqual(a.buildingKey.hex, b.buildingKey.hex) &&
      a.buildingKey.position === b.buildingKey.position &&
      a.buildingState === b.buildingState &&
      a.ownerId === b.ownerId &&
      a.wall === b.wall &&
      a.metropolis === b.metropolis
    ```

5. **Players (`PlayerModel`):**

    ```typescript
    (a, b) =>
      a.id === b.id &&
      a.score === b.score &&
      a.victoryPointCards === b.victoryPointCards &&
      a.hasLongestRoad === b.hasLongestRoad &&
      a.hasLargestArmy === b.hasLargestArmy &&
      a.participatingInSupplemental === b.participatingInSupplemental &&
      resourcesEqual(a.resourcesThisTurn, b.resourcesThisTurn) &&
      // Entitlements (array of strings)
      shallowArrayEqual(a.unspentEntitlements, b.unspentEntitlements) &&
      // Owned Harbors (array of objects)
      deepArrayEqual(a.ownedHarbors, b.ownedHarbors, harborKeysEqual)
    ```

6. **Resources (`ResourcesModel`):**

    ```typescript
    (a, b) =>
      a.brick === b.brick &&
      a.wood === b.wood &&
      a.ore === b.ore &&
      a.sheep === b.sheep &&
      a.wheat === b.wheat &&
      a.goldMine === b.goldMine &&
      a.count === b.count
      // ... check all resource fields
    ```

7. **Robber (`RobberModel`):**

    ```typescript
    (a, b) =>
      coordsEqual(a.coordinates, b.coordinates) &&
      a.movedBy === b.movedBy &&
      a.targeted === b.targeted
    ```

8. **Harbors (`HarborModel`):**

    ```typescript
    (a, b) =>
      // Check key (type/location)
      harborKeysEqual(a.harborKey, b.harborKey) &&
      // Check owner ID (don't recurse into full PlayerModel)
      a.owner?.id === b.owner?.id
    ```

9. **Roll (`RollModel`):**

    ```typescript
    (a, b) =>
       // Turn Roll
       a.turnRollModel.redRoll === b.turnRollModel.redRoll &&
       a.turnRollModel.whiteRoll === b.turnRollModel.whiteRoll &&
       a.turnRollModel.specialDice === b.turnRollModel.specialDice &&
       // Game Roll (stats)
       a.gameRollModel.totalRolls === b.gameRollModel.totalRolls &&
       shallowArrayEqual(a.gameRollModel.rollCounts, b.gameRollModel.rollCounts)
    ```

**Helper Functions:**

- `harborKeysEqual(a, b)`: checks `hexCoordinates`, `side`, and `harborType` (value equality).
- `shallowArrayEqual(a[], b[])`: compares length and primitive elements.
- `deepArrayEqual(a[], b[], comparator)`: compares length and elements using comparator.

**Implementation Strategy:**

Create `lib/utils/reconciliation.ts`:

```typescript
export function reconcileArray<T>(
  oldArray: T[],
  newArray: T[],
  isEqual: (a: T, b: T) => boolean
): T[] {
  if (oldArray === newArray) return oldArray;
  if (oldArray.length !== newArray.length) return newArray;

  // Check valid equality
  for (let i = 0; i < oldArray.length; i++) {
    if (!isEqual(oldArray[i], newArray[i])) {
      return newArray; // Change detected, return new reference
    }
  }

  return oldArray; // No changes, return OLD reference
}

export function reconcileObject<T>(
  oldObj: T | undefined,
  newObj: T,
  isEqual: (a: T, b: T) => boolean
): T {
  if (!oldObj) return newObj;
  if (oldObj === newObj) return oldObj;
  return isEqual(oldObj, newObj) ? oldObj : newObj;
}
```

**Store Update Logic:**

```typescript
updateGame: (gameId, newModel) => {
  set(state => {
    const current = state.games[gameId];
    if (!current) return { games: { ...state.games, [gameId]: newModel } };

    // Structural sharing: keep old references if values match
    const reconciledModel = {
      ...newModel,
      // Arrays
      tiles: reconcileArray(current.tiles, newModel.tiles, areTilesEqual),
      roads: reconcileArray(current.roads, newModel.roads, areRoadsEqual),
      buildings: reconcileArray(current.buildings, newModel.buildings, areBuildingsEqual),
      players: reconcileArray(current.players, newModel.players, arePlayersEqual),
      harbors: reconcileArray(current.harbors, newModel.harbors, areHarborsEqual),
      
      // Objects
      robber: reconcileObject(current.robber, newModel.robber, areRobbersEqual),
      rollModel: reconcileObject(current.rollModel, newModel.rollModel, areRollsEqual),
      resourceRules: reconcileObject(current.resourceRules, newModel.resourceRules, areResourceRulesEqual),
    };

    return { games: { ...state.games, [gameId]: reconciledModel } };
  });
}

**Note on Undo/Redo & Player Identity:**
The `GameModel` contains authoritative game state which is reversible via Undo. However, player metadata like **Colors** and **Avatars** resides in
`PlayerProfilesStore` (client-side or separate service) to persist across games. This separation means `Undo` effectively reverts game mechanics
(resources, board state) without affecting user profile settings, which is the intended behavior.
```

### 0.5 GameServiceProxy (Unified Client) ✅ COMPLETED

**Tasks:**

1. Install SignalR client: `npm install @microsoft/signalr`
2. Create `lib/services/GameServiceProxy.ts` - unified client class that:
   - Manages SignalR connection for real-time updates
   - Sends game commands via REST (`/api/game/action`)
   - Supports both local (`localhost:8080`) and deployed service URLs
3. Create `lib/services/config.ts` - service URL configuration
4. Create `hooks/useGameConnection.ts` - React hook wrapping GameServiceProxy
5. Implement **Page Visibility API** handler for mobile sleep/wake recovery:
   - Listen for `document.visibilitychange` event
   - On `visible`: check connection state, force reconnect if disconnected
   - After reconnect: fetch full game state via REST to catch missed events

**Architecture Note:** The React client uses REST for commands and SignalR for updates.
This differs from the Blazor client (SignalR both directions), but both can play in the
same game because updates broadcast to all clients in the SignalR group.

**Mobile Note:** Standard SignalR auto-reconnect handles network glitches but fails when
the OS suspends the browser (phone sleep/app switch). The visibility handler ensures
immediate recovery when the user returns.

**Actual Implementation:**

- @microsoft/signalr v10.0.0 installed
- `lib/services/GameServiceProxy.ts` - Full implementation with REST + SignalR
- `lib/services/config.ts` - Service URL configuration
- `lib/hooks/useGameConnection.ts` - React hook wrapper
- Integration tests in `GameServiceProxy.integration.test.ts`

**Test Milestone:** ✅

- [x] Start GameService: `pwsh ./catan.ps1 run`
- [x] Create test page that calls `useGameConnection('test-game', 'test-player')`
- [x] Console shows `[GameServiceProxy] Connected` or connection error
- [x] Browser DevTools Network tab shows WebSocket connection
- [x] Create a game via REST, join via SignalR, receive GameStateUpdated
- [ ] Lock phone/switch apps, return - connection recovers automatically (needs verification)

### 0.6 REST API Integration ✅ COMPLETED

**Tasks:**

1. Verify `GameServiceProxy.sendAction()` works for all message types
2. Test game commands: `undo()`, `redo()`, `next()`, `roll()`, `purchase()`
3. Verify SignalR receives `GameStateUpdated` after REST command
4. Verify state re-fetch on reconnect catches missed events

**Actual Implementation:**

- `RecordingPlayer.ts` - Full recording playback system
- Integration tests validate all commands work correctly
- `npm run test:integration` runs full game playback with hash verification

**Test Milestone:** ✅

- [x] Start GameService
- [x] `proxy.createGame({ gameType: 'Regular', playerIds: ['p1', 'p2', 'p3'] })` succeeds
- [x] `proxy.joinGame(gameId)` receives initial GameStateUpdated
- [x] `proxy.roll({ diceValue: 7 })` triggers GameStateUpdated broadcast
- [x] Open Blazor app on same game - both clients receive updates
- [ ] Simulate disconnect, make move in Blazor, reconnect React - state syncs correctly (needs verification)

### 0.7 Geometry Module ✅ COMPLETED

**Tasks:**

1. Create `lib/geometry/boardGeometry.ts`
2. Port all functions from `BoardGeometry.cs`:
   - `axialToPixel`, `pixelToHex`
   - `getHexVertices`, `generateHexPath`
   - `getBuildingPosition`, `getRoadEndpoints`
   - `clientToSvgCoords`
3. Port constants from `BoardSvgConstants.cs`

**Actual Implementation:**

- `lib/geometry/boardGeometry.ts` - Full port from C#
- `lib/geometry/boardConstants.ts` - SVG constants
- `lib/geometry/index.ts` - Barrel export
- Unit tests in `boardGeometry.test.ts`

**Test Milestone:** ✅

- [x] Write unit tests for geometry functions
- [x] `axialToPixel(0, 0)` returns center coordinates (540, 468)
- [x] `pixelToHex(540, 468)` returns `{q: 0, r: 0, s: 0}`
- [x] `getHexVertices(100, 100, 60)` returns 6 vertices at distance 60 from center

### 0.8 Model Utilities ✅ COMPLETED

**Tasks:**

1. Create `lib/utils/modelUtils.ts`
2. Port MUST-PORT extension methods:
   - `getBuildingAliases(key)`
   - `findBuilding(buildings, key)`
   - `getAdjacentTiles(tiles, tile)`
   - `getTileStars(number)`

**Actual Implementation:**

- `lib/utils/modelUtils.ts` - Building/road/tile navigation utilities
- `lib/utils/index.ts` - Barrel export
- Unit tests in `modelUtils.test.ts`

**Test Milestone:** ✅

- [x] `getTileStars(6)` returns 5
- [x] `getTileStars(2)` returns 1
- [x] `getBuildingAliases({ position: 'TopRight', ... })` returns 2 aliases

### 0.9 LayoutStore (LocalStorage Persistence) ⬜ NOT STARTED

**Goal:** Persist viewport state, window dimensions, and UI preferences per-game to LocalStorage.
Users can pan/zoom, refresh (F5), and return to the same view.

**Problem:**

- User pans to view corner of board, zooms to 2.5x
- Hits F5 (refresh) or browser crashes
- Viewport resets to default → frustrating loss of state

**Solution:** LocalStorage-backed LayoutStore stores viewport + window state per `gameId`.

**Store Interface:**

```typescript
interface LayoutStoreState {
  layouts: Record<string, GameLayout>;  // Keyed by gameId
  loadLayout: (gameId: string) => GameLayout | null;
  updateViewport: (gameId: string, viewport: ViewportState) => void;
  updateWindow: (gameId: string, window: WindowState) => void;
  updateActiveTab: (gameId: string, tab: TabName) => void;
  resetLayout: (gameId: string) => void;  // Reset to defaults
}

interface GameLayout {
  gameId: string;
  clientId: string;       // Browser/device identifier (generated once)
  lastUpdated: string;    // ISO timestamp

  viewport: {
    centerQ: number;      // Hex coordinate of viewport center
    centerR: number;
    zoom: number;         // 0.25 - 3.0
  };

  window: {
    left: number;         // WINDOWPLACEMENT structure
    top: number;
    width: number;
    height: number;
    maximized: boolean;
    minimized: boolean;
  };

  activeTab?: 'board' | 'controls' | 'players' | 'me';
}
```

**Auto-Save Logic:**

```typescript
// Debounce: Save 2 seconds after last change
const debouncedSave = debounce((gameId: string, layout: GameLayout) => {
  localStorage.setItem(`catan_layout_${gameId}`, JSON.stringify(layout));
}, 2000);

// Also save on unmount (browser close)
useEffect(() => {
  return () => {
    const layout = useLayoutStore.getState().layouts[gameId];
    if (layout) {
      localStorage.setItem(`catan_layout_${gameId}`, JSON.stringify(layout));
    }
  };
}, [gameId]);
```

**ClientId Generation:**

```typescript
function getOrCreateClientId(): string {
  let clientId = localStorage.getItem('catan_client_id');
  if (!clientId) {
    clientId = crypto.randomUUID();
    localStorage.setItem('catan_client_id', clientId);
  }
  return clientId;
}
```

**Integration with ViewportStore:**

When loading a game:

1. Check if layout exists in LocalStorage for this `gameId`
2. If yes: restore viewport state to ViewportStore
3. If no: use Settings defaults (Phase 9.3)

**Tasks:**

1. Create `lib/stores/layoutStore.ts`
2. Implement LocalStorage read/write with debouncing
3. Generate/store clientId on first use
4. Integrate with ViewportStore (load on mount, save on change)
5. Add window size tracking (track resize events)

**Test Milestone:**

- [ ] Load game, pan/zoom, refresh (F5) → viewport restored
- [ ] Browser crash simulation → layout persists
- [ ] Multiple games have independent layout state
- [ ] Debouncing works (no excessive localStorage writes)
- [ ] ClientId persists across sessions

---

## Phase 1: Home Page + Navigation 🔄 IN PROGRESS

**Goal:** Complete the home page with working navigation as the first vertical slice.

### 1.1 Root Layout ✅ COMPLETED

**Tasks:**

1. Create `app/layout.tsx` with dark theme, Catan font, global styles
2. Set up `<html>` and `<body>` with proper classes
3. Include viewport meta tag for mobile

**Actual Implementation:**

- `app/layout.tsx` with dark theme class, Catan font via `next/font/local`
- `app/globals.css` with Tailwind v4 `@theme` configuration
- Font preload for Catan.ttf

**Test Milestone:** ✅

- [x] Page renders with dark background
- [x] No console errors

### 1.2 NavMenu Component (Hamburger) ✅ COMPLETED

**Tasks:**

1. Create `components/layout/NavMenu.tsx`
2. Implement hamburger icon that toggles sidebar
3. Add navigation links: Home, New Game, Load Game, Edit Players, Settings, Stats
4. Use custom implementation (no shadcn/ui needed)
5. Menu overlay with click-outside to close

**Actual Implementation:**

- `components/layout/NavMenu.tsx` - Context-aware menu (shows different items per page)
- `components/layout/MainLayout.tsx` - Wrapper with hamburger button and overlay
- Font Awesome icons installed and used for all menu items
- CSS styles ported from Blazor WebUI (NavMenu.razor.css, MainLayout.razor.css)

**Test Milestone:**

- [x] Hamburger icon visible in header
- [x] Click hamburger opens sidebar menu
- [x] Menu shows all navigation options
- [x] Click outside menu closes it
- [x] Click menu item navigates and closes menu

### 1.3 Home Page ✅ COMPLETED

**Tasks:**

1. Create `app/page.tsx`
2. Display welcome message and action buttons
3. Buttons: "New Game", "Load Game", "Edit Players", "Statistics"
4. Style to match Blazor home page

**Actual Implementation:**

- `app/page.tsx` fully ported from Blazor Home.razor
- All menu buttons: New Game, Open Game, Edit Players, Stats
- Conditional "Return to Game" button (shown when activeGameId exists)
- Font Awesome icons for all buttons
- CSS styles ported from Blazor (home-container, menu-button, etc.)
- Mobile responsive styles included

**Test Milestone:**

- [x] Home page renders at `/`
- [x] All buttons visible with icons
- [x] "New Game" button navigates to `/new-game`
- [x] All buttons present and navigate to correct routes

### 1.4 Placeholder Pages ✅ COMPLETED

**Tasks:**

1. Create placeholder pages for all routes
2. Each page shows "To Do" badge and description
3. Pages use MainLayout for consistent navigation

**Actual Implementation:**

- `app/new-game/page.tsx` - New Game placeholder
- `app/load-game/page.tsx` - Load Game placeholder
- `app/edit-players/page.tsx` - Edit Players placeholder
- `app/settings/page.tsx` - Settings placeholder
- `app/stats/page.tsx` - Stats placeholder
- All use MainLayout wrapper with hamburger menu
- CSS styles for placeholder pages added to globals.css

**Test Milestone:**

- [x] All routes accessible
- [x] Each shows "To Do" badge
- [x] Back link returns to home
- [x] Hamburger menu works on all pages

### 1.5 Orientation Variants + 3D Transform Utilities ❌ NOT STARTED

**Note:** The React UI uses Tailwind CSS orientation variants (`portrait:` and `landscape:`)
for responsive layouts instead of JavaScript-based viewport scaling. See
`.design/ui/react/responsive-design.md` for the full approach.

**Tasks:**

1. Add orientation custom variants to `react-ui/app/globals.css`:

   ```css
   @custom-variant portrait (orientation: portrait);
   @custom-variant landscape (orientation: landscape);
   ```

2. Add 3D transform utilities for card flip animations (needed for board reveal in Phase 4):

   ```css
   @layer utilities {
     .preserve-3d {
       transform-style: preserve-3d;
     }
     .backface-hidden {
       backface-visibility: hidden;
     }
     .rotate-y-180 {
       transform: rotateY(180deg);
     }
   }
   ```

3. Verify orientation variants work with basic test

**Why Tailwind Instead of JS:**

- Form pages use pure CSS orientation variants (no JS needed)
- Game page uses ViewportStore (Phase 3.2) for pan/zoom state
- No global viewport scale factor needed

**Test Milestone:**

- [ ] `portrait:flex-col` applies in portrait orientation
- [ ] `landscape:flex-row` applies in landscape orientation
- [ ] 3D transform utilities work for card flip demos
- [ ] Browser orientation change triggers Tailwind variant switch

---

## Phase 2: New Game Page 🔄 IN PROGRESS

**Goal:** Complete the new game flow end-to-end.

### 2.1 New Game Page Structure 🔄 IN PROGRESS

**Design Reference:** See `.design/ui/react/responsive-design.md` for New Game page layout pattern.

**Tasks:**

1. Create `app/new-game/page.tsx`
2. Responsive two-column layout using Tailwind orientation variants:

   ```tsx
   <div className="flex portrait:flex-col landscape:flex-row gap-6
                   portrait:max-w-[600px] landscape:max-w-[1400px] mx-auto">
     <div className="landscape:w-1/2 xl:landscape:w-[55%]">
       {/* Game type selector */}
     </div>
     <div className="landscape:w-1/2 xl:landscape:w-[45%] space-y-6">
       {/* Players, options, start button */}
     </div>
   </div>
   ```

3. Portrait: single column, game type above players
4. Landscape: two columns, game type left, players right

**Test Milestone:**

- [ ] Page renders at `/new-game`
- [ ] Shows game type options and player inputs
- [ ] Portrait orientation: single column layout
- [ ] Landscape orientation: two column layout

### 2.2 Game Type Selection

**Tasks:**

1. Create game type radio buttons or cards
2. Options: Regular, Expansion (if supported)
3. Visual selection state (highlighted card)

**Test Milestone:**

- [ ] Click "Regular" highlights that option
- [ ] Click "Expansion" highlights that option
- [ ] Only one option selected at a time

### 2.3 Player Selection

**Tasks:**

1. Load player profiles into PlayerProfilesStore:

   ```tsx
   const profiles = usePlayerProfilesStore(state => state.profiles);
   const loadProfiles = usePlayerProfilesStore(state => state.loadProfiles);

   useEffect(() => {
     loadProfiles(); // Fetches from /api/players and populates store
   }, []);
   ```

2. Display player dropdown/selector for each slot (3-4 players)
3. Show player colors from `profile.color`
4. Add/remove player slots
5. Prevent selecting same player twice

**Test Milestone:**

- [ ] Player list loads from API into PlayerProfilesStore
- [ ] Can select different players for each slot
- [ ] Cannot select same player twice
- [ ] Player colors display correctly from store
- [ ] Changing player color in Edit Players page updates New Game dropdowns

### 2.4 Start Game Flow

**Tasks:**

1. "Start Game" button calls `gameApi.createGame()`
2. On success, navigate to `/game/[gameId]`
3. Show loading state during API call
4. Handle errors gracefully

**Test Milestone:**

- [ ] Click "Start Game" creates new game via REST API
- [ ] Browser navigates to `/game/[gameId]`
- [ ] If API fails, error message displays
- [ ] GameService logs show new game created

---

## Phase 3: Main Game Page - Foundation (Infinite Ocean Architecture)

**Goal:** Establish the game page structure with infinite ocean base layer, floating panels, and
GameServiceProxy connection.

**Design Reference:** See `.design/ui/react/game-page-design.md` for full architecture.

### Architecture Overview

The game page uses a layered z-index architecture:

```text
┌─────────────────────────────────────────────────────────────────────┐
│                         Z-INDEX STACK                               │
├─────────────────────────────────────────────────────────────────────┤
│  z-50: Hamburger Menu + Navigation Overlay                          │
│  z-40: Modal Dialogs (winner celebration, robber target, etc.)      │
│  z-30: Left Panel (Controls) + Right Panel (Players)                │
│  z-20: Portrait Mode Tabs (when visible)                            │
│  z-10: Game Board Tiles (land hexes on the ocean)                   │
│  z-0:  Infinite Hex Ocean (water tiles, base layer)                 │
└─────────────────────────────────────────────────────────────────────┘
```

### 3.1 Game Page Shell + Store Loading

**Tasks:**

1. Create `app/game/[gameId]/page.tsx`:

   ```tsx
   export default function GamePage({ params }: { params: { gameId: string } }) {
     const { gameId } = params;
     const loadGame = useGameStore(state => state.loadGame);
     const setViewport = useViewportStore(state => state.setViewport);

     useEffect(() => {
       // Load game model into store (initially null, then from SignalR)
       loadGame(gameId, null);

       // Initialize viewport for this game
       setViewport(gameId, { centerQ: 0, centerR: 0, zoom: 1.0 });

       return () => {
         // Cleanup on unmount
         clearGame(gameId);
       };
     }, [gameId]);

     return <GameContainer gameId={gameId} />;
   }
   ```

2. Create `components/game/GameContainer.tsx` - manages z-index layers
3. GameContainer renders Board, panels, tabs - all subscribe to store by gameId
4. Implement event isolation: panels use `pointer-events-auto` and `stopPropagation`

**The Pattern:** Page loads game into store, components subscribe by gameId.

**Test Milestone:**

- [ ] Navigate to `/game/[valid-game-id]`
- [ ] Game loads into `store.games[gameId]`
- [ ] Viewport initializes in `store.viewports[gameId]`
- [ ] Page cleanup removes game from store
- [ ] Clicking on panel area doesn't trigger ocean events

### 3.2 Viewport Store + Pan/Zoom State

**Tasks:**

1. Create `lib/stores/viewportStore.ts` with Zustand:

   ```typescript
   interface ViewportStore {
     centerQ: number;
     centerR: number;
     zoom: number;           // 1.0 default, 0.25 min, 3.0 max
     pan: (deltaQ: number, deltaR: number) => void;
     zoomTo: (level: number, focalPoint?: Point) => void;
     resetView: () => void;
     fitBoardToView: () => void;
   }
   ```

2. Create `hooks/useViewport.ts` - React hook for viewport state
3. Create `hooks/usePanGesture.ts` - Mouse drag / touch pan handling
4. Create `hooks/useZoomGesture.ts` - Scroll wheel / pinch zoom handling

**Test Milestone:**

- [ ] Viewport store updates when pan/zoom called
- [ ] Pan gesture (drag on ocean) updates centerQ/centerR
- [ ] Zoom gesture (scroll wheel) updates zoom level
- [ ] Zoom limited to 0.25-3.0 range

### 3.3 Infinite Ocean Layer (Base)

**Tasks:**

1. Create `components/game/OceanLayer.tsx`
2. SVG layer filling entire viewport
3. Dynamically render water hexes based on viewport + zoom level
4. Only render visible hexes + buffer (2 hex margin for smooth panning)
5. Water hexes use "face-down" tile appearance (blue gradient with wave pattern)

**Rendering Strategy:**

```typescript
function getVisibleHexes(viewport: ViewportState): HexCoord[] {
  const buffer = 2;
  // Calculate hex grid bounds based on viewport, zoom, and screen size
  // Return array of hex coordinates to render
}
```

**Test Milestone:**

- [ ] Ocean fills viewport with water hexes
- [ ] Hexes tile seamlessly
- [ ] Panning renders new hexes dynamically
- [ ] Zooming adjusts hex size correctly
- [ ] Performance: No jank during pan/zoom

### 3.4 Floating Panel Containers

**Tasks:**

1. Create `components/game/ControlsPanel.tsx` (left panel)
2. Create `components/game/PlayersPanel.tsx` (right panel)
3. Implement glassmorphism effect with Tailwind utilities:

   ```tsx
   className="z-30 bg-black/60 backdrop-blur-md
              border border-white/10 rounded-lg
              shadow-xl shadow-black/50
              pointer-events-auto overflow-y-auto"
   ```

4. Panels stop event propagation to ocean (React `onPointerDown={e => e.stopPropagation()}`)
5. Landscape mode: panels float on sides with `landscape:fixed landscape:left-4` and `landscape:right-4`
6. Portrait mode: panels hidden, content shown in tabs instead

**Test Milestone:**

- [ ] Left and right panels float over ocean with `z-30`
- [ ] Glassmorphism effect (semi-transparent, blurred backdrop) looks good
- [ ] Mouse events on panels don't trigger ocean pan
- [ ] Panel content scrolls independently with `overflow-y-auto`
- [ ] Ocean visible through panel edges
- [ ] Panels hidden in portrait mode

### 3.5 GameServiceProxy Connection + Store Updates

**Tasks:**

1. Use `useGameConnection(gameId, playerId)` hook:

   ```tsx
   function GameContainer({ gameId }: { gameId: string }) {
     const updateGame = useGameStore(state => state.updateGame);

     useGameConnection(gameId, {
       onGameStateUpdated: (model: GameModel) => {
         // SignalR sends updates -> store them by gameId
         updateGame(gameId, model);
       },
     });

     return <Board gameId={gameId} />;
   }
   ```

2. GameServiceProxy emits events when SignalR receives `GameStateUpdated`
3. Hook listens to events and updates store at `store.games[gameId]`
4. All components subscribed to that gameId automatically re-render
5. Display connection status indicator

**The Flow:**

1. SignalR: GameStateUpdated event
2. GameServiceProxy: Emit event to hooks
3. Hook: `updateGame(gameId, newModel)`
4. Store: `games[gameId] = newModel`
5. Components: Re-render (only if subscribed data changed)

**Test Milestone:**

- [ ] Navigate to game page for existing game
- [ ] Connection status shows "Connected"
- [ ] SignalR events update `store.games[gameId]`
- [ ] Board components re-render when tiles change
- [ ] Player tiles re-render when player resources change
- [ ] Open same game in Blazor app - both see same state

### 3.6 Player ID Management

**Tasks:**

1. Determine which player "you are" (from localStorage or URL param)
2. Store `playerId` in gameStore
3. Pass `playerId` to GameServiceProxy for all operations

**Test Milestone:**

- [ ] Player ID persists across page refreshes
- [ ] GameServiceProxy `joinGame` sends correct playerId

---

## Phase 4: Main Game Page - Board Rendering (on Ocean)

**Goal:** Render game board tiles floating on the infinite ocean, with reveal animation.

**Design Reference:** See `.design/ui/react/game-page-design.md` for board reveal animation specs.

### Component Reusability Principle

**CRITICAL:** Board components subscribe directly to Zustand stores using **gameId** and **viewportId**
as keys. This enables fine-grained reactivity AND reusability:

- Game page: `<Board gameId="abc123" viewportId="abc123" />`
- Load game preview: `<Board gameId="preview-saved-123" viewportId="thumb-preview-saved-123" readonly />`
- Stats page: `<Board gameId="archive-win-456" viewportId="thumb-archive-win-456" readonly />`

**Architecture Pattern:**

```tsx
// ✅ CORRECT - Subscribe to store with selectors
interface BoardProps {
  gameId: string;           // Which game to render (key into store.games)
  viewportId?: string;      // Which viewport to use (defaults to gameId)
  readonly?: boolean;
  onTileClick?: (hex: HexCoord) => void;
  onBuildingClick?: (key: BuildingKey) => void;
}

export function Board({ gameId, viewportId, readonly, onTileClick }: BoardProps) {
  const vpId = viewportId ?? gameId;

  // Fine-grained subscriptions - only re-render when specific arrays change
  const tiles = useGameStore(state => state.games[gameId]?.tiles ?? []);
  const buildings = useGameStore(state => state.games[gameId]?.buildings ?? []);
  const roads = useGameStore(state => state.games[gameId]?.roads ?? []);
  const viewport = useViewportStore(state => state.viewports[vpId] ?? defaultViewport);

  return (
    <svg>
      <OceanLayer viewport={viewport} />
      <TilesLayer tiles={tiles} viewport={viewport} />
      <BuildingsLayer buildings={buildings} viewport={viewport} />
      <RoadsLayer roads={roads} viewport={viewport} />
    </svg>
  );
}

// ❌ WRONG - Don't pass gameModel as prop
export function Board({ gameModel }: { gameModel: GameModel }) {
  // This defeats Zustand's fine-grained reactivity!
}
```

**The Rule:** Pages load games into store with unique IDs, components subscribe by ID.

**Benefits:**

- Zustand's fine-grained reactivity (only re-render what changed)
- Multiple games can coexist in store (previews, thumbnails)
- Components are still reusable (just need a gameId)
- No manual memoization needed

### 4.1 Board Layer Component

**Tasks:**

1. Create `components/game/BoardLayer.tsx` (z-10, above ocean)
2. Position game board tiles on the ocean using viewport coordinates
3. Tiles move with pan/zoom (same transform as ocean)
4. Wrap board in **Error Boundary** - board crash shouldn't kill controls/player panel

**Test Milestone:**

- [ ] Board tiles render positioned on ocean
- [ ] Pan/zoom affects board tiles correctly
- [ ] Intentional error in board layer shows fallback UI, ocean/panels still work

### 4.2 Water Hex Component

**Tasks:**

1. Create `components/board/WaterHex.tsx`
2. Single water hex (face-down appearance)
3. Blue gradient with subtle wave pattern
4. Seamless tiling appearance
5. Used for both ocean layer AND face-down board tiles

**Test Milestone:**

- [ ] Water hex renders with wave texture
- [ ] Tiles appear seamless when adjacent
- [ ] Slight transparency variation for depth effect

### 4.3 Land Hex Component with Flip Animation

**Tasks:**

1. Create `components/board/LandHex.tsx`
2. Two-sided hex: face-down (water) and face-up (resource)
3. 3D flip animation using Tailwind utilities and custom transform
4. States: `face-down`, `face-up`, `dimmed`, `highlighted`
5. Easing: `ease-out` for satisfying snap (400ms duration)

**Tailwind Utilities Needed (add to `globals.css` if not present):**

```css
@layer utilities {
  .preserve-3d {
    transform-style: preserve-3d;
  }
  .backface-hidden {
    backface-visibility: hidden;
  }
  .rotate-y-180 {
    transform: rotateY(180deg);
  }
}
```

**Component Structure:**

```tsx
<g className={cn(
  "preserve-3d transition-transform duration-400 ease-out",
  isFaceDown && "rotate-y-180"
)}>
  {/* Front face (resource) */}
  <g className="backface-hidden">
    <ResourceHex ... />
  </g>
  {/* Back face (water) */}
  <g className="backface-hidden rotate-y-180">
    <WaterHex ... />
  </g>
</g>
```

**Test Milestone:**

- [ ] Hex flips smoothly from face-down to face-up
- [ ] Back side shows water texture
- [ ] Front side shows resource and number
- [ ] Animation feels satisfying (snap at end)
- [ ] Uses Tailwind utilities for all styling

### 4.4 Board Reveal Animation

**Tasks:**

1. Add reveal state to gameStore: `revealedTiles: Set<string>`, `isRevealing: boolean`
2. Create reveal orchestration: `startBoardReveal()` action
3. Implement ripple pattern: tiles flip from center outward
4. Stagger timing: `delay = distanceFromCenter * 50ms`
5. Trigger on game start (first load of new game)

**Configuration:**

```typescript
interface TileRevealConfig {
  pattern: 'ripple' | 'random' | 'spiral' | 'all-at-once';
  delayBetweenTiles: number;  // 50ms default
  flipDuration: number;        // 400ms per tile
}
```

**Test Milestone:**

- [ ] New game shows all tiles face-down initially
- [ ] Center tile flips first, then expands outward
- [ ] Wave effect creates satisfying reveal sequence
- [ ] ~2 seconds for full board reveal

### 4.5 SvgDefinitions Component

**Tasks:**

1. Create `components/board/SvgDefinitions.tsx`
2. Define gradients for each player color
3. Define gradients for resources (wheat, wood, ore, etc.)
4. Define water texture pattern for face-down tiles
5. Define any required patterns

**Test Milestone:**

- [ ] SVG `<defs>` element renders with gradients
- [ ] Water pattern defined for face-down tiles
- [ ] No console warnings about missing definitions

### 4.6 BaseLayer (Tiles)

**Tasks:**

1. Create `components/board/BaseLayer.tsx`
2. Render hexagonal tiles from `tiles` array using LandHex component
3. Position using `axialToPixel()` transformed by viewport
4. Display tile numbers with stars (probability dots)
5. Color tiles by resource type
6. Support dimming non-rolled tiles
7. Support highlighted state for gold tiles, valid placements

**Test Milestone:**

- [ ] All 19 tiles (regular game) render in correct positions
- [ ] Tile numbers display correctly
- [ ] Star dots show probability (more stars = higher probability)
- [ ] After roll, non-matching tiles dim

### 4.7 RoadsLayer

**Tasks:**

1. Create `components/board/RoadsLayer.tsx`
2. Render roads as SVG lines
3. Position using `getRoadEndpoints()` transformed by viewport
4. Color by player owner
5. Show buildable roads when applicable

**Test Milestone:**

- [ ] Roads render at correct edge positions
- [ ] Road colors match player colors
- [ ] Buildable roads have distinct appearance

### 4.8 BuildingsLayer

**Tasks:**

1. Create `components/board/BuildingsLayer.tsx`
2. Render settlements, cities, knights at vertices
3. Position using `getBuildingPosition()` transformed by viewport
4. Handle building aliases (shared vertices)
5. Different icons for settlement vs city vs metropolis

**Test Milestone:**

- [ ] Settlements render as small shapes
- [ ] Cities render as larger shapes
- [ ] Buildings appear at correct vertices
- [ ] Click building triggers callback with BuildingKey

### 4.9 RobberLayer

**Tasks:**

1. Create `components/board/RobberLayer.tsx`
2. Position robber on current tile
3. Animate movement when coordinates change
4. CSS transition for smooth movement

**Test Milestone:**

- [ ] Robber renders on correct tile
- [ ] Moving robber (via API) shows smooth animation
- [ ] Animation duration ~1.2s with easing

### 4.10 GoldTilesLayer

**Tasks:**

1. Create `components/board/GoldTilesLayer.tsx`
2. Highlight gold tiles when picking random gold
3. Show indicator for selected gold tiles

**Test Milestone:**

- [ ] Gold tiles highlight during gold selection phase
- [ ] Non-gold tiles don't show highlight

### 4.11 Tile Click Handling

**Tasks:**

1. Implement `clientToSvgCoords()` for click coordinate conversion
2. Account for viewport pan/zoom in coordinate transform
3. Convert SVG coords to hex coords with `pixelToHex()`
4. Pass clicked tile to parent component

**Test Milestone:**

- [ ] Click on center tile returns `{q: 0, r: 0}`
- [ ] Click on edge tiles returns correct coordinates
- [ ] Works at different zoom levels and pan positions

---

## Phase 5: Main Game Page - Player Panel

**Goal:** Complete the right-side player panel.

### 5.1 PlayersPanel Container

**Tasks:**

1. Create `components/players/PlayersPanel.tsx`
2. Vertical list of PlayerTile components
3. Highlight current player
4. Handle portrait mode (full-width, in Players tab)

**Test Milestone:**

- [ ] All players render in panel
- [ ] Current player has visual distinction
- [ ] Panel scrolls if needed

### 5.2 PlayerTile Component (Dual-Store Pattern)

**Tasks:**

1. Create `components/players/PlayerTile.tsx` using BOTH stores:

   ```tsx
   function PlayerTile({ gameId, playerId }: PlayerTileProps) {
     // Game state: resources, VP, dev cards, achievements from GameStore
     const playerState = useGameStore(state =>
       state.games[gameId]?.players.find(p => p.id === playerId)
     );

     // Visual identity: color, avatar, name from PlayerProfilesStore
     const profile = usePlayerProfilesStore(state => state.profiles[playerId]);

     if (!playerState || !profile) return null;

     return (
       <div
         className="border-2 rounded-lg p-4"
         style={{ borderColor: profile.color }}
       >
         <img src={profile.avatar} alt={profile.name} />
         <h3>{profile.name}</h3>
         <div>VP: {playerState.victoryPoints}</div>
         <ResourceDisplay resources={playerState.resources} />
         {playerState.hasLongestRoad && <Badge>Longest Road</Badge>}
         {playerState.hasLargestArmy && <Badge>Largest Army</Badge>}
       </div>
     );
   }
   ```

2. Display: player name (from profile), score (from state), resources (from state)
3. Player color bar using `profile.color`
4. Current player highlight
5. "Your turn" indicator for local player

**Key Point:** Changing player color in Edit Players page immediately updates all PlayerTiles.

**Test Milestone:**

- [ ] Player name and color come from PlayerProfilesStore
- [ ] Resources and VP come from GameStore
- [ ] Change player color in Edit Players → PlayerTiles update instantly
- [ ] Victory points show correctly
- [ ] Longest road/largest army badges show

### 5.3 PlayerCard (GoFirst Mode)

**Tasks:**

1. Create `components/players/PlayerCard.tsx`
2. Support "GoFirst" mode for roll order
3. 3D flip animation using Tailwind utilities (same pattern as LandHex):
   - `preserve-3d`, `backface-hidden`, `rotate-y-180`
   - Two-sided card: front shows player info, back shows "Go First" prompt
4. Click to flip card and select "go first"

**Test Milestone:**

- [ ] During WaitingForRollForOrder state, cards show "Go First" option
- [ ] Clicking card flips it with smooth 3D animation
- [ ] Selected player highlighted

### 5.4 PlayerCard (Supplemental Mode)

**Tasks:**

1. Add supplemental participation toggle
2. Show participation status
3. Handle PickSupplementalPlayers state

**Test Milestone:**

- [ ] During supplemental state, participation toggle appears
- [ ] Clicking toggle sends ParticipatingInSupplemental message
- [ ] State reflects in UI

### 5.5 PlayerCard (Victory Points Entry)

**Tasks:**

1. Add VP entry mode for development card VPs
2. Number input or +/- buttons
3. Submit VP changes

**Test Milestone:**

- [ ] Can enter development card victory points
- [ ] Changes persist to server
- [ ] Score updates reflect VP changes

---

## Phase 6: Main Game Page - Left Panel (Controls)

**Goal:** Complete the resource tracking and game controls.

### 6.1 ResourceTracking Component

**Tasks:**

1. Create `components/resources/ResourceTracking.tsx`
2. Display current player's resources in a row/grid
3. Resource icons with counts
4. Update reactively from gameStore

**Test Milestone:**

- [ ] Current player's resources display
- [ ] Counts update when resources change
- [ ] Icons match resource types

### 6.2 StarCounter Component

**Tasks:**

1. Create `components/resources/StarCounter.tsx`
2. Slider to adjust resource count
3. Drag or click to set value
4. Increment/decrement buttons

**Test Milestone:**

- [ ] Slider adjusts value
- [ ] +/- buttons work
- [ ] Value persists and updates parent

### 6.3 RollGrid Component

**Tasks:**

1. Create `components/controls/RollGrid.tsx`
2. 11 buttons (2-12) in specific layout
3. Click sends RollMessage
4. Disable when not current player's turn or wrong state
5. Highlight last rolled number

**Test Milestone:**

- [ ] All roll buttons 2-12 visible
- [ ] Clicking sends roll to server
- [ ] Board tiles dim appropriately after roll
- [ ] Last rolled number highlighted

### 6.4 PurchaseButton Component

**Tasks:**

1. Create `components/controls/PurchaseButton.tsx`
2. 3D flip animation on click using Tailwind utilities:
   - Button flips to show "purchasing" feedback
   - Uses `preserve-3d`, `backface-hidden`, `rotate-y-180`
3. Shows entitlement icon and count
4. Disabled state when `actionFlags.canPurchase*` is false:
   - Tailwind: `disabled:opacity-50 disabled:cursor-not-allowed`
5. Sends PurchaseMessage on click

**Test Milestone:**

- [ ] Settlement button shows settlement icon
- [ ] Button disabled when can't afford (grayed out)
- [ ] Click flips card with 3D animation and sends purchase command
- [ ] After purchase, count updates

### 6.5 Purchase Buttons Grid

**Tasks:**

1. Arrange purchase buttons: Settlement, City, Road, Development Card
2. Layout: 2x2 grid or appropriate arrangement
3. Conditionally show expansion buttons (Knight, etc.)

**Test Milestone:**

- [ ] All purchase buttons visible for game type
- [ ] Enable/disable states match actionFlags
- [ ] Expansion game shows additional buttons

### 6.6 GameControls (Undo/Next/Redo)

**Tasks:**

1. Create `components/controls/GameControls.tsx`
2. Undo button - sends Undo message
3. Redo button - sends Redo message
4. Next button - sends Next message
5. Disable based on undo/redo availability

**Test Milestone:**

- [ ] Undo button works and reverts game state
- [ ] Redo button works after undo
- [ ] Next button advances turn
- [ ] Buttons disable appropriately

---

## Phase 7: Main Game Page - Overlays & Modals

**Goal:** Complete modals, menus, and celebration animations.

### 7.1 Robber Target Menu

**Tasks:**

1. Create `components/shared/RobberMenu.tsx`
2. Context menu appears on robber placement
3. Shows targetable players
4. Click player to steal from

**Test Milestone:**

- [ ] After moving robber to occupied tile, menu appears
- [ ] Menu lists players with buildings adjacent to tile
- [ ] Clicking player sends MoveRobber with target
- [ ] Menu closes after selection

### 7.2 Winner Confirmation Dialog

**Tasks:**

1. Create modal dialog for winner confirmation using Tailwind utilities:

   ```tsx
   className="z-40 fixed inset-0 flex items-center justify-center
              bg-black/75 backdrop-blur-sm"
   ```

2. Display winner name and final score
3. "New Game" and "Review" buttons styled with Tailwind

**Test Milestone:**

- [ ] Dialog appears when game ends (z-40, overlays everything except nav)
- [ ] Shows winner information
- [ ] Buttons navigate appropriately
- [ ] Backdrop blur creates focus effect

### 7.3 Winner Celebration Animation

**Tasks:**

1. Create `components/celebrations/WinnerCelebration.tsx`
2. Full-screen overlay using Tailwind:

   ```tsx
   className="z-40 fixed inset-0 pointer-events-none
              flex items-center justify-center"
   ```

3. Confetti animation (100+ particles using JavaScript)
4. Trophy emoji with Tailwind animation utilities:

   ```tsx
   className="text-8xl animate-bounce"
   ```

5. Winner name display with Tailwind transition utilities

**Test Milestone:**

- [ ] Celebration triggers when winner declared
- [ ] Confetti particles fall (custom JS animation)
- [ ] Trophy bounces with `animate-bounce`
- [ ] Click anywhere dismisses

### 7.4 Grief Celebration Animation

**Tasks:**

1. Create `components/celebrations/GriefCelebration.tsx`
2. Triggered when "Dodgy" achievement
3. Different visual (rain, sad emoji, etc.)
4. Auto-dismiss after 6 seconds

**Test Milestone:**

- [ ] Grief animation triggers on Dodgy
- [ ] Different appearance from winner celebration
- [ ] Auto-dismisses after 6 seconds

### 7.5 BoardMeasurement Component

**Tasks:**

1. Create `components/board/BoardMeasurement.tsx`
2. Measure and count tiles, buildings, roads
3. Used for debugging/testing
4. Toggle visibility

**Test Milestone:**

- [ ] Shows tile count
- [ ] Shows building count
- [ ] Shows road count
- [ ] Can toggle visibility

---

## Phase 8: Main Game Page - Portrait Mode

**Goal:** Complete portrait/mobile layout with tabs including new "Me" controller tab.

**Design Reference:** See `.design/ui/react/game-page-design.md` for "Me" tab specs.

### 8.1 Portrait Tabs Component

**Tasks:**

1. Create `components/game/PortraitTabs.tsx`
2. Fixed bottom tab bar (z-20)
3. Four tabs: Board, Controls, Players, Me
4. Active tab indicator
5. Persist active tab in localStorage
6. Hidden in landscape orientation

**Test Milestone:**

- [ ] Tab bar appears in portrait orientation only
- [ ] Clicking tab switches content
- [ ] Active tab highlighted
- [ ] Tab selection persists across refresh
- [ ] Tab bar hidden in landscape

### 8.2 Portrait Board Tab

**Tasks:**

1. Ocean + board fills available space above tab bar
2. Touch gestures work (pan, pinch-to-zoom)
3. Floating panels hidden (use tabs instead)

**Test Milestone:**

- [ ] Ocean + board renders full-width in portrait
- [ ] Pinch-to-zoom works
- [ ] Single-finger pan works
- [ ] Can tap tiles and buildings

### 8.3 Portrait Controls Tab

**Tasks:**

1. Stack controls vertically: Resources, Roll, Purchase
2. Same content as left panel in landscape
3. Scrollable if needed

**Test Milestone:**

- [ ] All controls accessible in portrait
- [ ] Can scroll to reach all buttons

### 8.4 Portrait Players Tab

**Tasks:**

1. Full-width player list
2. Same content as right panel in landscape
3. Larger touch targets

**Test Milestone:**

- [ ] All players visible
- [ ] Touch targets sufficiently large
- [ ] Scrollable if many players

### 8.5 Portrait "Me" Tab (Personal Controller)

**Tasks:**

1. Create `components/game/MeTab.tsx`
2. Player selector dropdown: "Who am I?"
3. Store selected player ID in localStorage (per-device identity)
4. Contextual buttons based on game state:
   - **When it's your turn**: Large "NEXT" button
   - **During supplemental phase**: "Yes Sup" and "No Sup" buttons
5. Show current turn status (whose turn, what phase)

**Layout:**

```text
┌──────────────────────────┐
│  Who am I?               │
│  [Dropdown: Player List] │
├──────────────────────────┤
│                          │
│  (When it's your turn)   │
│  ┌────────────────────┐  │
│  │      NEXT          │  │
│  └────────────────────┘  │
│                          │
│  (During supplemental)   │
│  ┌─────────┐ ┌─────────┐ │
│  │ Yes Sup │ │ No Sup  │ │
│  └─────────┘ └─────────┘ │
│                          │
└──────────────────────────┘
```

**Use Case:** Players watching a shared TV/monitor. Each player has their phone as a
personal controller. They select who they are, and get contextual buttons for their turn.

**Test Milestone:**

- [ ] Dropdown lists all players in game
- [ ] Selected player persists in localStorage
- [ ] NEXT button appears when it's selected player's turn
- [ ] NEXT button sends Next message via GameServiceProxy
- [ ] Yes/No Sup buttons appear during supplemental phase
- [ ] Buttons send correct supplemental participation message

---

## Phase 9: Secondary Pages

**Goal:** Complete remaining pages.

### 9.1 Load Game Page

**Tasks:**

1. Create `app/load-game/page.tsx`
2. Fetch saved games from API
3. Display game list with metadata
4. Click to load and navigate to game

**Test Milestone:**

- [ ] Page lists saved games
- [ ] Shows game metadata (date, players, state)
- [ ] Clicking game loads it and navigates

### 9.2 Edit Players Page (Updates PlayerProfilesStore)

**Tasks:**

1. Create `app/edit-players/page.tsx`:

   ```tsx
   function EditPlayersPage() {
     const profiles = usePlayerProfilesStore(state => state.profiles);
     const updateProfile = usePlayerProfilesStore(state => state.updateProfile);
     const createProfile = usePlayerProfilesStore(state => state.createProfile);
     const deleteProfile = usePlayerProfilesStore(state => state.deleteProfile);

     const handleColorChange = async (playerId: string, newColor: string) => {
       // Update API
       await playerApi.updatePlayer(playerId, { color: newColor });

       // Update store - triggers re-render EVERYWHERE!
       // All PlayerTiles, Buildings, Roads update instantly
       updateProfile(playerId, { color: newColor });
     };

     return (
       <div>
         {Object.values(profiles).map(profile => (
           <PlayerEditor
             key={profile.id}
             profile={profile}
             onColorChange={(color) => handleColorChange(profile.id, color)}
             onNameChange={(name) => handleNameChange(profile.id, name)}
           />
         ))}
       </div>
     );
   }
   ```

2. List all player profiles from PlayerProfilesStore
3. Add new player form → `createProfile()`
4. Edit player (name, color, avatar) → `updateProfile()`
5. Delete player with confirmation → `deleteProfile()`

**The Magic:** Change player color here → all instances update:

- Game page PlayerTiles
- Game page Buildings (circles on board)
- Game page Roads (lines on board)
- New Game page player dropdowns
- Anywhere else player color is used

**Test Milestone:**

- [ ] Can view all players from PlayerProfilesStore
- [ ] Can add new player → updates store
- [ ] Can edit player name/color → updates store
- [ ] Change color → game page buildings immediately show new color
- [ ] Can delete player with confirmation

### 9.3 Settings Page

**Tasks:**

1. Create `app/settings/page.tsx`
2. House rules configuration
3. Sound on/off toggle
4. Theme options (if applicable)
5. Persist to localStorage and/or server

**Test Milestone:**

- [ ] Settings page renders
- [ ] Changes persist
- [ ] Settings affect game behavior

### 9.4 Stats Page

**Tasks:**

1. Create `app/stats/page.tsx`
2. Fetch player statistics from API
3. Display leaderboard
4. Show individual player stats
5. Win rates, games played, etc.

**Test Milestone:**

- [ ] Stats load from API
- [ ] Leaderboard displays correctly
- [ ] Individual player stats accessible

---

## Phase 10: Testing & Polish

**Goal:** Production readiness.

### 10.1 Unit Tests

**Tasks:**

1. Configure Vitest
2. Test geometry functions
3. Test store actions
4. Test utility functions
5. Target >70% coverage for `lib/` directory

**Test Milestone:**

- [ ] `npm run test` passes
- [ ] Geometry tests pass
- [ ] Store tests pass

### 10.2 Component Tests

**Tasks:**

1. Set up React Testing Library
2. Test critical components in isolation
3. Test PurchaseButton states
4. Test PlayerTile rendering

**Test Milestone:**

- [ ] Component tests pass
- [ ] No accessibility warnings

### 10.3 E2E Tests

**Tasks:**

1. Configure Playwright
2. Test new game flow
3. Test game play flow
4. Test navigation
5. Test portrait mode
6. Test **disconnect/reconnect** using Playwright network simulation:
   - Simulate network offline, make move in another client, restore network
   - Verify React client recovers and syncs state

**Test Milestone:**

- [ ] E2E tests pass
- [ ] Tests work in CI
- [ ] Disconnect/reconnect test validates mobile sleep/wake recovery

### 10.4 Storybook Setup

**Tasks:**

1. Configure Storybook for Next.js
2. Create stories for key components
3. Add visual regression tests
4. Document component props

**Test Milestone:**

- [ ] `npm run storybook` launches
- [ ] All key components have stories
- [ ] Visual tests pass

### 10.5 Performance Optimization

**Tasks:**

1. Review React DevTools profiler
2. Optimize re-renders (memoization)
3. Lazy load secondary pages
4. Image optimization

**Test Milestone:**

- [ ] No unnecessary re-renders on SignalR updates
- [ ] Lighthouse performance score >90

### 10.6 Cross-Browser Testing

**Tasks:**

1. Test in Chrome, Firefox, Safari, Edge
2. Fix any browser-specific issues
3. Test on iOS Safari
4. Test on Android Chrome

**Test Milestone:**

- [ ] Works in all major browsers
- [ ] No console errors
- [ ] Touch works on mobile

### 10.7 Documentation

**Tasks:**

1. Create `react-ui/README.md`
2. Document development workflow
3. Document component architecture
4. Document state management patterns

**Test Milestone:**

- [ ] README complete
- [ ] New developer can set up project following README

---

## Appendix: Detailed Component Checklist

### Board Components

| Component | Status | Notes |
|-----------|--------|-------|
| BoardContainer.tsx | ⬜ | |
| SvgDefinitions.tsx | ⬜ | |
| BaseLayer.tsx | ⬜ | |
| TilesLayer.tsx | ⬜ | |
| RoadsLayer.tsx | ⬜ | |
| BuildingsLayer.tsx | ⬜ | |
| RobberLayer.tsx | ⬜ | |
| GoldTilesLayer.tsx | ⬜ | |
| BoardMeasurement.tsx | ⬜ | |

### Player Components

| Component | Status | Notes |
|-----------|--------|-------|
| PlayersPanel.tsx | ⬜ | |
| PlayerTile.tsx | ⬜ | |
| PlayerCard.tsx | ⬜ | 3 modes: GoFirst, Supplemental, VP |
| GameResourcesHeader.tsx | ⬜ | |

### Resource Components

| Component | Status | Notes |
|-----------|--------|-------|
| ResourceTracking.tsx | ⬜ | |
| ResourceCard.tsx | ⬜ | |
| StarCounter.tsx | ⬜ | |

### Control Components

| Component | Status | Notes |
|-----------|--------|-------|
| PurchaseButton.tsx | ⬜ | |
| RollGrid.tsx | ⬜ | |
| GameControls.tsx | ⬜ | |

### Layout Components

| Component | Status | Notes |
|-----------|--------|-------|
| NavMenu.tsx | ⬜ | |
| PortraitTabs.tsx | ⬜ | |

### Celebration Components

| Component | Status | Notes |
|-----------|--------|-------|
| WinnerCelebration.tsx | ⬜ | |
| GriefCelebration.tsx | ⬜ | |

### Shared Components

| Component | Status | Notes |
|-----------|--------|-------|
| IconButton.tsx | ⬜ | |
| Modal.tsx | ⬜ | |
| RobberMenu.tsx | ⬜ | |

---

## Appendix: Test Validation Scripts

After each phase, run these validation checks:

### Phase 0 Validation

```bash
cd react-ui
npm run dev &
sleep 5
curl http://localhost:3000 | grep -q "Next" && echo "✅ Next.js running"
npm run generate-types && echo "✅ Type generation works"
npm run test && echo "✅ Geometry tests pass"
```

### Game Page Integration Test

```bash
# Start services
pwsh ./catan.ps1 run &
sleep 10

# Create game via API
GAME_ID=$(curl -s -X POST http://localhost:8080/api/game/new \
  -H "Content-Type: application/json" \
  -d '{"gameType":"Regular","playerIds":["p1","p2","p3"]}' | jq -r '.gameId')

# Navigate to game in browser
echo "Test URL: http://localhost:3000/game/$GAME_ID"
```

---

## Summary Timeline

| Phase | Description | Key Deliverable |
|-------|-------------|-----------------|
| 0 | Foundation | Infrastructure, types, SignalR working |
| 1 | Home + Nav | Navigation functional, hamburger menu |
| 2 | New Game | Create game end-to-end |
| 3 | Game Foundation | SignalR connected, page shell |
| 4 | Board | Full SVG board rendering |
| 5 | Players | Player panel complete |
| 6 | Controls | Left panel, all game controls |
| 7 | Overlays | Modals, celebrations |
| 8 | Portrait | Mobile layout complete |
| 9 | Secondary | All other pages |
| 10 | Polish | Tests, performance, docs |

Each phase should be fully tested before proceeding to the next.
