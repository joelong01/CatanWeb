using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;

namespace Catan3.Models
{



    //
    //  this has all the data about the player that the service doesn't care about
    //  e.g. how to display information about the player -- colors, picutre, etc.
    public partial class PlayerViewModel : ObservableRecipient
    {


        [ObservableProperty]
        private string _id = string.Empty;

        [ObservableProperty]
        private string _name = "Nameless";

        [ObservableProperty]
        private PlayerColorViewModel _playerColors;


        [ObservableProperty]
        private string _imageUri = "ms-appx:///Assets/guest.jpg";

        [JsonIgnore]
        [ObservableProperty]
        private WriteableBitmap _cropperImageSource = new (100, 100);

        [JsonIgnore]
        [ObservableProperty]
        BitmapImage _imageSource;


        //  used in the edit player UI
        [JsonIgnore]
        [ObservableProperty]
        private bool _selected = false;

        [JsonIgnore]
        [ObservableProperty]
        private PlayerModel _player = PlayerModel.Default;

        [ObservableProperty]
        private ResourcesViewModel _resourcesThisTurn = new(GameViewModelStatics.PlayerTrackResourceList);

        [ObservableProperty]
        private ResourcesViewModel _resourcesThisGame = new(GameViewModelStatics.PlayerTrackResourceList);

        [ObservableProperty]
        private ObservableCollection<PlayerStatsViewModel> _playerStats = [];

        public Dictionary<StatName, PlayerStatsViewModel> StatDictionary { get; } = [];

        public PlayerViewModel() : this("Nameless", "ms-appx:///Assets/guest.jpg", new PlayerColorViewModel(Colors.White, Colors.HotPink, Colors.HotPink))
        {
            ImageSource = new BitmapImage(new System.Uri(ImageUri));
            CropperImageSource = ConvertToWriteableBitmap(ImageSource).GetAwaiter().GetResult();

        }

        partial void OnImageUriChanging(string? oldValue, string newValue)
        {
            ImageSource = new BitmapImage(new System.Uri(newValue));
            this.TraceMessage($"{ImageSource.PixelHeight}");
            CropperImageSource = ConvertToWriteableBitmap(ImageSource).GetAwaiter().GetResult();
        }



        private async Task<WriteableBitmap> ConvertToWriteableBitmap(BitmapImage bitmapImage)
        {
            if (bitmapImage.PixelHeight * bitmapImage.PixelWidth == 0) return new WriteableBitmap(100, 100);
            // Create a WriteableBitmap with the same dimensions as the BitmapImage
            WriteableBitmap writeableBitmap = new WriteableBitmap(bitmapImage.PixelWidth, bitmapImage.PixelHeight);

            using (Stream stream = writeableBitmap.PixelBuffer.AsStream())
            {
                // Retrieve the pixels from the BitmapImage
                byte[] pixels = await GetPixelsFromBitmapImage(bitmapImage);
                // Write the pixels into the WriteableBitmap
                await stream.WriteAsync(pixels, 0, pixels.Length);
            }

            return writeableBitmap;
        }

        private async Task<byte[]> GetPixelsFromBitmapImage(BitmapImage bitmapImage)
        {
            WriteableBitmap tempBitmap = new WriteableBitmap(bitmapImage.PixelWidth, bitmapImage.PixelHeight);
            using (IRandomAccessStream stream = new InMemoryRandomAccessStream())
            {
                await tempBitmap.SetSourceAsync(stream);
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var pixelData = await decoder.GetPixelDataAsync();
                return pixelData.DetachPixelData();
            }
        }


        [JsonIgnore]
        public static PlayerViewModel Default { get; } = new();

