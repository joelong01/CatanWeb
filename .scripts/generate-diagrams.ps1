# Generate Mermaid Diagrams to SVG
# This script generates SVG images from Mermaid source files
# Prerequisites: npm install -g @mermaid-js/mermaid-cli

Write-Host "?? Generating Mermaid Diagrams for Catan3 Companion..." -ForegroundColor Cyan

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceDir = Join-Path $scriptPath "Catan3.GameService/wwwroot/mermaid-source"
$outputDir = Join-Path $scriptPath "Catan3.GameService/wwwroot/diagrams"

# Check if Mermaid CLI is installed
$mmdcVersion = $null
try {
    $mmdcVersion = mmdc --version 2>$null
    Write-Host "? Mermaid CLI found: $mmdcVersion" -ForegroundColor Green
} catch {
    Write-Host "? Mermaid CLI not found. Please install it first:" -ForegroundColor Red
    Write-Host "   npm install -g @mermaid-js/mermaid-cli" -ForegroundColor Yellow
    Write-Host "   Then run this script again." -ForegroundColor Yellow
    exit 1
}

# Ensure output directory exists
if (!(Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    Write-Host "?? Created output directory: $outputDir" -ForegroundColor Yellow
}

# Define diagram files to process
$diagrams = @(
    @{
        Name = "Picking Game Flow"
        Source = "picking-game-flow.mmd"
        Output = "picking-game-flow.svg"
        Description = "User browses and selects available games"
    },
    @{
        Name = "Joining Game Flow" 
        Source = "joining-game-flow.mmd"
        Output = "joining-game-flow.svg"
        Description = "Real-time connection establishment and game joining"
    },
    @{
        Name = "Updating Game Flow"
        Source = "updating-game-flow.mmd"
        Output = "updating-game-flow.svg"
        Description = "Async command processing with SignalR notifications"
    }
)

# Generate each diagram
$success = 0
$total = $diagrams.Count

foreach ($diagram in $diagrams) {
    $sourcePath = Join-Path $sourceDir $diagram.Source
    $outputPath = Join-Path $outputDir $diagram.Output
    
    Write-Host ""
    Write-Host "?? Generating: $($diagram.Name)..." -ForegroundColor Cyan
    Write-Host "   Source: $sourcePath" -ForegroundColor Gray
    Write-Host "   Output: $outputPath" -ForegroundColor Gray
    
    if (!(Test-Path $sourcePath)) {
        Write-Host "   ? Source file not found!" -ForegroundColor Red
        continue
    }
    
    try {
        # Generate SVG with optimal settings
        $result = mmdc -i $sourcePath -o $outputPath -t neutral -b white --scale 2 2>&1
        
        if (Test-Path $outputPath) {
            $fileSize = (Get-Item $outputPath).Length
            Write-Host "   ? Generated successfully! Size: $([math]::Round($fileSize/1KB, 1)) KB" -ForegroundColor Green
            $success++
        } else {
            Write-Host "   ? Generation failed!" -ForegroundColor Red
            Write-Host "   Error: $result" -ForegroundColor Red
        }
    } catch {
        Write-Host "   ? Generation failed with exception: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "?? Generation Summary:" -ForegroundColor Cyan
Write-Host "   ? Successful: $success/$total diagrams" -ForegroundColor Green

if ($success -eq $total) {
    Write-Host ""
    Write-Host "?? All diagrams generated successfully!" -ForegroundColor Green
    Write-Host "?? Diagrams are available at: $outputDir" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "?? Usage in companion.md:" -ForegroundColor Cyan
    Write-Host "   Replace Mermaid text blocks with:" -ForegroundColor Gray
    foreach ($diagram in $diagrams) {
        Write-Host "   ![Diagram]($($diagram.Output))" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "?? For web serving, diagrams are accessible at:" -ForegroundColor Cyan
    Write-Host "   http://localhost:8080/diagrams/$($diagrams[0].Output)" -ForegroundColor Gray
    Write-Host "   http://localhost:8080/diagrams/$($diagrams[1].Output)" -ForegroundColor Gray
    Write-Host "   http://localhost:8080/diagrams/$($diagrams[2].Output)" -ForegroundColor Gray
} else {
    Write-Host ""
    Write-Host "??  Some diagrams failed to generate. Check errors above." -ForegroundColor Yellow
    Write-Host "   You can also use online tools like https://mermaid.live" -ForegroundColor Yellow
}

Write-Host ""