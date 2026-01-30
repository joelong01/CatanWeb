# Recording & Stats Management As-Built

**Status:** As-Built
**Source:** `.design/recording-management.md` & `.design/stats-management.md`

## 1. Recording Management

The system includes a robust recording infrastructure to capture real gameplay for regression testing.

### Workflow
1.  **Record**: User starts a game with "Record Game" checked.
2.  **Streaming**: `RecordingService` captures every `GameModel` update and action message.
3.  **Persistence**: Recordings are saved to the `Recordings` database table immediately to survive crashes.
4.  **Replay**: The `replay` command (`./catan.ps1 recording replay`) re-runs the recorded dictionary of actions against the `GameStateMachine` and verifies the resulting `GameHash` matches the recorded hash.

### Key Components
*   **RecordingEntity**: Database storage for the log.
*   **IRecordedMessage**: Interface for serializing actions.
*   **CLI**: `./catan.ps1 recording [list|save|load|delete|replay]`.

## 2. Stats Management

Lifetime statistics are tracked to resolve "who is the best player" debates.

### Metrics Tracked
*   **General**: Games Played, Wins, Win %.
*   **Aggregates**: Total Score, Average Score.
*   **Special**: Longest Road Wins, Largest Army Wins.
*   **Misery Stats**: Times Targeted (Robber), Resources Lost (7s).

### Implementation
*   **Persistence**: Stored within the `PlayerEntity` JSON document in the database.
*   **Update Trigger**: `GameStateMachine.UpdateScore` calculates local scores, but `GameService` finalizes/persists lifetime stats only when a Winner is declared.
*   **Test Isolation**: "Save Lifetime Stats" flag in `NewGameMessage` prevents test games from polluting the leaderboard.

### CLI Operations
*   `./catan.ps1 stats list`: View leaderboard.
*   `./catan.ps1 stats export`: Backup to JSON.
*   `./catan.ps1 stats import`: Restore from JSON.
*   `./catan.ps1 stats reset`: Clear all data.
