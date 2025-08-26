# Script to copy latest saved games to test directory
param(
    [string]$TestDataPath = "Tests.DesktopApp.UI\ScriptedTestData"
)

Write-Host "Updating test files from latest saved games..." -ForegroundColor Green

# Get the saved games directory from environment variable
$documentsPath = $env:CATAN_DOCUMENTS_PATH
if (-not $documentsPath) {
    $documentsPath = [Environment]::GetFolderPath("MyDocuments")
    Write-Warning "CATAN_DOCUMENTS_PATH not set, using default: $documentsPath"
} else {
    Write-Host "Using CATAN_DOCUMENTS_PATH: $documentsPath" -ForegroundColor Yellow
}

$savedGamesDir = Join-Path $documentsPath "Catan Saved Games"
Write-Host "Saved games directory: $savedGamesDir" -ForegroundColor Yellow

if (-not (Test-Path $savedGamesDir)) {
    Write-Error "Saved games directory not found: $savedGamesDir"
    exit 1
}

# Find latest .catan_test files for each game type
$regularFiles = Get-ChildItem -Path $savedGamesDir -Filter "Regular-*.catan_test" | Sort-Object LastWriteTime -Descending
$expansionFiles = Get-ChildItem -Path $savedGamesDir -Filter "Expansion-*.catan_test" | Sort-Object LastWriteTime -Descending

$testDataDir = Resolve-Path $TestDataPath
Write-Host "Test data directory: $testDataDir" -ForegroundColor Yellow

function ValidateCatanTestFile {
    param([string]$filePath)
    
    try {
        # Check encoding - read as bytes to check for compression
        $bytes = [System.IO.File]::ReadAllBytes($filePath)
        
        if ($bytes[0] -eq 0x1B) {
            Write-Error "File is compressed/binary: $filePath"
            return $false
        }
        
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
            Write-Warning "File has UTF-8 BOM: $filePath"
        }
        
        # Validate JSON structure
        $jsonContent = Get-Content $filePath -Raw -Encoding UTF8 | ConvertFrom-Json
        
        if (-not $jsonContent.gameModel) {
            Write-Error "Missing gameModel property: $filePath"
            return $false
        }
        
        Write-Host "Validated: $filePath" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Error "Invalid JSON: $filePath - $($_.Exception.Message)"
        return $false
    }
}

function CopyGameFile {
    param(
        [string]$sourceFile,
        [string]$targetFile,
        [string]$gameType
    )
    
    Write-Host "Processing $gameType game..." -ForegroundColor Cyan
    Write-Host "  Source: $sourceFile" -ForegroundColor Gray
    Write-Host "  Target: $targetFile" -ForegroundColor Gray
    
    if (ValidateCatanTestFile $sourceFile) {
        Copy-Item $sourceFile $targetFile -Force
        Write-Host "Copied $gameType game to $targetFile" -ForegroundColor Green
        return $true
    } else {
        Write-Error "Failed to validate $gameType source file"
        return $false
    }
}

$success = $true

# Copy Regular game if found
if ($regularFiles.Count -gt 0) {
    $latestRegular = $regularFiles[0].FullName
    $regularTarget = Join-Path $testDataDir "Regular.catan_test"
    
    if (-not (CopyGameFile $latestRegular $regularTarget "Regular")) {
        $success = $false
    }
} else {
    Write-Warning "No Regular-*.catan_test files found in $savedGamesDir"
}

# Copy Expansion game if found
if ($expansionFiles.Count -gt 0) {
    $latestExpansion = $expansionFiles[0].FullName
    $expansionTarget = Join-Path $testDataDir "Expansion.catan_test"
    
    if (-not (CopyGameFile $latestExpansion $expansionTarget "Expansion")) {
        $success = $false
    }
} else {
    Write-Warning "No Expansion-*.catan_test files found in $savedGamesDir"
}

if ($success) {
    Write-Host "Test files updated successfully!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some operations failed - see errors above" -ForegroundColor Red
    exit 1
}