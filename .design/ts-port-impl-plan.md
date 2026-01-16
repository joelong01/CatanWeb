# TypeScript React Port - Implementation Plan

**Last Updated:** 2026-01-16
**Design Document:** `typescript-porting-design.md`
**Target Location:** `ReactUi/`

## Overview

This document provides a detailed, page-by-page implementation plan for porting the Blazor WebAssembly
frontend to React + Next.js + TypeScript. Each phase has testable milestones to verify correctness
before proceeding.

The plan follows a "vertical slice" approach: complete one page fully before moving to the next,
rather than building all components horizontally.

---

## Phase 0: Foundation (Infrastructure)

**Goal:** Establish the development environment and core infrastructure before any UI work.

### 0.1 Project Scaffolding

**Tasks:**

1. Create `ReactUi/` directory
2. Initialize Next.js 15 project with TypeScript:

   ```bash
   npx create-next-app@latest ReactUi --typescript --tailwind --eslint --app --no-src-dir
   ```

3. Configure `tsconfig.json` with path aliases (`@/`)
4. Set up `.env.local` with `NEXT_PUBLIC_API_URL=http://localhost:8080`

**Test Milestone:**

- [ ] `cd ReactUi && npm run dev` starts dev server on port 3000
- [ ] Browser shows Next.js default page at `http://localhost:3000`

### 0.2 Tailwind Configuration

**Tasks:**

1. Create `tailwind.config.ts` with game colors, Catan font, 3D utilities
2. Create `styles/globals.css` with CSS custom properties from Blazor app
3. Copy `Catan.ttf` to `public/fonts/`
4. Define Tailwind utilities for `backface-hidden`, `perspective-1000`, `preserve-3d`
5. Add font preload in `app/layout.tsx` to prevent FOUT:

   ```tsx
   <link rel="preload" href="/fonts/Catan.ttf" as="font" type="font/ttf" crossOrigin="anonymous" />
   ```

**Test Milestone:**

- [ ] Create test page with `<span className="font-catan">ABC</span>`
- [ ] Catan font renders correctly (no flash of unstyled text)
- [ ] Game background color (`bg-game-bg-primary`) applies

### 0.3 Type Generation Pipeline (NSwag)

**Tasks:**

1. Install NSwag as devDependency: `npm install -D nswag`
2. Create `nswag.json` with `aspNetCoreToOpenApi` configuration (DLL-based)
3. Add `generate-types` script to `package.json`
4. Create `types/generated/` directory
5. Run initial generation, verify output

**Test Milestone:**

- [ ] `npm run generate-types` succeeds (requires `pwsh ./catan.ps1 build` first)
- [ ] `types/generated/api.ts` contains `GameModel`, `PlayerModel`, `TileModel` interfaces
- [ ] Enum types are string literal unions (not numeric)
- [ ] Property names are camelCase

### 0.4 Zustand Stores

**Tasks:**

1. Install Zustand: `npm install zustand`
2. Create `stores/gameStore.ts` with `subscribeWithSelector` middleware
3. Create `stores/uiStore.ts` with `persist` middleware
4. Create `stores/connectionStore.ts` for SignalR connection state
5. Define all selectors per design document

**Test Milestone:**

- [ ] Import stores in a test component
- [ ] `useGameStore.getState().setGameModel(mockData)` works
- [ ] `useUIStore.getState().setOrientation(true, false)` works
- [ ] Refresh browser, persisted state (e.g., `activePortraitTab`) restores

### 0.5 GameServiceProxy (Unified Client)

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

**Test Milestone:**

- [ ] Start GameService: `pwsh ./catan.ps1 run`
- [ ] Create test page that calls `useGameConnection('test-game', 'test-player')`
- [ ] Console shows `[GameServiceProxy] Connected` or connection error
- [ ] Browser DevTools Network tab shows WebSocket connection
- [ ] Create a game via REST, join via SignalR, receive GameStateUpdated
- [ ] Lock phone/switch apps, return - connection recovers automatically

### 0.6 REST API Integration

**Tasks:**

1. Verify `GameServiceProxy.sendAction()` works for all message types
2. Test game commands: `undo()`, `redo()`, `next()`, `roll()`, `purchase()`
3. Verify SignalR receives `GameStateUpdated` after REST command
4. Verify state re-fetch on reconnect catches missed events

**Test Milestone:**

