@echo off
echo ?? Opening Catan3 Companion Interface...
echo.

REM Check if the game service is running
set SERVICE_URL=http://localhost:8080
set COMPANION_URL=%SERVICE_URL%/companion

echo ?? Checking if Catan3 Game Service is running...

REM Try to access the games API to check if service is running
curl -s -o nul -w "%%{http_code}" %SERVICE_URL%/api/companion/games > response_code.tmp 2>nul
set /p RESPONSE_CODE=<response_code.tmp
del response_code.tmp

if "%RESPONSE_CODE%"=="200" (
    echo ? Game service is running successfully!
    echo.
    echo ?? Available Companion URLs:
    echo    ?? Main: %COMPANION_URL%
    echo    ?? Demo: %SERVICE_URL%/demo
    echo    ?? Games API: %SERVICE_URL%/api/companion/games
    echo.
    echo ?? Opening companion interface in your default browser...
    start "" "%COMPANION_URL%"
    echo.
    echo ?? Tips:
    echo    • Make sure to use HTTP (not HTTPS^)
    echo    • If you see SSL errors, try incognito/private mode
    echo    • Clear browser cache if redirects to HTTPS persist
) else (
    echo ? Game service is not running or not accessible!
    echo.
    echo ?? To start the game service:
    echo    run-game-service.bat
    echo    OR
    echo    run-game-service.ps1
    echo.
    echo ?? Make sure you're in the correct directory and run:
    echo    cd D:\GitHub\Catan3
    echo    run-game-service.bat
    echo.
    if not "%RESPONSE_CODE%"=="000" (
        echo Response code: %RESPONSE_CODE%
    ) else (
        echo Could not connect to service
    )
)

echo.
pause