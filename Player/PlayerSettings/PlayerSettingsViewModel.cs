using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Catan3.Services;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using System.Diagnostics;
using WinUIEx;
using Microsoft.UI;
using System.IO;
using System.Linq;
namespace Catan3.Models
{
    /// <summary>
    /// Represents the view model for editing player colors, including color name, background, foreground, and selection state.
    /// </summary>
    public partial class EditPlayerColors(ColorName name, Color background, Color foreground) : ObservableObject
    {
        /// <summary>
        /// Gets or sets the color name.
        /// </summary>
        [ObservableProperty]
        public partial ColorName ColorName { get; set; } = name;

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        [ObservableProperty]
        public partial Color Background { get; set; } = background;

        /// <summary>
        /// Gets or sets the foreground color.
        /// </summary>
        [ObservableProperty]
        public partial Color Foreground { get; set; } = foreground;

        /// <summary>
        /// Gets or sets a value indicating whether the color is selected.
        /// </summary>
        [ObservableProperty]
        public partial bool Selected { get; set; } = false;

        /// <summary>
        /// Gets a brush for the specified color.
        /// </summary>
        /// <param name="color">The color to convert to a brush.</param>
        /// <returns>A Brush representing the specified color.</returns>
        public Brush GetBrush(Color color)
        {
            return BrushCache.GetSolidColorBrush(color);
        }

        /// <summary>
        /// Gets the display name for the specified color name.
        /// </summary>
        /// <param name="name">The color name.</param>
        /// <returns>The display name for the color name.</returns>
        public string DisplayName(ColorName name)
        {
            return name switch
            {
                ColorName.PrimaryBackground => "Primary Background",
                ColorName.SecondaryBackground => "Secondary Background",
                ColorName.Foreground => "Foreground",
                _ => throw new Exception("You forgot to update here when you added a new color"),
            };
        }
    }
    /// <summary>
    /// Represents the view model for player settings, including current color setting, player colors, selected player, and image handling.
    /// </summary>
    public partial class PlayerSettingsViewModel : ObservableObject
    {
        /// <summary>
        /// Gets or sets the current color setting.
        /// </summary>
        [ObservableProperty]
        public partial EditPlayerColors CurrentColorSetting { get; set; }

        /// <summary>
        /// Gets or sets the collection of player colors for editing.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<EditPlayerColors> EditPlayerColors { get; set; } = [];

        /// <summary>
        /// Gets or sets the selected player.
        /// </summary>
        [ObservableProperty]
        public partial PlayerViewModel SelectedPlayer { get; set; }

        /// <summary>
        /// Gets or sets the original image.
        /// </summary>
        [ObservableProperty]
        public partial WriteableBitmap? OriginalImage { get; set; }

        /// <summary>
        /// Gets the dispatcher queue.
        /// </summary>
        private readonly DispatcherQueue _dispatcherQueue;

        /// <summary>
        /// Gets the parent window.
        /// </summary>
        private readonly WindowEx _parentWindow;

        /// <summary>
        /// Gets or sets the player database.
        /// </summary>
        [ObservableProperty]
        public partial IPlayerDatabase PlayerDatabase { get; set; }

        /// <summary>
        /// Initializes a new instance of the PlayerSettingsViewModel class with the specified parent window and player database.
        /// </summary>
        /// <param name="parent">The parent window.</param>
        /// <param name="playerDatabase">The player database.</param>
        public PlayerSettingsViewModel(WindowEx parent, IPlayerDatabase playerDatabase)
        {
            PlayerDatabase = playerDatabase;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _parentWindow = parent;

            EditPlayerColors = [
                new(ColorName.PrimaryBackground, PlayerDatabase.AllPlayers[0].PlayerColors.PrimaryBackground, PlayerDatabase.AllPlayers[0].PlayerColors.Foreground),
                    new(ColorName.SecondaryBackground, PlayerDatabase.AllPlayers[0].PlayerColors.SecondaryBackground, PlayerDatabase.AllPlayers[0].PlayerColors.Foreground),
                    new(ColorName.Foreground, PlayerDatabase.AllPlayers[0].PlayerColors.Foreground, PlayerDatabase.AllPlayers[0].PlayerColors.Foreground),
                ];
            CurrentColorSetting = EditPlayerColors[0];
            SelectedPlayer = PlayerDatabase.AllPlayers[0];
        }

