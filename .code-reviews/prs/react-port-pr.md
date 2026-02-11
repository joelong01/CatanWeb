# PR #8 Copilot Review Analysis

**PR:** typescript-react-port -> main
**Reviewed:** 2026-02-10
**Total comments:** 25 (from GitHub Copilot)
**Verdict:** 4 real bugs, 5 reasonable cleanups, rest are nitpicks or incorrect

---

## Must-Fix Summary

| # | File | Issue | Impact |
|---|------|-------|--------|
| 1 | `reorient-evenodd.mjs:18` | `new URL().pathname` breaks on spaces | Font build fails in paths with spaces |
| 2 | `catan-font-glyphs.html:317` | Implicit `window.event` global | Breaks in Firefox/strict mode |
| 3 | `create-catan-font.ps1:107` | `$maxCodepoint=0` on empty map | Auto-assigns outside PUA range |
| 4 | `create-catan-font.ps1:584` | `Get-NetTCPConnection` is Windows-only | Dev server check never works on macOS |

---

## Must-Fix Details

### 1. `reorient-evenodd.mjs` - URL-encoded path breaks on directory with spaces

**File:** `.assets/SVG For Font/reorient-evenodd.mjs:18`

**Current code:**
```js
const projectRoot = join(dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Z]:)/, '$1')), '../..');
```

**Issue:** The script lives under `.assets/SVG For Font/` -- a directory with a
space. `new URL(import.meta.url).pathname` returns URL-encoded paths where spaces
become `%20`. The regex only handles the Windows drive prefix, not `%20` encoding.
On macOS, this produces `.assets/SVG%20For%20Font/` which doesn't exist on disk.

**Fix:** Use `fileURLToPath(import.meta.url)` from `node:url` which handles all
edge cases (spaces, special chars, Windows drives).

**Real impact:** Yes -- font build script currently works by accident because
`dirname()` + `../..` navigates above the space-containing directory. But the
`createRequire()` call on line 19 would fail if the resolved path ever needed to
traverse through the space-encoded segment.

---

### 2. `catan-font-glyphs.html` - Implicit `event` global in `switchView()`

**File:** `.assets/SVG For Font/catan-font-glyphs.html:317`

**Current code:**
```js
function switchView(view) {
  // ...
  event.target.classList.add('active');  // bare `event` = window.event
}
```
Called via: `onclick="switchView('grid')"`

**Issue:** Uses the implicit `window.event` global which is non-standard. Works
in Chrome but Firefox has historically not populated `window.event` in all
contexts, and strict mode / future browser versions could break it.

**Fix:** Change to `onclick="switchView('grid', event)"` and accept the parameter:
`function switchView(view, event)`.

---

### 3. `create-catan-font.ps1` - `$maxCodepoint` starts at 0 when map is empty

**File:** `.assets/SVG For Font/create-catan-font.ps1:107-128`

**Current code:**
```powershell
$maxCodepoint = 0
foreach ($hex in $existingMap.Values) {
    $val = [Convert]::ToInt32($hex, 16)
    if ($val -gt $maxCodepoint) { $maxCodepoint = $val }
}
$nextCodepoint = $maxCodepoint + 1
```

**Issue:** If `$existingMap` is empty (fresh `glyph-map.json`), `$maxCodepoint`
stays 0 and new glyphs get assigned starting at `U+0001` instead of the Private
Use Area (`U+E900`). Would produce a broken font.

**Fix:** Use `$StartHex` parameter as the floor:
```powershell
$maxCodepoint = [Convert]::ToInt32($StartHex, 16) - 1
```

---

### 4. `create-catan-font.ps1` - `Get-NetTCPConnection` is Windows-only

**File:** `.assets/SVG For Font/create-catan-font.ps1:584`

**Current code:**
```powershell
$listener = Get-NetTCPConnection -LocalPort 3000 -State Listen -ErrorAction SilentlyContinue
```

