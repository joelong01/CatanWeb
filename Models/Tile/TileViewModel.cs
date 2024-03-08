using System.ComponentModel;
namespace Catan3.Models
{
    public partial class TileViewModel : INotifyPropertyChanged
    {
        
        public void Init()
        {
            
            if (Layout is not null && Layout is RegularBoardLayout rbl)
            {
                rbl.PropertyChanged += Layout_PropertyChanged;
              
            }
           
            UpdateLayout();
        }
        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not null && sender is RegularBoardLayout layout)
            {
               
               // this.TraceMessage($"{e.PropertyName} changed for tile {Tile.TileKey}");
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
            if (Layout != null)
            {
                Left = Layout.Left(Tile.TileKey);
                Top = Layout.Top(Tile.TileKey);
            }
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
