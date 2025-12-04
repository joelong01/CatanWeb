# Coordinate System

Source: design_docs/Coordinate-Design.md

## Cube Coordinates

- `HexCoordinates` (shared utility) uses cube coordinates `(Q, R, S)` with invariant `Q + R + S = 0`.
- Provides directional vectors (`Direction.North`, etc.) through `HexCoordinates.Directions` static dictionary.
- Overloads arithmetic operators, distance computation, adjacency checks, and conversion between pixel and cube space.

## Usage in Models

- `TileModel.TileKey`, `RoadKey.TileKey`, and `BuildingKey.HexCoordinates` all rely on `HexCoordinates` to identify board positions.
- `RoadKey` couples a `HexSide` enumeration with a `HexCoordinates` origin to represent oriented edges.
- `BuildingKey` uses `HexPosition` enumeration to map to vertex corners.
- `HarborModel.HexKey` indicates which water tile anchors the harbor; adjacency checks use shared extensions.

## Conversion Helpers

- `HexCoordinates.ToPixelCenter(size, offsetX, offsetY)` matches Blazor SVG and WinUI layout code for flat-top hexagons.
- `HexCoordinates.FromPixel(...)` performs cube rounding to convert pointer coordinates into tile selection (used in Desktop drag/drop).
- `HexCoordinates.MidPoint(...)` calculates visual anchor points for road highlighting.

## Extensions

- `GameModelExtensions.TilesForBuildings(buildingKey)` collects tiles adjacent to a vertex by traversing `HexCoordinates.GetAllNeighbors()`.
- `HexExtensions.Neighbors()` (in `Catan3.Shared.Extensions`) wraps directional offsets for readability.

## TODO / Open Issues

- Both Desktop and WebUI duplicate pixel conversion constants; consolidate into shared helper to avoid drift.
- Road adjacency rules rely on manual lists of neighbor offsets; consider generating from `HexCoordinates.Directions` for maintainability.
