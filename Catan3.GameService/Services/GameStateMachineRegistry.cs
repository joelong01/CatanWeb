using System.Collections.Concurrent;
using Catan3.Shared.GameLogic;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// Static registry for GameStateMachine instances
    /// Shared between GameApiController and GameHub
    /// </summary>
    public static class GameStateMachineRegistry
    {
        private static readonly ConcurrentDictionary<string, GameStateMachine> _games = [];

        /// <summary>
        /// Gets an existing GameStateMachine for the specified gameId
        /// Throws GameException if the game doesn't exist
        /// </summary>
        public static GameStateMachine GetGameStateMachine(string gameId)
        {
            if (_games.TryGetValue(gameId, out var gameStateMachine))
            {
                return gameStateMachine;
            }
            throw new GameException($"Game {gameId} not found");
        }

        /// <summary>
        /// Adds a GameStateMachine to the registry
        /// </summary>
        public static void AddGameStateMachine(string gameId, GameStateMachine gameStateMachine)
        {
            _games[gameId] = gameStateMachine;
        }

        /// <summary>
        /// Gets all available games as GameInfo objects
        /// </summary>
        public static List<GameInfo> GetAvailableGames()
        {
            var availableGames = new List<GameInfo>();

            foreach (var kvp in _games)
            {
                var gameId = kvp.Key;
                var gameStateMachine = kvp.Value;
                var gameModel = gameStateMachine.GetCurrentState();

                var gameInfo = new GameInfo
                {
                    GameId = gameId,
                    DisplayName = gameModel.GameName ?? gameId,
                    GameType = gameModel.GameType.ToString(),
                    GameState = gameModel.GameState.ToString(),
                    PlayerCount = gameModel.Players.Count,
                    // PlayerNames and CurrentPlayer are left empty: the registry has no access
                    // to player profiles, and a name must never be derived from an ID. The
                    // client resolves both from the ID fields below (issue #208).
                    PlayerIds = gameModel.Players.Select(p => p.Id).ToList(),
                    CurrentPlayerId = gameModel.CurrentPlayerId ?? "",
                    CreatedTime = gameModel.CreatedTime,
                    CreatedTimeDisplay = gameModel.CreatedTime.ToString("yyyy-MM-dd HH:mm"),
                    IsActive = gameModel.GameState != GameState.GameOver,
                    GameStateMachineVersion = gameModel.GameStateMachineVersion,
                    Summary = $"{gameModel.Players.Count} players, {gameModel.GameState}"
                };

                availableGames.Add(gameInfo);
            }

            return availableGames;
        }

        /// <summary>
        /// Removes a GameStateMachine from the registry
        /// Returns true if the game was found and removed
        /// </summary>
        public static bool DeleteGameStateMachine(string gameId)
        {
            return _games.TryRemove(gameId, out _);
        }

        /// <summary>
        /// Gets all game IDs that contain any of the specified player IDs.
        /// Used to notify games when player profiles are updated.
        /// </summary>
        public static List<string> GetGamesWithPlayers(IEnumerable<string> playerIds)
        {
            var playerIdSet = new HashSet<string>(playerIds);
            var matchingGameIds = new List<string>();

            foreach (var kvp in _games)
            {
                var gameModel = kvp.Value.GetCurrentState();
                if (gameModel.Players.Any(p => playerIdSet.Contains(p.Id)))
                {
                    matchingGameIds.Add(kvp.Key);
                }
            }

            return matchingGameIds;
        }
    }
}