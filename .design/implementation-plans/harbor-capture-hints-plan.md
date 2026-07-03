# Implementation Plan: Harbor capture hints (issue #201)

Design: [.design/harbor-capture-hints.md](../harbor-capture-hints.md) (approved).

## Summary

For every **unowned** harbor, draw two thin arcs from the harbor marker circle
out to the two settlement corners that would capture it, each ending in a small
target dot. Everything is contained in `HarborHexContent` inside
[GameBoard.tsx](../../react-ui/components/game/board/GameBoard.tsx), drawn in the
water hex's own SVG (viewBox `0 0 100 86.6`). No overlay, model, or backend
changes.

## Geometry (worked out)

In the water hex viewBox the harbor circle is `C = (50, 43.3)`, radius
`r = 26`. Every hex corner is exactly distance **50** from `C`, so the arc's
start point on the circle perimeter toward a vertex `V` is:

```text
start = C + (V - C) * (r / 50)   // r/50 = 0.52
end   = V                        // the capture corner (a build spot)
```

`SIDE_TO_VERTICES[side]` ([GameBoard.tsx:118-147](../../react-ui/components/game/board/GameBoard.tsx#L118-L147))
supplies the two `V`s already (bound as `dockVertices` at
[line 196](../../react-ui/components/game/board/GameBoard.tsx#L196)). Because the
water hex shares those corners with the owning land tile, each `end` lands
exactly where the on-board `Building` marker renders.

Arc shape: a quadratic Bézier `M start Q ctrl end`, where `ctrl` is the chord
midpoint pushed perpendicular by `BOW * chordLength`, signed to bow **away from
the edge midpoint** (the two arcs splay apart). `BOW = 0` degrades to straight
lines — kept as a single tunable constant.

## Per-file changes

### 1. `react-ui/components/game/board/GameBoard.tsx`

**(a) Add hint constants** near `DOCK_COLORS`
([line 166](../../react-ui/components/game/board/GameBoard.tsx#L166)):

```tsx
/** Harbor capture-hint geometry (viewBox units). Circle matches HarborHexContent. */
const HINT_CIRCLE = { cx: 50, cy: 43.3, r: 26 };
/** Arc bow as a fraction of chord length; 0 = straight lines. */
const HINT_BOW = 0.16;
```

**(b) Add an exported `HarborCaptureArcs` helper** (place just above
`HarborHexContent`, ~line 177). Exported so it can be unit-tested in isolation:

```tsx
/**
 * Capture hint for an UNOWNED harbor: two arcs from the marker circle to the
 * two settlement corners (SIDE_TO_VERTICES[side]) that would claim it, each
 * ending in a target dot. Rendered inside the harbor's SVG (viewBox 0 0 100 86.6).
 */
export function HarborCaptureArcs({ side }: { side: HexSide }) {
  const verts = SIDE_TO_VERTICES[side];
  const { cx, cy, r } = HINT_CIRCLE;
  // Edge midpoint: bow each arc away from this so the pair splays apart.
  const ex = (verts[0][0] + verts[1][0]) / 2;
  const ey = (verts[0][1] + verts[1][1]) / 2;

  return (
    <g className="harbor-capture-hint" fill="none" pointerEvents="none">
      {verts.map(([vx, vy], i) => {
        const sx = cx + (vx - cx) * (r / 50);
        const sy = cy + (vy - cy) * (r / 50);
        const mx = (sx + vx) / 2;
        const my = (sy + vy) / 2;
        const dx = vx - sx;
        const dy = vy - sy;
        const len = Math.hypot(dx, dy) || 1;
        // Perpendicular to the chord, normalized.
        let px = -dy / len;
        let py = dx / len;
        // Flip so the control point moves away from the edge midpoint E.
        if ((mx - ex) * px + (my - ey) * py < 0) {
          px = -px;
          py = -py;
        }
        const bow = HINT_BOW * len;
        const ctrlX = mx + px * bow;
        const ctrlY = my + py * bow;
        return (
          <path
            key={`arc-${i}`}
            d={`M ${sx} ${sy} Q ${ctrlX} ${ctrlY} ${vx} ${vy}`}
            stroke="var(--harbor-hint)"
            strokeWidth={2.5}
            strokeOpacity={0.85}
            strokeLinecap="round"
          />
        );
      })}
      {verts.map(([vx, vy], i) => (
        <circle
          key={`dot-${i}`}
          cx={vx}
          cy={vy}
          r={3.5}
          fill="var(--harbor-hint)"
          fillOpacity={0.9}
        />
      ))}
    </g>
  );
}
```

**(c) Render the hint in both mode branches, gated on `!ownerColors`.**

- Font mode: insert immediately **before** the center circle comment at
  [line 283](../../react-ui/components/game/board/GameBoard.tsx#L283):

  ```tsx
  {!ownerColors && <HarborCaptureArcs side={side} />}
  ```

- Image mode: insert immediately **before** the `{/* Harbor circle */}` element
  at [line 355](../../react-ui/components/game/board/GameBoard.tsx#L355):

  ```tsx
  {!ownerColors && <HarborCaptureArcs side={side} />}
  ```

Placing it before the circle keeps the circle + glyph crisp on top; the arcs sit
above the base polygon / dock line and reach out to the corners. `harborType ===
'None'` already returns null earlier
([line 204](../../react-ui/components/game/board/GameBoard.tsx#L204)), so `side`
is always real here.

### 2. `react-ui/app/globals.css`

Add the hint color next to the other hex custom properties (the block near
[line 44](../../react-ui/app/globals.css#L44)):

```css
/* Harbor capture hint (#201): arcs from an unowned harbor marker to the two
   settlement spots that would claim it. Amber reads on blue water and does not
   collide with player colors. */
--harbor-hint: #facc15;
```

### 3. `react-ui/components/game/board/__tests__/HarborCaptureArcs.test.tsx` (new)

Unit-test the exported helper's geometry (no theme providers needed):

- Renders `<svg><HarborCaptureArcs side="Top" /></svg>` and asserts:
  - exactly **2** `<path>` elements and **2** `<circle>` (dot) elements;
  - the two dots are centered on `SIDE_TO_VERTICES.Top` → `(25, 86.6)` and
    `(75, 86.6)`;
  - each path's `d` starts (`M sx sy`) at `lerp(center, vertex, 0.52)` — e.g.
    for `(25, 86.6)`: `start = (37, 65.8)` (±0.1);
  - each path ends at its vertex.
- Repeat a quick assertion for one diagonal side (e.g. `TopRight`) to cover a
  non-symmetric case.

Ownership gating (`!ownerColors` hides the arcs) lives in `HarborHexContent`,
which pulls theme hooks (`useFontRendering`, `useHarborFontConfig`) that need
providers — that path is covered by **manual verification** below rather than a
heavyweight render test.

## Files-modified table

| File | Type | Change |
|---|---|---|
| [GameBoard.tsx](../../react-ui/components/game/board/GameBoard.tsx) | modify | Add `HINT_CIRCLE`/`HINT_BOW` constants; add exported `HarborCaptureArcs` helper; render it (gated on `!ownerColors`) in both font and image branches of `HarborHexContent` |
| [globals.css](../../react-ui/app/globals.css) | modify | Add `--harbor-hint` custom property |
| [HarborCaptureArcs.test.tsx](../../react-ui/components/game/board/__tests__/HarborCaptureArcs.test.tsx) | add | Geometry unit test for the arcs + dots |

## Verification steps

1. `cd react-ui && npm run typecheck` — clean.
2. `npm run test:run` — full suite passes, including the new test.
3. From repo root: `pwsh ./catan.ps1 lint` — TypeScript/ESLint/Prettier/markdown/
   spelling clean (add any new words to `cspell.json` if flagged).
4. **Manual** (`pwsh ./catan.ps1 run`): on a live board, confirm
   - every unowned harbor shows two amber arcs ending in dots at its two corner
     settlement spots, in both image and font rendering modes;
   - building a settlement on either corner (claiming the harbor) removes both
     arcs;
   - arcs are legible but not distracting on a full board and do not intercept
     clicks (`pointerEvents="none"`).

## Rollback

All changes are additive and isolated to `HarborHexContent` + one CSS variable;
reverting the three files fully removes the feature with no data or API impact.

## Notes / tunables

- `HINT_BOW` (0.16) and stroke width/opacity are single-point tunables; set
  `HINT_BOW = 0` for straight lines if arcs read as clutter.
- Color is a CSS variable, so theme-specific overrides are trivial later.
