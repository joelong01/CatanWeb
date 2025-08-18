# Simple script to update .catan_test files to use correct JSON format
param(
    [string]$TestDataPath = "Tests.DesktopApp.UI\ScriptedTestData"
)

Write-Host "Updating .catan_test files to use correct JsonSerializerOptions..." -ForegroundColor Green

# Build the CLI tool first
Write-Host "Building CLI tool..." -ForegroundColor Yellow
dotnet build "Catan3.CLI\Catan3.CLI.csproj" -c Debug

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build CLI tool"
    exit 1
}

# Find all .catan_test files
$testFiles = Get-ChildItem -Path $TestDataPath -Filter "*.catan_test" -Recurse

foreach ($file in $testFiles) {
    Write-Host "Updating $($file.Name)..." -ForegroundColor Cyan
    
    # Create backup
    $backupPath = "$($file.FullName).backup"
    Copy-Item $file.FullName $backupPath
    
    try {
        # Read the JSON content
        $jsonContent = Get-Content $file.FullName -Raw | ConvertFrom-Json
        
        # Convert enum values from integers to strings
        if ($jsonContent.GameModel.EntitlementPurchaseModel) {
            foreach ($item in $jsonContent.GameModel.EntitlementPurchaseModel) {
                switch ($item.Entitlement) {
                    0 { $item.Entitlement = "undefined" }
                    1 { $item.Entitlement = "devCard" }
                    2 { $item.Entitlement = "settlement" }
                    3 { $item.Entitlement = "city" }
                    4 { $item.Entitlement = "road" }
                    5 { $item.Entitlement = "ship" }
                    6 { $item.Entitlement = "buyKnight" }
                    7 { $item.Entitlement = "upgradeKnight" }
                    8 { $item.Entitlement = "activateKnight" }
                    9 { $item.Entitlement = "soldier" }
                    default { 
                        Write-Warning "Unknown entitlement value: $($item.Entitlement)"
                    }
                }
            }
        }
        
        # Convert back to JSON with proper formatting
        $updatedJson = $jsonContent | ConvertTo-Json -Depth 20
        Set-Content -Path $file.FullName -Value $updatedJson
        
        Write-Host "✓ Updated $($file.Name)" -ForegroundColor Green
        
        # Remove backup since update succeeded
        Remove-Item $backupPath
    }
    catch {
        Write-Error "Failed to update $($file.Name): $($_.Exception.Message)"
        # Restore from backup
        Move-Item $backupPath $file.FullName -Force
    }
}

Write-Host "Test file update complete!" -ForegroundColor Green