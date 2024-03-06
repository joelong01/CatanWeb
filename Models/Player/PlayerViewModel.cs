
using System.ComponentModel;
using Windows.UI;
namespace Catan3.Models
{
    //
    //  this has all the data about the player that the service doesn't care about
    //  e.g. how to display information about the player -- colors, picutre, etc.
    public partial class PlayerViewModel : INotifyPropertyChanged
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; }
        public Color Foreground { get; set; }
        public Color Background { get; set; }
        public string ImageFileName { get; set; } = "ms-appx:///Assets/guest.jpg";
        public PlayerModel Player { get; set; } = PlayerModel.Default;
        public PlayerViewModel(string name,  Color foreground, Color background)
        {
            Foreground = foreground;
            Background = background;
            Name = name;
           
        }
        public override string ToString()
        {
            return $"{Name}";
        }
    }
}
