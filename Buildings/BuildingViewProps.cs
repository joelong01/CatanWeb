using System.ComponentModel;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

namespace Catan3.Models
{
    public partial class BuildingViewModel : ObservableObject
    {
        [ObservableProperty]
        private BuildingModel _building;

        [ObservableProperty]
        private BoardLayout _layout;

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



    }
}

