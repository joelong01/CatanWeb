# React UI Responsive Design Standards

**Last Updated:** 2026-01-22
**Status:** Design Document
**Location:** `.design/ui/react/`

## Overview

This document defines the responsive design approach for the React UI, covering orientation detection, layout patterns, and testing requirements across desktop, phone, and TV devices.

## Key Principle: Form Pages vs Game Page

The React UI has two distinct layout categories:

### Game Page (Board + Controls)

Uses **infinite ocean architecture** documented in `game-page-design.md`:

- Infinite hex ocean as base layer (dynamically rendered water tiles)
- Game board tiles float on the ocean
- Left/Right panels float above with semi-transparent glassmorphism
- Pinch-to-zoom and pan/swipe for exploration
- Portrait mode uses tabs (Board, Controls, Players, Me)
- Board reveal animation: tiles flip from face-down to face-up on game start

This is a significant evolution from the Blazor approach and enables future features like multi-island games and exploration modes.

### Form Pages (New Game, Edit Players, Settings, Stats, etc.)

Uses **responsive CSS layouts** with Tailwind orientation variants:

- Fluid layouts that adapt to available space
- Two-column in landscape, single-column in portrait
- Standard CSS Grid/Flexbox responsive patterns
- No transform scaling needed

## Tailwind Orientation Variants

### Configuration

Add to `react-ui/app/globals.css` after the Tailwind import:

```css
@import 'tailwindcss';

/* Orientation variants for responsive layouts */
@custom-variant portrait (orientation: portrait);
@custom-variant landscape (orientation: landscape);
```

### Usage

```tsx
{/* Two-column in landscape, single-column in portrait */}
<div className="flex portrait:flex-col landscape:flex-row gap-6">
  <div className="landscape:w-1/2">Left/Top content</div>
  <div className="landscape:w-1/2">Right/Bottom content</div>
</div>

{/* Hide element in portrait */}
<div className="portrait:hidden">Only visible in landscape</div>

{/* Different padding per orientation */}
<div className="portrait:px-4 landscape:px-8">Content</div>
```

### Why Orientation Variants Over Width Breakpoints

| Approach | Pros | Cons |
|----------|------|------|
| `portrait:`/`landscape:` | Semantically accurate, works on rotated devices | Requires Tailwind v4 custom variant |
| `lg:` breakpoint | Simple, no config needed | Width-based, not true orientation |

**Recommendation:** Use orientation variants for layout structure, width breakpoints for fine-tuning.

## Layout Patterns

### Two-Column Form Layout (Landscape)

```text
┌─────────────────────────────────────────────────────────────┐
│ [Header with navigation]                                     │
├──────────────────────────┬──────────────────────────────────┤
│                          │                                  │
│  Primary Selection       │  Secondary Configuration         │
│  (e.g., Game Type)       │  (e.g., Players, Options)        │
│                          │                                  │
│                          │  [Action Button]                 │
└──────────────────────────┴──────────────────────────────────┘
```

### Single-Column Form Layout (Portrait)

```text
┌──────────────────────────┐
│ [Header]                 │
├──────────────────────────┤
│ Primary Selection        │
├──────────────────────────┤
│ Secondary Configuration  │
├──────────────────────────┤
│ [Action Button]          │
└──────────────────────────┘
```

### Implementation Pattern

```tsx
export default function FormPage() {
  return (
    <MainLayout>
      <div className="min-h-screen p-5 pt-[60px] overflow-y-auto">
        {/* Header - same in both orientations */}
        <header className="mb-6">...</header>

        {/* Main content - responsive layout */}
        <div className="flex portrait:flex-col landscape:flex-row gap-6
                        portrait:max-w-[600px] landscape:max-w-[1400px] mx-auto">

          {/* Primary section */}
          <div className="landscape:w-1/2 xl:landscape:w-[55%]">
            <PrimaryContent />
          </div>

          {/* Secondary section */}
          <div className="landscape:w-1/2 xl:landscape:w-[45%] space-y-6">
            <SecondaryContent />
            <ActionButton />
          </div>
        </div>
      </div>
    </MainLayout>
  );
}
```

## Device Targets

### Desktop

| Orientation | Typical Resolution | Layout |
|-------------|-------------------|--------|
| Landscape | 1920x1080, 2560x1440, 3840x2160 | Two-column |
| Portrait | 1080x1920 (rotated monitor) | Single-column |

### Phone

| Orientation | Typical Resolution | Layout |
|-------------|-------------------|--------|
| Portrait | 390x844 (iPhone), 360x800 (Android) | Single-column |
| Landscape | 844x390, 800x360 | Two-column if space permits |

### Tablet

| Orientation | Typical Resolution | Layout |
|-------------|-------------------|--------|
| Portrait | 768x1024 (iPad), 800x1280 | Single-column |
| Landscape | 1024x768, 1280x800 | Two-column |

### TV

| Orientation | Typical Resolution | Layout |
|-------------|-------------------|--------|
| Landscape | 1920x1080, 3840x2160 | Two-column |
| Portrait | Rare, but supported | Single-column |

## Breakpoint Reference

Tailwind's default breakpoints (for fine-tuning within orientation):

| Breakpoint | Min Width | Use Case |
|------------|-----------|----------|
| `sm` | 640px | Large phones landscape |
| `md` | 768px | Tablets |
| `lg` | 1024px | Small laptops |
| `xl` | 1280px | Desktops |
| `2xl` | 1536px | Large desktops |

**Combined usage:**

```tsx
{/* Different column widths at different landscape sizes */}
<div className="landscape:w-1/2 xl:landscape:w-[55%]">
```

## Touch Considerations

For touch devices (detected via `pointer: coarse` media query):

- Minimum touch target: 44x44px (Apple HIG), 48x48dp (Material)
- Increase padding on interactive elements
- Larger font sizes for readability

Already configured in `globals.css`:

```css
@media (pointer: coarse) {
  .hamburger-btn {
    font-size: 4.5rem;
    min-width: 90px;
    min-height: 90px;
  }
}
```

## Testing Requirements

### Before marking a page complete, verify

**Desktop Browser:**

- [ ] Landscape: Layout uses horizontal space effectively
- [ ] Portrait (narrow window): Single-column, scrollable
- [ ] Resize transitions smoothly between layouts

**Mobile Emulation (DevTools):**

- [ ] iPhone 14 Pro portrait: Touch-friendly, readable
- [ ] iPhone 14 Pro landscape: Appropriate layout
- [ ] iPad portrait: Single or two-column as appropriate
- [ ] iPad landscape: Two-column layout

**Physical Devices (if available):**

- [ ] iOS Safari: Touch gestures work
- [ ] Android Chrome: Touch gestures work
- [ ] Orientation change: Layout adapts immediately

**TV/Large Display:**

- [ ] 1080p: Content appropriately sized
- [ ] 4K: No excessive whitespace, readable text

## File Organization

React-specific design documents live in `.design/ui/react/`:

```text
.design/
└── ui/
    └── react/
        ├── typescript-porting-design.md  # Main porting design
        ├── ts-port-impl-plan.md          # Implementation phases
        ├── uiscale-design.md             # Game page scaling
        └── responsive-design.md          # This document
```

## Related Documents

- `game-page-design.md` - Infinite ocean architecture for game page
- `uiscale-design.md` - Original scaling approach (reference for concepts)
- `typescript-porting-design.md` - Overall React migration design
- `ts-port-impl-plan.md` - Phase-by-phase implementation plan