- [ ] Start GameService
- [ ] `proxy.createGame({ gameType: 'Regular', playerIds: ['p1', 'p2', 'p3'] })` succeeds
- [ ] `proxy.joinGame(gameId)` receives initial GameStateUpdated
- [ ] `proxy.roll({ diceValue: 7 })` triggers GameStateUpdated broadcast
- [ ] Open Blazor app on same game - both clients receive updates
- [ ] Simulate disconnect, make move in Blazor, reconnect React - state syncs correctly

### 0.7 Geometry Module

**Tasks:**

1. Create `lib/geometry/boardGeometry.ts`
2. Port all functions from `BoardGeometry.cs`:
   - `axialToPixel`, `pixelToHex`
   - `getHexVertices`, `generateHexPath`
   - `getBuildingPosition`, `getRoadEndpoints`
   - `clientToSvgCoords`
3. Port constants from `BoardSvgConstants.cs`

**Test Milestone:**

- [ ] Write unit tests for geometry functions
- [ ] `axialToPixel(0, 0)` returns center coordinates (540, 468)
- [ ] `pixelToHex(540, 468)` returns `{q: 0, r: 0, s: 0}`
- [ ] `getHexVertices(100, 100, 60)` returns 6 vertices at distance 60 from center

### 0.8 Model Utilities

**Tasks:**

1. Create `lib/utils/modelUtils.ts`
2. Port MUST-PORT extension methods:
   - `getBuildingAliases(key)`
   - `findBuilding(buildings, key)`
   - `getAdjacentTiles(tiles, tile)`
   - `getTileStars(number)`

**Test Milestone:**

- [ ] `getTileStars(6)` returns 5
- [ ] `getTileStars(2)` returns 1
- [ ] `getBuildingAliases({ position: 'TopRight', ... })` returns 2 aliases

---

## Phase 1: Home Page + Navigation

**Goal:** Complete the home page with working navigation as the first vertical slice.

### 1.1 Root Layout

**Tasks:**

1. Create `app/layout.tsx` with dark theme, Catan font, global styles
2. Set up `<html>` and `<body>` with proper classes
3. Include viewport meta tag for mobile

**Test Milestone:**

- [ ] Page renders with dark background
- [ ] No console errors

### 1.2 NavMenu Component (Hamburger)

**Tasks:**

1. Create `components/layout/NavMenu.tsx`
2. Implement hamburger icon that toggles sidebar
3. Add navigation links: Home, New Game, Load Game, Edit Players, Settings, Stats
4. Use shadcn/ui `DropdownMenu` or custom implementation
5. Animate sidebar open/close

**Test Milestone:**

- [ ] Hamburger icon visible in header
- [ ] Click hamburger opens sidebar menu
- [ ] Menu shows all navigation options
- [ ] Click outside menu closes it
- [ ] Click menu item navigates and closes menu

### 1.3 Home Page

**Tasks:**

1. Create `app/page.tsx`
2. Display welcome message and action buttons
3. Buttons: "New Game", "Load Game", "Edit Players", "Settings", "Statistics"
4. Style to match Blazor home page

**Test Milestone:**

- [ ] Home page renders at `/`
- [ ] All buttons visible
- [ ] "New Game" button navigates to `/new-game`
- [ ] Other buttons navigate to correct routes

### 1.4 Viewport Scaler Hook

**Tasks:**

1. Create `hooks/useViewportScaler.ts`
2. Detect portrait vs landscape orientation
3. Detect mobile vs desktop (pointer: coarse)
4. Calculate viewport scale factor
5. Update uiStore on resize/orientation change

**Test Milestone:**

- [ ] Resize browser window, store updates `isPortrait` correctly
- [ ] On mobile emulation, `isMobile` is true
- [ ] `viewportScale` changes with window size

---

## Phase 2: New Game Page

**Goal:** Complete the new game flow end-to-end.

### 2.1 New Game Page Structure

**Tasks:**

1. Create `app/new-game/page.tsx`
2. Layout: Game type selection, player selection, start button
3. Connect to uiStore for responsive layout

**Test Milestone:**

- [ ] Page renders at `/new-game`
- [ ] Shows game type options and player inputs

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

1. Fetch available players from `/api/players`
2. Display player dropdown/selector for each slot (3-4 players)
3. Add/remove player slots
4. Show player colors

**Test Milestone:**

