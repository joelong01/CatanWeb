#!/usr/bin/env pwsh
param(
    [Parameter(Position=0)]
    [string]$Arg0,
    [switch]$Clean,
    [switch]$NoBuild,
    [switch]$NoTest,
    [switch]$Release,
    [switch]$NoRegister,
    [switch]$Unregister,
    [switch]$Help,
    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64"
)

# Custom error handling for parameter validation
trap {
    if ($_.Exception.Message -like "*cannot validate argument*" -and $_.Exception.Message -like "*--*") {
        Write-Host "❌ Invalid parameter format detected!" -ForegroundColor Red
        Show-Help
        exit 1
    }
    Write-Host "❌ Parameter error: $($_.Exception.Message)" -ForegroundColor Red
    Show-Help
    exit 1
}

# Function to show help
function Show-Help {
    Write-Host @"
Catan Desktop App Build Script

USAGE:
    .\build.ps1 [OPTIONS]

OPTIONS:
    -Clean          Clean the project before building
    -NoBuild        Skip the build step, only publish
    -NoTest         Skip running tests
    -Release        Build in Release configuration (default: Debug)
    -NoRegister     Do not register the app after publish (default is to register)
    -Unregister     Unregister the app from the system and exit
    -Platform       Target platform: x64, x86, ARM64 (default: x64)
    -Verbose        Enable verbose output (built-in PowerShell)
    -Help           Show this help message

EXAMPLES:
    .\build.ps1                           # Build, test, publish, and register (default)
    .\build.ps1 -Clean -Release           # Clean release build (registers by default)
    .\build.ps1 -NoTest -NoRegister       # Build and publish without tests and skip registration
    .\build.ps1 -Unregister               # Unregister the app and exit (implies -NoRegister)
    .\build.ps1 -Platform ARM64 -Release  # Build for ARM64 in Release mode

DEFAULTS:
    By default, this script will build, test, publish, and register the app so you can
    find it in the Start menu and launch/debug it immediately. Use -NoRegister to opt out.

NOTE: Use PowerShell syntax (-Parameter) not bash syntax (--parameter)

COMMON ERRORS:
    --help     →  -Help
    --clean    →  -Clean
    --release  →  -Release
    --no-test  →  -NoTest
    --no-build →  -NoBuild
    --no-register → -NoRegister
"@ -ForegroundColor Cyan
}

# Helper: log commands in a readable, copy-pastable way
function Write-Command([string]$command, [object[]]$cmdArgs) {
    $fmt = $cmdArgs | ForEach-Object {
        if ($_ -is [string] -and $_ -match '\s') { '"' + $_ + '"' } else { $_ }
    }
    Write-Host ("» {0} {1}" -f $command, ($fmt -join ' ')) -ForegroundColor DarkGray
}

# Handle bash-style --help (captured via Arg0)
if ($Arg0) {
    if ($Arg0 -eq "--help" -or $Arg0 -eq "-h" -or $Arg0 -eq "help") {
        Show-Help
        exit 0
    }
    if ($Arg0 -like "--*") {
        Write-Host "❌ Invalid parameter format detected!" -ForegroundColor Red
        Show-Help
        exit 1
    }
}

# Show help if requested
if ($Help) {
    Show-Help
    exit 0
}

# Configuration
$NoRegister = $NoRegister -or $Unregister
$Configuration = if ($Release) { "Release" } else { "Debug" }
$ProjectPath = "DesktopApp\Catan Desktop.csproj"
$RuntimeId = "win-$Platform"
$OutputPath = "DesktopApp\bin\$Platform\$Configuration\net9.0-windows10.0.22621.0\$RuntimeId"
$VerbosityLevel = if ($VerbosePreference -eq 'Continue') { "normal" } else { "minimal" }
$PackageId = "606d7833-a1be-4389-aa5f-fe8dd1dd1da3"

Write-Host "🏗️  Catan Desktop App Build Script" -ForegroundColor Green
Write-Host "Configuration: $Configuration | Platform: $Platform | Runtime: $RuntimeId" -ForegroundColor Cyan

