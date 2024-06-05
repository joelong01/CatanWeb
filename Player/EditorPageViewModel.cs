using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Catan3.Models
{
    /// <summary>
    ///     this class drives the UI binding of a ListView that can be selected to update a players color
    /// </summary>
    /// <param name="name"></param>
    /// <param name="c"></param>
    public partial class EditPlayerColors(ColorName name, Color background, Color foreground) : ObservableObject
    {
        [ObservableProperty]
        ColorName _colorName = name;
        [ObservableProperty]
        Color _background = background;
        [ObservableProperty]
        Color _foreground = foreground;

        [ObservableProperty]
        bool _selected = false;

        public Brush GetBrush(Color color)
        {
            return BrushCache.GetSolidColorBrush(color);
        }

        public string DisplayName(ColorName name)
        {
            switch (name)
            {
                case ColorName.PrimaryBackground:
                    return "Primary Background";
                case ColorName.SecondaryBackground:
                    return "Secondary Background";
                case ColorName.Foreground:
                    return "Foreground";
                default:
                    throw new System.Exception("You forgot to update here when you added a new color");

            }
        }
    }


    public partial class EditPlayerViewModel : ObservableObject
    {

        [ObservableProperty]
        private EditPlayerColors _currentColorSetting;

        [ObservableProperty]
        private ObservableCollection<PlayerViewModel> _players;
        [ObservableProperty]
        ObservableCollection<EditPlayerColors> _editPlayerColors;

        [ObservableProperty]
        private PlayerViewModel _selectedPlayer;

        public EditPlayerViewModel(IList<PlayerViewModel> players)
        {
            Players = [.. players];

            EditPlayerColors =
            [
                    new (ColorName.PrimaryBackground, players[0].PlayerColors.PrimaryBackground, players[0].PlayerColors.Foreground),
                    new (ColorName.SecondaryBackground, players[0].PlayerColors.SecondaryBackground, players[0].PlayerColors.Foreground),
                    new (ColorName.Foreground, players[0].PlayerColors.Foreground, players[0].PlayerColors.Foreground),

             ];


            CurrentColorSetting = EditPlayerColors[0];
            SelectedPlayer = players[0];
        }
        //
        //  update the Colors that the EditPlayer UI binds to when the selected player changes
        //  this is for the part of the UI that is used to pick which color is being modified
        //  Note that the last onw ( EditPlayerColors[2]) is used to update the colors.
        //  in order to see the binding of the text, we make the Foreground the PrimaryBackground and
        //  the Background equal to the Foreground.
        partial void OnSelectedPlayerChanged(PlayerViewModel? oldValue, PlayerViewModel newValue)
        {

            EditPlayerColors[0].Background = newValue.PlayerColors.PrimaryBackground;
            EditPlayerColors[1].Background = newValue.PlayerColors.SecondaryBackground;
            EditPlayerColors[2].Background = newValue.PlayerColors.Foreground;

            EditPlayerColors[0].Foreground = newValue.PlayerColors.Foreground;
            EditPlayerColors[1].Foreground = newValue.PlayerColors.Foreground;
            EditPlayerColors[2].Foreground = newValue.PlayerColors.PrimaryBackground;

            if (oldValue is not null) oldValue.Selected = false;

            newValue.Selected = true;

        }

        partial void OnCurrentColorSettingChanged(EditPlayerColors? oldValue, EditPlayerColors newValue)
        {
            if (oldValue is not null) oldValue.Selected = false;

            newValue.Selected = true;
        }

        public Brush GetBrush(ColorName playerColor)
        {
            switch (playerColor)
            {
                case ColorName.PrimaryBackground:
                    return BrushCache.GetSolidColorBrush(SelectedPlayer.PlayerColors.PrimaryBackground);
                case ColorName.SecondaryBackground:
                    return BrushCache.GetSolidColorBrush(SelectedPlayer.PlayerColors.SecondaryBackground);
                case ColorName.Foreground:
                    return BrushCache.GetSolidColorBrush(SelectedPlayer.PlayerColors.Foreground);
                default:
                    throw new System.Exception("Forget to add to this switch?");
            }
        }

        public Color GetColor(ColorName playerColor, PlayerViewModel player)
        {
            switch (playerColor)
            {
                case ColorName.PrimaryBackground:
                    return player.PlayerColors.PrimaryBackground;

                case ColorName.SecondaryBackground:
                    return player.PlayerColors.SecondaryBackground;
                case ColorName.Foreground:
                    return player.PlayerColors.Foreground;
                default:
                    throw new System.Exception("Did you forget this switch when you added a configured color?");
            }
        }

        public void SetColor(ColorPicker sender, ColorChangedEventArgs args)
        {
           
            var newColor = args.NewColor;
            switch (CurrentColorSetting.ColorName)
            {
                case ColorName.PrimaryBackground:
                    SelectedPlayer.PlayerColors.PrimaryBackground = newColor;
                    break;
                case ColorName.SecondaryBackground:
                    SelectedPlayer.PlayerColors.SecondaryBackground = newColor;
                    break;
                case ColorName.Foreground:
                    SelectedPlayer.PlayerColors.Foreground = newColor;
                    break;
                default:
                    throw new System.Exception("Did you forget this switch when you added a configured color?");
            }
        }

        /// <summary>
        ///     Used by XAML binding to show a checkbox when the player is selected
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public Visibility ShowCheck(PlayerViewModel player)
        {
            return player.Id == SelectedPlayer.Id ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
