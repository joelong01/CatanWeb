using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3.Controls
{
    public sealed partial class PlayerStatsCtrl : UserControl
    {
        public PlayerStatsCtrl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(PlayerStatsViewModel), typeof(PlayerStatsCtrl), new PropertyMetadata(null));
        public PlayerStatsViewModel ViewModel
        {
            get => ( PlayerStatsViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
    }
}
