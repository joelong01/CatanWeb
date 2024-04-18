using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Catan3.Models
{
    public partial class GameViewModel
    {
        /// <summary>
        ///     return a string with the number of times the roll (Roll enum) has happened.
        ///     GameModel and TotalRolls should be passed in so that when they change bindings 
        ///     are updated
        /// </summary>
      
   

        public string StateMessage(GameModel _, GameState gameState)
        {
            return gameState.Description();
        }
    }
}
