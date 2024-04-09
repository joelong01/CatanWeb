using System.ComponentModel;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System.Text.Json.Serialization;
using System;

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
        private int _stars = 0;

        [ObservableProperty]
        private Brush? _background = BrushCache.GetSolidColorBrush(Colors.Transparent);

        [ObservableProperty]
        private Brush? _foreground = BrushCache.GetSolidColorBrush(Colors.Transparent);

        [ObservableProperty]
        private string _stateGlyph = string.Empty;

        [JsonIgnore]
        private PlayerViewModel CurrentPlayer { get; set; } = PlayerViewModel.Default;

       
    }
}

