using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Catan3.Services
{
    /// <summary>
    /// Desktop implementation of IGameFactory that uses the existing GameFactory static methods.
    /// Sets reasonable defaults for GameId and GameName for desktop usage.
    /// </summary>
    public class DesktopGameFactory : IGameFactory
    {
        public GameModel CreateGame(GameType gameType, IList<string> playerIds)
        {
            var game = Catan3.Models.GameFactory.CreateGame(gameType, playerIds);
            
            // Set reasonable defaults for desktop usage
            game.GameId = GenerateDesktopGameId();
            game.GameName = GenerateDefaultGameName(gameType, playerIds);
            
            return game;
        }

        public void Shuffle(GameModel gameModel)
        {
            Catan3.Models.GameFactory.Shuffle(gameModel);
        }

        private static string GenerateDesktopGameId()
        {
            // Generate a short, readable ID for desktop usage
            // Format similar to base64 but more readable (no +/= chars)
            var guid = Guid.NewGuid();
            var bytes = guid.ToByteArray();
            var base64 = Convert.ToBase64String(bytes);
            // Take first 14 chars and make URL-safe
            return base64.Substring(0, 14).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        private static string GenerateDefaultGameName(GameType gameType, IList<string> playerIds)
        {
            // Create a default name like "Regular - Alice +2" or "Expansion - Bob +3"
            var firstPlayer = playerIds.FirstOrDefault() ?? "Player1";
            var additionalCount = playerIds.Count - 1;
            
            if (additionalCount > 0)
            {
                return $"{gameType} - {firstPlayer} +{additionalCount}";
            }
            else
            {
                return $"{gameType} - {firstPlayer}";
            }
        }
    }
}