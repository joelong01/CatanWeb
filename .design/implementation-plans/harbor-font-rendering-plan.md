# Harbor Font Rendering Experiment

## Goal

Replace PNG harbor images with CatanFont glyphs on the controls-test
board to evaluate readability at distance on large 4K displays.

## Changes

### GameBoard.tsx

**File:** `react-ui/components/game/board/GameBoard.tsx`

- Import `CatanGlyph` from `@/lib/constants/catanGlyphs`
- Add `HARBOR_FONT_CONFIG` constant mapping each `HarborType` to its
  hex-background glyph, center-circle glyph, and foreground color
- Add `fontRendering?: boolean` prop to `HarborHexContentProps`
- When `fontRendering` is true, `HarborHexContent` renders:
  - Background: Large CatanFont hex glyph filling the hex shape via
    centered `font-catan` span, clipped to hex, in resource color
  - Center circle: SVG `<text>` with CatanFont harbor glyph replacing
    the `<image>` element
  - Keep dock circle stroke and connection lines
- Add `fontRendering?: boolean` prop to `GameBoardProps`
- Pass through to `HarborHexContent` in `harborItems` builder (~line 522)

Color mapping:

| HarborType   | Hex Glyph         | Harbor Glyph      | Color     |
| ------------ | ----------------- | ----------------- | --------- |
| Wheat        | WheatHex (E93F)   | WheatHarbor (E940)| `#deb887` |
| Wood         | WoodHex (E941)    | WoodHarbor (E942) | `#228b22` |
| Sheep        | SheepHex (E93C)   | SheepHarbor (E93D)| `#90ee90` |
| Brick        | BrickHex (E933)   | BrickHarbor (E934)| `#cd5c5c` |
| Ore          | OreHex (E93A)     | OreHarbor (E93B)  | `#a0a0a0` |
| ThreeForOne  | ThreeToOneHarbor  | ThreeToOneHarbor  | `#c0c0c0` |

### controls-test/page.tsx

**File:** `react-ui/app/controls-test/page.tsx`

- Pass `fontRendering` to `<GameBoard>` (~line 2152). One-line change.

## Files Modified

| File                                           | Action                                  |
| ---------------------------------------------- | --------------------------------------- |
| `react-ui/components/game/board/GameBoard.tsx` | Font config, HarborHexContent, new prop |
| `react-ui/app/controls-test/page.tsx`          | Pass `fontRendering` prop               |

## Verification

1. `pwsh ./catan.ps1 build` -- must pass
2. Controls-test page: all harbors show CatanFont glyphs instead of PNGs
3. Wheat harbor (topmost): WheatHex background in wheat color,
   WheatHarbor glyph in center circle
4. Other harbors render with their respective glyphs and colors
