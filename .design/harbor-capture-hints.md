# Design: Harbor capture hints — show where to build to claim a harbor (issue #201)

## Problem

From #201:

> GamePlay: hard to see where to build to get a harbor.
>
> we need to fix this. maybe an un-owned harbor makes the 2 buildings that
> can cause ownership to render at say 50% opacity with the glyph being the
> resource type of the harbor at some small size that makes it not too
> distracting.

Today a harbor is drawn as a marker floating on the water hex next to its
owning tile (a resource circle plus a "dock" line along the shared edge). The
marker tells you _which tile/edge_ the harbor belongs to, but it does **not**
tell you _where you would build a settlement to capture it_. New players (and
even experienced ones on a busy board) can't quickly see that a harbor is one
settlement away.

## Chosen direction (from the developer)

Discussed and refined live with the developer. Instead of dimming the two
buildable settlement spots (the issue's first idea), we draw a lightweight
**connector from the harbor marker to the two build spots that would capture
it**:

- The harbor circle is treated as "just a marker."
- From that circle, draw a thin **arc to each of the two vertices** that can
  claim the harbor.
- Shown for **unowned harbors only**, and **always** while unowned (not gated
  on hover or on the current player's buildability).
- Rendered **on the water hex**, inside the existing harbor content — not a
  board-level overlay.

Answers captured from the developer:

| Question | Decision |
|---|---|
| When shown | Always, for every unowned harbor |
| Which vertices | Both capturing vertices |
| Where the glyph sits | On the water hex, at/around the existing harbor circle |
| Connector shape | Arc from the harbor circle to each vertex |

## Key geometric insight (why this is simple)

The two settlement spots that can capture a harbor are exactly the **two
endpoints of the harbor's edge** — and the existing renderer _already_ computes
them.

In [GameBoard.tsx:118](../react-ui/components/game/board/GameBoard.tsx#L118),
`SIDE_TO_VERTICES[side]` returns those two endpoints as
`[[x1,y1],[x2,y2]]` in the **water hex's local viewBox** (`0 0 100 86.6`).
The harbor content already binds this as `dockVertices`
([GameBoard.tsx:196](../react-ui/components/game/board/GameBoard.tsx#L196)) and
draws the dock line between them
([GameBoard.tsx:273-281](../react-ui/components/game/board/GameBoard.tsx#L273-L281)).
The harbor circle sits at `(cx=50, cy=43.3)` in the same space
([GameBoard.tsx:199-201](../react-ui/components/game/board/GameBoard.tsx#L199-L201)).

Because the water hex shares that edge (and therefore those two corners) with
the owning land tile, the local-space vertices `(25,86.6)`, `(75,86.6)`, etc.
land **exactly on the on-board settlement spots** where the `Building` markers
render. So an arc drawn from `(50,43.3)` to each `dockVertices[i]`, entirely
within the harbor's own SVG, visually points from the harbor marker straight at
the two buildable corners — with **no cross-hex coordinate math and no
board-level overlay**.

This is the crux: everything lives in `HarborHexContent`, in coordinates it
already has.

## Why "unowned only" needs no extra ownership lookup

A harbor is owned iff a player has a settlement/city on _either_ endpoint of
its edge. Therefore, while a harbor is **unowned, both endpoints are guaranteed
empty** and are legitimate capture spots. That means:

- We can always draw arcs to _both_ vertices without checking each spot's
  buildability — an unowned harbor can never have a building on either endpoint.
- The existing owned/unowned signal is already in hand:
  `ownerColors` is non-null when owned
  ([GameBoard.tsx:669-672](../react-ui/components/game/board/GameBoard.tsx#L669-L672)),
  so the hint is simply "render arcs when `!ownerColors`."

## Approach

All changes are contained in `HarborHexContent`
([GameBoard.tsx:190](../react-ui/components/game/board/GameBoard.tsx#L190)).

### 1. Gate on unowned

Add an early computed flag `showCaptureHint = harborType !== 'None' && !ownerColors`.
When a harbor is owned, nothing new renders (the current marker is unchanged).

### 2. Draw two arcs from the circle to the vertices

For each of the two `dockVertices`, render an SVG arc (a quadratic/cubic Bézier)
that:

- **starts** on the harbor circle's perimeter (radius 26 from `(50,43.3)`) in
  the direction of that vertex, and
- **ends** at the vertex point `dockVertices[i]`.

The arc bows gently (control point offset perpendicular to the straight line)
so the two connectors read as a pair of "reach lines" rather than a hard V.
Exact control-point math is deferred to the implementation plan; conceptually
it is `M <circle-edge> Q <bowed-control> <vertex>`.

Styling (final values tuned in the plan):

- Thin stroke (~2–3 viewBox units), semi-transparent, `stroke-linecap: round`.
- A small filled dot at the vertex end to mark the target spot.
- Color: a neutral/attention hue that reads on water and does not collide with
  player colors. Candidate: a dedicated CSS custom property
  (e.g. `--harbor-hint`) rather than a hard-coded value, per the project's
  "no hard-coded colors" rule.
- Rendered **below** the harbor circle and glyph (so the circle/glyph stay
  crisp on top) but **above** the dock line.

The existing harbor circle already contains the resource glyph, so it doubles
as the "small circle with the harbor image" from the original sketch — we reuse
it as the anchor instead of adding a second glyph circle. (If we later want the
anchor smaller, that is a one-number change to `circleRadius`, isolated here.)

### 3. Apply to both render modes

`HarborHexContent` has two branches — font mode
([GameBoard.tsx:212-308](../react-ui/components/game/board/GameBoard.tsx#L212-L308))
and image mode
([GameBoard.tsx:326+](../react-ui/components/game/board/GameBoard.tsx#L326)).
The arcs are the same geometry in both; factor the arc-drawing into a small
local helper/subcomponent (e.g. `HarborCaptureArcs`) and render it in both
branches so behavior is identical regardless of theme.

### 4. No data-flow, model, or backend changes

The harbor model already carries `owner`, `side`, and `harborType`; the water
hex already receives everything. Nothing new is threaded, no new props on
`RollRing`-style callers, no type regeneration.

## Files to change (design-level)

| File | Change |
|---|---|
| [GameBoard.tsx](../react-ui/components/game/board/GameBoard.tsx) | In `HarborHexContent`: compute `showCaptureHint`; add a `HarborCaptureArcs` helper drawing two arcs from the circle to `dockVertices`; render it in both font and image branches when unowned |
| [globals.css](../react-ui/app/globals.css) | Add `--harbor-hint` (and any dot fill) CSS custom property for the connector color |

No changes to the board overlay, geometry helpers, model types, or backend.

## Testing

- **Unit (RTL):** render `HarborHexContent` (or a thin wrapper) with an unowned
  harbor and assert two arc paths + target dots are present and terminate at the
  expected `dockVertices` for a given `side`; render an owned harbor and assert
  the arcs are absent.
- **Regression:** existing harbor marker rendering (circle, glyph, dock line)
  is visually unchanged for both owned and unowned harbors; run the board test
  suite.
- **Manual:** on a running board, confirm every unowned harbor shows two thin
  arcs pointing at its two corner settlement spots; capturing the harbor
  (building on either spot) makes the arcs disappear; arcs are legible but not
  distracting on a busy board and across themes (font + image modes).

## Non-goals

- Dimming/50%-opacity treatment of the settlement `Building` markers (the
  issue's first idea) — superseded by the arc approach.
- Any change to harbor ownership rules or to which spots are buildable.
- Hover/selection interactivity (the hint is always-on for unowned harbors).
- Animating the arcs (possible later polish, not v1).

## Open questions

1. **Arc vs. straight line.** Design assumes a gently bowed arc per the
   developer's "draw an arc" note. If straight lines read cleaner in practice,
   it is a trivial swap (drop the control point). Confirm during implementation
   review.
2. **Target dot.** Whether to place a small dot at each vertex end, or let the
   arc terminate bare. Recommend the dot — it reinforces "build _here_."
3. **Color source.** Confirm `--harbor-hint` is the right home, or whether an
   existing theme token should be reused.
