# Design Review: Home Page Hex Grid

**Date:** 2026-01-25
**Reviewer:** Gemini
**Scope:** `react-ui/components/hex-grid/`, `home-page-hex-design.md`

## 1. Architectural Analysis: Container-Owned Borders

The proposal to move border rendering to the container ("Container-Owned Borders") is **architecturally superior** for grid layouts but presents implementation challenges with the current DOM-based `HexTile` approach.

### Strengths

* **Visual Consistency:** Solves the double-border problem (adjacent borders creating 2x thickness lines).
* **Gap Management:** "Gap reveals container background" is a clean way to handle separation without complex clip-path calculations on child elements.
* **Scalability:** Allows individual tiles to focus purely on content.

### Implementation Risks (HexTile)

The current `HexTile` component uses CSS `clip-path: polygon(...)` via the `.hex-clip-flat` utility class (defined in `globals.css` likely).

* **The Problem:** `clip-path` clips *everything*, including borders defined on the element itself. You cannot have a `border` property on a clipped element; the border gets clipped out.
* **Current Workaround:** As seen in `HexTile` examples, we currently use nested divs:

    ```tsx
    <div className="absolute inset-0 bg-blue-500"> {/* "Border" via background color */}
      <div className="absolute inset-0 scale-[0.9] hex-clip-flat bg-white"> {/* Content */}
    ```

* **Proposed "Gap" Logic:**
    If the container renders a solid background (e.g., "Dark Blue") and we space the `HexTile` elements apart using a `gap` prop, the "borders" are effectively the space between tiles.
  * **However:** The design document says "HexGrid renders border hex shapes at full size". This implies the `HexGrid` iterates twice? Or just renders a background?
  * **Clarification:** If the implementation is "Space the tiles apart", then the `HexGrid` doesn't need to render "border shapes", it just needs to calculate positions with a `gap` offset?
  * **Correction:** `gap` in hex grids is not a simple CSS `gap`. It requires modifying the `hexToPixel` math (increasing spacing multiplier) while keeping the hex element size constant? OR keeping spacing constant and shrinking the element size?
  * **Preferred Approach:** **Shrink the Element**. Keep standard hex spacing logic. The `HexTile` (Outer) is the full size (touching neighbors). It renders the *Border Color*. The `HexContent` (Inner) is scaled down (e.g., 96%). The gap is the pixels between the Inner clip and the Outer edge.

### Conclusion on Borders

The design doc's "Container-Owned Borders" description is slightly confusing.

* **Interpretation A:** The Container renders a big SVG web of strokes. (Best for perfect lines, hard to align with DOM elements).
* **Interpretation B:** The Container renders background. The Tiles render "Inner Content" only, floating with gaps between them. (Easiest).
* **Interpretation C:** The Tiles render a "Border Div" (full size) and an "Content Div" (inset).
  * *Critique:* Interpretation C causes the double-border issue unless `z-index` or strict overlap is managed.

**Recommendation:** Go with **Interpretation C (modified)** or **Interpretation B**.

1. **HexGrid** calculates positions for tightly packed hexes.
2. **HexTile** renders at `size - gap`.
3. **HexGrid** has a `style={{ backgroundColor: borderColor }}`? No, that fills the empty corners too.

**Refined Recommendation (The "Gap" Strategy):**
The design doc says: *"HexGrid renders border hex shapes... Content hexes render inside at scale(1-gap)"*.
This actually implies **Interpretation C**:
The `HexTile` component should accept a `border` prop.

* It renders a `div` (The Border) at 100% size.
* It renders the `children` (The Content) at `100% - gap` size.
* *But wait*, adjacent 100% size hexes overlap? No, they tessellate.
* If they tessellate perfectly, 1px rounding errors cause gaps.
* **Solution:** Make the background of the container the "Border Color". Space the tiles with `gap`.
  * `HexSize` in math = 100.
  * `HexWidth` rendered = 98 (100 - gap).
  * No... that makes the grid shrink.

**Final Decision Logic:**

