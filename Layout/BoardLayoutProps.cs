
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    /// Represents the layout of the game board, including tile offsets, board dimensions, hex sizes, and other layout properties.
    /// </summary>
    public partial class BoardLayout : ObservableObject
    {
        /// <summary>
        /// Gets or sets the vertical offset for tiles.
        /// </summary>
        [ObservableProperty]
        public partial double TileYOffset { get; set; } = 0;

        /// <summary>
        /// Gets or sets the horizontal offset for tiles.
        /// </summary>
        [ObservableProperty]
        public partial double TileXOffset { get; set; } = 0;

        /// <summary>
        /// Gets or sets the width of the board. Notifies property changes for BoardHeight.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BoardHeight))]
        public partial double BoardWidth { get; set; } = 500;

        /// <summary>
        /// Gets or sets the height of the board. Notifies property changes for BoardWidth.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BoardWidth))]
        public partial double BoardHeight { get; set; } = 500;

        /// <summary>
        /// Gets or sets the size of the outer hexagon. Notifies property changes for multiple properties.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ControlWidth))]
        [NotifyPropertyChangedFor(nameof(ControlHeight))]
        [NotifyPropertyChangedFor(nameof(BoardWidth))]
        [NotifyPropertyChangedFor(nameof(BoardHeight))]
        [NotifyPropertyChangedFor(nameof(InnerHexPoints))]
        [NotifyPropertyChangedFor(nameof(InnerHexSize))]
        [NotifyPropertyChangedFor(nameof(OuterHexPoints))]
        [NotifyPropertyChangedFor(nameof(PointyHexPoints))]
        public partial double OuterHexSize { get; set; } = 100.0;

        /// <summary>
        /// Gets or sets the stroke thickness of the inner hexagon. Notifies property changes for InnerHexPoints and InnerHexSize.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InnerHexPoints))]
        [NotifyPropertyChangedFor(nameof(InnerHexSize))]
        public partial double InnerHexStrokeThickness { get; set; } = 16.0;

        /// <summary>
        /// Gets or sets the stroke thickness of the outer hexagon. Notifies property changes for OuterHexPoints and OuterHexSize.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(OuterHexPoints))]
        [NotifyPropertyChangedFor(nameof(OuterHexSize))]
        public partial double OuterHexStrokeThickness { get; set; } = 0.0;

        /// <summary>
        /// Gets or sets the gap between tiles. Notifies property changes for InnerHexPoints and InnerHexSize.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(InnerHexPoints))]
        [NotifyPropertyChangedFor(nameof(InnerHexSize))]
        public partial double TileGap { get; set; } = 2;

        /// <summary>
        /// Gets or sets the margin of the game board. Notifies property changes for BoardWidth and BoardHeight.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BoardWidth))]
        [NotifyPropertyChangedFor(nameof(BoardHeight))]
        public partial double GameMargin { get; set; } = 7.0;

        /// <summary>
        /// Gets or sets the size of the buildings. Notifies property changes for BoardWidth, BoardHeight, and OuterHexPoints.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BoardWidth))]
        [NotifyPropertyChangedFor(nameof(BoardHeight))]
        [NotifyPropertyChangedFor(nameof(OuterHexPoints))]
        public partial double BuildingSize { get; set; } = 40;

        /// <summary>
        /// Gets or sets the stroke thickness of the roads.
        /// </summary>
        [ObservableProperty]
        public partial double RoadStrokeThickness { get; set; } = 2.0;

        /// <summary>
        /// Gets or sets the stroke width of the layout guide.
        /// </summary>
        [ObservableProperty]
        public partial double LayoutGuideStrokeWidth { get; set; } = 0.0;
    }
}
