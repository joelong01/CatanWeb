# Open Catan3 Companion Interface
# This script opens the companion interface in your default browser with the correct HTTP URL

Write-Host "?? Opening Catan3 Companion Interface..." -ForegroundColor Cyan
Write-Host ""

# Check if the game service is running
$servicePort = 8080
$serviceUrl = "http://localhost:$servicePort"
$companionUrl = "$serviceUrl/companion"

try {
    Write-Host "?? Checking if Catan3 Game Service is running..." -ForegroundColor Yellow
    
    # Test if the service is responding
    $response = Invoke-WebRequest -Uri "$serviceUrl/api/companion/games" -Method GET -TimeoutSec 5 -ErrorAction Stop
    
    if ($response.StatusCode -eq 200) {
        Write-Host "? Game service is running successfully!" -ForegroundColor Green
        Write-Host ""
        
        # Display available URLs
        Write-Host "?? Available Companion URLs:" -ForegroundColor Cyan
        Write-Host "   ?? Main: $companionUrl" -ForegroundColor Green
        Write-Host "   ?? Demo: $serviceUrl/demo" -ForegroundColor Gray
        Write-Host "   ?? Games API: $serviceUrl/api/companion/games" -ForegroundColor Gray
        Write-Host ""
        
        # Open the companion interface
        Write-Host "?? Opening companion interface in your default browser..." -ForegroundColor Yellow
        Start-Process $companionUrl
        
        Write-Host ""
        Write-Host "?? Tips:" -ForegroundColor Cyan
        Write-Host "   • Make sure to use HTTP (not HTTPS)" -ForegroundColor Gray
        Write-Host "   • If you see SSL errors, try incognito/private mode" -ForegroundColor Gray
        Write-Host "   • Clear browser cache if redirects to HTTPS persist" -ForegroundColor Gray
        
    } else {
        Write-Host "??  Service responded but with status: $($response.StatusCode)" -ForegroundColor Yellow
    }
    
} catch {
    Write-Host "? Game service is not running or not accessible!" -ForegroundColor Red
    Write-Host ""
    Write-Host "?? To start the game service:" -ForegroundColor Cyan
    Write-Host "   .\run-game-service.ps1" -ForegroundColor Gray
    Write-Host "   OR" -ForegroundColor Gray
    Write-Host "   .\run-game-service.bat" -ForegroundColor Gray
    Write-Host ""
    Write-Host "?? Make sure you're in the correct directory and run:" -ForegroundColor Yellow
    Write-Host "   cd D:\GitHub\Catan3" -ForegroundColor Gray
    Write-Host "   .\run-game-service.ps1" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Error details: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""