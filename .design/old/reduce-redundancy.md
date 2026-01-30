# WebUI Redundancy Reduction Plan

Goal: consolidate duplicated logic/helpers so rendering and layout stay consistent and easier to maintain. Proposal only; no code changed. This doc is the decision aid before touching code.

Status: pending approval. If approved, execute in the order below with small, isolated PRs; run WebUI build after each step.

## 1) Pattern ID helpers (tiles & harbors)

- **Problem:** Same `GetPatternId`/`GetHarborPatternId` implementations live in multiple files: [WebUI/Components/Board/SharedDefinitions.razor](WebUI/Components/Board/SharedDefinitions.razor), [WebUI/Services/Rendering/BoardSvgGenerator.cs](WebUI/Services/Rendering/BoardSvgGenerator.cs), [WebUI/Services/Rendering/TileSvgRenderer.cs](WebUI/Services/Rendering/TileSvgRenderer.cs), [WebUI/Services/Rendering/HarborSvgRenderer.cs](WebUI/Services/Rendering/HarborSvgRenderer.cs).
- **Problem:** Same `GetPatternId`/`GetHarborPatternId` implementations live in multiple files: [WebUI/Components/Board/SharedDefinitions.razor](WebUI/Components/Board/SharedDefinitions.razor), [WebUI/Services/Rendering/BoardSvgGenerator.cs](WebUI/Services/Rendering/BoardSvgGenerator.cs), [WebUI/Services/Rendering/TileSvgRenderer.cs](WebUI/Services/Rendering/TileSvgRenderer.cs), [WebUI/Services/Rendering/HarborSvgRenderer.cs](WebUI/Services/Rendering/HarborSvgRenderer.cs).
- **Proposal:** Centralize in one static helper (e.g., `BoardSvgConstants` or new `PatternIds` class in Rendering). All callers use it; no duplicate switches.
- **Example shape:** add two static helpers to the chosen utility class and swap call sites to that helper, deleting local copies. No behavior change expected.
- **Risk:** Low; pure refactor. Ensure all call sites updated to avoid missing switch cases.

## 2) Road edge vertex calculation

- **Problem:** `GetEdgeVertices(RoadKey)` exists in both [WebUI/Components/Board/RoadOverlay.razor](WebUI/Components/Board/RoadOverlay.razor) and [WebUI/Services/Rendering/RoadSvgRenderer.cs](WebUI/Services/Rendering/RoadSvgRenderer.cs) with identical side-to-vertex mapping.
- **Proposal:** Move shared logic into `BoardGeometry.GetEdgeVertices(RoadKey)` (builds on existing `GetEdgeVerticesForSide`). Call from overlay and SVG renderer.
- **Risk:** Low; geometry math already shared. Validate hover/build indexing visually.
- **Example change:**
  - In `BoardGeometry`:

    ```csharp
    public static ((double x, double y) v1, (double x, double y) v2) GetEdgeVertices(RoadKey roadKey)
    {
        var (tileX, tileY) = AxialToPixel(roadKey.TileKey.Q, roadKey.TileKey.R);
        var vertices = GetHexVertices(tileX, tileY);
        var (v1Idx, v2Idx) = GetEdgeVerticesForSide(roadKey.HexSide);
        return (vertices[v1Idx], vertices[v2Idx]);
    }
    ```

  - Replace per-file implementations with the shared helper.

## 3) Building vertex position

- **Problem:** `GetVertexPosition(BuildingKey)` appears in both [WebUI/Services/Rendering/BoardGeometry.cs](WebUI/Services/Rendering/BoardGeometry.cs) and [WebUI/Services/Rendering/BuildingSvgRenderer.cs](WebUI/Services/Rendering/BuildingSvgRenderer.cs).
- **Proposal:** Keep the canonical implementation in `BoardGeometry` and have `BuildingSvgRenderer` call it.
- **Risk:** Low; code already identical. Confirm star/hover positions unchanged.
- **Example change:**
  - In `BuildingSvgRenderer`, replace local method body with a call:

    ```csharp
    private static (double x, double y) GetVertexPosition(BuildingKey buildingKey)
        => BoardGeometry.GetVertexPosition(buildingKey);
    ```

  - Or remove the local method and call `BoardGeometry.GetVertexPosition` directly where used.

## 4) ViewBox bounds computation

- **Problem:** `CalculateViewBoxBounds` exists in [WebUI/Components/Board/BoardContainer.razor](WebUI/Components/Board/BoardContainer.razor) (includes harbors and padding) and [WebUI/Pages/Game.razor](WebUI/Pages/Game.razor) (tiles only). Logic is nearly identical.
- **Proposal:** Extract a shared helper (e.g., `BoardGeometry.CalculateViewBoxBounds(GameModel game, bool includeHarbors = false)`) that optionally includes harbors and applies padding. Call from both component and page with appropriate flag.
- **Risk:** Medium; viewBox affects pointer hit-testing and layout. Must verify robber/tile clicks and rasterization sizing.
- **Example change:**

  ```csharp
  public static (double minX, double minY, double width, double height) CalculateViewBoxBounds(
      GameModel gameModel,
      bool includeHarbors,
      double padding = BoardSvgConstants.Padding)
  {
      // compute min/max from tiles; optionally include harbors; apply padding; return tuple
  }
  ```

  - In `BoardContainer`: call with `includeHarbors: true`.
  - In `Game`: call with `includeHarbors: false` (or true if desired) and drop local copy.

## 5) Pattern assets path mapping

- **Problem:** `GetAssetPath` exists in [WebUI/Components/Board/SharedDefinitions.razor](WebUI/Components/Board/SharedDefinitions.razor) and [WebUI/Services/ClientAssetService.cs](WebUI/Services/ClientAssetService.cs) with overlapping purpose.
- **Proposal:** Expose a single asset resolver on `ClientAssetService` and inject/use it everywhere. `SharedDefinitions` should delegate to `AssetService.GetAssetPath` only (no fallback table duplication).
- **Risk:** Low/medium; ensure base-theme fallbacks remain available when service is null during early render.
- **Example change:**
  - Keep resolver in `ClientAssetService` and ensure `SharedDefinitions` calls it directly:

    ```csharp
    private string GetAssetPath(AssetName assetName, string defaultPath)
        => AssetService?.GetAssetPath(assetName) ?? defaultPath;
    ```

  - Remove any other asset-path mapping tables that duplicate the service’s data.

## 6) Purchase/command handlers with shared shapes (optional follow-up)

- **Observation:** Several click/command handlers share patterns (error handling and connection checks). Optional: introduce small helpers (e.g., `ExecuteCommand(Func<Task> action)`). Do only after rendering items are stable.

## Rollout notes

- Order: 1 (patterns), 2 (roads), 3 (buildings), 4 (viewBox), 5 (assets). Item 6 optional.
- Tests/checks per step: run `build-webui`; manual spot-check board render, hover/click, robber target selection, rasterization sizing, and asset/theme loading.
- Keep changes isolated per PR; avoid mixing multiple refactors.
- If any visual regression appears, revert the step and re-apply with tighter tests.
