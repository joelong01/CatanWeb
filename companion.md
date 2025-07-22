# Catan3 Phone Companion Design

## Overview
This document outlines the design for a phone companion app that allows players to control the Catan3 WinUI3 game remotely. The companion app will enable players to trigger game actions like "Next", "Undo", "Purchase", etc., from their mobile devices.

**MAJOR ARCHITECTURE UPDATE**: The system is being redesigned to extract the GameController into a dedicated ASP.NET Core service with a web-based mobile companion interface, enabling better separation of concerns, testability, and potential for distributed gameplay.

## Architecture Overview

### Current Game Architecture (Legacy)
The Catan3 game currently uses MVVM pattern with CommunityToolkit.Mvvm:
- Commands are implemented as `RelayCommand` methods
- Inter-component communication uses `WeakReferenceMessenger`
- Main actions: `DoAction(GameAction)`, `PurchaseMessage(Entitlement)`, `BuildingUpgradeMessage`, etc.
- Game state is managed through `GameController` and `GameViewModel`

### New Proposed Architecture

**Three-Tier Architecture:**

1. **Client Tier (WinUI3 App)**:
   - UI components and ViewModels
   - MVVM messaging system for UI communication
   - GameControllerProxy: Receives MVVM messages and forwards to Game Service via REST
   - Real-time updates via hanging GET pattern

2. **Game Service Tier (ASP.NET Core)**:
   - GameController (moved from client)
   - Game state management and logging
   - REST API endpoints for game actions
   - Web-based companion interface (HTML/JavaScript)
   - Real-time notifications via hanging GET

3. **Web Companion Interface**:
   - Simple responsive web interface for mobile devices
   - Player selection and identification
   - Action buttons based on game state and current player
   - Real-time updates via hanging GET
   - **No native mobile app required!**

### Communication Technology Choice

**Selected: HTTP REST API + UDP Broadcast Discovery + Web Interface**

**Why this architecture:**
- ? Clean separation of concerns
- ? Game logic isolated and testable
- ? Enables future distributed gameplay
- ? Web-based mobile interface - no app store deployment needed
- ? Universal compatibility across all mobile devices
- ? Maintains existing MVVM patterns in UI
- ? Real-time updates without WebSocket complexity

**Discovery Protocol: UDP Broadcast**
- Game service broadcasts availability on local network (port 8765)
- WinUI3 client listens for service availability
- **Mobile devices connect directly via web browser using announced URL**
- Broadcast message includes: GameId, PlayerCount, GameState, Web URL

**Command Protocol: HTTP REST API + Web Interface**
- Game service hosts ASP.NET Core API (port 8080)
- Game service serves web companion interface at `/companion`
- WinUI3 client sends HTTP requests via GameControllerProxy
- **Mobile devices use web interface with JavaScript for REST calls**
- JSON request/response format
- Real-time updates via hanging GET (long polling)

## Mobile Companion Web Interface

**User Experience Flow:**

1. **Discovery**: Players scan QR code or manually enter URL shown on desktop app
2. **Connection**: Browser loads web companion interface from game service
3. **Player Selection**: Interface shows list of players, user selects their identity
4. **Game Interaction**: Interface shows current game state and available actions
5. **Real-time Updates**: Interface updates automatically when game state changes

**Web Interface Features:**

- **Player Selection**: Dropdown/list of current game players
- **Current Player Indicator**: Clear display of whose turn it is
- **Action Buttons**: 
  - "Next" button (when current player and next is available)
  - "Undo" button (when current player and undo is available)
  - Purchase buttons for each available entitlement
- **Game State Display**: Shows current game state and whose turn it is
- **Responsive Design**: Works on phones, tablets, and desktop browsers

## MVVM Integration

**How the New Architecture Integrates with MVVM:**

1. **UI Layer**: ViewModels continue using MVVM messaging patterns
2. **Proxy Layer**: GameControllerProxy receives MVVM messages and translates to REST calls
3. **Service Layer**: GameController processes requests and manages state
4. **Web Layer**: Browser-based interface for mobile companion
5. **Update Flow**: Hanging GET pattern delivers real-time updates to all clients

