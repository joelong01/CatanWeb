using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    public partial class BuildingViewModel : ObservableRecipient
    {
        [JsonIgnore]
        [ObservableProperty]
        public partial BuildingModel Building { get; set; } = BuildingModel.Default;

        [ObservableProperty]
        public partial BoardLayout Layout { get; set; } = BoardLayout.Default;

        [ObservableProperty]
        public partial double Left { get; set; }

        [ObservableProperty]
        public partial double Top { get; set; }

        [ObservableProperty]
        public partial BuildingVisualState VisualState { get; set; } = BuildingVisualState.Hidden;

        [JsonIgnore]
        [ObservableProperty]
        public partial PlayerViewModel CurrentPlayer { get; set; } = PlayerViewModel.Default;

        [ObservableProperty]
        public partial int BuildIndex { get; set; } = 0;

        [ObservableProperty]
        public partial int Stars { get; set; } = -2;
    }
}
