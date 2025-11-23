# Session & Bootstrap Design Document

## Overview

This document describes the session management, user identity, game discovery, and bootstrap flow for the WebUI. The design supports both **shared screen** mode (one device, multiple players taking turns) and **companion mode** (each player on their own device).

## Session State Management

### Cookie Structure

A persistent cookie stores the user's session state as JSON:

```json
{
  "playerId": "Doug-a1b2c3-ID",
  "currentGameId": "game-xyz-123",
  "lastConnected": "2025-11-22T10:30:00Z"
}
```

**Cookie Properties:**
- **Name**: `CatanSession`
- **Persistence**: Survives browser close and crashes
- **Expiration**: 30 days (or configurable)
- **HttpOnly**: No (needs JavaScript access for SignalR)
- **SameSite**: Lax

### Session Fields

| Field | Type | Purpose |
|-------|------|---------|
| `playerId` | string | Unique player identifier (format: `Name-Salt-ID`) |
| `currentGameId` | string? | Active game ID, null if not in a game |
| `lastConnected` | datetime | Last successful connection timestamp |

### F5/Page Refresh Handling

When the page loads:

```
1. Check for CatanSession cookie
   ├─ No cookie → Redirect to Login page
   └─ Has cookie → Parse session data
       ├─ No currentGameId → Show NewGame page
       └─ Has currentGameId → Attempt to rejoin game
           ├─ Game exists → Join and render GameModel
           └─ Game not found → Clear currentGameId, show NewGame page
```

**Implementation in App startup:**

```csharp
// In Program.cs or a session service
public async Task<SessionState> InitializeSession()
{
    var session = GetSessionFromCookie();

    if (session == null)
    {
        NavigateTo("/login");
        return null;
    }

    if (!string.IsNullOrEmpty(session.CurrentGameId))
    {
        var gameExists = await GameService.GameExistsAsync(session.CurrentGameId);
        if (gameExists)
        {
            NavigateTo($"/game/{session.CurrentGameId}");
        }
        else
        {
            session.CurrentGameId = null;
            SaveSessionToCookie(session);
            NavigateTo("/newgame");
        }
    }

    return session;
}
```

## User Identity

### Player ID Format

Player IDs should be unique and readable:

```
Format: {FirstName}-{Salt}-ID
Examples:
  - Doug-a1b2c3-ID
  - Sarah-x7y8z9-ID
  - Guest-000001-ID
```

**Salt generation**: 6 alphanumeric characters from GUID

### Login Flow

**First-time user (no cookie):**

```
Login Page
├─ "Select Player" dropdown (existing players from database)
│   └─ Select → Set playerId in cookie → Navigate to NewGame
├─ "Create Account" button
│   └─ Click → Navigate to Account Setup page
└─ "Continue as Guest" button
    └─ Click → Create guest player → Navigate to NewGame
```

**Returning user (has cookie):**
- Automatically use stored playerId
- Can change player via Account menu

### Account Setup Page

New player registration:

```
┌─────────────────────────────────┐
│     Create Your Account         │
├─────────────────────────────────┤
│                                 │
│  [Photo/Avatar upload]          │
│                                 │
│  Name: [____________]           │
│                                 │
│  Primary Color:   [Color Picker]│
│  Secondary Color: [Color Picker]│
│  Text Color:      [Color Picker]│
│                                 │
│  [Create Account]  [Cancel]     │
└─────────────────────────────────┘
```

**Server-side storage:**
- Player profile saved to `Players` table in database
- Profile image saved to `Images` table
- Returns generated playerId

### Account Menu (In-App)

Upper-right corner of NewGamePage and GamePage:

```
┌──────────────────────┐
│              [Photo] │
└──────────────────────┘
         ↓ Click
┌──────────────────────┐
│ Doug                 │
│ ──────────────────── │
│ Edit Profile         │
│ Change Photo         │
│ Change Colors        │
│ ──────────────────── │
│ Switch Player        │
│ Log Out              │
└──────────────────────┘
```

