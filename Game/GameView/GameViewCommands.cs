

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.Windows.AppLifecycle;

namespace Catan3.Models
{

    public partial class GameViewModel
    {
        public GameViewModel()
        {

        }

        [RelayCommand]
        private void Shuffle()
        {
            if (GameModel is null) return;

            GameModel.Shuffle();
            SetStars();
            ShowStarValues(ShownStars);
        }

        
        [RelayCommand]
        private void ShowStarValues(int stars)
        {
            if (GameModel is null || CurrentPlayer is null) return;


            foreach (var building in Buildings)
            {
                if (building.Building.Owner != null) continue;
                int buildingStars = GameModel.BuildingStars(building.Building.BuildingKey);
                if (buildingStars >= stars)
                {

                    building.Background = BrushCache.GetGradientBrush(CurrentPlayer.Background, Colors.Black);
                    building.Foreground = BrushCache.GetSolidColorBrush(CurrentPlayer.Foreground);
                    building.Building.BuildingState = BuildingState.Stars;

                }
                else
                {
                    building.Background = BrushCache.GetSolidColorBrush(Colors.Transparent);
                    building.Foreground = BrushCache.GetSolidColorBrush(Colors.Transparent);
                    building.Building.BuildingState = BuildingState.Empty;
                }

            }

        }
    }
}