**Message Flow:**Desktop: UI ViewModel -> MVVM Message -> GameControllerProxy -> HTTP Request -> Game Service
Mobile: Web Interface -> JavaScript -> HTTP Request -> Game Service
Game Service -> GameController -> UpdateGameModel -> HTTP Response -> All Clients (Desktop + Mobile)
## Real-Time Updates: Hanging GET Pattern

**Implementation:**
- All clients (WinUI3 and web browsers) use hanging GET for real-time updates
- Clients make GET requests to `/api/gamestate/listen?gameId=123&version=42&playerId=player1`
- If client version is behind server version, immediate response with new state
- If client is up-to-date, request "hangs" waiting for changes
- When game state changes, server completes all pending requests with new state
- 30-second timeout prevents indefinite hanging
- **Web interface uses JavaScript fetch with automatic retry logic**

**Benefits:**
- Consistent pattern across all client types
- No WebSocket complexity on mobile
- Works through firewalls and proxies
- No persistent connections to manage
- Graceful fallback on network issues
- **Universal browser compatibility**

## API Design

### Discovery Protocol (UDP)// Broadcast message every 5 seconds
{
  "gameId": "guid",
  "gameName": "Catan Game",
  "playerCount": 4,
  "gameState": "WaitingForRoll",
  "servicePort": 8080,
  "webCompanionUrl": "http://192.168.1.100:8080/companion",
  "roomCode": "1234",
  "timestamp": "2024-01-01T12:00:00Z"
}
### REST API Endpoints (Game Service)

#### GET /companion
Serve web companion interface<!-- Simple responsive HTML page with embedded JavaScript -->
<!DOCTYPE html>
<html>
<head>
    <title>Catan Companion</title>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <!-- Bootstrap or simple CSS for mobile-friendly UI -->
</head>
<body>
    <!-- Player selection, game state display, action buttons -->
</body>
</html>
#### GET /api/players/{gameId}
Get list of players for selection{
  "gameId": "guid",
  "players": [
    {
      "id": "player1",
      "name": "Alice",
      "isCurrentPlayer": true
    }
  ]
}
#### POST /api/game/action
Execute game actions from web interface// Request
{
  "gameId": "guid",
  "playerId": "player1",
  "messageType": "DoAction" | "PurchaseMessage",
  "messageData": {
    "action": "Next" | "Undo" | "Redo",
    "entitlement": "Settlement" | "City" | "Road" | "Soldier"
  },
  "timestamp": "2024-01-01T12:00:00Z"
}

// Response
{
  "success": true,
  "gameStateVersion": 43,
  "message": "Action executed successfully"
}
#### GET /api/gamestate/{gameId}
Get current game state for web interface{
  "gameId": "guid",
  "currentPlayerId": "player1",
  "gameState": "WaitingForNext",
  "actionFlags": {
    "nextEnabled": true,
    "undoEnabled": false,
    "rollsEnabled": false
  },
  "availableEntitlements": [
    {
      "entitlement": "Settlement",
      "enabled": true
    },
    {
      "entitlement": "Road", 
      "enabled": true
    }
  ],
  "version": 42,
  "timestamp": "2024-01-01T12:00:00Z"
}
#### GET /api/gamestate/{gameId}/listen?playerId=player1&version=42
Hanging GET for real-time updates (used by web interface JavaScript)
- Same as above but waits for changes
- Web interface polls this endpoint continuously
- JavaScript handles reconnection and error recovery

## Implementation Plan

### Migration Strategy: Copy-First, Never Break Compilation

**Key Principle**: Never move or delete files during migration. Always copy files to new projects first, then switch references only when new projects are fully working.

