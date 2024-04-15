
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class RobberModel : ObservableObject
    {
        [ObservableProperty]
        private HexCoordinates _coordinates = HexCoordinates.Default;

        [ObservableProperty]
        private string? _movedBy = null;

        [ObservableProperty]
        private string? _targetted = null;

        [ObservableProperty]
        private int _resourcesStolen = 0;

      

        public override string ToString()
        {
            return $"{Coordinates}-{MovedBy}->{Targetted}: {ResourcesStolen}";
        }

    }

}
