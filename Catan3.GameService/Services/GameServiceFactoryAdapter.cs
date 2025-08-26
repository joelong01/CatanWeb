using System.Collections.Generic;
using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using Catan3.GameService.Factory;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// GameService implementation of IGameFactory that wraps the existing GameFactory class.
    /// Bridges the shared GameStateMachine factory interface to GameService's existing GameFactory.
    /// </summary>
    public class GameServiceFactoryAdapter : IGameFactory
    {
        public GameModel CreateGame(GameType gameType, IList<string> playerIds)
        {
            return GameFactory.CreateGame(gameType, playerIds);
        }

        public void Shuffle(GameModel gameModel)
        {
            GameFactory.Shuffle(gameModel);
        }
    }
}