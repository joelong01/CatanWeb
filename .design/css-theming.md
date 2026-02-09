# CSS & Theming

**Last verified:** January 30, 2026

## Architecture

The React UI uses **Tailwind CSS v4** with CSS custom properties for
theming. All styling is in `react-ui/app/globals.css` -- the project does
NOT use CSS Modules.

**Critical:** Custom utilities must use the `@utility` directive, not
`@layer utilities`. Tailwind v4 treats `@layer utilities` as a standard
CSS cascade layer and silently drops matching class names.

## Design Tokens

### Game Background Colors

| Token | Value | Usage |
|-------|-------|-------|
| `--game-bg-primary` | `#222222` | Main background |
| `--game-bg-secondary` | `#333333` | Secondary surfaces |
| `--game-bg-panel` | `#2a2a2a` | Panel backgrounds |

### Text Colors

| Token | Value | Usage |
|-------|-------|-------|
| `--text-primary` | `#eeeeee` | Primary text |
| `--text-secondary` | `#cccccc` | Secondary text |
| `--text-muted` | `#999999` | Muted/disabled text |

### Accent Colors

| Token | Value | Usage |
|-------|-------|-------|
| `--accent-primary` | `#007bff` | Action buttons |
| `--accent-hover` | `#0056b3` | Button hover state |
| `--accent-success` | `#4caf50` | Success states |
| `--accent-error` | `#f44336` | Error states |
| `--accent-warning` | `#ff9800` | Warning states |

### Hex Grid

| Token | Value | Usage |
|-------|-------|-------|
| `--hex-border-idle` | `rgba(255,255,255,0.3)` | Default hex border |
| `--hex-border-hover` | `#3b82f6` | Hex hover highlight |

## Player Colors

Each player has primary, secondary, and foreground colors:

| Player | Primary | Secondary | Foreground |
|--------|---------|-----------|------------|
| White | `#ffffff` | `#d0d0d0` | `#000000` |
| Blue | `#0078d4` | `#005a9e` | `#ffffff` |
| Orange | `#ff8c00` | `#d67700` | `#000000` |
| Red | `#e81123` | `#c50f1f` | `#ffffff` |
| Green | `#107c10` | `#0b5c0b` | `#ffffff` |
| Brown | `#8b4513` | `#6b3410` | `#ffffff` |

Accessed via `--color-player-{color}-primary`, `--color-player-{color}-secondary`,
`--color-player-{color}-foreground`.

## Resource Colors

| Resource | Color | Token |
|----------|-------|-------|
| Wheat | `#f4d03f` | `--color-wheat` |
| Wood | `#2e7d32` | `--color-wood` |
| Brick | `#c0392b` | `--color-brick` |
| Sheep | `#81c784` | `--color-sheep` |
| Ore | `#607d8b` | `--color-ore` |
| Desert | `#d4a574` | `--color-desert` |

## Tailwind v4 Integration

All CSS custom properties are exposed as Tailwind tokens via `@theme` in
`globals.css`:

```css
@theme {
  --color-game-bg-primary: var(--game-bg-primary);
  --color-player-blue-primary: var(--color-player-blue-primary);
  /* ... etc */
  --spacing-panel: 1rem;
  --spacing-card: 0.75rem;
}
```

This allows using tokens directly in Tailwind classes like
`bg-game-bg-primary` or `text-player-blue-primary`.

## Custom @utility Directives

### 3D Transform Utilities

| Utility | Purpose |
|---------|---------|
| `backface-hidden` | `backface-visibility: hidden` |
| `preserve-3d` | `transform-style: preserve-3d` |
| `perspective-1000` | `perspective: 1000px` |
| `rotate-y-180` | `transform: rotateY(180deg)` |

### Hex Clip Paths

| Utility | Purpose |
|---------|---------|
| `hex-clip` | Pointy-top hexagon clip-path |
| `hex-clip-flat` | Flat-top hexagon clip-path (used by game board) |

### Animation Utilities

| Utility | Purpose |
|---------|---------|
| `animate-winner-spin` | Victory hex rotation (3x spin) |
| `animate-winner-counter-spin` | Counter-rotation for inner content |
| `animate-confetti-burst` | Particle explosion effect |
| `animate-firework-rocket` | Upward trajectory animation |
| `animate-firework-flash` | Explosion flash at apex |
| `animate-firework-spark` | Radial spark burst with gravity |

All animations use CSS custom properties (`--delay`, `--duration`) set
via inline React styles for per-instance timing.

## Typography

### Catan Font

Custom font loaded via Next.js `localFont()` in `layout.tsx`:

```typescript
const catanFont = localFont({
  src: './fonts/Catan.ttf',
  variable: '--font-catan',
});
```

Used for game-specific glyphs (shield, pirate, resource icons) via
`CatanFont` character codes.

### Icon Libraries

- **FontAwesome 6.x** -- General UI icons
- **Catan.ttf** -- Game-specific glyphs (robber, buildings, resources)

## Responsive Layout

- **Dark theme** always active (`<html class="dark">`)
- **App shell:** Three-zone grid layout (nav column + header bar +
  content area). See [app-shell.md](app-shell.md) for full details.
- **Landscape:** Full board with floating panels around edges
- **Portrait:** Planned tabbed layout (board vs controls) -- not yet
  fully implemented
- **Touch targets:** Minimum 44px for mobile interaction
