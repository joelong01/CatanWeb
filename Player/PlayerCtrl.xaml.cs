using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3.Controls
{
    public class StatTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? NormalStatTemplate { get; set; }
        public DataTemplate? ScoreStatTemplate { get; set; }

        protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is PlayerStatsViewModel viewModel)
            {
                return viewModel.Name switch
                {
                    StatName.Score => ScoreStatTemplate,
                    _ => NormalStatTemplate,
                };
            }

            return base.SelectTemplateCore(item, container);
        }
    }

    public sealed partial class PlayerCtrl : UserControl
    {
        public PlayerCtrl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty PlayerViewModelProperty = DependencyProperty.Register("PlayerViewModel", typeof(PlayerViewModel), typeof(PlayerCtrl), new PropertyMetadata(null));
        public PlayerViewModel PlayerViewModel
        {
            get => ( PlayerViewModel )GetValue(PlayerViewModelProperty);
            set => SetValue(PlayerViewModelProperty, value);
        }
       
    }
}