**Edit Profile** opens a simplified PlayerEditor (like Desktop's `PlayerEditorPage.xaml` but for single player only).

## Game Discovery & Joining

### One Game at a Time

- Each player can only be in **one active game**
- Starting a new game while in another requires ending the current game
- Cookie stores only one `currentGameId`

### Game Invitation Model

**Host creates game:**
1. Host on NewGamePage selects players from database
2. Host clicks "Start Game"
3. GameService creates game and stores player list
4. GameService broadcasts invitation to all selected players

**Player receives invitation:**
```
┌─────────────────────────────────┐
│  Game Invitation                │
│                                 │
│  Doug wants you to join a game  │
│                                 │
│  Players: Doug, Sarah, Mike     │
│  Game Type: Expansion           │
│                                 │
│  [Accept]  [Decline]            │
└─────────────────────────────────┘
```

**Invitation states:**
- **Online player**: Receives real-time SignalR notification
- **Offline player**: Invitation stored, shown on next login

### Checking for Pending Invitations

On login/connect, check for pending game invitations:

```csharp
// GameService endpoint
public async Task<List<GameInvitation>> GetPendingInvitations(string playerId)
{
    // Check all active games for this player
    var games = await GetAllActiveGames();
    return games
        .Where(g => g.Players.Any(p => p.Id == playerId))
        .Where(g => !g.HasPlayerJoined(playerId))
        .Select(g => new GameInvitation(g))
        .ToList();
}
```

### Join Flow

```
1. Player logs in
2. Check for pending invitations
   ├─ Has invitations → Show invitation dialog
   │   ├─ Accept → Set currentGameId, navigate to game
   │   └─ Decline → Remove from game, show NewGame
   └─ No invitations → Show NewGame page
3. Player can also manually enter GameId to join
```

## Game Lifecycle

### Creating a New Game

**NewGamePage flow:**
1. Select game type (Regular/Expansion)
2. Select players from database
3. Configure options (optional)
4. Click "Create Game"
5. GameService creates game, sends invitations
6. Host's cookie updated with `currentGameId`
7. Host navigated to Game page

### During Game

- All players connected via SignalR
- Each action validated: only `CurrentPlayerId` can act
- GameModel broadcast to all players on each state change

### Ending a Game

**When game completes or host ends early:**

```
┌─────────────────────────────────┐
│  Game Over                      │
│                                 │
│  Who won?                       │
│  ○ Doug (7 VP)                  │
│  ● Sarah (10 VP) ← selected     │
│  ○ Mike (8 VP)                  │
│                                 │
│  [Record & End]  [Cancel]       │
└─────────────────────────────────┘
```

**On end:**
1. Record game statistics to Stats table (future feature)
2. Clear `currentGameId` from all players' sessions
3. Notify all players game has ended
4. Navigate to NewGame page

### Rejoining a Game

**Scenarios:**
- Browser refresh (F5)
- Browser crash
- Switch devices
- Network disconnect/reconnect

**Rejoin flow:**
1. Cookie has `currentGameId`
2. Connect to SignalR
3. Call `JoinGame(gameId, playerId)`
4. Receive current GameModel
5. Render game state

## Dual Mode Support

### Shared Screen Mode

Traditional local multiplayer on one device:

- One browser shows full game interface
- All players gather around same screen
- Current player takes their turn, passes to next
- Like the Desktop app experience

### Companion Mode

Each player on their own device:

- Main display shows the game board (could be a TV/large monitor)
- Each player has companion view on their phone/tablet
- Companion shows:
  - Current game state
  - Their hand/resources (private info)
  - Action buttons (Next, Purchase, etc.)
  - Board view for piece placement

**Companion considerations:**
- Mobile-responsive design required
- Simplified UI for smaller screens
- Touch-friendly controls
- May hide some elements (like other players' resources)

### SignalR Connection Management

Both modes use the same SignalR infrastructure:

```csharp
// On connect, identify device type
await _hubConnection.InvokeAsync("JoinGame", gameId, playerId, new DeviceInfo
{
    Type = DeviceType.Companion, // or DeviceType.Main
    ScreenSize = "mobile",
    Features = new[] { "touch", "camera" }
});
```

**Server tracks:**
- Which devices are connected for each player
- Device capabilities
- Can send targeted messages (e.g., private info only to player's devices)

## API Endpoints

### Session Management

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/session/validate` | GET | Check if session/playerId is valid |
| `/api/session/player/{playerId}` | GET | Get player profile |

### Player Management

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/players` | GET | List all players |
| `/api/players` | POST | Create new player |
| `/api/players/{id}` | PUT | Update player profile |
| `/api/players/{id}/image` | POST | Upload player image |

### Game Discovery

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/games/invitations/{playerId}` | GET | Get pending invitations |
| `/api/games/{gameId}/join` | POST | Accept invitation and join |
| `/api/games/{gameId}/decline` | POST | Decline invitation |
| `/api/games/{gameId}/exists` | GET | Check if game exists |

### SignalR Hub Methods

**Client → Server:**
- `JoinGame(gameId, playerId, deviceInfo)`
- `LeaveGame(gameId, playerId)`
- `AcceptInvitation(gameId, playerId)`
- `DeclineInvitation(gameId, playerId)`

**Server → Client:**
- `GameInvitation(invitation)` - New game invitation
- `GameStateUpdated(gameModel)` - Game state changed
- `GameEnded(gameId, results)` - Game has ended
- `PlayerJoined(gameId, playerId)` - Player joined the game
- `PlayerLeft(gameId, playerId)` - Player left/disconnected

## Security Considerations

### Current Implementation (Phase 1)

- **Identification only**: Players select from list or create profile
- **No password**: Trust that users select their own profile
- **PlayerId validation**: Server validates only current player can act
- **Session cookie**: Not HttpOnly, accessible to JavaScript

### Future Authentication (Phase 2)

- Add password to player profiles
- JWT tokens for API authentication
- Secure HttpOnly cookies
- Rate limiting on login attempts
- Account lockout after failed attempts

## Database Schema Additions

### GameInvitations Table

```sql
CREATE TABLE GameInvitations (
    Id TEXT PRIMARY KEY,
    GameId TEXT NOT NULL,
    PlayerId TEXT NOT NULL,
    InvitedAt DATETIME NOT NULL,
    Status TEXT NOT NULL, -- 'pending', 'accepted', 'declined'
    RespondedAt DATETIME,
    FOREIGN KEY (GameId) REFERENCES GameSaves(GameId),
    FOREIGN KEY (PlayerId) REFERENCES Players(Id)
);
```

### GameStats Table (Future)

```sql
CREATE TABLE GameStats (
    Id TEXT PRIMARY KEY,
    GameId TEXT NOT NULL,
    PlayerId TEXT NOT NULL,
    WinnerId TEXT,
    FinalScore INT,
    RoadsBuilt INT,
    SettlementsBuilt INT,
    CitiesBuilt INT,
    -- ... other stats
    CompletedAt DATETIME NOT NULL
);
```

## Page Flow Diagram

```
                    ┌─────────┐
                    │  Start  │
                    └────┬────┘
                         │
                    ┌────▼────┐
                    │  Has    │
                    │ Cookie? │
                    └────┬────┘
                    No   │   Yes
              ┌──────────┴──────────┐
              │                     │
         ┌────▼────┐          ┌─────▼─────┐
         │  Login  │          │   Has     │
         │  Page   │          │  GameId?  │
         └────┬────┘          └─────┬─────┘
              │                No   │   Yes
              │          ┌──────────┴──────────┐
              │          │                     │
              │    ┌─────▼─────┐         ┌─────▼─────┐
              │    │  Check    │         │   Game    │
              │    │  Invites  │         │  Exists?  │
              │    └─────┬─────┘         └─────┬─────┘
              │          │               No    │   Yes
              │          │         ┌───────────┴──────┐
              │          │         │                  │
              │    ┌─────▼─────┐   │           ┌──────▼──────┐
              └───►│  NewGame  │◄──┘           │   Rejoin    │
                   │   Page    │               │    Game     │
                   └─────┬─────┘               └──────┬──────┘
                         │                           │
                         │ Create Game               │
                         │                           │
                   ┌─────▼─────────────────────▼─────┐
                   │          Game Page              │
                   └─────────────────────────────────┘
```

## Implementation Phases

### Phase 1: Basic Session (Current Priority)

1. Cookie-based session storage
2. Player selection (pick from list)
3. F5 refresh → rejoin game
4. Basic account menu

### Phase 2: Player Management

1. Create new player flow
2. Edit profile (photo, colors)
3. Player ID format update
4. Server-side profile persistence

### Phase 3: Game Invitations

1. Invitation model
2. Pending invitation check
3. Accept/decline flow
4. Notification system

### Phase 4: Companion Mode

1. Mobile-responsive layout
2. Device detection
3. Private information handling
4. Touch-optimized controls

### Phase 5: Authentication

1. Password support
2. JWT tokens
3. Secure cookies
4. Login/logout flow

## File References

### Desktop App (Reference)

- `DesktopApp/Player/PlayerSettings/PlayerEditorPage.xaml` - Player profile editor
- `DesktopApp/Player/PlayerSettings/PlayerSettingsCtrl.xaml` - Settings control

### WebUI (To Create)

- `WebUI/Pages/Login.razor` - Player selection/login
- `WebUI/Pages/AccountSetup.razor` - New player registration
- `WebUI/Components/AccountMenu.razor` - Profile menu dropdown
- `WebUI/Services/SessionService.cs` - Cookie management
- `WebUI/Services/PlayerService.cs` - Player API client

### GameService (To Create/Update)

- `Controllers/SessionController.cs` - Session validation
- `Controllers/PlayerController.cs` - Player CRUD
- `Hubs/GameHub.cs` - Add invitation methods
- `Services/InvitationService.cs` - Invitation management

### Shared Models

- `Catan3.Shared/Models/SessionState.cs` - Cookie data model
- `Catan3.Shared/Models/GameInvitation.cs` - Invitation model
- `Catan3.Shared/Models/DeviceInfo.cs` - Device capabilities

## Testing Considerations

### Session Tests

- Cookie creation/reading
- Session expiration
- Invalid session handling
- F5 rejoin scenarios

### Invitation Tests

- Send invitation to online player
- Send invitation to offline player
- Accept/decline flow
- Multiple pending invitations

### Companion Tests

- See `Tests/GameService/Companion/` for existing companion tests
- Mobile viewport testing
- Touch interaction testing
- Multi-device synchronization
