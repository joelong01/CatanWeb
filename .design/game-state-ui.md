# GameState to UI Mapping

**Last verified:** January 30, 2026

## Overview

The `GameState` enum (33 values) drives all UI behavior. The server
sends `GameModel` with the current `GameState`, and the React UI
renders controls, overlays, and board interactions accordingly.

The client does NOT validate game rules. It reads `ActionFlags` from
`GameModel` to enable/disable buttons and trusts `BuildingState` /
`RoadState` for visibility.

## ActionFlags

Server-controlled booleans that drive button states:

| Flag | Controls |
|------|----------|
| `undoEnabled` | Undo button |
| `redoEnabled` | Redo button |
| `nextEnabled` | Next/End Turn button |
| `rollsEnabled` | Roll ring interaction |

## Entitlement System

Three-tier purchase tracking per entitlement type:

| Tier | Meaning |
|------|---------|
| `unspent` | Available to purchase this turn |
| `spentThisTurn` | Purchased during current turn |
| `spentTotal` | Total purchased across all turns |

The UI shows purchase buttons based on `unspent > 0` and displays
count badges showing `spentThisTurn`.

## GameState UI Requirements

### Setup Phase

| State | Board | Controls | Overlay |
|-------|-------|----------|---------|
| `PickingBoard` | Tiles visible, no buildings/roads | Shuffle, Balance, Swap, Next | None |
| `WaitingForRollForOrder` | Board locked | Roll ring active | None |
| `FinishedRollOrder` | Board locked | None | GoFirst overlay (player hex ring) |

### Allocation Phase

| State | Board | Controls | Overlay |
|-------|-------|----------|---------|
| `AllocateResourceForward` | Buildable spots shown | Settlement + Road placement | Current player indicator |
| `AllocateResourceReverse` | Buildable spots shown | Settlement + Road placement | Current player indicator |

### Main Gameplay

| State | Board | Controls | Overlay |
|-------|-------|----------|---------|
| `WaitingForRoll` | Previous roll dimming clears | Roll ring active, Next disabled | None |
| `WaitingForNext` | Full interaction | All purchase buttons, Undo/Redo, Next | None |
| `MustMoveRobber` | Tile click targets active | None (must click tile first) | Target player menu after tile click |

### Special States

| State | Board | Controls | Overlay |
|-------|-------|----------|---------|
| `PickSupplementalPlayers` | Read-only | None | SupplementalOverlay (player selection) |
| `Supplemental` | Build-only for participant | Limited purchase buttons | Supplemental indicator |
| `TooManyCards` | Read-only | None | Discard UI (not yet implemented) |
| `GameOver` | Read-only | None | WinnerOverlay (3-phase) |

## Board Element Visibility

### Buildings

Two rendering loops for correct z-ordering:

1. **Owned buildings** (settlements, cities) -- player colors, full
   opacity, no click handler
2. **Buildable spots** -- semi-transparent, click to place

| BuildingState | Visible | Clickable | Style |
|---------------|---------|-----------|-------|
| `Settlement` | Always | No | Player color, circle r=24 |
| `City` | Always | No | Player color, larger marker |
| `PossibleSettlement` | During placement | Yes | Semi-transparent, r=18 |
| `NotBuildable` | Never | No | Hidden |

### Roads

| RoadState | Visible | Clickable | Style |
|-----------|---------|-----------|-------|
| `Road` | Always | No | Player color, solid |
| `Buildable` | During placement | Yes | Semi-transparent |
| `Unowned` | Never | No | Hidden |

### Build Indexes

During placement states, buildable spots show numbered indexes (1-9)
for keyboard shortcut selection. Rectangle overlay: 36x28px, centered
on the buildable position.

## Robber Rendering

- **Glyph:** CatanFont characters (SolidShield `\uE925` + Pirate
  `\uE90C`)
- **Movement:** CSS transition 1200ms with cubic-bezier easing
- **Player targeting:** Gradient fill using target player's colors
- **Click flow:** Click tile -> move robber -> show target player menu
  (if valid targets)

## Roll Dimming

After a roll, non-matching tiles dim to `opacity: 0.5` for 5 seconds.
This is client-side only (managed by `uiStore.lastRolledNumber` with
a `setTimeout` clearing mechanism).

## Harbor Ownership

Harbors show ownership when a player has a settlement/city adjacent
to the harbor hex. Visual indicator: harbor hex dock side colored
with owning player's color.

## Implementation Status

See [react-porting-status.md](react-porting-status.md) for detailed
coverage of which GameStates have complete React UI implementations.
