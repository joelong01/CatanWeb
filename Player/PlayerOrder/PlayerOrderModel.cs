using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Catan3.Models
{
    public partial class PlayerOrderCollection : ObservableRecipient
    {
        [ObservableProperty]
        private ObservableCollection<PlayerOrderModel> _players = [];

        public PlayerOrderCollection(IList<PlayerViewModel> players)
        {
            foreach (var player in players)
            {
                var data = new PlayerOrderModel(player);
                Players.Add(data);
            }
        }

        internal void GoFirst(PlayerOrderModel model)
        {
            this.TraceMessage($"{model.Name} goes first ");
        }
    }
    public partial class PlayerOrderModel : ObservableRecipient
    {
        [ObservableProperty]
        private string _croppedImageUri ="ms-appx:///Assets/guest.jpg";
        [ObservableProperty]
        private string _name = "Nameless";
        [ObservableProperty]
        private PlayerColorViewModel _playerColors;
        [ObservableProperty]
        private string _id;

        public PlayerOrderModel(PlayerViewModel player)
        {
            CroppedImageUri = player.CroppedImageUri;
            Name = player.Name;
            _playerColors = player.PlayerColors;
            Id = player.Id;
        }

      
    }
}
