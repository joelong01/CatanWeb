
using System.ComponentModel;
using Microsoft.UI;
using Windows.UI;

namespace Catan3.Models
{
    //
    //  this has all the data about the player that the service doesn't care about
    //  e.g. how to display information about the player -- colors, picutre, etc.
    public partial class PlayerViewModel : INotifyPropertyChanged
    {
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
