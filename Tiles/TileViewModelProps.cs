using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class TileViewModel : ObservableObject
    {
        [JsonIgnore]
        [ObservableProperty]
        private TileModel _tile;

        [ObservableProperty]
        private BoardLayout? _layout;

        [ObservableProperty]
        private double _left = 110.0;

        [ObservableProperty]
        private double _top = 200.0;

        [ObservableProperty]
        private int _index = -1;

        [ObservableProperty]
        private CatanOrientation _orientation = CatanOrientation.FaceUp;
        [JsonIgnore]
        public static TileViewModel Default { get; } = new(TileModel.Default, BoardLayout.Default);
    }
}
