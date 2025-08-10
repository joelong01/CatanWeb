#!/usr/bin/env pwsh

# Publish script for Catan Desktop App
# This script builds, publishes, and registers the Catan Desktop application

Write-Host "Publishing Catan Desktop App..." -ForegroundColor Green

try {
    # Clean and build with package generation
    Write-Host "🧹 Cleaning project..." -ForegroundColor Yellow
    dotnet clean "DesktopApp\Catan Desktop.csproj" -c Debug -p:Platform=x64
    
    Write-Host "🔨 Building with package generation..." -ForegroundColor Yellow
    dotnet build "DesktopApp\Catan Desktop.csproj" `
        --configuration Debug `
        -p:Platform=x64 `
        -p:GenerateAppxPackageOnBuild=true `
        --verbosity minimal

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Build completed successfully!" -ForegroundColor Green
        
        # Unregister old app if it exists
        Write-Host "🔄 Updating app registration..." -ForegroundColor Yellow
        $existingApp = Get-AppxPackage | Where-Object {$_.PackageFullName -like "*606d7833*"}
        if ($existingApp) {
            $existingApp | Remove-AppxPackage
            Write-Host "📱 Unregistered previous version" -ForegroundColor Cyan
        }
        
        # Register the new app
        Add-AppxPackage -Path "DesktopApp\bin\x64\Debug\net9.0-windows10.0.22621.0\win-x64\AppxManifest.xml" -Register
        Write-Host "📱 Registered new version in Start menu" -ForegroundColor Cyan
        
        Write-Host "🎉 Catan Desktop App published and registered successfully!" -ForegroundColor Green
        Write-Host "📁 Published to: DesktopApp\bin\x64\Debug\net9.0-windows10.0.22621.0\win-x64\" -ForegroundColor Cyan
        Write-Host "🚀 App should now be available in Start menu" -ForegroundColor Cyan
    } else {
        Write-Host "❌ Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}
catch {
    Write-Host "❌ Error during publish: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
