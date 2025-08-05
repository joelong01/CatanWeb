#!/usr/bin/env pwsh

Write-Host "=======================================" -ForegroundColor Cyan
Write-Host "Starting Catan3 Game Service" -ForegroundColor Cyan  
Write-Host "=======================================" -ForegroundColor Cyan
Write-Host ""

Set-Location "$PSScriptRoot/Catan3.GameService"

Write-Host "Building project..." -ForegroundColor Yellow
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ Build failed! Please check the errors above." -ForegroundColor Red
    Read-Host "Press Enter to continue"
    exit 1
}

Write-Host ""
Write-Host "✅ Build successful! Starting service..." -ForegroundColor Green
Write-Host ""

dotnet run --configuration Release

Read-Host "Press Enter to continue"
cd ..