        public PlayerViewModel(string name, string imageUri, PlayerColorViewModel playerColors)
        {
            Name = name;
            Id = name + "-0001";

            //
            //  each view model needs its own instance of the stats - we put into a dictionary
            //  to make it easy to update them.
            foreach (var stat in PlayerStatsViewModel.StatsTemplate)
            {

                StatDictionary[stat.Name] = new PlayerStatsViewModel(stat, playerColors);

            }

            //
            //  the list of stats to bind to
            PlayerStats.AddRange([.. StatDictionary.Values.ToList()]);


            //
            //  set these last so that the PlayerStats get their colors
            this.PlayerColors = playerColors;

            ImageUri = imageUri;
            ImageSource = new BitmapImage(new System.Uri(ImageUri));

            //
            // subscribe to color changes to notify the rest of the models
            playerColors.PropertyChanged += PlayerColors_PropertyChanged;
        }


        private void PlayerColors_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "BackgroundBrush":
                case "ForegroundBrush":
                    Messenger.Send(new PlayerColorChanged(this));
                    break;
                default:
                    break;

            }


        }

        /// <summary>
        ///     the MVVM notification when the model gets updated -- we set the per person data and update the stats
        /// </summary>
        /// <param name="value"></param>
        partial void OnPlayerChanged(PlayerModel value)
        {
            ResourcesThisTurn.ResourceModel = value.ResourcesThisTurn;
            ResourcesThisGame.ResourceModel = value.ResourcesThisGame;


            StatDictionary[StatName.Score].Count = value.Score;
            StatDictionary[StatName.Score].Highlighted = value.HighestScore;
            StatDictionary[StatName.RoadsPlayed].Count = value.SpentEntitlementsThisGame.Count(e => e == Entitlement.Road);
            StatDictionary[StatName.CitiesPlayed].Count = value.SpentEntitlementsThisGame.Count(e => e == Entitlement.City);
            StatDictionary[StatName.SettlementsPlayed].Count = value.SpentEntitlementsThisGame.Count(e => e == Entitlement.Settlement);
            StatDictionary[StatName.SoldierPlayed].Count = value.SpentEntitlementsThisGame.Count(e => e == Entitlement.Soldier);
            StatDictionary[StatName.SoldierPlayed].Highlighted = value.LargestArmy;
            StatDictionary[StatName.ResourcesLostToRobber].Count = value.ResourcesThisGame.Robber;
            StatDictionary[StatName.TimesTargetted].Count = value.TimesTargeted;
            StatDictionary[StatName.TotalResources].Count = value.ResourcesThisGame.Count;
            StatDictionary[StatName.GoodRolls].Count = value.GoodRolls;
            StatDictionary[StatName.BadRolls].Count = value.BadRolls;
            StatDictionary[StatName.Stars].Count = value.Stars;
            StatDictionary[StatName.LongestRoad].Count = value.LongestRoad;
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

    }

    public enum ColorName { PrimaryBackground, SecondaryBackground, Foreground }



    /// <summary>
    ///     this keeps the player's colors/brushes straight.
    ///     This is serializable
    /// </summary>
    public partial class PlayerColorViewModel : ObservableObject
    {
        public PlayerColorViewModel(Color foreground, Color primary, Color secondary)
        {
            _primaryBackground = primary;
            _secondaryBackground = secondary;
            _foreground = foreground;

            OnPrimaryBackgroundChanged(primary);
            OnSecondaryBackgroundChanged(secondary);
            OnForegroundChanged(foreground);
        }

        [ObservableProperty]
        private Color _primaryBackground;

        [ObservableProperty]
        private Color _secondaryBackground;


        [ObservableProperty]
        private Color _foreground;

        [JsonIgnore]
        [ObservableProperty]
        private Brush _foregroundBrush = BrushCache.GetSolidColorBrush(Colors.White);

        [JsonIgnore]
        [ObservableProperty]
        private Brush _backgroundBrush = BrushCache.GetSolidColorBrush(Colors.Black);

        partial void OnPrimaryBackgroundChanged(Color value)
        {
            BackgroundBrush = BrushCache.GetGradientBrush(value, SecondaryBackground);

        }

        partial void OnSecondaryBackgroundChanged(Color value)
        {
            BackgroundBrush = BrushCache.GetGradientBrush(PrimaryBackground, value);
        }

        partial void OnForegroundChanged(Color value)
        {
            ForegroundBrush = BrushCache.GetSolidColorBrush(value);
        }
    }

}
