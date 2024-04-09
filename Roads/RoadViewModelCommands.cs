using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
namespace Catan3.Models
{
    public partial class RoadViewModel : ObservableRecipient
    {
        [RelayCommand]
        private void MouseEnter()
        {
            if (Road.Owner is null)
            {
                Background = BrushCache.GetGradientBrush(CurrentPlayer.Background, Colors.Black);
                Foreground = BrushCache.GetSolidColorBrush(CurrentPlayer.Foreground);
                Road.RoadState = RoadState.Highlighted;


            }
        }

        [RelayCommand]
        private void MouseExit()
        {
            if (Road.RoadState == RoadState.Highlighted)
            {
                Background = BrushCache.GetSolidColorBrush(Colors.Transparent);
                Foreground = BrushCache.GetSolidColorBrush(Colors.Transparent);
                Road.RoadState = RoadState.Unowned;
            }
        }

    }
}
