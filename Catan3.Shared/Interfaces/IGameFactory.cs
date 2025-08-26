using System.Collections.Generic;
using Catan3.Shared.Models;

namespace Catan3.Shared.Interfaces
{
    /// <summary>
    /// Interface for creating and manipulating game models.
    /// Abstracts platform-specific game creation logic.
    /// </summary>
    public interface IGameFactory
    {
        /// <summary>
        /// Creates a new game model with the specified type and players.
        /// </summary>
        /// <param name="gameType">The type of game to create.</param>
        /// <param name="playerIds">The list of player IDs for the game.</param>
        /// <returns>A new GameModel configured for the specified game type and players.</returns>
        GameModel CreateGame(GameType gameType, IList<string> playerIds);

        /// <summary>
        /// Shuffles the content of a game model (tiles, numbers, resources).
        /// This affects the tile contents and arrangement, not the player order.
        /// </summary>
        /// <param name="gameModel">The game model to shuffle.</param>
        void Shuffle(GameModel gameModel);
    }
}