        /// <summary>
        /// Adds a new player to the player database.
        /// </summary>
        [RelayCommand]
        public void AddPlayer()
        {
            PlayerDatabase.AddPlayer();
            SelectedPlayer = PlayerDatabase.AllPlayers.Last();
        }

        /// <summary>
        /// Deletes the selected player from the player database.
        /// </summary>
        [RelayCommand]
        public void DeletePlayer()
        {
            int index = PlayerDatabase.AllPlayers.IndexOf(SelectedPlayer);
            if (index < PlayerDatabase.AllPlayers.Count - 1)
                index++;
            else
                index--;
            PlayerDatabase.DeletePlayer(SelectedPlayer);
            SelectedPlayer = PlayerDatabase.AllPlayers[index];
        }

        /// <summary>
        /// Handles changes to the selected player.
        /// </summary>
        /// <param name="oldValue">The previous selected player.</param>
        /// <param name="newValue">The new selected player.</param>
        partial void OnSelectedPlayerChanged(PlayerViewModel oldValue, PlayerViewModel newValue)
        {
            if (newValue == null) return;
            EditPlayerColors[0].Background = newValue.PlayerColors.PrimaryBackground;
            EditPlayerColors[1].Background = newValue.PlayerColors.SecondaryBackground;
            EditPlayerColors[2].Background = newValue.PlayerColors.Foreground;
            EditPlayerColors[0].Foreground = newValue.PlayerColors.Foreground;
            EditPlayerColors[1].Foreground = newValue.PlayerColors.Foreground;
            EditPlayerColors[2].Foreground = newValue.PlayerColors.PrimaryBackground;
            LoadImageOnSelectedPlayerChanged(newValue.ImageUri);
            if (oldValue is not null) oldValue.Selected = false;
            newValue.Selected = true;
        }

        /// <summary>
        /// Loads the image for the selected player when the selected player changes.
        /// </summary>
        /// <param name="imageUri">The URI of the image to load.</param>
        private void LoadImageOnSelectedPlayerChanged(string imageUri)
        {
            Task.Run(async () =>
            {
                var bitmap = await LoadImageFromFilePathAsync(imageUri);
                // Update the ImageSource property on the UI thread
                _dispatcherQueue.TryEnqueue(() => OriginalImage = bitmap);
            });
        }

        /// <summary>
        /// Handles changes to the current color setting.
        /// </summary>
        /// <param name="oldValue">The previous color setting.</param>
        /// <param name="newValue">The new color setting.</param>
        partial void OnCurrentColorSettingChanged(EditPlayerColors oldValue, EditPlayerColors newValue)
        {
            if (oldValue is not null) oldValue.Selected = false;
            newValue.Selected = true;
        }

        /// <summary>
        /// Gets a brush for the specified player color.
        /// </summary>
        /// <param name="playerColor">The player color.</param>
        /// <returns>A Brush representing the specified player color.</returns>
        public Brush GetBrush(ColorName playerColor)
        {
            return playerColor switch
            {
                ColorName.PrimaryBackground => BrushCache.GetSolidColorBrush(SelectedPlayer.PlayerColors.PrimaryBackground),
                ColorName.SecondaryBackground => BrushCache.GetSolidColorBrush(SelectedPlayer.PlayerColors.SecondaryBackground),
                ColorName.Foreground => BrushCache.GetSolidColorBrush(SelectedPlayer.PlayerColors.Foreground),
                _ => throw new Exception("Forget to add to this switch?"),
            };
        }

        /// <summary>
        /// Gets the color for the specified player color and player.
        /// </summary>
        /// <param name="playerColor">The player color.</param>
        /// <param name="player">The player view model.</param>
        /// <returns>The Color representing the specified player color.</returns>
        public Color GetColor(ColorName playerColor, PlayerViewModel player)
        {
            return playerColor switch
            {
                ColorName.PrimaryBackground => player.PlayerColors.PrimaryBackground,
                ColorName.SecondaryBackground => player.PlayerColors.SecondaryBackground,
                ColorName.Foreground => player.PlayerColors.Foreground,
                _ => throw new Exception("Did you forget this switch when you added a configured color?"),
            };
        }

