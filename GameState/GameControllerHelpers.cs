using Catan3.Models;
using Catan3.Utility;

namespace Catan3.Controller
{
    /// <summary>
    /// Provides helper methods to change the current player in a GameModel.
    /// </summary>
    public static class ChangePlayerHelper
    {
        /// <summary>
        /// Calculates the player ID that is a specified number of positions away from a given start player ID.
        /// </summary>
        /// <param name="gameModel">The game model containing the players.</param>
        /// <param name="startPlayerId">The ID of the player from which to start counting.</param>
        /// <param name="numberOfPositions">The number of positions to move forward in the player list; can be negative.</param>
        /// <returns>The player ID of the player numberOfPositions away from the start player.</returns>
        /// <exception cref="GameException">Thrown if the start player ID is invalid or not in the game.</exception>
        public static string NextPlayerId(this GameModel gameModel, string startPlayerId, int numberOfPositions)
        {
            // Validate and find the starting player
            var startPlayer = gameModel.Players.PlayerFromId(startPlayerId) ??
            throw new GameException($"Invalid id: {startPlayerId}");

            int idx = gameModel.Players.IndexOf(startPlayer);
            if (idx == -1)
                throw new GameException("The player must be in the game!");

            int count = gameModel.Players.Count;

            // Calculate the index of the next player, wrapping around if necessary
            int newPlayerIndex = (idx + numberOfPositions) % count;
            if (newPlayerIndex < 0)
                newPlayerIndex += count;

            // Retrieve the new player's ID
            var newPlayer = gameModel.Players[newPlayerIndex];
            return newPlayer.Id;
        }

        /// <summary>
        /// Changes the current player to the player a specified number of positions forward.
        /// </summary>
        /// <param name="gameModel">The game model where the current player will be changed.</param>
        /// <param name="numberOfPositions">The number of positions to move forward in the player list.</param>
        /// /// <exception cref="GameException">Thrown if the player ID is invalid.</exception>
        public static void ChangePlayer(this GameModel gameModel, int numberOfPositions)
        {
            // Ensure the current player ID is valid
            if (string.IsNullOrEmpty(gameModel.CurrentPlayerId))
                throw new GameException("Current player ID must not be null or empty.");

            // Get the next player ID and change to it
            var id = NextPlayerId(gameModel, gameModel.CurrentPlayerId, numberOfPositions);
            gameModel.ChangePlayerTo(id);
        }

        /// <summary>
        /// Sets the current player to the specified player ID.
        /// </summary>
        /// <param name="gameModel">The game model where the current player will be set.</param>
        /// <param name="playerId">The player ID to set as current.</param>
        /// <exception cref="GameException">Thrown if the player ID is invalid.</exception>
        public static void ChangePlayerTo(this GameModel gameModel, string playerId)
        {
            // Validate and find the new player
            var newPlayer = gameModel.Players.PlayerFromId(playerId) ??
            throw new GameException($"Invalid id: {playerId}");

            // Set the current player ID
            gameModel.CurrentPlayerId = newPlayer.Id;
        }
    }
}
