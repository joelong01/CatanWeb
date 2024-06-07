
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    public partial class HouseRules : ObservableObject
    {
        [ObservableProperty]
        private int _goldTiles = 1;
        [ObservableProperty]
        private bool _wallsProtectCities = true;
        [ObservableProperty]
        private bool _hideBaronBeforeInvasion = false;
        [ObservableProperty]
        private bool _knightMovesBaronBeforeRoll = true;
        [ObservableProperty]
        private bool _hideRobberBeforeInvasion =  false;
        [ObservableProperty]
        private bool _knightMovesRobberBeforeRoll = false;
    }
}
