# PowerShell script to copy the most recent .catan_test file from saved games directory
# and rename it based on game type (Regular or Expansion)

# Check for CATAN_DOCUMENTS_PATH environment variable first
$documentsPath = $env:CATAN_DOCUMENTS_PATH
if ($documentsPath) {
    $sourceDir = Join-Path $documentsPath "Catan Saved Games"
    if (Test-Path $sourceDir) {
        Write-Host "Using CATAN_DOCUMENTS_PATH: $sourceDir" -ForegroundColor Green
    } else {
        Write-Host "CATAN_DOCUMENTS_PATH set but directory not found: $sourceDir" -ForegroundColor Red
        $sourceDir = $null
    }
}

# If no env var or path doesn't exist, try common locations
if ($null -eq $sourceDir) {
    $possiblePaths = @(
        [System.IO.Path]::Combine([Environment]::GetFolderPath("MyDocuments"), "Catan Saved Games"),
        "C:\Users\$env:USERNAME\OneDrive\Documents\Catan Saved Games",
        "C:\Users\$env:USERNAME\Documents\Catan Saved Games"
    )

    foreach ($path in $possiblePaths) {
        if (Test-Path $path) {
            $sourceDir = $path
            Write-Host "Found Catan Saved Games at: $sourceDir" -ForegroundColor Green
            break
        }
    }
}

if ($null -eq $sourceDir) {
    Write-Host "Could not find Catan Saved Games directory." -ForegroundColor Red
    Write-Host "Set CATAN_DOCUMENTS_PATH environment variable or ensure saved games exist in:" -ForegroundColor Yellow
    foreach ($path in $possiblePaths) {
        Write-Host "  $path" -ForegroundColor Gray
    }
    exit 1
}

$destDir = $PSScriptRoot

# Find the most recent .catan_test file
$latestFile = Get-ChildItem -Path $sourceDir -Filter "*.catan_test" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1

if ($null -eq $latestFile) {
    Write-Host "No .catan_test files found in $sourceDir" -ForegroundColor Red
    exit 1
}

Write-Host "Found latest file: $($latestFile.Name)" -ForegroundColor Green

# Read the file to determine game type
$content = Get-Content -Path $latestFile.FullName -Raw | ConvertFrom-Json

$gameType = $content.gameModel.gameType
Write-Host "Game type detected: $gameType" -ForegroundColor Yellow

# Determine destination filename based on game type
$destFileName = switch ($gameType.ToLower()) {
    "regular" { "Regular.catan_test" }
    "expansion" { "Expansion.catan_test" }
    default { 
        Write-Host "Unknown game type: $gameType. Using original filename." -ForegroundColor Yellow
        $latestFile.Name 
    }
}

$destPath = Join-Path -Path $destDir -ChildPath $destFileName

# Copy the file
try {
    Copy-Item -Path $latestFile.FullName -Destination $destPath -Force
    Write-Host "Successfully copied to: $destFileName" -ForegroundColor Green
    Write-Host "Source: $($latestFile.FullName)" -ForegroundColor Gray
    Write-Host "Destination: $destPath" -ForegroundColor Gray
}
catch {
    Write-Host "Error copying file: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}