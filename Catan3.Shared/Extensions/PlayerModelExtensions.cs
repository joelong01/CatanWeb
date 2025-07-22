using System.Collections.Generic;
using System.Linq;
using Catan3.Shared.Models;

namespace Catan3.Shared.Extensions
{
    public static class PlayerModelExtensions
    {
        /// <summary>
        /// Finds a player by their ID from a collection of players
        /// </summary>
        /// <param name="players">Collection of players</param>
        /// <param name="id">The player ID to search for</param>
        /// <returns>The player with the specified ID, or null if not found</returns>
        public static PlayerModel? PlayerFromId(this IList<PlayerModel> players, string id)
        {
            return players.FirstOrDefault(p => p.Id == id);
        }

        /// <summary>
        /// Finds a player by their ID from an enumerable collection of players
        /// </summary>
        /// <param name="players">Collection of players</param>
        /// <param name="id">The player ID to search for</param>
        /// <returns>The player with the specified ID, or null if not found</returns>
        public static PlayerModel? PlayerFromId(this IEnumerable<PlayerModel> players, string id)
        {
            return players.FirstOrDefault(p => p.Id == id);
        }
    }
}