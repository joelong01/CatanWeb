using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Catan3.Controls
{
    /// <summary>
    /// Interaction logic for TileCtrl.xaml
    /// </summary>
    public partial class TileCtrl : UserControl
    {
        public TileCtrl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty TileViewModelProperty = DependencyProperty.Register("TileViewModel", typeof(TileViewModel), typeof(TileCtrl), new PropertyMetadata(null, TileViewModelChanged));
        public TileViewModel TileViewModel
        {
            get => ( TileViewModel )GetValue(TileViewModelProperty);
            set => SetValue(TileViewModelProperty, value);
        }
        private static void TileViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as TileCtrl;
            var depPropValue = (TileViewModel)e.NewValue;
            depPropClass?.SetTileViewModel(depPropValue);
        }
        private void SetTileViewModel(TileViewModel tileViewModel)
        {
            this.DataContext = tileViewModel;
        }

        private Visibility PipsVisibility(int tileNumber, int pipIndex)
        {
            Visibility visibility = Visibility.Collapsed;
            switch (tileNumber)
            {
                case 2:
                case 12:
                    if (pipIndex < 1) visibility = Visibility.Visible;
                    break;
                case 3:
                case 11:
                    if (pipIndex < 2) visibility = Visibility.Visible;
                    break;
                case 4:
                case 10:
                    if (pipIndex < 3) visibility = Visibility.Visible;
                    break;
                case 5:
                case 9:
                    if (pipIndex < 4) visibility = Visibility.Visible;
                    break;
                case 6:
                case 8:
                    if (pipIndex < 5) visibility = Visibility.Visible;
                    break;
                case 7:
                    return Visibility.Collapsed;
            }
            return visibility;
        }

        public SolidColorBrush GetPipsForeground(int number)
        {
            if (number == 6 || number == 8)
            {
                return StaticBrushes.RedBrush;
            }
            return StaticBrushes.WhiteBrush;
        }

    }
}
