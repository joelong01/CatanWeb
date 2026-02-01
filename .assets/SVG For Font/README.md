# SVG For Font

This directory contains the source SVGs and build pipeline for the **Catan.ttf** icon font.

## Quick Start

From the project root:

```powershell
pwsh "./.assets/SVG For Font/create-catan-font.ps1"
```

This builds the font, copies it to all project locations, installs it to the OS, and
clears the Next.js cache.

## How It Works

### Pipeline

```text
SVG files  -->  svgicons2svgfont  -->  SVG font  -->  svg2ttf  -->  Catan.ttf
```

1. **create-catan-font.ps1** sets up a temporary Node project (`.iconfont-build/`)
2. **svg-font.ts** runs the actual build using `svgicons2svgfont` and `svg2ttf`
3. The built font is written to `Catan3.GameService/wwwroot/fonts/`
4. Copies are installed to all other project locations that need the font
5. The font is installed to the OS per-user font directory (Windows/macOS/Linux)

### Codepoint Assignment

Every SVG is mapped to a fixed Unicode codepoint in **glyph-map.json**. All assignments
are explicit to ensure idempotent builds -- adding or removing an SVG never shifts
other glyphs' codepoints.

The codepoints live in the Unicode Private Use Area (U+E900--U+E942).

## Files

| File | Purpose |
|------|---------|
| `create-catan-font.ps1` | Build script -- run this to rebuild the font |
| `fix-svg-for-font.ps1` | Fix SVG exports for font compatibility (fill-rule, colors, strokes) |
| `svg-font.ts` | TypeScript build pipeline (svgicons2svgfont + svg2ttf) |
| `glyph-map.json` | SVG filename to Unicode codepoint mapping (source of truth) |
| `catan-font-glyphs.html` | Standalone glyph viewer (open in browser, uses local font) |
| `*.svg` | Source glyph artwork |

## Font Locations

The built font is installed to these locations:

| Location | Used By |
|----------|---------|
| `Catan3.GameService/wwwroot/fonts/` | Primary build output, served by GameService |
| `react-ui/public/fonts/` | Next.js (loaded via `localFont()` in layout.tsx) |
| `react-ui/public/themes/base/fonts/` | Theme reference |
| `WebUI/wwwroot/themes/base/fonts/` | Blazor WebAssembly |
| `DesktopApp/Assets/Fonts/` | WinUI3 desktop app |

## Adding a New Glyph

1. Export the SVG from Affinity Designer (or similar) into this directory
2. Add an entry to `glyph-map.json` with the next available codepoint
3. Add a corresponding entry to `react-ui/lib/constants/catanGlyphs.ts`
4. Run `pwsh "./.assets/SVG For Font/create-catan-font.ps1"`

## Using Affinity Designer 2 to Export SVGs

The font builder (`svgicons2svgfont`) converts `<path>` elements into font glyphs.
Getting the export settings right is critical -- incorrect settings produce blank
or broken glyphs.

### Document Setup

- **Document size doesn't need to be specific** -- the font builder uses
  `--normalize` to scale each glyph into the font's em square
- **Exception: related glyphs must match.** Glyphs that represent the same
  category (e.g. hex resource tiles) should all use the same document size so
  they render at the same relative scale. The hex tiles use **236 x 208**.

### Fill Rule

The correct `fill-rule` depends on the SVG structure:

| Structure | Fill Rule | When |
|-----------|-----------|------|
| **Multiple `<path>` elements** | `nonzero` | Each shape is a separate path (brick, sheep, wood style) |
| **Single compound `<path>`** | `evenodd` | All shapes merged into one curve (wheat, ore style) |

**Multiple paths + nonzero (preferred):** Export shapes as separate layers.
Each `<path>` gets `style="fill-rule:nonzero;"`. This is how brick, sheep,
and wood hex glyphs work.

**Single compound path + evenodd:** Use **Layer > Geometry > Compound** to
merge all shapes into one curve, then export. The single `<path>` needs
`style="fill-rule:evenodd;"` so overlapping sub-paths create correct
holes. This is how wheat and ore hex glyphs work.

**Why it matters:** With `nonzero`, path winding direction determines
fill vs hole. Compound paths from Designer have arbitrary winding, so
`nonzero` fills everything solid. `evenodd` ignores direction and
alternates fill/hole based on overlap count.

The `fix-svg-for-font.ps1` script detects compound paths automatically
and chooses the correct fill-rule.

