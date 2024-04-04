using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    ///     this should have all the data representing per player state that is bound to the UI 
    /// </summary>
    /// <param name="idx"></param>
    public partial class PlayerModel(string id) : ObservableObject
    {
        [ObservableProperty]
        private string _id = id;

        [ObservableProperty]
        private int _citiesPlayed;

        [ObservableProperty]
        private int _knightsPlayed = 0;

        [ObservableProperty]
        private int _goldRolls = 0;

        [ObservableProperty]
        private bool _goodRoll = false;

        [ObservableProperty]
        private bool _hasLongestRoad = false;

        [ObservableProperty]
        private bool _isCurrentPlayer = false;

        [ObservableProperty]
        private int _islandsPlayed;

        [ObservableProperty]
        private bool _largestArmy = false;

        [ObservableProperty]
        private int _longestRoad;

        [ObservableProperty]
        private int _maxNoResourceRolls = 0;

        [ObservableProperty]
        private int _noResourceCount = 0;

        [ObservableProperty]
        private int _pips = 0;

        [ObservableProperty]
        private int _roadsPlayed;

        [ObservableProperty]
        private int _score;

        [ObservableProperty]
        private int _settlementsPlayed;

        [ObservableProperty]
        private int _shipsPlayed;

        [ObservableProperty]
        private int _timesTargeted;
        public static PlayerModel Default { get; } = new PlayerModel("Nameless-001");

        public override string ToString()
        {
            return $"{Id}";
        }
       
    }
}