- [ ] Player list loads from API
- [ ] Can select different players for each slot
- [ ] Cannot select same player twice
- [ ] Player colors display correctly

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

## Phase 3: Main Game Page - Foundation

**Goal:** Establish the game page structure and GameServiceProxy connection.

### 3.1 Game Page Shell

**Tasks:**

1. Create `app/game/[gameId]/page.tsx`
2. Extract `gameId` from URL params
3. Set up three-panel landscape layout (left/center/right)
4. Set up portrait tabs structure

**Test Milestone:**

- [ ] Navigate to `/game/[valid-game-id]`
- [ ] Page renders with panel structure
- [ ] Console shows page loading with correct gameId

### 3.2 GameServiceProxy Connection

**Tasks:**

1. Use `useGameConnection(gameId, playerId)` hook (wraps GameServiceProxy)
2. Display connection status indicator
3. Handle reconnection gracefully
4. Store gameModel in Zustand when `GameStateUpdated` received via SignalR

**Test Milestone:**

- [ ] Navigate to game page for existing game
- [ ] Connection status shows "Connected"
- [ ] Console logs `GameStateUpdated` events
- [ ] `useGameStore.getState().gameModel` is populated
- [ ] Open same game in Blazor app - both see same state

### 3.3 Player ID Management

**Tasks:**

1. Determine which player "you are" (from localStorage or URL param)
2. Store `playerId` in gameStore
3. Pass `playerId` to GameServiceProxy for all operations

**Test Milestone:**

- [ ] Player ID persists across page refreshes
- [ ] GameServiceProxy `joinGame` sends correct playerId

---

## Phase 4: Main Game Page - Board Rendering

**Goal:** Complete SVG board rendering with all layers.

### 4.1 BoardContainer Component

**Tasks:**

1. Create `components/board/BoardContainer.tsx`
2. Set up dual SVG structure (static + interactive layers)
3. Apply correct viewBox and aspect ratio
4. Responsive sizing
5. Wrap board in **Error Boundary** - board crash shouldn't kill controls/player panel

**Test Milestone:**

- [ ] Empty board container renders
- [ ] Container maintains correct aspect ratio
- [ ] Resizing window scales correctly
- [ ] Intentional error in board layer shows fallback UI, controls still work

### 4.2 SvgDefinitions Component

**Tasks:**

1. Create `components/board/SvgDefinitions.tsx`
2. Define gradients for each player color
3. Define gradients for resources (wheat, wood, ore, etc.)
4. Define any required patterns

**Test Milestone:**

- [ ] SVG `<defs>` element renders with gradients
- [ ] No console warnings about missing definitions

### 4.3 BaseLayer (Tiles)

**Tasks:**

1. Create `components/board/BaseLayer.tsx`
2. Render hexagonal tiles from `tiles` array
3. Position using `axialToPixel()`
4. Display tile numbers with stars (probability dots)
5. Color tiles by resource type
6. Support dimming non-rolled tiles

**Test Milestone:**

- [ ] All 19 tiles (regular game) render in correct positions
- [ ] Tile numbers display correctly
- [ ] Star dots show probability (more stars = higher probability)
- [ ] After roll, non-matching tiles dim

### 4.4 RoadsLayer

**Tasks:**

1. Create `components/board/RoadsLayer.tsx`
2. Render roads as SVG lines
3. Position using `getRoadEndpoints()`
4. Color by player owner
5. Show buildable roads when applicable

**Test Milestone:**

- [ ] Roads render at correct edge positions
- [ ] Road colors match player colors
- [ ] Buildable roads have distinct appearance

### 4.5 BuildingsLayer

**Tasks:**

1. Create `components/board/BuildingsLayer.tsx`
2. Render settlements, cities, knights at vertices
3. Position using `getBuildingPosition()`
4. Handle building aliases (shared vertices)
5. Different icons for settlement vs city vs metropolis

**Test Milestone:**

- [ ] Settlements render as small shapes
- [ ] Cities render as larger shapes
- [ ] Buildings appear at correct vertices
- [ ] Click building triggers callback with BuildingKey

### 4.6 RobberLayer

**Tasks:**

1. Create `components/board/RobberLayer.tsx`
2. Position robber on current tile
3. Animate movement when coordinates change
4. CSS transition for smooth movement

**Test Milestone:**

