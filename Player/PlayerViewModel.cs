
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Windows.UI;

namespace Catan3.Models
{
    //
    //  this has all the data about the player that the service doesn't care about
    //  e.g. how to display information about the player -- colors, picutre, etc.
    public partial class PlayerViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = "Nameless";

        [ObservableProperty]
        private Color _foreground = Colors.White;

        [ObservableProperty]
        private Color _background = Colors.HotPink;

        [ObservableProperty]
        private string _imageFileName = "ms-appx:///Assets/guest.jpg";

        [ObservableProperty]
        private PlayerModel _player = PlayerModel.Default;
        public PlayerViewModel() : this("Nameless", Colors.White, Colors.HotPink) { }
        public PlayerViewModel(string name, Color foreground, Color background)
        {
            Name = name;
            Foreground = foreground;
            Background = background;
            Id = name + "-0001";
        }
        public override string ToString()
        {
            return $"{Name}";
        }
    }
}
