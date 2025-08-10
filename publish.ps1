#!/usr/bin/env pwsh

# Publish script for Catan Desktop App
# This script publishes the Catan Desktop application for x64 platform

Write-Host "Publishing Catan Desktop App..." -ForegroundColor Green

try {
    # Publish the desktop app for x64 platform
    dotnet publish "DesktopApp\Catan Desktop.csproj" `
        --configuration Debug `
        --runtime win-x64 `
        --self-contained true `
        --output "DesktopApp\bin\x64\Debug\net9.0-windows10.0.22621.0\win-x64" `
        --verbosity minimal `
        -p:Platform=x64

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Catan Desktop App published successfully!" -ForegroundColor Green
        Write-Host "📁 Published to: DesktopApp\bin\x64\Debug\net9.0-windows10.0.22621.0\win-x64\" -ForegroundColor Cyan
    } else {
        Write-Host "❌ Publish failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}
catch {
    Write-Host "❌ Error during publish: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
