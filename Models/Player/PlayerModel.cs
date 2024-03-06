using System.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    ///     this should have all the data representing per player state that is bound to the UI 
    /// </summary>
    /// <param name="idx"></param>
    public partial class PlayerModel(int idx) : INotifyPropertyChanged
    {
        public int Index { get; } = idx;
        public override string ToString()
        {
            return $"{Index}";
        }
        public static PlayerModel Default { get; } = new PlayerModel(-1);
        public int CitiesPlayed { get; set; }
        public int KnightsPlayed { get; set; } = 0;
        public int GoldRolls { get; set; } = 0;
        public bool GoodRoll { get; set; } = false;
        public bool HasLongestRoad { get; set; } = false;
        public bool IsCurrentPlayer { get; set; } = false;
        public int IslandsPlayed { get; set; } = 0;
        public bool LargestArmy { get; set; } = false;
        public int LongestRoad { get; set; } = 0;
        
        public int MaxNoResourceRolls { get; set; } = 0;
     
        public int NoResourceCount { get; set; } = 0;
        public int Pips { get; set; } = 0;
        public int RoadsPlayed { get; set; } = 0;
        public int Score { get; set; } = 0;
        public int SettlementsPlayed { get; set; } = 0;
        public int ShipsPlayed { get; set; } = 0;
        public int TimesTargeted { get; set; } = 0;
    }
}
