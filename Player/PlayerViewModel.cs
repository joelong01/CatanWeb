using System.Diagnostics;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
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
        [JsonIgnore]
        [ObservableProperty]
        private PlayerModel _player = PlayerModel.Default;

        [ObservableProperty]
        private ResourcesViewModel _resourcesThisTurn = new(GameViewModelStatics.PlayerTrackResourceList);

        [ObservableProperty]
        private ResourcesViewModel _resourcesThisGame = new(GameViewModelStatics.PlayerTrackResourceList);

        public PlayerViewModel() : this("Nameless", Colors.White, Colors.HotPink) { 
        
            
        }


        [JsonIgnore]
        public static PlayerViewModel Default { get; } = new();

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

        public Brush GetBrush(Color color, bool foreground)
        {
            if (foreground)
                return BrushCache.GetSolidColorBrush(color);

            else
                return BrushCache.GetGradientBrush(color, Colors.Black);
        }

        public Brush ForegroundBrush => GetBrush(this.Foreground, true);
        public Brush BackgroundBrush => GetBrush(this.Background, false);

        [RelayCommand]
        private void PurchaseEntitlement(Entitlement entitlement)
        {
            Debug.Assert(false);
        }


    }
}
