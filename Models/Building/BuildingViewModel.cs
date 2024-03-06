using System.ComponentModel;
using Catan3.Utility;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;


namespace Catan3.Models
{
    public partial class BuildingViewModel : INotifyPropertyChanged
    {
        public double Left { get; private set; }
        public double Top { get; private set; }
        public IBoardLayout Layout { get; set; }
        public BuildingModel Building { get; set; }
        public int Index { get; set; }
        private static int _index = 0;
        public int Pips { get; set; } = 0;
        public Brush Background { get; set; } = BrushCache.GetSolidColorBrush(Colors.Transparent);
        public Brush Foreground { get; set; } = BrushCache.GetSolidColorBrush(Colors.Transparent);
        public BuildingViewModel(BuildingModel building, IBoardLayout layout)
        {
            Building = building;
            building.PropertyChanged += Building_PropertyChanged;
            if (layout is not null && layout is RegularBoardLayout rbl)
            {
                rbl.PropertyChanged += Layout_PropertyChanged;
                Layout = layout;
            }
            else
            {
                Layout = RegularBoardLayout.Default;
            }
            UpdateLayout();
            Index = _index;
            _index++;
        }
        private void Building_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Manually raise PropertyChanged for dependent properties - this cannot be done via
            // Fody's [DependsOn] attribute because it is in a different class.
            switch (e.PropertyName)
            {
                case nameof(BuildingModel.BuildingState):
                    OnPropertyChanged(nameof(StateGlyph));
                    break;
                case nameof(BuildingModel.Owner):
                    {
                    }
                    break;
                default:
                    break;
            }
        }
        public string StateGlyph
        {
            get
            {
                return Building.BuildingState switch
                {
                    BuildingState.Empty => "",
                    BuildingState.Settlement => CatanFont.Gate,
                    BuildingState.City => CatanFont.City,
                    BuildingState.Pips => Pips.ToString() + " ",
                    BuildingState.Knight => CatanFont.Knight,
                    _ => "",
                };
            }
        }
        private string EmptyString(BuildingPosition position)
        {
            string s=$"";
            switch (position)
            {
                case BuildingPosition.Right:
                    return s + "R";
                case BuildingPosition.BottomRight:
                    return s + "BR";
                case BuildingPosition.BottomLeft:
                    return s + "BL";
                case BuildingPosition.Left:
                    return s + "L";
                case BuildingPosition.TopLeft:
                    return s + "TL";
                case BuildingPosition.TopRight:
                    return s + "TR";
                case BuildingPosition.None:
                    return s + "None";
                default:
                    return s + "?";
            }
        }
        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is RegularBoardLayout layout)
            {
                this.Layout = layout;
                UpdateLayout();
            }
        }
        private void UpdateLayout()
        {
            Top = GetTop(Building.BuildingKey);
            Left = GetLeft(Building.BuildingKey);
        }
        private double GetTop(BuildingKey key)
        {
            var top =  Layout.Top(key.TileKey);
            var center = Layout.BuildingHexPoints[(int)key.BuildingPosition];
            top += center.Y;
            top -= ( Layout.BuildingSize ) * 0.5;
            return top;
        }
        private double GetLeft(BuildingKey key)
        {
            var left =  Layout.Left(key.TileKey) ;
            var center = Layout.BuildingHexPoints[(int)key.BuildingPosition];
            left -= Layout.BuildingSize / 2.0;
            left += center.X;
            return left;
        }
        public override string ToString()
        {
            return Building.ToString();
        }
    }
}
