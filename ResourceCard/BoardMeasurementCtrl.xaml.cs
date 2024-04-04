using System.Collections.ObjectModel;
using Catan3.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Controls
{
    public sealed partial class BoardMeasurementCtrl : UserControl
    {

        ObservableCollection<ResourceCardModel> ResourceCards { get; set; } = [];

        public BoardMeasurementCtrl()
        {
            ResourceCardType[] resources = [ ResourceCardType.Sheep, ResourceCardType.Wheat, ResourceCardType.Wood, ResourceCardType.Brick, ResourceCardType.Ore];
            foreach (var resource in resources)
            {
                ResourceCards.Add(new ResourceCardModel()
                {
                    ResourceType = resource,
                    Orientation = CatanOrientation.FaceUp,
                    CountVisibility = Visibility.Visible,
                    Count = 0

                });
            }


            this.InitializeComponent();
        }
        public static readonly DependencyProperty GameModelProperty = DependencyProperty.Register("GameModel", typeof(GameModel), typeof(BoardMeasurementCtrl), new PropertyMetadata(null, GameModelChanged));
        public GameModel GameModel
        {
            get => ( GameModel )GetValue(GameModelProperty);
            set => SetValue(GameModelProperty, value);
        }
        private static void GameModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as BoardMeasurementCtrl;
            var depPropValue = (GameModel)e.NewValue;
            depPropClass?.SetGameModel(depPropValue);
        }
        private void SetGameModel(GameModel value)
        {
            if (value is null) return;
            this.DataContext = value;

            foreach (var card in ResourceCards)
            {
                card.Count = value.StarCount(CardTypeToTileType(card.ResourceType));
            }
        }

        private ResourceTileType CardTypeToTileType(ResourceCardType cardType)
        {
            switch (cardType)
            {
                case ResourceCardType.Sheep:
                    return ResourceTileType.Sheep;
                case ResourceCardType.Wood:
                    return ResourceTileType.Wood;
                case ResourceCardType.Ore:
                    return ResourceTileType.Ore;
                case ResourceCardType.Wheat:
                    return ResourceTileType.Wheat;
                case ResourceCardType.Brick:
                    return ResourceTileType.Brick;
                case ResourceCardType.GoldMine:
                    return ResourceTileType.GoldMine;
                case ResourceCardType.Desert:
                    return ResourceTileType.Desert;

                case ResourceCardType.Back:
                    return ResourceTileType.Back;
                case ResourceCardType.None:
                    return ResourceTileType.None;
                case ResourceCardType.Sea:
                case ResourceCardType.Coin:
                case ResourceCardType.Cloth:
                case ResourceCardType.Paper:
                case ResourceCardType.Politics:
                case ResourceCardType.Trade:
                case ResourceCardType.Science:
                case ResourceCardType.AnyDevCard:
                case ResourceCardType.VictoryPoint:
                case ResourceCardType.Invasion:
                default:
                    return ResourceTileType.None;
            }
        }

        private void OnFlip(object sender, RoutedEventArgs e)
        {
            foreach (var card in ResourceCards)
            {
                if (card.Orientation == CatanOrientation.FaceUp)
                {
                    card.Orientation = CatanOrientation.FaceDown;
                }
                else
                {
                    card.Orientation = CatanOrientation.FaceUp;
                }
            }
        }

        public string BIND_StarCount(int stars, GameModel gameModel)
        {
            int count = 0;
            foreach (var building in gameModel.Buildings)
            {
                var tiles = gameModel.TilesForBuildings(building.BuildingKey);
                if (tiles.Stars() == stars) count++;

            }
            return count.ToString();
        }
    }
}