**Phases:**
1. **Copy Phase**: Create new projects and copy (don't move) required files
2. **Build Phase**: Ensure new projects compile independently 
3. **Integration Phase**: Switch main client to use new projects
4. **Cleanup Phase**: Remove duplicated files from original project

### Stage 1: Shared Models Project ? 
1. ? Create `Catan3.Shared` project in same directory (DONE!)
2. ? **Copy** (don't move) common models, enums, and message types to shared project - verify that this works
3. ? Ensure `Catan3.Shared` compiles independently 



**Files to Copy to Catan3.Shared:**
- `Models/MessageObjects.cs` ? `Catan3.Shared/Models/MessageObjects.cs`
- `Models/enums.cs` ? `Catan3.Shared/Models/GameEnums.cs`
- `Game/GameModel/GameModel.cs` ? `Catan3.Shared/Models/GameModel.cs`
- `Player/PlayerModel.cs` ? `Catan3.Shared/Models/PlayerModel.cs`
- Related model files that don't depend on UI (TileModel, BuildingModel, etc.)

### Stage 2: ASP.NET Core Game Service with Web Companion ? NEXT
1. Create `Catan3.GameService` ASP.NET Core project in same directory (DONE!)
2. Add reference to `Catan3.Shared`
3. **Copy** (don't move) `GameController.cs` to service project
4. **Copy** required services and utilities for GameController to work
5. Create REST API endpoints for game actions
6. **Create simple web companion interface at `/companion`**
7. **Implement player selection and basic action buttons**
8. **Add JavaScript for hanging GET and real-time updates**
9. Add service discovery broadcasting with web URL
10. Ensure `Catan3.GameService` compiles and runs independently
11. Test web interface works on mobile devices

### Stage 3: Client Proxy Layer ? PENDING
1. Create `GameControllerProxy` in main client
2. Implement MVVM message handling in proxy
3. Add HTTP client for REST calls to game service
4. Implement hanging GET client for real-time updates
5. **Copy** existing GameController usage patterns to proxy
6. Add feature flag to switch between local GameController and proxy
7. Test both modes work correctly
8. Switch default to use proxy mode
9. **Add QR code generation for easy mobile access**
10. Remove local GameController (cleanup phase)

### Stage 4: Integration & Testing ? PENDING
1. End-to-end testing of client + service + web companion
2. **Test web interface on various mobile browsers**
3. Multiple client support (desktop + multiple mobile browsers)
4. Performance optimization and stress testing
5. Error handling and resilience testing
6. Backwards compatibility verification

### Stage 5: Advanced Features ? FUTURE
1. Enhanced web interface with game state visualization
2. Multiple game sessions and game lobby
3. Enhanced security and authentication
4. **Progressive Web App (PWA) capabilities**
5. **Offline support for mobile companion**
6. Performance monitoring and analytics

## Web Companion Interface Design

### Initial Simple Implementation

**Page Structure:**<!DOCTYPE html>
<html>
<head>
    <title>Catan Companion</title>
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <style>
        /* Simple responsive CSS */
        body { font-family: Arial, sans-serif; padding: 20px; }
        .player-select { margin-bottom: 20px; }
        .current-player { font-weight: bold; color: green; }
        .action-buttons button { padding: 15px; margin: 5px; width: 100%; }
        .disabled { opacity: 0.5; pointer-events: none; }
    </style>
</head>
<body>
    <h1>Catan Companion</h1>
    
    <div class="player-select">
        <label for="playerSelect">Select Your Player:</label>
        <select id="playerSelect">
            <option value="">Choose...</option>
        </select>
    </div>
    
    <div id="gameState">
        <p>Current Player: <span id="currentPlayer"></span></p>
        <p>Game State: <span id="gameStateDisplay"></span></p>
    </div>
    
    <div class="action-buttons" id="actionButtons">
        <button id="nextBtn" onclick="doAction('Next')">Next</button>
        <button id="undoBtn" onclick="doAction('Undo')">Undo</button>
        <div id="purchaseButtons"></div>
    </div>

    <script>
        // JavaScript for hanging GET, player selection, and action buttons
        let gameId = window.location.search.get('gameId') || 'default';
        let selectedPlayerId = null;
        let currentVersion = 0;
        
        // Implementation details for hanging GET and actions
    </script>
</body>
</html>
**JavaScript Features:**
- Player selection dropdown populated from `/api/players/{gameId}`
- Current player highlighting
- Action buttons enabled/disabled based on game state and selected player
- Hanging GET loop for real-time updates
- Error handling and reconnection logic
- Simple alert/notification system for action results

### Advanced Features (Future)

**Enhanced UI:**
- Player colors and avatars
- Visual representation of available purchases
- Game state icons and progress indicators
- Notification sounds and vibrations
- Dark mode support

**Progressive Web App:**
- App manifest for "Add to Home Screen"
- Service worker for offline support
- Push notifications for turn changes
- Background sync capabilities

## Security Considerations

**Trust Model:** 
- Local network deployment initially
- Game service and all clients on same local network
- Physical presence assumed for initial authentication
- **Web interface accessible to anyone with URL - relies on player selection**

**Implemented Security:**
- Game session isolation by gameId
- Player identity verification for actions
- Request timeouts to prevent resource exhaustion
- Input validation on all endpoints
- **HTTPS support for production deployments**

## Technical Details

### Dependencies

**Game Service:**
- ASP.NET Core (.NET 9)
- Static file serving for web companion
- System.Text.Json
- Reference to Catan3.Shared

**Web Companion:**
- Modern browser with JavaScript ES6+ support
- CSS Grid/Flexbox for responsive layout
- Fetch API for REST calls
- No external dependencies - pure HTML/CSS/JS

### Configuration

**Game Service:**
- Configurable ports (default: HTTP 8080, UDP discovery 8765)
- Static file serving for `/companion` endpoint
- CORS configuration for cross-origin requests
- Game session management

**Web Companion:**
- Responsive design for mobile devices
- Configurable polling intervals
- Error retry logic
- Offline detection and handling

### Performance Considerations
- Lightweight web interface (< 50KB total)
- Efficient hanging GET implementation
- Minimal JavaScript footprint
- Progressive loading of features
- **Mobile-optimized rendering and interactions**

## Deployment Strategy

**Local Development:**
1. Start game service on localhost:8080
2. Desktop app connects to localhost service
3. Mobile devices connect to `http://[desktop-ip]:8080/companion`
4. QR code displayed on desktop for easy mobile access

**Production/Network Deployment:**
1. Game service runs on dedicated machine or NAS
2. Desktop app connects to network service
3. Mobile devices connect via network URL
4. **Optional: Custom domain and SSL certificates**

## Future Enhancements

1. **Enhanced Web UI**: 
   - Visual game board representation
   - Animated transitions and feedback
   - Advanced player statistics display
   
2. **Progressive Web App**: 
   - Offline gameplay capabilities
   - Push notifications for turn changes
   - Native app-like experience
   
3. **Multi-Game Support**: 
   - Game lobby and selection interface
   - Multiple concurrent game sessions
   - Spectator mode for non-players
   
4. **Advanced Features**: 
   - Voice commands integration
   - Accessibility improvements
   - Internationalization support

## File Structure (After Migration)Catan3/
??? Catan3.csproj                        # WinUI3 client
??? Catan3.Shared/
?   ??? Catan3.Shared.csproj            # Shared models and types
?   ??? Models/
?   ?   ??? GameModel.cs
?   ?   ??? PlayerModel.cs
?   ?   ??? MessageObjects.cs
?   ??? Enums/
?       ??? GameEnums.cs
??? Catan3.GameService/
?   ??? Catan3.GameService.csproj       # ASP.NET Core service
?   ??? Controllers/
?   ?   ??? GameController.cs           # Moved from client
?   ?   ??? GameApiController.cs        # REST endpoints
?   ??? Services/
?   ?   ??? GameSessionManager.cs
?   ?   ??? DiscoveryService.cs
?   ??? wwwroot/
?   ?   ??? companion.html              # Web companion interface
?   ?   ??? companion.js
?   ?   ??? companion.css
?   ??? Program.cs
??? Services/
?   ??? GameControllerProxy.cs          # Client proxy for game service
?   ??? Companion/                      # Legacy - can be removed
??? companion.md                        # This design document
## Conclusion

This new architecture with web-based mobile companion provides:

- **Universal Access**: Any device with a web browser can participate
- **Zero Installation**: No app store deployment or mobile app development needed
- **Clean Separation**: UI logic separated from game logic
- **Scalability**: Service can handle multiple clients across different platforms
- **Testability**: Game logic can be tested independently
- **Simplicity**: Web interface is easier to develop and maintain than native apps
- **Flexibility**: Enables future distributed gameplay scenarios
- **Real-time**: Consistent hanging GET pattern for all client types

The web-based approach eliminates the complexity of native mobile app development while providing a superior user experience that works across all devices and platforms.