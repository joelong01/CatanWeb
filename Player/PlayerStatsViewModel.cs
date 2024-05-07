using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    /// <summary>
    ///     this is the class that we create an observable collection of instances that are then displayed 
    ///     in the PlayerCtrl that show statistics about the player
    /// </summary>
    public partial class PlayerStatsViewModel(string glyph) : ObservableObject
    {
   
        [ObservableProperty]
        private int _count = 0;
        [ObservableProperty]
        private string _glyph = glyph;


    }
}