1. Math uses `hexSize` (e.g., 60px). Distance between centers is fixed.
2. Rendered Tile is `hexSize - gap`.
3. The space between is empty.
4. If we want visible borders, we can't just leave it empty unless the container background *is* the border color.
5. But the container is the "Ocean" (animated SVG? or static div?).
6. **Correction:** The Home Page isn't the Ocean. It's a dashboard.
7. **Pivot:** Lets stick to the specific design document instruction:
    * *"HexGrid renders border hex shapes at full size"*
    * *"Content hexes render at scale(1 - gap/hexSize) inside"*
    * This implies the `HexGrid` iterates the items *twice*? Or `HexGridItem` is responsible for the Border?
    * If `HexGrid` renders borders, it effectively draws the grid lines.

## 2. Component Strategy

The proposed components (`CenterHex`, `MenuHex`, `WaterHex`) are **excellent additions**.
They standardize the "UI within a Hex" pattern which is currently hardcoded in `PlayerSelector`.

**MenuHex:**

* Needs to handle both Client-Side Navigation (`Link`) and Action (`onClick`).
* Should support `disabled` state visually (grayscale/opacity).
* **Grouping:** Should we group these in `components/hex-grid/content/`? Yes.

**CenterHex:**

* Purely decorative.
* Should likely support an optional `pulse` animation for the "Press Start" feel?

**WaterHex:**

* Crucial for the "Infinite" look.
* Should it be animated? (Simple CSS opacity pulse).

## 3. Layout Strategy

The layout `CLUSTER_7` is standard.
**Troubleshooting Section:**

* Essential for Alpha testing.
* Positioning it *below* the SVG grid is correct (don't try to embed it in the graphics).

## 4. Implementation Details

### HexGrid.tsx Updates

Detailed requirements for the update:

1. Add `gap` prop (number, pixels).
2. Add `borderColor` prop (string, CSS color).
3. **Render Loop:**
    * Render `items` as `HexTile`s.
    * Pass `gap` to `HexTile`?
    * **Alternative:** The "Container-Owned" part suggests the GLOW/Border is a separate layer.
    * *Actually*, the cleanest implementation of "Single Borders" in a DOM grid is:
        * Render a BACKGROUND grid of "Border Hexes" (slightly larger? or exact size).
        * Render FOREGROUND grid of "Content Hexes" (inset by gap).
        * This requires two render passes in the `HexGrid` component.
        * **Pass 1 (Background):** `<HexTile className="bg-amber-500" ... />` (The Border)
        * **Pass 2 (Foreground):** `<HexTile className="scale-90" ... />` (The Content)
        * *Correction:* Pass 1 isn't needed if the foreground tiles just float over a background... but the background needs to be hex-shaped? No, if the gaps are small, the container background shows through.
        * *If the container is transparent (e.g. over water)*, then we do need explicit border hexes.

**Recommended `HexGrid` Render Logic:**

```tsx
// 1. Render Border Layer (if borderColor is set)
{items.map(item => (
  <HexTile
    key={`border-${item.id}`}
    width={dims.width}
    height={dims.height}
    position={pixelPos}
    className="z-0" // Behind content
  >
     <div className="w-full h-full hex-clip-flat" style={{ background: borderColor }} />
  </HexTile>
))}

// 2. Render Content Layer
{items.map(item => (
  <HexTile
    key={item.id}
    width={dims.width - (gap * 2)} // Shrink by gap
    height={dims.height - (gap * 2)}
    position={pixelPos}
    className="z-10"
  >
    {item.content}
  </HexTile>
))}
```

*Wait*, shrinking width/height isn't enough for Hexes, aspect ratio matters. Better to use `transform: scale()`.

## 5. Summary of Recommendations

1. **HexGrid Update:** Implement the **Two-Layer Render** approach defined above. It guarantees perfect borders and content sizing without complex CSS nesting inside the consumer components.
2. **Gap Prop:** Add `gap` (default 4px) to `HexGridProps`.
3. **Scale Prop:** Use `scale` for the "Inner Content" reduction if `gap` is tricky to calculate in pixels for hexes. (Actually, `gap` is better, calculate scale `1 - (gap/size)`).
4. **Files:** Proceed with creating the `content/` directory.

## 6. Code Review of Pending Changes

(Waiting for implementation - design looks solid).
