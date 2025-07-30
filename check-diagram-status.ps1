# Status Check for Catan3 Companion Diagram Generation
# This script checks the status of Node.js, npm, Mermaid CLI, and generated diagrams

Write-Host "?? Catan3 Companion Diagram Generation Status" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Gray
Write-Host ""

# Check Node.js
try {
    $nodeVersion = node --version 2>$null
    if ($nodeVersion) {
        Write-Host "? Node.js: $nodeVersion" -ForegroundColor Green
    } else {
        Write-Host "? Node.js: Not found" -ForegroundColor Red
    }
} catch {
    Write-Host "? Node.js: Not found" -ForegroundColor Red
}

# Check npm
try {
    $npmVersion = npm --version 2>$null
    if ($npmVersion) {
        Write-Host "? npm: $npmVersion" -ForegroundColor Green
    } else {
        Write-Host "? npm: Not found" -ForegroundColor Red
    }
} catch {
    Write-Host "? npm: Not found" -ForegroundColor Red
}

# Check Mermaid CLI
try {
    $mmdcVersion = mmdc --version 2>$null
    if ($mmdcVersion) {
        Write-Host "? Mermaid CLI: $mmdcVersion" -ForegroundColor Green
    } else {
        Write-Host "? Mermaid CLI: Not found" -ForegroundColor Red
    }
} catch {
    Write-Host "? Mermaid CLI: Not found" -ForegroundColor Red
}

Write-Host ""
Write-Host "?? Diagram Files Status:" -ForegroundColor Cyan

$diagramsPath = "Catan3.GameService\wwwroot\diagrams"
$sourcePath = "Catan3.GameService\wwwroot\mermaid-source"

if (Test-Path $diagramsPath) {
    $svgFiles = Get-ChildItem -Path $diagramsPath -Filter "*.svg"
    if ($svgFiles.Count -gt 0) {
        Write-Host "? Diagrams directory exists with $($svgFiles.Count) SVG files:" -ForegroundColor Green
        foreach ($file in $svgFiles) {
            $sizeKB = [math]::Round($file.Length / 1KB, 1)
            Write-Host "   ?? $($file.Name) ($sizeKB KB)" -ForegroundColor Gray
        }
    } else {
        Write-Host "??  Diagrams directory exists but no SVG files found" -ForegroundColor Yellow
    }
} else {
    Write-Host "? Diagrams directory not found" -ForegroundColor Red
}

Write-Host ""
Write-Host "?? Source Files Status:" -ForegroundColor Cyan

if (Test-Path $sourcePath) {
    $mmdFiles = Get-ChildItem -Path $sourcePath -Filter "*.mmd"
    if ($mmdFiles.Count -gt 0) {
        Write-Host "? Source directory exists with $($mmdFiles.Count) Mermaid files:" -ForegroundColor Green
        foreach ($file in $mmdFiles) {
            $sizeKB = [math]::Round($file.Length / 1KB, 1)
            Write-Host "   ?? $($file.Name) ($sizeKB KB)" -ForegroundColor Gray
        }
    } else {
        Write-Host "??  Source directory exists but no .mmd files found" -ForegroundColor Yellow
    }
} else {
    Write-Host "? Source directory not found" -ForegroundColor Red
}

Write-Host ""
Write-Host "?? Web URLs (when GameService is running):" -ForegroundColor Cyan
$diagrams = @("picking-game-flow.svg", "joining-game-flow.svg", "updating-game-flow.svg")
foreach ($diagram in $diagrams) {
    if (Test-Path (Join-Path $diagramsPath $diagram)) {
        Write-Host "? http://localhost:8080/diagrams/$diagram" -ForegroundColor Green
    } else {
        Write-Host "? http://localhost:8080/diagrams/$diagram (file missing)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "?? Available Commands:" -ForegroundColor Cyan
Write-Host "   .\generate-diagrams.ps1     - Generate all diagrams" -ForegroundColor Gray
Write-Host "   .\generate-diagrams.bat     - Generate all diagrams (batch)" -ForegroundColor Gray
Write-Host "   .\run-game-service.ps1      - Start the game service" -ForegroundColor Gray
Write-Host "   .\run-game-service.bat      - Start the game service (batch)" -ForegroundColor Gray

# Check if all components are ready
$nodeOk = Test-Command "node"
$npmOk = Test-Command "npm"
$mmdcOk = Test-Command "mmdc"
$diagramsOk = (Test-Path $diagramsPath) -and ((Get-ChildItem -Path $diagramsPath -Filter "*.svg").Count -eq 3)

function Test-Command($cmdname) {
    return [bool](Get-Command -Name $cmdname -ErrorAction SilentlyContinue)
}

Write-Host ""
if ($nodeOk -and $npmOk -and $mmdcOk -and $diagramsOk) {
    Write-Host "?? All systems ready! Diagram generation is fully functional." -ForegroundColor Green
} elseif ($nodeOk -and $npmOk -and $mmdcOk) {
    Write-Host "??  Tools installed but diagrams need to be generated." -ForegroundColor Yellow
    Write-Host "   Run: .\generate-diagrams.ps1" -ForegroundColor Gray
} else {
    Write-Host "? Some components are missing. Run setup:" -ForegroundColor Red
    Write-Host "   .\setup-nodejs-and-mermaid.ps1" -ForegroundColor Gray
}

Write-Host ""