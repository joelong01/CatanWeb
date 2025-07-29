@echo off
echo =======================================
echo Starting Catan3 Game Service
echo =======================================
echo.

cd /d "%~dp0Catan3.GameService"

echo Building project...
dotnet build --configuration Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ? Build failed! Please check the errors above.
    pause
    exit /b 1
)

echo.
echo ? Build successful! Starting service...
echo.

dotnet run --configuration Release

pause