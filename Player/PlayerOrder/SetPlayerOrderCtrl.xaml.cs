using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Controls

{
    public sealed partial class SetPlayerOrderCtrl : UserControl
    {
        public SetPlayerOrderCtrl(IList<PlayerViewModel> players)
        {
            ViewModel = new PlayerOrderCollection(players);
            this.InitializeComponent();
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(PlayerOrderCollection), typeof(SetPlayerOrderCtrl), new PropertyMetadata(null));
        public PlayerOrderCollection ViewModel
        {
            get => ( PlayerOrderCollection )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private void ListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            var players = ViewModel.Players;

            // Create a new list based on the current order of items in the ListView
            var reorderedPlayers = sender.Items.Cast<PlayerOrderModel>().ToList();

            // Update the ObservableCollection with the new order
            players.Clear();
            foreach (var player in reorderedPlayers)
            {
                players.Add(player);
            }
        }

        private void GoFirstClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && ((Button)sender).Tag is PlayerOrderModel model)
            {
                ViewModel.GoFirst(model);
            }
          
        }
    }
}
