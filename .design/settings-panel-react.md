# Settings Panel React Port Design

## Overview

Port the Settings panel from Blazor to React, providing user-configurable house rules
and game options. The Settings button in NavMenu already navigates to `/settings`.

**Deferred:** GriefDodgy setting will not be implemented in this phase.

## Settings Catalog

### House Rules Category

| Setting | Type | Options | Default | Purpose |
|---------|------|---------|---------|---------|
| ExpansionGoldTiles | dropdown | 0, 1, 2, 3, 4 | 2 | Gold tiles for expansion games |
| RegularGoldTiles | dropdown | 0, 1, 2 | 1 | Gold tiles for regular games |
| SupplementalMinPlayers | dropdown | 3, 4, 5, 6 | 5 | Min players for supplemental build phase |

### Game Configuration Category

| Setting | Type | Options | Default | Purpose |
|---------|------|---------|---------|---------|
| ShowDebugInfo | checkbox | N/A | false | Display debug info on game page |
| AnimationSpeed | dropdown | Slow, Normal, Fast, None | Normal | UI animation speed |

## Architecture

### Storage Layer

Match Blazor's dual-storage approach using localStorage:

1. **Individual settings**: `setting_{SettingName}` - UI state reconstruction
2. **Serialized HouseRules**: `HouseRules` - JSON object for game logic

### Settings Store (Zustand)

Create `react-ui/lib/stores/settingsStore.ts`:

```typescript
interface SettingsState {
  // Individual settings
  expansionGoldTiles: number;
  regularGoldTiles: number;
  supplementalMinPlayers: number;
  showDebugInfo: boolean;
  animationSpeed: 'Slow' | 'Normal' | 'Fast' | 'None';

  // Derived
  isInitialized: boolean;

  // Actions
  initialize: () => void;
  setSetting: (name: string, value: unknown) => void;
  resetToDefaults: () => void;
  getHouseRules: (gameType: GameType) => HouseRules;
}
```

### Data Flow

```text
Settings Page UI
    ↓ onChange
settingsStore.setSetting()
    ├─→ localStorage["setting_*"]
    └─→ localStorage["HouseRules"]

New Game Creation
    ↓
settingsStore.getHouseRules(gameType)
    ↓
Passes to gameApi.createGame()

Game Page
    ↓
Reads gameModel.houseRules from server
    ↓
Uses server-authoritative house rules
```

### Settings Consumption

| Consumer | Setting | Usage |
|----------|---------|-------|
| New Game page | GoldTiles | Passed to game creation API |
| New Game page | SupplementalMinPlayers | Passed to game creation API |
| Game page | ShowDebugInfo | Conditionally renders debug panel |
| Game page | AnimationSpeed | CSS transition durations |

## UI Design

### Page Layout

```text
┌─────────────────────────────────────────┐
│ Settings                                │
│ Configure game preferences              │
├─────────────────────────────────────────┤
│                                         │
│ House Rules                             │
│ ┌─────────────────────────────────────┐ │
│ │ Gold tiles (Expansion)    [▼ 2   ] │ │
│ │ Gold tiles (Regular)      [▼ 1   ] │ │
│ │ Supplemental build min    [▼ 5   ] │ │
│ └─────────────────────────────────────┘ │
│                                         │
│ Game Configuration                      │
│ ┌─────────────────────────────────────┐ │
│ │ Show debug information    [ ]      │ │
│ │ Animation speed           [▼Normal]│ │
│ └─────────────────────────────────────┘ │
│                                         │
│ [Reset to Defaults]  [Save]             │
│                                         │
│ ✓ Settings saved                        │
└─────────────────────────────────────────┘
```

### Styling

- Match existing React UI patterns (Tailwind classes)
- Dark theme consistent with other pages
- Tooltip icons (?) for setting descriptions
- Success/error toast messages

## Integration Points

### New Game Page

Currently hardcodes house rules in `new-game/page.tsx:146-157`.
After implementation:

```typescript
const houseRules = settingsStore.getHouseRules(gameType);
```

### Game Page

Add optional debug panel controlled by `showDebugInfo` setting.
Apply animation speed to transitions.

### API Integration

When saving settings with an active game, optionally update the game's house rules
via `PUT /api/game/{gameId}/houserules` (same as Blazor).

## File Structure

```text
react-ui/
├── lib/stores/
│   └── settingsStore.ts          # New - Zustand store
├── app/settings/
│   └── page.tsx                  # Update - Full implementation
└── types/
    └── settings.ts               # New - Setting type definitions
```

## Out of Scope

- GriefDodgy setting (deferred per requirements)
- Server-side settings sync (client-only like Blazor)
- Cross-device settings sync
