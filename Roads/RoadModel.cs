using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class RoadModel(RoadKey roadKey) : ObservableObject
    {
        [ObservableProperty]
        private RoadKey _roadKey = roadKey;

        [ObservableProperty]
        private RoadState _roadState = RoadState.Unowned;

        [ObservableProperty]
        private PlayerModel? _owner;
    }
}