### Path Structure

Two export approaches work:

- **Separate paths (preferred):** Export shapes as individual layers.
  Results in multiple `<path>` elements with `fill-rule:nonzero` each.
  Brick (3 paths), sheep (11), wood (16) use this approach.
- **Single compound curve:** Use Layer > Geometry > Compound to merge
  everything, then export. Results in one `<path>` with many sub-paths.
  Requires `fill-rule:evenodd`. Wheat and ore use this approach.

Avoid the middle ground -- hundreds of tiny separate paths (the default
Affinity export without compounding) may lose detail.

### Export Dialog Settings

When exporting SVG from Affinity Designer 2:

| Setting | Value | Why |
|---------|-------|-----|
| **Rasterize** | **Nothing** | Rasterized content (`<image>`, `<use xlink:href>`) produces blank glyphs |
| **Flatten transforms** | **On** | Bakes group transforms into path coordinates so `svgicons2svgfont` sees them |
| **Export text as curves** | **On** | Any text must be converted to paths |

### Troubleshooting

**Glyph is blank or a solid rectangle:**

- Check that the SVG has `<path>` elements (not `<image>` or `<use>` refs)
- Re-export with **Rasterize** set to **Nothing**

**Glyph lost detail / holes are filled in:**

- Multi-path SVG: check that each `<path>` has `style="fill-rule:nonzero;"`
- Single compound path: check that it has `style="fill-rule:evenodd;"`
- Run `fix-svg-for-font.ps1` to auto-detect and fix

**Glyph is the wrong size relative to similar glyphs:**

- Check that the document size / viewBox matches the other glyphs in the
  same category (e.g. hex tiles should all be `236 x 208`)

### Automated Fix-Up

After exporting, run `fix-svg-for-font.ps1` to fix common issues automatically:

```powershell
# Fix a specific file
pwsh "./.assets/SVG For Font/fix-svg-for-font.ps1" wheat-bw-hex.svg

# Fix all hex tile SVGs and set viewBox to 236x208
pwsh "./.assets/SVG For Font/fix-svg-for-font.ps1" *-bw-hex.svg -ViewBox "236 208"

# Preview changes without modifying files
pwsh "./.assets/SVG For Font/fix-svg-for-font.ps1" -WhatIf

# Fix all SVGs in the directory
pwsh "./.assets/SVG For Font/fix-svg-for-font.ps1"
```

The script auto-detects compound paths vs multi-path SVGs and sets the
correct fill-rule (`evenodd` for compound, `nonzero` for multi-path).
It also removes fill colors and strokes, and optionally normalizes the
viewBox. Files are overwritten in place (use git to revert).

### Quick Diagnostic

To check an SVG's structure after export:

```powershell
# Count paths, groups, and check for rasterized content
Select-String -Pattern "<path" -Path my-glyph.svg | Measure-Object
Select-String -Pattern "<g[ >]" -Path my-glyph.svg | Measure-Object
Select-String -Pattern "<image|<use|xlink:href" -Path my-glyph.svg
Select-String -Pattern "fill-rule" -Path my-glyph.svg
```

A healthy SVG for font conversion has:

- A few `<path>` elements with `fill-rule:nonzero`, OR one compound
  `<path>` with `fill-rule:evenodd` (not hundreds of tiny paths)
- Zero `<image>`, `<use>`, or `xlink:href` elements
- No fill colors or strokes (font glyphs are monochrome)
- A viewBox matching related glyphs

## OS Font Installation

By default, the script installs the font to the OS per-user font directory so it
appears in system tools like charmap and font viewers. No admin/root required.

| OS | Install Location |
|----|-----------------|
| Windows | `%LOCALAPPDATA%\Microsoft\Windows\Fonts\` + HKCU registry |
| macOS | `~/Library/Fonts/` |
| Linux | `~/.local/share/fonts/` + `fc-cache` |

Use `-NoInstall` to skip OS font installation.

## Script Options

```text
pwsh "./.assets/SVG For Font/create-catan-font.ps1" -Help
pwsh "./.assets/SVG For Font/create-catan-font.ps1" -SkipInstall    # build only, don't copy to project dirs
pwsh "./.assets/SVG For Font/create-catan-font.ps1" -NoInstall       # skip OS font installation
pwsh "./.assets/SVG For Font/create-catan-font.ps1" -SkipClearCache  # don't clear .next
```
