
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class BoardLayout : ObservableObject
    {
        [ObservableProperty]
        private double _tileYOffset = 0;

        [ObservableProperty]
        private double _tileXOffset = 0;

        [ObservableProperty]

        [NotifyPropertyChangedFor(nameof(BoardHeight))]
        private double _boardWidth = 500;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BoardWidth))]
        private double _boardHeight = 500;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ControlWidth))]
        [NotifyPropertyChangedFor(nameof(ControlHeight))]
        [NotifyPropertyChangedFor(nameof(BoardWidth))]
        [NotifyPropertyChangedFor(nameof(BoardHeight))]
        [NotifyPropertyChangedFor(nameof(InnerHexPoints))]
        [NotifyPropertyChangedFor(nameof(InnerHexSize))]
        [NotifyPropertyChangedFor(nameof(OuterHexPoints))]
        [NotifyPropertyChangedFor(nameof(PointyHexPoints))]
        private double _outerHexSize = 100.0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InnerHexPoints))]
        [NotifyPropertyChangedFor(nameof(InnerHexSize))]
        private double _innerHexStrokeThickness = 16.0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(OuterHexPoints))]
        [NotifyPropertyChangedFor(nameof(OuterHexSize))]
        private double _outerHexStrokeThickness = 0.0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InnerHexPoints))]
        [NotifyPropertyChangedFor(nameof(InnerHexSize))]
        private double _tileGap = 2;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BoardWidth))]
        [NotifyPropertyChangedFor(nameof(BoardHeight))]
        private double _gameMargin = 7.0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BoardWidth))]
        [NotifyPropertyChangedFor(nameof(BoardHeight))]
        [NotifyPropertyChangedFor(nameof(OuterHexPoints))]
        private double _buildingSize = 40;

        [ObservableProperty]
        private double _roadStrokeThickness = 2.0;

        [ObservableProperty]
        private double _layoutGuideStrokeWidth = 0.0;
    }

}
