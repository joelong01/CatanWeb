using Catan3.Models;
using Catan3.Shared.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualBasic;
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
        public static readonly DependencyProperty GameStateProperty = DependencyProperty.Register("GameState", typeof(GameState), typeof(PlayerCtrl), new PropertyMetadata(GameState.Uninitialized, GameStateChanged));
        public GameState GameState
        {
            get => (GameState)GetValue(GameStateProperty);
            set => SetValue(GameStateProperty, value);
        }
        private static void GameStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as PlayerCtrl;
            var depPropValue = (GameState)e.NewValue;
            depPropClass?.SetGameState(depPropValue);
        }
        private void SetGameState(GameState value)
        {
           
        }

        public PlayerViewModel PlayerViewModel
        {
            get => ( PlayerViewModel )GetValue(PlayerViewModelProperty);
            set => SetValue(PlayerViewModelProperty, value);
        }

        private Visibility SupplementalVisibility(GameState state, bool PartipatingInSupplemental)
        {
            if (state != GameState.Supplemental) return Visibility.Visible;

            var ret = PartipatingInSupplemental ? Visibility.Visible : Visibility.Collapsed;
          //  this.TraceMessage($"{PlayerViewModel.Player} Suppl={PlayerViewModel.PartipatingInSupplemental} SupplementalVisibility={ret}");
            return ret;

        }



    }
}