- [ ] Robber renders on correct tile
- [ ] Moving robber (via API) shows smooth animation
- [ ] Animation duration ~1.2s with easing

### 4.7 GoldTilesLayer

**Tasks:**

1. Create `components/board/GoldTilesLayer.tsx`
2. Highlight gold tiles when picking random gold
3. Show indicator for selected gold tiles

**Test Milestone:**

- [ ] Gold tiles highlight during gold selection phase
- [ ] Non-gold tiles don't show highlight

### 4.8 Tile Click Handling

**Tasks:**

1. Implement `clientToSvgCoords()` for click coordinate conversion
2. Convert SVG coords to hex coords with `pixelToHex()`
3. Pass clicked tile to parent component

**Test Milestone:**

- [ ] Click on center tile returns `{q: 0, r: 0}`
- [ ] Click on edge tiles returns correct coordinates
- [ ] Works at different zoom levels

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

### 5.2 PlayerTile Component

**Tasks:**

1. Create `components/players/PlayerTile.tsx`
2. Display: player name, score, resources, development cards
3. Player color bar/indicator
4. Current player highlight
5. "Your turn" indicator for local player

**Test Milestone:**

- [ ] Player name and score display
- [ ] Resources update when gameModel changes
- [ ] Victory points show correctly
- [ ] Longest road/largest army badges show

### 5.3 PlayerCard (GoFirst Mode)

**Tasks:**

1. Create `components/players/PlayerCard.tsx`
2. Support "GoFirst" mode for roll order
3. 3D flip animation using Framer Motion
4. Click to select "go first"

**Test Milestone:**

- [ ] During WaitingForRollForOrder state, cards show "Go First" option
- [ ] Clicking card flips it
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
2. 3D flip animation on click
3. Shows entitlement icon and count
4. Disabled state when `actionFlags.canPurchase*` is false
5. Sends PurchaseMessage on click

**Test Milestone:**

- [ ] Settlement button shows settlement icon
- [ ] Button disabled when can't afford
- [ ] Click flips card and sends purchase command
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

1. Create modal dialog for winner confirmation
2. Display winner name and final score
3. "New Game" and "Review" buttons

**Test Milestone:**

- [ ] Dialog appears when game ends
- [ ] Shows winner information
- [ ] Buttons navigate appropriately

### 7.3 Winner Celebration Animation

**Tasks:**

1. Create `components/celebrations/WinnerCelebration.tsx`
2. Full-screen overlay
3. Confetti animation (100+ particles)
4. Trophy emoji animation
5. Winner name display with spring animation

**Test Milestone:**

- [ ] Celebration triggers when winner declared
- [ ] Confetti particles fall
- [ ] Trophy bounces
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

**Goal:** Complete portrait/mobile layout.

### 8.1 Portrait Tabs Component

**Tasks:**

1. Create `components/layout/PortraitTabs.tsx`
2. Fixed bottom tab bar
3. Three tabs: Board, Controls, Players
4. Active tab indicator
5. Persist active tab in localStorage

**Test Milestone:**

- [ ] Tab bar appears in portrait orientation
- [ ] Clicking tab switches content
- [ ] Active tab highlighted
- [ ] Tab selection persists across refresh

### 8.2 Portrait Board Tab

**Tasks:**

1. Board fills available space
2. Touch gestures work
3. No horizontal scroll

**Test Milestone:**

- [ ] Board renders full-width in portrait
- [ ] Can tap tiles and buildings
- [ ] No layout issues

### 8.3 Portrait Controls Tab

**Tasks:**

1. Stack controls vertically: Resources, Roll, Purchase
2. Scrollable if needed

**Test Milestone:**

- [ ] All controls accessible in portrait
- [ ] Can scroll to reach all buttons

### 8.4 Portrait Players Tab

**Tasks:**

1. Full-width player list
2. Larger touch targets

**Test Milestone:**

- [ ] All players visible
- [ ] Touch targets sufficiently large
- [ ] Scrollable if many players

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

### 9.2 Edit Players Page

**Tasks:**

1. Create `app/edit-players/page.tsx`
2. List all player profiles
3. Add new player form
4. Edit existing player (name, color, avatar)
5. Delete player (with confirmation)

**Test Milestone:**

- [ ] Can view all players
- [ ] Can add new player
- [ ] Can edit player name/color
- [ ] Can delete player

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

1. Create `ReactUi/README.md`
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
cd ReactUi
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
