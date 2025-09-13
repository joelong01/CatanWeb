# Catan3 Start New Game Script
# Creates a new game with 3 players for testing the companion UI
# Usage: .\start-new-game.ps1 [-GameId <gameId>] [-ServerUrl <url>] [-PlayerNames <name1,name2,name3>] [-GameType <Regular|Expansion>]

param(
    [string]$GameId = "companion-test-$(Get-Date -Format 'yyyyMMdd-HHmmss')",
    [string]$ServerUrl = "http://localhost:8080",
    [string[]]$PlayerNames = @("Alice", "Bob", "Charlie"),
    [string]$GameType = "Regular"
)

# Function to make HTTP requests with error handling
function Invoke-GameApiRequest {
    param(
        [string]$Method,
        [string]$Uri,
        [object]$Body = $null
    )
    
    $headers = @{
        'Content-Type' = 'application/json'
        'Accept' = 'application/json'
    }
    
    $params = @{
        Method = $Method
        Uri = $Uri
        Headers = $headers
        Verbose = $true
    }
    
    if ($Body) {
        $jsonBody = ($Body | ConvertTo-Json -Depth 10)
        $params.Body = $jsonBody
        Write-Host "📤 Request Body:" -ForegroundColor Gray
        Write-Host $jsonBody -ForegroundColor DarkGray
    }
    
    Write-Host "🌐 $Method $Uri" -ForegroundColor Cyan
    
    try {
        $response = Invoke-RestMethod @params
        Write-Host "✅ Response received successfully" -ForegroundColor Green
        
        if ($response) {
            Write-Host "📥 Response:" -ForegroundColor Gray
            $responseJson = ($response | ConvertTo-Json -Depth 5)
            Write-Host $responseJson -ForegroundColor DarkGray
        }
        
        return $response
    }
    catch {
        Write-Host "❌ Request failed with error: $($_.Exception.Message)" -ForegroundColor Red
        
        if ($_.Exception.Response) {
            $statusCode = $_.Exception.Response.StatusCode
            Write-Host "📊 HTTP Status: $statusCode" -ForegroundColor Red
            
            try {
                if ($_.Exception.Response.Content) {
                    $errorBody = $_.Exception.Response.Content.ReadAsStringAsync().Result
                    Write-Host "📥 Error Response Body:" -ForegroundColor Red
                    Write-Host $errorBody -ForegroundColor DarkRed
                }
            }
            catch {
                Write-Host "⚠️  Could not read error response body: $($_.Exception.Message)" -ForegroundColor Yellow
            }
        }
        
        throw
    }
}

