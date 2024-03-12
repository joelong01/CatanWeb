using System.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    ///     this is the partial class to the template generated TileViewModel.  we subscribe to change events for Layout changes
    ///     and then update the layout calculations based on updates to the layoutproperties (Hex Size, Gap, Stroke, etc.)
    /// </summary>
    public partial class TileViewModel
    {
        /// <summary>
        ///     Init is called from the TD4 template generated TileViewModel.  We register for update notification so that we 
        ///     can update the Tile geormetry when any of the base measurements change.
        /// </summary>

        public void Init()
        {

            if (Layout is not null && Layout is BoardLayout rbl)
            {
                rbl.PropertyChanged += Layout_PropertyChanged;

            }

            UpdateLayout();
        }
        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not null && sender is BoardLayout layout)
            {

                // this.TraceMessage($"{e.PropertyName} changed for tile {Tile.TileKey}");
                Layout = layout;
                UpdateLayout();
                if (e.PropertyName == nameof(BoardLayout.OuterHexSize))
                {
                    OnPropertyChanged(nameof(Layout.InnerHexPoints)); // Notify the UI to reevaluate this path
                    OnPropertyChanged(nameof(Layout.OuterHexPoints)); // Notify the UI to reevaluate this path
                    OnPropertyChanged(nameof(Layout.ControlHeight));
                    OnPropertyChanged(nameof(Layout.ControlWidth));
                    
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
