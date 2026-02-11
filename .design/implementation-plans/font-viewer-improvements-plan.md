# Font Viewer Improvements

## Goal

Reorganize font viewer glyphs into logical categories and add
right-click copy-to-clipboard for pasting glyphs into other apps.

## Changes

### font-viewer/page.tsx

**File:** `react-ui/app/font-viewer/page.tsx`

#### Category Grouping

Add `GLYPH_CATEGORIES` constant defining display order:

- **Buildings & Roads**: City, Settlement, Road
- **Resource Tiles**: BrickHex, DesertHex, OreHex, SheepHex, WheatHex,
  WoodHex
- **Harbors**: ThreeToOneHarbor, BrickHarbor, OreHarbor, SheepHarbor,
  WheatHarbor, WoodHarbor
- **Knights & Cities**: Knight, KnightAttacking, KnightKneeling,
  KnightStanding, Deserter, Diplomat, Inventor, Merchant, Politics,
  Intrigue, Science, Entry, Metro, Wagon
- **Stats & Scoring**: Laurel, Star, Sum, BadRoll, GoodRoll,
  LargestArmy, LongestRoad, Target, Check
- **Game Elements**: Ship, Robber, PirateShip, SolidShield

Modify `getGlyphEntries()` to return grouped entries:
`{ label: string; entries: GlyphEntry[] }[]`

- Grid view: category header (`<h2>`) + glyph grid per section
- Table view: category header rows as full-width dividers

#### Copy to Clipboard

- Add `onContextMenu` handler on each glyph card/row
- `e.preventDefault()` to suppress browser context menu
- `navigator.clipboard.writeText(entry.char)` to copy the raw Unicode
  character
- Brief "Copied!" visual feedback (1.5s timeout)
- State: `copiedKey: string | null` to track which glyph was copied
- Both grid and table views get the handler and feedback

## Files Modified

| File                                | Action                             |
| ----------------------------------- | ---------------------------------- |
| `react-ui/app/font-viewer/page.tsx` | Category grouping + clipboard copy |

## Verification

1. `pwsh ./catan.ps1 build` -- must pass
2. Font viewer: glyphs grouped by category with section headers
3. Both grid and table views show grouping
4. Right-click any glyph: character copied, "Copied!" feedback shown
5. Paste into Word/Notepad: glyph renders (requires Catan font on OS)
