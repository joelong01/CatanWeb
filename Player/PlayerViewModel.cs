
using System.ComponentModel;
using Windows.UI;
namespace Catan3.Models
{
    //
    //  this has all the data about the player that the service doesn't care about
    //  e.g. how to display information about the player -- colors, picutre, etc.
    public partial class PlayerViewModel(string name, Color foreground, Color background) : INotifyPropertyChanged
    {
        public override string ToString()
        {
            return $"{Name}";
        }
    }
}
