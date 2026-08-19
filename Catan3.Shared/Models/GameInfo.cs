namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents user-friendly information about an available game
    /// Hides technical gameId and shows relevant game details
    /// Used for game discovery and companion interface
    /// </summary>
    public class GameInfo
    {
        public string GameId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string GameType { get; set; } = "";
        public string GameState { get; set; } = "";
        public int PlayerCount { get; set; }
        public List<string> PlayerNames { get; set; } = [];
        public List<string> PlayerIds { get; set; } = [];
        public string CurrentPlayer { get; set; } = "";

        /// <summary>
        /// ID of the player whose turn it is. Callers should resolve the display name from
        /// the player profile by this ID rather than parsing it (issue #208).
        /// </summary>
        public string CurrentPlayerId { get; set; } = "";
        public DateTime CreatedTime { get; set; }
        public string CreatedTimeDisplay { get; set; } = "";
        public bool IsActive { get; set; }
        public int GameStateMachineVersion { get; set; }
        public string Summary { get; set; } = "";
    }

    /// <summary>
    /// Represents a player in a new game request
    /// Follows the Desktop app pattern with both Id and Name
    /// </summary>
    public class PlayerInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";

        public PlayerInfo() { }

        public PlayerInfo(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    /// <summary>
    /// Request structure for creating a new game
    /// Supports both simple string arrays (backward compatibility) and complex player objects
    /// </summary>
    public class NewGameRequest
    {
        public string GameId { get; set; } = "";
        public GameType GameType { get; set; } = GameType.Regular;
        public List<string>? PlayerIds { get; set; }
        public List<PlayerInfo>? Players { get; set; }

        /// <summary>
        /// Gets the effective player IDs from either PlayerIds or Players
        /// </summary>
        public List<string> GetPlayerIds()
        {
            if (Players != null && Players.Count > 0)
            {
                return Players.Select(p => p.Id).Where(id => !string.IsNullOrEmpty(id)).ToList();
            }

            return PlayerIds?.Where(id => !string.IsNullOrEmpty(id)).ToList() ?? [];
        }

    }
}