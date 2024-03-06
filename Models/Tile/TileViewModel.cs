using System.ComponentModel;
namespace Catan3.Models
{
    public partial class TileViewModel : INotifyPropertyChanged
    {
        // Fody will weave property change notifications into these properties.
        public double Left { get; private set; } = 110.0;
        public double Top { get; private set; } = 200.0;
        public TileModel Tile { get; private set; }
        public IBoardLayout Layout { get; set; }
        public TileViewModel(TileModel tile, IBoardLayout layout)
        {
            Tile = tile;
            layout.PropertyChanged += Layout_PropertyChanged;
            Layout = layout;
            UpdateLayout();
        }
        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is IBoardLayout layout)
            {
               
              //  this.TraceMessage($"{e.PropertyName} changed for tile {Tile.TileKey}");
                Layout = layout;
                UpdateLayout();
                if (e.PropertyName == nameof(IBoardLayout.HexSize))
                {
                    OnPropertyChanged(nameof(Layout.TileHexPoints)); // Notify the UI to reevaluate this path
                }
            }
        }
        private void UpdateLayout()
        {
            Left = Layout.Left(Tile.TileKey);
            Top = Layout.Top(Tile.TileKey);
        }
        public override string ToString()
        {
            return Tile.ToString();
        }
        public void TraceIfFirst()
        {
            if (this.Tile.TileKey.Q == 0 && this.Tile.TileKey.R == 0 && this.Tile.TileKey.S == 0)
            {
                this.TraceMessage($"[{Tile}]:[Left={Left}][Top={Top}]");
            }
        }
    }
}
