# Animation Design

This document describes the animation system for the React UI, including speed settings, CSS custom properties, and guidelines for implementing new animations.

## Overview

The animation system provides consistent, user-controllable animation speeds across the application. Animations scale proportionally based on a user setting, with "Normal" as the baseline.

### Key Principles

1. **Game animations scale with user settings** - Card flips, robber movement, and other gameplay animations respect the Animation Speed setting
2. **UI feedback stays fast** - Hover effects, button presses, and mouse in/out events remain at fixed fast speeds for responsive feel
3. **Proportional scaling** - All animation categories scale together based on a single multiplier

## Animation Speed Setting

Users can configure animation speed in Settings. The setting controls a multiplier applied to all base durations:

| Setting | Multiplier | Description |
|---------|-----------|-------------|
| Slow    | 2.0x      | Deliberate, easy-to-follow animations |
| Normal  | 1.0x      | Standard animation speeds (baseline) |
| Fast    | 0.5x      | Quick animations for experienced users |
| None    | 0x        | Instant transitions (no animation) |

## CSS Custom Properties

The `useAnimationSpeed` hook (called in `MainLayout`) sets these CSS variables on `:root`:

| Variable | Base Duration | Slow | Normal | Fast | None |
|----------|--------------|------|--------|------|------|
| `--animation-fast` | 100ms | 200ms | 100ms | 50ms | 0ms |
| `--animation-medium` | 250ms | 500ms | 250ms | 125ms | 0ms |
| `--animation-slow` | 500ms | 1000ms | 500ms | 250ms | 0ms |
| `--animation-extra-slow` | 2000ms | 4000ms | 2000ms | 1000ms | 0ms |
| `--animation-button-press` | 200ms | 400ms | 200ms | 100ms | 0ms |
| `--animation-color-transition` | 300ms | 600ms | 300ms | 150ms | 0ms |
| `--animation-delay` | 1000ms | 2000ms | 1000ms | 500ms | 0ms |
| `--animation-duration` | 250ms | 500ms | 250ms | 125ms | 0ms |

### Choosing the Right Duration

- **Fast (100ms)**: Quick UI feedback, subtle transitions
- **Medium (250ms)**: Standard animations, modal transitions
- **Slow (500ms)**: Card flips, robber movement, emphasis animations
- **Extra Slow (2000ms)**: Tile dimming delays, extended animations

## Animation Categories

### Game Animations (Scale with Setting)

These animations are part of gameplay and should use CSS variables:

| Animation | Variable | Component |
|-----------|----------|-----------|
| Card flip (action buttons) | `--animation-slow` | `ActionCluster.tsx` |
| Card flip (resources header) | `--animation-slow` | `GameResourcesHeader.tsx` |
| Card flip (player resources) | `--animation-slow` | `PlayersPanel.tsx` |
| Robber movement | `--animation-slow` | `GameBoard.tsx` |

### UI Feedback (Fixed Speed)

These animations provide immediate visual feedback and should use fixed Tailwind classes:

| Animation | Duration | Usage |
|-----------|----------|-------|
| Button hover scale | `duration-150` | Hex buttons, menu items |
| Button press scale | `duration-150` | Action buttons, controls |
| Border color change | `duration-150` | Hex button borders |
| Icon scale on press | `duration-150` | FontAwesome icons in buttons |
| Menu transitions | `duration-200` | NavMenu, settings dropdowns |

## Implementation Guide

### Using CSS Variables in Inline Styles

For game animations, use CSS variables in the `style` prop:

```tsx
// Card flip animation
<div
  className="transition-transform"
  style={{
    transformStyle: 'preserve-3d',
    transform: isFlipped ? 'rotateY(180deg)' : 'rotateY(0deg)',
    transitionDuration: 'var(--animation-slow)',
  }}
>
```

### Using CSS Variables for Position Animations

```tsx
// Robber movement
<div
  style={{
    position: 'absolute',
    left: `${x}px`,
    top: `${y}px`,
    transition: 'left var(--animation-slow) ease-in-out, top var(--animation-slow) ease-in-out',
  }}
>
```

### Using Tailwind for UI Feedback

For hover and press effects, use Tailwind's fixed duration classes:

```tsx
// Button hover effect (stays fast)
<div className="transition-transform duration-150 hover:scale-105">

// Border color on hover (stays fast)
<div className="transition-colors duration-150 hover:border-blue-500">
```

### Using JavaScript Animation Durations

For programmatic animations (e.g., with `requestAnimationFrame` or timers):

```tsx
import { getAnimationDuration } from '@/lib/hooks';
import { useSettingsStore } from '@/lib/stores/settingsStore';

function MyComponent() {
  const animationSpeed = useSettingsStore((state) => state.animationSpeed);

  const handleAnimate = () => {
    const duration = getAnimationDuration('slow', animationSpeed);
    // Use duration (in ms) for programmatic animation
  };
}
```

## Adding New Animations

### Step 1: Determine Category

Ask: "Is this a game animation or UI feedback?"

- **Game animation**: User might want to slow down to see what's happening
- **UI feedback**: Should always be fast for responsive feel

### Step 2: Choose Duration

For game animations, select the appropriate CSS variable:

- **Fast transitions** (subtle): `var(--animation-fast)`
- **Standard animations**: `var(--animation-medium)`
- **Emphasis animations** (card flips, movement): `var(--animation-slow)`
- **Long delays**: `var(--animation-extra-slow)`

For UI feedback, use Tailwind:

- **Immediate feedback**: `duration-150`
- **Smooth transitions**: `duration-200`

### Step 3: Implement

```tsx
// Game animation example
<div
  className="transition-opacity"
  style={{ transitionDuration: 'var(--animation-medium)' }}
>

// UI feedback example
<div className="transition-all duration-150 hover:bg-white/10">
```

## File Locations

| File | Purpose |
|------|---------|
| `lib/hooks/useAnimationSpeed.ts` | Hook that applies CSS variables to :root |
| `lib/stores/settingsStore.ts` | Zustand store with animation speed setting |
| `types/settings.ts` | AnimationSpeed type definition |
| `app/globals.css` | Default CSS variable values (fallback) |
| `components/layout/MainLayout.tsx` | Calls `useAnimationSpeed()` once at app root |

## Matching Desktop App

The React animation system mirrors the Desktop app's `AnimationSpeed.cs`:

| Desktop Property | React Variable |
|-----------------|----------------|
| `AnimationSpeed.Fast` | `--animation-fast` |
| `AnimationSpeed.Medium` | `--animation-medium` |
| `AnimationSpeed.Slow` | `--animation-slow` |
| `AnimationSpeed.ExtraSlow` | `--animation-extra-slow` |
| `AnimationSpeed.ButtonPress` | `--animation-button-press` |
| `AnimationSpeed.ColorTransition` | `--animation-color-transition` |
| `AnimationSpeed.Delay` | `--animation-delay` |

## Testing Animations

1. Go to Settings and change Animation Speed
2. Verify game animations (card flips, robber) scale appropriately
3. Verify UI feedback (hover, button press) remains fast
4. Test with "None" to ensure instant transitions work
