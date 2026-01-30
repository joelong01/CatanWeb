# Game Play Description

This document describes the unique "hybrid" play style of this Catan adaptation. Unlike a fully digital implementation or a purely physical board game, this project is designed to enhance an in-person game session played around a table.

## The "Hybrid" Experience

The game is played **together, seated around a table** with a shared monitor (or TV) displaying the application.

- **Physical Components**:
  - **Dice**: Players roll physical dice.
  - **Cards**: Resource and Development cards are physical decks managed by the players.
  - **Trading**: Done verbally and with physical cards.
- **Digital Components**:
  - **Game Board**: The layout (hexes, numbers, harbors) is generated and displayed by the app.
  - **State Tracking**: Roads, settlements, cities, and the robber/baron position are tracked in the app.
  - **Score Keeping**: The app tracks victory points (visible and hidden) and turn order.
  - **Statistics**: The app tracks roll history and board statistics (pip distribution).

### Trust Model

The application operates on a **high-trust model**. Because the app does not currently track the individual hands of players (resource and dev cards), it relies on the **shared goodwill** of the players.

- **Honor System**: Players are trusted to pay the correct resources when buying items in the app.
- **No Verification**: The app will not stop a player from building a city if they don't have the cards—it assumes they paid the bank.
- **Discarding**: When a 7 is rolled, players are trusted to discard half their cards if over the limit.

## Gameplay Flow

1. **Turn Start**: The app indicates whose turn it is.
2. **Roll**:
    - The active player rolls physical dice.
    - **Input**: The roll result (2-12) is clicked in the app's "Roll Grid".
    - **Resolution**: The app highlights producing hexes (visual aid only) or triggers the Robber/Baron logic if a 7 is rolled.
3. **Trade & Build**:
    - Players trade physical cards freely.
    - To build, players pay cards to the physical bank and click the corresponding purchase button in the app (Road, Settlement, City, Dev Card).
    - **Placement**: For visual items (Roads/Buildings), the player clicks the board location to place them.
4. **End Turn**: The player clicks "Next" to pass the turn.

## House Rules

The game includes several configurable "House Rules" that can be toggled in the settings. These rules modify the standard Catan mechanics or add unique flair.

| Rule | Description | Default |
| :--- | :--- | :--- |
| **Gold Tiles** | Adds Gold tiles to the board generation (players receive gold resource, which acts as a wildcard). | Enabled (1) |
| **Walls Protect Cities** | (Cities & Knights style) Walls prevent the robber from blocking a city? *Note: Needs verification of implementation logic.* | Enabled |
| **Supplemental Build Phase** | Enables the "Special Build Phase" for 5-6 player games (Catan Expansion rule). | Min 5 Players |
| **Grief Dodgy** | A specific "fun" mode targeting a player named "Dodgy". Adds animations and special effects (e.g., cheering when the robber hits him). | Enabled |
| **Baron / Robber Logic** | | |
| - *Hide Before Invasion* | Hides the Robber/Baron piece until the first 7 is rolled or a Knight is played. | Disabled |
| - *Knight Moves Before Roll* | Allows playing a Knight card to move the Robber/Baron *before* rolling the dice (strategic variance). | Enabled (Knight), Disabled (Robber) |

### Board Layout

The board is dynamically generated. Players can "re-roll" the board layout during the setup phase (Voting on board fairness) before locking it in to start the game. The app calculates "Pips" (probability dots) to help evaluate board balance.

## Statistics

Statistics are a crucial part of the game experience, often used to settle debates about luck and strategy. The app tracks both in-game roll statistics and lifetime player statistics.

### In-Game Roll Statistics

During a game, the app tracks every dice roll. This data is displayed in real-time on the main game screen (in the "Roll Grid").

| Statistic | Description |
| :--- | :--- |
| **Roll Count** | The total number of times each number (2-12) has been rolled in the current game. |
| **Roll Percentage** | The percentage of total rolls for each number. Players often compare this to the statistical probability (e.g., 7 should be ~16%). |

### Lifetime Player Statistics

The "Stats" page (`/stats`) tracks usage data across all games played. These stats are aggregated per player profile.

| Category | Statistic | Description |
| :--- | :--- | :--- |
| **General** | **Games Played** | Total number of games the player has participated in. |
| | **Wins** | Total number of games won. |
| **Special** | **Longest Road Wins** | Number of games where the player ended with the Longest Road. |
| | **Largest Army Wins** | Number of games where the player ended with the Largest Army. |
| **Aggregates** | **Soldiers** | Tracks Knights/Soldiers played. Includes Total, Max (in one game), Min, and Average. |
| | **Stars** | Tracks Victory Points (Stars). Includes Total, Max, Min, and Average. |
| **Misery** | **Times Targeted** | How often the player has been stolen from by the robber. Includes Total, Max, Min, and Average. |
| | **Robber Losses** | Resources lost to the "7 rule" (discarding half cards). Includes Total, Max, Min, and Average. |