        /// <summary>
        /// Sets the color for the current color setting based on the color picker selection.
        /// </summary>
        /// <param name="sender">The color picker.</param>
        /// <param name="args">The color changed event arguments.</param>
        public void SetColor(ColorPicker sender, ColorChangedEventArgs args)
        {
            var newColor = args.NewColor;
            switch (CurrentColorSetting.ColorName)
            {
                case ColorName.PrimaryBackground:
                    SelectedPlayer.PlayerColors.PrimaryBackground = newColor;
                    EditPlayerColors[0].Background = newColor;
                    break;
                case ColorName.SecondaryBackground:
                    SelectedPlayer.PlayerColors.SecondaryBackground = newColor;
                    EditPlayerColors[1].Background = newColor;
                    break;
                case ColorName.Foreground:
                    SelectedPlayer.PlayerColors.Foreground = newColor;
                    EditPlayerColors[2].Background = newColor;
                    break;
                default:
                    throw new Exception("Did you forget this switch when you added a configured color?");
            }
        }

        /// <summary>
        /// Shows a checkbox when the player is selected.
        /// </summary>
        /// <param name="player">The player view model.</param>
        /// <returns>Visibility.Visible if the player is selected; otherwise, Visibility.Collapsed.</returns>
        public Visibility ShowCheck(PlayerViewModel player)
        {
            return player.Id == SelectedPlayer.Id ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Loads an image asynchronously.
        /// </summary>
        [RelayCommand]
        public async Task LoadImageAsync()
        {
            var filters = new List<string> { ".jpg", ".png" };
            var fileServiceHelper = new FileServiceHelper();
            StorageFile? file = await fileServiceHelper.GetStorageFileAsync(_parentWindow, filters);
            if (file is not null)
            {
                OriginalImage = await LoadImageFromFileAsync(file);
            }
        }

        /// <summary>
        /// Reloads the cropped image asynchronously.
        /// </summary>
        [RelayCommand]
        public async Task ReloadCroppedImage()
        {
            await LoadImageFromFilePathAsync(SelectedPlayer.CroppedImageUri);
        }

        /// <summary>
        /// Loads an image from the specified file path asynchronously.
        /// </summary>
        /// <param name="filePath">The file path of the image.</param>
        /// <returns>A WriteableBitmap representing the loaded image.</returns>
        private async Task<WriteableBitmap> LoadImageFromFilePathAsync(string filePath)
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            return await LoadImageFromFileAsync(file);
        }

        /// <summary>
        /// Loads an image from the specified storage file asynchronously.
        /// </summary>
        /// <param name="file">The storage file.</param>
        /// <returns>A WriteableBitmap representing the loaded image.</returns>
        private async Task<WriteableBitmap> LoadImageFromFileAsync(StorageFile file)
        {
            // Ensure the execution happens on the UI thread
            var tcs = new TaskCompletionSource<WriteableBitmap>();
            Debug.Assert(_dispatcherQueue is not null);
            _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
                    {
                        var bitmapImage = new BitmapImage();
                        await bitmapImage.SetSourceAsync(fileStream);
                        var result = new WriteableBitmap(bitmapImage.PixelWidth, bitmapImage.PixelHeight);
                        fileStream.Seek(0);
                        await result.SetSourceAsync(fileStream);
                        tcs.SetResult(result);
                    }
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            return await tcs.Task;
        }

        /// <summary>
        /// Saves the cropped image to the player database.
        /// </summary>
        /// <param name="memoryStream">The memory stream containing the cropped image.</param>
        internal async Task SaveCroppedImage(MemoryStream memoryStream)
        {
            if (PlayerDatabase is null) return;
            await PlayerDatabase.SaveCroppedImage(SelectedPlayer, memoryStream);
        }
    }
}