try {
    # Unregister app if requested (and exit immediately)
    if ($Unregister) {
        Write-Host "🗑️  Unregistering app..." -ForegroundColor Yellow
        $existingApp = Get-AppxPackage | Where-Object {$_.PackageFullName -like "*$PackageId*"}
        if ($existingApp) {
            Write-Host "Found $($existingApp.Count) installed instance(s) to remove" -ForegroundColor Yellow
            $existingApp | ForEach-Object {
                Write-Command "Remove-AppxPackage" @($_.PackageFullName)
            }
            $existingApp | Remove-AppxPackage
            Write-Host "✅ App unregistered successfully" -ForegroundColor Green
        } else {
            Write-Host "ℹ️  App not currently registered" -ForegroundColor Yellow
        }
        exit 0
    }

    # Clean if requested
    if ($Clean) {
        Write-Host "🧹 Cleaning project..." -ForegroundColor Yellow
        $cleanArgs = @(
            $ProjectPath,
            "-c", $Configuration,
            "-p:Platform=$Platform",
            "--verbosity", $VerbosityLevel
        )
        Write-Command "dotnet clean" $cleanArgs
        dotnet clean @cleanArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Clean failed with exit code: $LASTEXITCODE"
        }
    }

    # Build step
    if (!$NoBuild) {
        Write-Host "🔨 Building project..." -ForegroundColor Yellow
        $buildArgs = @(
            $ProjectPath,
            "-c", $Configuration,
            "-p:Platform=$Platform",
            "--verbosity", $VerbosityLevel
        )
        Write-Command "dotnet build" $buildArgs
        dotnet build @buildArgs
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code: $LASTEXITCODE"
        }
        Write-Host "✅ Build completed successfully" -ForegroundColor Green

        # Run tests if not skipped
        if (!$NoTest) {
            Write-Host "🧪 Running tests..." -ForegroundColor Yellow
            $testArgs = @(
                "--no-build",
                "-c", $Configuration,
                "--verbosity", $VerbosityLevel
            )
            Write-Command "dotnet test" $testArgs
            dotnet test @testArgs
            if ($LASTEXITCODE -ne 0) {
                throw "Tests failed with exit code: $LASTEXITCODE. Skipping publish."
            }
            Write-Host "✅ All tests passed" -ForegroundColor Green
        }
    }

    # Publish step
    Write-Host "📦 Publishing application..." -ForegroundColor Yellow
    $publishArgs = @(
        $ProjectPath,
        "--configuration", $Configuration,
        "--runtime", $RuntimeId,
        "--self-contained", "true",
        "--output", $OutputPath,
        "--verbosity", $VerbosityLevel,
        "-p:Platform=$Platform"
    )
    Write-Command "dotnet publish" $publishArgs
    dotnet publish @publishArgs

    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed with exit code: $LASTEXITCODE"
    }

    Write-Host "✅ Publish completed successfully" -ForegroundColor Green

    # Verify published version
    Write-Host "🔍 Verifying published version..." -ForegroundColor Yellow
    $exePath = Join-Path $OutputPath "Catan Desktop.exe"
    if (Test-Path $exePath) {
        $fileInfo = Get-Item $exePath
        $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($exePath)
        
        Write-Host "`n📋 Published Version Information:" -ForegroundColor Cyan
        Write-Host "┌─────────────────────────────────────────────────────────────────┐" -ForegroundColor Gray
        Write-Host "│ Property              │ Value                                   │" -ForegroundColor Gray
        Write-Host "├─────────────────────────────────────────────────────────────────┤" -ForegroundColor Gray
        Write-Host "│ File Path             │ $($exePath.Substring(0, [Math]::Min(39, $exePath.Length)).PadRight(39)) │" -ForegroundColor White
        Write-Host "│ File Version          │ $($versionInfo.FileVersion.PadRight(39)) │" -ForegroundColor White
        Write-Host "│ Product Version       │ $($versionInfo.ProductVersion.PadRight(39)) │" -ForegroundColor White
        Write-Host "│ File Size             │ $([Math]::Round($fileInfo.Length / 1KB, 2).ToString().PadLeft(6)) KB".PadRight(39) + " │" -ForegroundColor White
        Write-Host "│ Last Modified         │ $($fileInfo.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss').PadRight(39)) │" -ForegroundColor White
        Write-Host "│ Configuration         │ $($Configuration.PadRight(39)) │" -ForegroundColor White
        Write-Host "│ Target Platform       │ $($Platform.PadRight(39)) │" -ForegroundColor White
        Write-Host "│ Runtime Identifier    │ $($RuntimeId.PadRight(39)) │" -ForegroundColor White
        Write-Host "└─────────────────────────────────────────────────────────────────┘" -ForegroundColor Gray
    } else {
        Write-Host "⚠️  Warning: Published executable not found at expected path" -ForegroundColor Yellow
    }

    # Register app by default unless -NoRegister specified
    if (-not $NoRegister) {
        Write-Host "`n📱 Registering app for testing..." -ForegroundColor Yellow
        
        # Unregister existing version first
        $existingApp = Get-AppxPackage | Where-Object {$_.PackageFullName -like "*$PackageId*"}
        if ($existingApp) {
            $existingApp | ForEach-Object { Write-Command "Remove-AppxPackage" @($_.PackageFullName) }
            $existingApp | Remove-AppxPackage
            Write-Host "🔄 Unregistered previous version" -ForegroundColor Cyan
        }
        
        # Register new version
        $manifestPath = Join-Path $OutputPath "AppxManifest.xml"
        if (Test-Path $manifestPath) {
            Write-Command "Add-AppxPackage" @("-Path", $manifestPath, "-Register")
            Add-AppxPackage -Path $manifestPath -Register
            Write-Host "✅ App registered in Start menu" -ForegroundColor Green
            
            # Verify registration
            $newApp = Get-AppxPackage | Where-Object {$_.PackageFullName -like "*$PackageId*"}
            if ($newApp) {
                Write-Host "📋 Registered App Info:" -ForegroundColor Cyan
                Write-Host "   Name: $($newApp.Name)" -ForegroundColor White
                Write-Host "   Version: $($newApp.Version)" -ForegroundColor White
                Write-Host "   Status: $($newApp.Status)" -ForegroundColor White
            }
        } else {
            Write-Host "⚠️  Warning: AppxManifest.xml not found, cannot register app" -ForegroundColor Yellow
        }
    }

    Write-Host "`n🎉 Build process completed successfully!" -ForegroundColor Green
    Write-Host "📁 Output location: $OutputPath" -ForegroundColor Cyan
    
    if (-not $NoRegister) {
        Write-Host "🚀 App is now available in Start menu" -ForegroundColor Cyan
    } else {
        Write-Host "💡 Registration skipped (remove -NoRegister to install the app in Start menu)" -ForegroundColor Yellow
    }

} catch {
    Write-Host "❌ Build process failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
