using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    public partial class BuildingViewModel : ObservableRecipient
    {
        [JsonIgnore]
        [ObservableProperty]
        private BuildingModel _building = BuildingModel.Default;
        [ObservableProperty]
        private BoardLayout _layout = BoardLayout.Default;
        [ObservableProperty]
        private double _left;
        [ObservableProperty]
        private double _top;
        [ObservableProperty]
        private BuildingVisualState _visualState = BuildingVisualState.Hidden;
        [JsonIgnore]
        [ObservableProperty]
        private PlayerViewModel _currentPlayer = PlayerViewModel.Default;
        [ObservableProperty]
        private int _buildIndex = 0;
        [ObservableProperty]
        private int _stars = -2;
    }
}
