# PR Code Review: typescript-react-port (2026-02-01 session)

**Branch:** typescript-react-port
**Base:** main
**Reviewed:** 2026-02-01
**Reviewer:** Claude Opus 4.5
**Commits reviewed:** 9f83878, e7c570e, f91cc47, 4717217

## Summary

This PR adds a complete Catan font build pipeline (42 glyphs), automated SVG
fixing for Affinity Designer exports, a two-ring home page layout, and a font
viewer page. The font pipeline includes cross-platform OS font installation
and file-lock diagnostics.

## Changes Overview

| Commit | Purpose |
|--------|---------|
| 9f83878 | Font build pipeline, SVG sources, built fonts, fix script |
| e7c570e | Two-ring home page + font viewer page |
| f91cc47 | Session summary |
| 4717217 | Remove duplicate 3-1.svg |

## Files Changed

| File | Changes | Risk |
|------|---------|------|
| `.assets/SVG For Font/create-catan-font.ps1` | New: font build script with OS install | Medium |
| `.assets/SVG For Font/fix-svg-for-font.ps1` | New: SVG fixer for Designer exports | Low |
| `.assets/SVG For Font/svg-font.ts` | TypeScript font builder | Low |
| `.assets/SVG For Font/README.md` | Comprehensive docs | Low |
| `react-ui/app/page.tsx` | Two-ring home page | Low |
| `react-ui/app/font-viewer/page.tsx` | New: font viewer | Low |
| `react-ui/lib/constants/catanGlyphs.ts` | Updated glyph constants | Low |
| `*.ttf` (5 locations) | Rebuilt font binary | Low |
| 42 SVG files | Glyph source artwork | Low |

## Critical Issues

None. The reviewed code is a build tooling pipeline and UI pages. No
production server code, no user-facing security surfaces, no data handling.

## Important Issues

### 1. File encoding not specified in fix-svg-for-font.ps1

**Location:** `fix-svg-for-font.ps1:157, 367`

`Get-Content` and `Set-Content` default to system encoding, not UTF-8.
SVG files should be read/written as UTF-8 to avoid corruption of any
non-ASCII content.

**Recommendation:** Add `-Encoding UTF8` to both calls.

### 2. Registry write lacks error handling in create-catan-font.ps1

**Location:** `create-catan-font.ps1:391-393`

Font registration to HKCU Fonts registry key has no try/catch. If it
fails, the font is copied to disk but may not be recognized by Windows.

**Recommendation:** Wrap in try/catch with warning message.

### 3. npm install without version pinning

**Location:** `create-catan-font.ps1:230-231`

The font build creates a fresh npm project each time with unpinned
dependency versions. This is acceptable for a local dev tool but could
break if upstream packages publish breaking changes.

**Recommendation:** Pin versions in the inline package.json or check in
a package-lock.json alongside the build script.

## Suggestions

### 1. Font viewer getGlyphEntries() could be memoized

**Location:** `font-viewer/page.tsx:13-29`

Called on every render for 42 items. Not a performance issue at this
scale, but `useMemo` would be cleaner.

### 2. activeGameId is hardcoded to null in home page

**Location:** `page.tsx:49-50`

The TODO comment notes this needs to come from the connection service.
The "Return to Game" button path is currently dead code. Fine for now
since the feature isn't ready.

### 3. fix-svg-for-font.ps1 hexOutlineCount variable initialization order

**Location:** `fix-svg-for-font.ps1:248`

`$script:hexOutlineCount = 0` is set after the if-block that reads it
(line 200). On the first file processed, the variable may not exist if
the Replace callback doesn't run. Should initialize before the
compound path detection block.

## Security Review

- No hardcoded credentials or secrets
- P/Invoke code (Restart Manager API) is diagnostic-only, not in critical
  path -- silent failure is acceptable
- SVG files are artwork assets, no executable content
- Font build runs locally, not in CI -- npm supply chain risk is minimal
- No user input handling in the React pages beyond navigation

## Testing Verification

- Build: All projects pass (`pwsh ./catan.ps1 build`)
- Tests: 57 passed, 2 skipped (pre-existing deprecated), 0 failed
- Font build: 42 glyphs built successfully
- Manual verification: ore, wheat, desert hex glyphs render correctly
  after fix-svg-for-font.ps1 processing

## Approval Status

- [x] No critical issues
- [x] Build passes
- [x] Tests pass
- [x] Ready for PR
