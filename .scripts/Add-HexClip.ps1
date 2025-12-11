param(
    [Parameter(Mandatory = $true)]
    [Alias("in")]
    [string] $InputFile,

    [Parameter(Mandatory = $true)]
    [Alias("out")]
    [string] $OutputFile
)

Write-Host "=== Add-HexClip.ps1 ===" -ForegroundColor Cyan

# Resolve to absolute paths immediately
$InputSvgPath = [System.IO.Path]::GetFullPath($InputFile)
$OutputSvgPath = [System.IO.Path]::GetFullPath($OutputFile)

Write-Host "Input:  $InputSvgPath"
Write-Host "Output: $OutputSvgPath"

if (-not (Test-Path $InputSvgPath)) {
    throw "Input file not found: $InputSvgPath"
}

# Load SVG as XML
[xml] $doc = Get-Content -LiteralPath $InputSvgPath -Raw

$root = $doc.DocumentElement  # <svg ...>

if ($root -eq $null -or $root.Name -ne "svg") {
    throw "Input file does not appear to be an <svg> document."
}

# --- Get width/height (prefer viewBox) ---
Write-Host "`n--- Parsing dimensions ---" -ForegroundColor Yellow

# Try viewBox first: "minX minY width height"
$viewBox = $root.GetAttribute("viewBox")
Write-Host "viewBox attribute: '$viewBox'"

if ([string]::IsNullOrWhiteSpace($viewBox)) {
    # Fallback to width/height attributes, stripping "px"
    $widthAttr  = $root.GetAttribute("width")
    $heightAttr = $root.GetAttribute("height")

    if ([string]::IsNullOrWhiteSpace($widthAttr) -or
        [string]::IsNullOrWhiteSpace($heightAttr)) {
        throw "SVG has no viewBox and no width/height attributes; can't size the hex."
    }

    $width  = [double]($widthAttr  -replace "px","")
    $height = [double]($heightAttr -replace "px","")

    $minX = 0.0
    $minY = 0.0
    Write-Host "Using width/height attributes: width=$width, height=$height"
}
else {
    $parts = $viewBox -split "\s+"
    if ($parts.Count -ne 4) {
        throw "Unexpected viewBox format: '$viewBox'"
    }

    $minX   = [double]$parts[0]
    $minY   = [double]$parts[1]
    $width  = [double]$parts[2]
    $height = [double]$parts[3]
    Write-Host "Parsed viewBox: minX=$minX, minY=$minY, width=$width, height=$height"
}

# --- Compute a point-up hex based on height ---
Write-Host "`n--- Computing hex geometry ---" -ForegroundColor Yellow

# Center of the SVG
$cx = $minX + $width  / 2.0
$cy = $minY + $height / 2.0
Write-Host "Center: cx=$cx, cy=$cy"

# For a point-up regular hex: total vertical size = 2 * R
# Use the full height for the hex (you can scale this down if you want a margin)
$R = $height / 2.0
Write-Host "Hex radius (R): $R"

# Build 6 points around the circle, starting at the top (point-up)
$points = New-Object System.Collections.Generic.List[string]

for ($i = 0; $i -lt 6; $i++) {
    $angleDeg = -90.0 + 60.0 * $i  # -90, -30, 30, 90, 150, 210
    $angleRad = [Math]::PI * $angleDeg / 180.0

    $x = $cx + $R * [Math]::Cos($angleRad)
    $y = $cy + $R * [Math]::Sin($angleRad)

    # Round to 3 decimals to keep file clean
    $xStr = "{0:0.###}" -f $x
    $yStr = "{0:0.###}" -f $y

    $points.Add("$xStr,$yStr")
}

$pointsAttr = [string]::Join(" ", $points)
Write-Host "Hex points: $pointsAttr"

# --- Create <defs>/<clipPath>/<polygon> ---
Write-Host "`n--- Creating clip elements ---" -ForegroundColor Yellow

$ns = $root.NamespaceURI
Write-Host "SVG namespace: '$ns'"

if (-not $ns) {
    # Some files omit an explicit namespace, that's fine.
    Write-Host "Creating elements without namespace"
    $polygon = $doc.CreateElement("polygon")
    $clip    = $doc.CreateElement("clipPath")
    $defs    = $doc.CreateElement("defs")
    $group   = $doc.CreateElement("g")
}
else {
    Write-Host "Creating elements with namespace"
    $polygon = $doc.CreateElement("polygon", $ns)
    $clip    = $doc.CreateElement("clipPath", $ns)
    $defs    = $doc.CreateElement("defs", $ns)
    $group   = $doc.CreateElement("g", $ns)
}

$clipId = "hexClipMask"

$clip.SetAttribute("id", $clipId)
$polygon.SetAttribute("points", $pointsAttr)

# Optional: give it a fill so it’s visible if you ever turn off clipping
$polygon.SetAttribute("fill", "#ffffff")

$clip.AppendChild($polygon) | Out-Null
$defs.AppendChild($clip)   | Out-Null

# --- Move drawable content into a group using the clip-path ---
Write-Host "`n--- Moving content into clipped group ---" -ForegroundColor Yellow

$group.SetAttribute("clip-path", "url(#$clipId)")

# Collect child nodes we want to move (skip defs/title/metadata/desc)
$nodesToMove = @()
$skipNames = @("defs", "title", "metadata", "desc")

Write-Host "Scanning child nodes..."
foreach ($child in @($root.ChildNodes)) {
    if ($child.NodeType -ne [System.Xml.XmlNodeType]::Element) {
        Write-Host "  Skipping non-element: $($child.NodeType)"
        continue
    }

    if ($skipNames -contains $child.Name) {
        Write-Host "  Keeping at root: <$($child.Name)>"
        continue
    }

    $nodesToMove += $child
}

Write-Host "Moving $($nodesToMove.Count) nodes into clipped group..."
foreach ($n in $nodesToMove) {
    $root.RemoveChild($n) | Out-Null
    $group.AppendChild($n) | Out-Null
}

# Insert our new defs and group at the root
# Keep any existing <defs> first, then ours, then the clipped group
Write-Host "`n--- Inserting defs and group ---" -ForegroundColor Yellow

# If there is already a <defs>, insert after it, else prepend
$existingDefs = $null
foreach ($child in $root.ChildNodes) {
    if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and
        $child.Name -eq "defs") {
        $existingDefs = $child
        break
    }
}

if ($existingDefs -ne $null) {
    Write-Host "Found existing <defs>, inserting new defs after it"
    # Insert our defs after existingDefs
    $root.InsertAfter($defs, $existingDefs) | Out-Null
}
else {
    Write-Host "No existing <defs>, inserting new defs at top"
    # Put defs at the top
    if ($root.HasChildNodes) {
        $first = $root.FirstChild
        $root.InsertBefore($defs, $first) | Out-Null
    }
    else {
        $root.AppendChild($defs) | Out-Null
    }
}

Write-Host "Appending clipped group to root"
$root.AppendChild($group) | Out-Null

# --- Save result ---
Write-Host "`n--- Saving ---" -ForegroundColor Yellow

try {
    $doc.Save($OutputSvgPath)

    # Verify the file was actually written
    if (Test-Path $OutputSvgPath) {
        $fileInfo = Get-Item $OutputSvgPath
        Write-Host "SUCCESS: Wrote $($fileInfo.Length) bytes to '$OutputSvgPath'" -ForegroundColor Green
    }
    else {
        Write-Host "ERROR: Save() completed but file does not exist at '$OutputSvgPath'" -ForegroundColor Red
    }
}
catch {
    Write-Host "ERROR saving file: $_" -ForegroundColor Red
    throw
}
