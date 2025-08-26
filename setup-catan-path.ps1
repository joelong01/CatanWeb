# Simple setup script for Catan path
$newPath = "D:\CatanGames"

Write-Host "Setting up Catan Games directory at: $newPath" -ForegroundColor Cyan

# Set user environment variable (no admin needed)
[Environment]::SetEnvironmentVariable("CATAN_DOCUMENTS_PATH", $newPath, "User")
Write-Host "✓ User environment variable CATAN_DOCUMENTS_PATH set to: $newPath" -ForegroundColor Green

# Create directory structure
$catanDir = "$newPath\Catan Saved Games"
New-Item -ItemType Directory -Path $catanDir -Force | Out-Null
Write-Host "✓ Created directory: $catanDir" -ForegroundColor Green

New-Item -ItemType Directory -Path "$catanDir\Players" -Force | Out-Null
Write-Host "✓ Created directory: $catanDir\Players" -ForegroundColor Green

New-Item -ItemType Directory -Path "$catanDir\Tests" -Force | Out-Null
Write-Host "✓ Created directory: $catanDir\Tests" -ForegroundColor Green

Write-Host "`nSetup complete!" -ForegroundColor Green
Write-Host "Catan will now save games to: $catanDir" -ForegroundColor White
Write-Host "Restart the Catan Desktop app to use the new location." -ForegroundColor Yellow