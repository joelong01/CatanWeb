# Coordinates & Rendering As-Built

**Status:** As-Built
**Source:** `Catan3.Shared` & `react-ui/components/hex-grid`

## 1. Coordinate System

The game uses **Cubic Coordinates** (q, r, s) where `q + r + s = 0`.

* **Model**: `HexCoordinates` class in `Catan3.Shared`.
* **Orientation**: Pointy-topped hexes.
* **Storage**: Serialized as simple objects `{q, r, s}` in JSON.

## 2. Geometry Conversions

React UI replicates the C# geometry logic to ensure precise overlay alignment.

**Formula (Hex to Pixel):**

```typescript
x = size * (sqrt(3) * q + sqrt(3)/2 * r)
y = size * (3./2 * r)
```

## 3. Rendering Layers

The React `GameBoard` component acts as a layered engine:

1. **SVG Layer (Bottom)**
    * `<InfiniteOcean>`: Dynamic water background.
    * `<HexGrid>`: The generic grid system.
    * Individual Hexes (Terrain patterns + Colors).
    * Roads (Path elements between coordinates).
    * Settlements/Cities (Usage of `HexPosition` at vertices).

2. **HTML Overlay Layer (Top)**
    * `NumberToken`: Divs absolutely positioned using `toPixel` conversion.
    * `Robber`: Animated div moving between hex centers.
    * **Interaction**: Click targets are often transparent SVG elements acting as hit-boxes for strict boundary detection.

## 4. Key Differences

* **Blazor**: Uses single large SVG.
* **React**: Uses SVG for geometry/shapes but often overlays HTML/Divs for text, icons, and interactive buttons (Action Clusters) to leverage better accessibility and CSS layout capabilities for UI elements.