**Issue:** `Get-NetTCPConnection` is a Windows-only cmdlet. On macOS (the current
dev platform), this silently throws, gets caught, and `$devRunning` stays `$false`.
The safety check to avoid deleting `.next` cache while the dev server runs is
completely non-functional on macOS.

**Fix:** Use cross-platform check:
```powershell
if ($IsWindows) {
    $listener = Get-NetTCPConnection -LocalPort 3000 -State Listen -ErrorAction SilentlyContinue
    if ($listener) { $devRunning = $true }
} else {
    $result = bash -c "lsof -ti:3000 2>/dev/null"
    if ($result) { $devRunning = $true }
}
```

---

## Should-Fix Issues

### 5. `catan-font-glyphs.html` - Embedded GLYPH_MAP out of sync

**File:** `.assets/SVG For Font/catan-font-glyphs.html:253`

The HTML viewer has a hardcoded `GLYPH_MAP` ending at `E942`, but `glyph-map.json`
now goes to `E956` (62 glyphs). The viewer is missing 20 glyphs. Worth fixing
since the viewer exists to verify font output.

### 6. `svg-font.ts` - `mappedCount` is dead code

**File:** `.assets/SVG For Font/svg-font.ts:192`

`mappedCount` is incremented in a loop but never read. The actual value is computed
independently via `preEntries.filter((pe) => pe.explicit).length` on line 296.
Easy cleanup.

### 7. `svg-font.ts` - Header comment says wrong filename

**File:** `.assets/SVG For Font/svg-font.ts:2`

Line 2 says `build-icon-font.ts` but the file is `svg-font.ts`. Leftover from
a rename. Trivial fix.

### 8. `svg-font.ts` - Glyph names not sanitized for spaces

**File:** `.assets/SVG For Font/svg-font.ts:116`

`safeGlyphName()` returns the basename without sanitizing spaces. Entries like
`"pirate ship.svg"` produce glyph name `"pirate ship"`. Not currently breaking
anything but could cause issues with some font editors.

### 9. `README.md` - Codepoint range is stale

**File:** `.assets/SVG For Font/README.md:36`

Docs say `U+E900--U+E943` but actual range is now `U+E900--U+E956`.

---

## Nitpicks (no action needed)

| # | File | Suggestion | Why skip |
|---|------|-----------|----------|
| 10 | `glyph-map.json` | "codepoint" -> "code point" | Both spellings valid |
| 11 | `.ai/ai-rules.md` | Section numbering inconsistency | AI instruction file |
| 12 | `session-summary.md` | List numbering resets to 1 | MD renderers auto-number |
| 13 | `catan-font-glyphs.html` | Hardcoded CSS colors | Standalone dev tool, not app |
| 14 | `svg-font.ts` | O(n^2) find on 60 items | Irrelevant perf in build script |

---

## Skipped / Incorrect Suggestions

| # | File | Suggestion | Why wrong |
|---|------|-----------|-----------|
| 15 | `svg-font.ts` | Clarify auto-assignment unused | Help text describes tool behavior, not project state |
| 16 | `.ai/ai-rules.md` | Use `catan.ps1 lint md` | That subcommand may not exist |
| 17 | `README.md` | "may lose detail" is vague | Clear enough in context |
| 18 | `fix-svg-for-font.ps1` | Add key discovery to DESCRIPTION | Over-documenting |
| 19 | `README.md` | Add troubleshooting subsection | Doc request, not code issue |
| 20 | `fix-svg-for-font.ps1` | Use relative paths in examples | Project-root paths are intentional |
| 21 | `checkin.md` | Build cmd described as formatter | AI instruction file |

---

## Stats

| Category | Count |
|----------|-------|
| Must Fix | 4 bugs |
| Should Fix | 5 cleanups |
| Nitpick | 5 suggestions |
| Skip/Incorrect | 7 suggestions |
| **Copilot hit rate (real bugs)** | **16% (4/25)** |
