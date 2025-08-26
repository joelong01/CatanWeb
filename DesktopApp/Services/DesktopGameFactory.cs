using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using System.Collections.Generic;

namespace Catan3.Services
{
    /// <summary>
    /// Desktop implementation of IGameFactory that uses the existing GameFactory static methods.
    /// </summary>
    public class DesktopGameFactory : IGameFactory
    {
        public GameModel CreateGame(GameType gameType, IList<string> playerIds)
        {
            return Catan3.Models.GameFactory.CreateGame(gameType, playerIds);
        }

        public void Shuffle(GameModel gameModel)
        {
            Catan3.Models.GameFactory.Shuffle(gameModel);
        }
    }
}