# Function to check if game service is running
function Test-GameService {
    param([string]$ServerUrl)
    
    try {
        Write-Host "🔍 Checking if game service is running at $ServerUrl..." -ForegroundColor Yellow
        
        # Try to access a simple endpoint first - use a more reliable endpoint
        $testUri = "$ServerUrl/api/gamestate/nonexistent-game-test"
        Write-Host "🌐 Testing connectivity with: $testUri" -ForegroundColor Gray
        
        $response = Invoke-WebRequest -Uri $testUri -Method GET -TimeoutSec 10 -ErrorAction SilentlyContinue
        
        Write-Host "📊 Response Status: $($response.StatusCode)" -ForegroundColor Gray
        
        # Even if it returns 404, that means the service is running and responding
        if ($response.StatusCode -eq 404 -or $response.StatusCode -eq 200) {
            Write-Host "✅ Game service is running and responding!" -ForegroundColor Green
            return $true
        }
    }
    catch [System.Net.WebException] {
        $webException = $_.Exception
        Write-Host "🌐 Web Exception: $($webException.Message)" -ForegroundColor Yellow
        
        # Check if it's a 404 (which means service is running)
        if ($webException.Response -and $webException.Response.StatusCode -eq 404) {
            Write-Host "✅ Game service is running (404 response indicates server is active)!" -ForegroundColor Green
            return $true
        }
        
        # Check for connection refused or timeout (service not running)
        if ($webException.Message -match "refused|timeout|unreachable") {
            Write-Host "❌ Cannot connect to game service - connection refused or timeout" -ForegroundColor Red
        }
        else {
            Write-Host "⚠️  Unexpected web exception: $($webException.Message)" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "❌ Connectivity test failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "🔍 Exception type: $($_.Exception.GetType().Name)" -ForegroundColor Gray
    }
    
    Write-Host "❌ Game service is not running or not accessible at $ServerUrl" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please start the game service first:" -ForegroundColor Yellow
    Write-Host "  1. Open a new terminal/command prompt" -ForegroundColor Gray
    Write-Host "  2. Navigate to the Catan3.GameService directory" -ForegroundColor Gray
    Write-Host "  3. Run: dotnet run" -ForegroundColor Gray
    Write-Host "  4. Wait for the service to start (you should see startup messages)" -ForegroundColor Gray
    Write-Host "  5. Look for 'Ready for connections!' message" -ForegroundColor Gray
    Write-Host ""
    return $false
}

# Function to create a new game
function New-CatanGame {
    param(
        [string]$GameId,
        [string]$ServerUrl,
        [string[]]$PlayerNames,
        [string]$GameType
    )
    
    Write-Host "🎲 Creating new Catan game..." -ForegroundColor Cyan
    Write-Host "  Game ID: $GameId" -ForegroundColor Gray
    Write-Host "  Game Type: $GameType" -ForegroundColor Gray
    Write-Host "  Players: $($PlayerNames -join ', ')" -ForegroundColor Gray
    Write-Host "  Server: $ServerUrl" -ForegroundColor Gray
    Write-Host ""
    
    $newGameBody = @{
        gameId = $GameId
        gameType = $GameType
        playerIds = $PlayerNames
    }
    
    try {
        $response = Invoke-GameApiRequest -Method POST -Uri "$ServerUrl/api/game/new" -Body $newGameBody
        
        if ($response.success) {
            Write-Host "✅ Game created successfully!" -ForegroundColor Green
            return $true
        }
        else {
            Write-Host "❌ Game creation failed: $($response.message)" -ForegroundColor Red
            return $false
        }
    }
    catch {
        Write-Host "❌ Failed to create game" -ForegroundColor Red
        return $false
    }
}

# Function to get game state and verify it was created
function Get-GameState {
    param(
        [string]$GameId,
        [string]$ServerUrl
    )
    
    try {
        Write-Host "🔍 Verifying game state..." -ForegroundColor Yellow
        $gameState = Invoke-GameApiRequest -Method GET -Uri "$ServerUrl/api/gamestate/$GameId"
        
        if ($gameState) {
            Write-Host "✅ Game state retrieved successfully!" -ForegroundColor Green
            Write-Host "  Current State: $($gameState.gameState)" -ForegroundColor Gray
            Write-Host "  Current Player: $($gameState.currentPlayerId)" -ForegroundColor Gray
            Write-Host "  Version: $($gameState.version)" -ForegroundColor Gray
            Write-Host "  Players: $($gameState.players.Count)" -ForegroundColor Gray
            
            foreach ($player in $gameState.players) {
                $status = if ($player.id -eq $gameState.currentPlayerId) { "(current)" } else { "" }
                Write-Host "    - $($player.id) $status" -ForegroundColor Gray
            }
            
            return $gameState
        }
        else {
            Write-Host "❌ Could not retrieve game state" -ForegroundColor Red
            return $null
        }
    }
    catch {
        Write-Host "❌ Failed to get game state" -ForegroundColor Red
        return $null
    }
}

# Function to display companion URLs
function Show-CompanionUrls {
    param(
        [string]$GameId,
        [string]$ServerUrl
    )
    
    $serverHost = ([System.Uri]$ServerUrl).Host
    $serverPort = ([System.Uri]$ServerUrl).Port
    
    Write-Host ""
    Write-Host "📱 COMPANION UI URLS:" -ForegroundColor Cyan
    Write-Host "=================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "🎮 Game Selection Interface (RECOMMENDED):" -ForegroundColor Yellow
    Write-Host "  $ServerUrl/companion" -ForegroundColor White
    Write-Host "  → Shows list of available games to join" -ForegroundColor Gray
    Write-Host ""
    Write-Host "🎯 Direct Game Access:" -ForegroundColor Yellow
    Write-Host "  $ServerUrl/companion?gameId=$GameId" -ForegroundColor White
    Write-Host "  → Connect directly to this game" -ForegroundColor Gray
    Write-Host ""
    Write-Host "🎨 Demo/Preview URLs:" -ForegroundColor Yellow
    Write-Host "  Demo Hub:       $ServerUrl/demo" -ForegroundColor Gray
    Write-Host "  Board Setup:    $ServerUrl/companion/demo/PickingBoard" -ForegroundColor Gray
    Write-Host "  Settlement:     $ServerUrl/companion/demo/AllocateResourceForward" -ForegroundColor Gray
    Write-Host "  Roll Dice:      $ServerUrl/companion/demo/WaitingForRoll" -ForegroundColor Gray
    Write-Host "  Purchase:       $ServerUrl/companion/demo/WaitingForNext" -ForegroundColor Gray
    Write-Host ""
    Write-Host "📱 For Mobile Testing:" -ForegroundColor Yellow
    Write-Host "  1. Ensure your mobile device is on the same WiFi network" -ForegroundColor Gray
    Write-Host "  2. Open browser and go to: $ServerUrl/companion" -ForegroundColor Gray
    Write-Host "  3. Select the game you want to join from the list" -ForegroundColor Gray
    Write-Host "  4. Select your player from the dropdown" -ForegroundColor Gray
    Write-Host "  5. Start testing the UI!" -ForegroundColor Gray
    Write-Host ""
}

# Function to display quick test instructions
function Show-TestInstructions {
    param(
        [string]$GameId,
        [string[]]$PlayerNames
    )
    
    Write-Host "🧪 QUICK TEST INSTRUCTIONS:" -ForegroundColor Cyan
    Write-Host "=================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "🎮 PREFERRED METHOD - Game Selection:" -ForegroundColor Yellow
    Write-Host "1. Open the main companion URL: $ServerUrl/companion" -ForegroundColor Gray
    Write-Host "2. You should see your game listed with:" -ForegroundColor Gray
    Write-Host "   - Game type: $GameType" -ForegroundColor Gray
    Write-Host "   - Players: $($PlayerNames -join ', ')" -ForegroundColor Gray
    Write-Host "   - Current state and creation time" -ForegroundColor Gray
    Write-Host "3. Click 'Join Game' on your game" -ForegroundColor Gray
    Write-Host "4. Select your player from the dropdown" -ForegroundColor Gray
    Write-Host "5. Try game actions like 'Next' or 'Shuffle Board'" -ForegroundColor Gray
    Write-Host ""
    Write-Host "🎯 ALTERNATIVE METHOD - Direct Access:" -ForegroundColor Yellow
    Write-Host "1. Use the direct game URL if you know the gameId" -ForegroundColor Gray
    Write-Host "2. Follow steps 4-5 from the preferred method" -ForegroundColor Gray
    Write-Host ""
    Write-Host "🔄 REAL-TIME TESTING:" -ForegroundColor Yellow
    Write-Host "1. Open multiple browser tabs/windows" -ForegroundColor Gray
    Write-Host "2. Join the same game as different players" -ForegroundColor Gray
    Write-Host "3. Actions should update in real-time across all clients" -ForegroundColor Gray
    Write-Host ""
    Write-Host "💡 Tips:" -ForegroundColor Yellow
    Write-Host "  - Only the current player can perform actions" -ForegroundColor Gray
    Write-Host "  - Game list refreshes automatically when new games are created" -ForegroundColor Gray
    Write-Host "  - Use browser dev tools (F12) to monitor network requests" -ForegroundColor Gray
    Write-Host "  - Check both server console and browser console for detailed logs" -ForegroundColor Gray
    Write-Host ""
}

# Function to display enhanced testing guide
function Show-EnhancedTestingGuide {
    param(
        [string]$GameId,
        [string]$ServerUrl,
        [string[]]$PlayerNames
    )
    
    Write-Host ""
    Write-Host "🔧 ENHANCED DEBUGGING & TESTING GUIDE:" -ForegroundColor Cyan
    Write-Host "=========================================" -ForegroundColor Cyan
    Write-Host ""
    
    Write-Host "📊 Server-Side Logging:" -ForegroundColor Yellow
    Write-Host "  The GameService now includes comprehensive logging for all API calls:" -ForegroundColor Gray
    Write-Host "  - Request URLs and parameters" -ForegroundColor Gray
    Write-Host "  - Response status codes and timing" -ForegroundColor Gray
    Write-Host "  - Game state changes and versions" -ForegroundColor Gray
    Write-Host "  - Error details and stack traces" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  📝 Monitor the server console output while testing!" -ForegroundColor White
    Write-Host ""
    
    Write-Host "🖥️ Client-Side Logging:" -ForegroundColor Yellow
    Write-Host "  The companion app now logs all network activity to browser console:" -ForegroundColor Gray
    Write-Host "  - API request URLs and payloads" -ForegroundColor Gray
    Write-Host "  - Response times and status codes" -ForegroundColor Gray
    Write-Host "  - Real-time update polling" -ForegroundColor Gray
    Write-Host "  - Connection status changes" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  📝 Open browser Developer Tools (F12) → Console tab" -ForegroundColor White
    Write-Host "     Look for [COMPANION] prefixed log messages" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "🔍 Debugging Steps:" -ForegroundColor Yellow
    Write-Host "  1. Start GameService and verify startup logs show 'Ready for connections!'" -ForegroundColor Gray
    Write-Host "  2. Open companion URL: $ServerUrl/companion?gameId=$GameId" -ForegroundColor Gray
    Write-Host "  3. Open Browser DevTools (F12) and go to Console tab" -ForegroundColor Gray
    Write-Host "  4. Look for connection attempts and API responses" -ForegroundColor Gray
    Write-Host "  5. Select a player and try performing actions" -ForegroundColor Gray
    Write-Host "  6. Check both server console and browser console for errors" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "🚨 Common Issues & Solutions:" -ForegroundColor Yellow
    Write-Host "  ❌ 'Game not found' error:" -ForegroundColor Red
    Write-Host "     → Verify the game was created successfully (check server logs)" -ForegroundColor Gray
    Write-Host "     → Confirm the gameId in the URL matches the created game" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  ❌ Connection timeout/refused:" -ForegroundColor Red
    Write-Host "     → Ensure GameService is running on port 8080" -ForegroundColor Gray
    Write-Host "     → Check Windows Firewall/antivirus blocking the port" -ForegroundColor Gray
    Write-Host "     → Try accessing $ServerUrl directly in browser" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  ❌ Actions not working:" -ForegroundColor Red
    Write-Host "     → Verify you selected the correct player" -ForegroundColor Gray
    Write-Host "     → Check if it's your turn (only current player can act)" -ForegroundColor Gray
    Write-Host "     → Look for game state validation errors in server logs" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "🎯 Testing Checklist:" -ForegroundColor Yellow
    Write-Host "  □ Server starts successfully with startup messages" -ForegroundColor Gray
    Write-Host "  □ Browser can access $ServerUrl/demo (should show demo page)" -ForegroundColor Gray
    Write-Host "  □ Companion loads and shows player selection" -ForegroundColor Gray
    Write-Host "  □ Game state displays correctly after selecting player" -ForegroundColor Gray
    Write-Host "  □ Actions trigger server requests (visible in console)" -ForegroundColor Gray
    Write-Host "  □ Real-time updates work across multiple browser tabs" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "📱 Mobile Testing:" -ForegroundColor Yellow
    Write-Host "  For mobile device testing:" -ForegroundColor Gray
    Write-Host "  1. Ensure phone is on same WiFi network" -ForegroundColor Gray
    Write-Host "  2. Find your computer's IP address" -ForegroundColor Gray
    Write-Host "  3. Use network URL (shown in server startup)" -ForegroundColor Gray
    Write-Host "  4. Enable mobile browser developer tools if available" -ForegroundColor Gray
    Write-Host ""
}

# Main execution
try {
    Write-Host "🎲 Catan3 Game Setup Script" -ForegroundColor Cyan
    Write-Host "============================" -ForegroundColor Cyan
    Write-Host ""
    
    # Validate parameters
    if ($PlayerNames.Count -lt 2 -or $PlayerNames.Count -gt 6) {
        Write-Error "Invalid number of players. Must be between 2 and 6 players."
        exit 1
    }
    
    if ($GameType -notin @("Regular", "Expansion")) {
        Write-Error "Invalid game type. Must be 'Regular' or 'Expansion'."
        exit 1
    }
    
    # Check if game service is running
    if (-not (Test-GameService -ServerUrl $ServerUrl)) {
        exit 1
    }
    
    Write-Host ""
    
    # Create the game
    if (New-CatanGame -GameId $GameId -ServerUrl $ServerUrl -PlayerNames $PlayerNames -GameType $GameType) {
        Write-Host ""
        
        # Wait a moment for the game to be fully initialized
        Start-Sleep -Seconds 1
        
        # Verify game state
        $gameState = Get-GameState -GameId $GameId -ServerUrl $ServerUrl
        
        if ($gameState) {
            # Show companion URLs
            Show-CompanionUrls -GameId $GameId -ServerUrl $ServerUrl
            
            # Show enhanced testing guide
            Show-EnhancedTestingGuide -GameId $GameId -ServerUrl $ServerUrl -PlayerNames $PlayerNames
            
            Write-Host "🎉 Game setup complete! Ready for companion UI testing with enhanced logging." -ForegroundColor Green
        }
        else {
            Write-Host "⚠️  Game was created but could not verify state." -ForegroundColor Yellow
            Show-CompanionUrls -GameId $GameId -ServerUrl $ServerUrl
            Show-EnhancedTestingGuide -GameId $GameId -ServerUrl $ServerUrl -PlayerNames $PlayerNames
        }
    }
    else {
        Write-Host "❌ Game setup failed." -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "❌ Script execution failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
