using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan10.Models
{
    public partial class TradeResourcesModel : ObservableObject
    {
        [ObservableProperty]
        private int brick;

        [ObservableProperty]
        private int goldMine;

        [ObservableProperty]
        private int ore;

        [ObservableProperty]
        private int sheep;

        [ObservableProperty]
        private int wheat;

        [ObservableProperty]
        private int wood;

        [ObservableProperty]
        private int paper;

        [ObservableProperty]
        private int cloth;

        [ObservableProperty]
        private int coin;

        [ObservableProperty]
        private int politics;

        [ObservableProperty]
        private int trade;

        [ObservableProperty]
        private int science;

        [ObservableProperty]
        private int victoryPoint;

        [ObservableProperty]
        private int anyDevCard;

        public TradeResourcesModel() { }

        public TradeResourcesModel(TradeResourcesModel tradeResources)
        {
            Wheat = tradeResources.Wheat;
            Wood = tradeResources.Wood;
            Brick = tradeResources.Brick;
            Ore = tradeResources.Ore;
            Sheep = tradeResources.Sheep;
            GoldMine = tradeResources.GoldMine;
            Cloth = tradeResources.Cloth;
            Coin = tradeResources.Coin;
            Paper = tradeResources.Paper;

            Politics = tradeResources.Politics;
            Trade = tradeResources.Trade;
            Science = tradeResources.Science;
            VictoryPoint = tradeResources.VictoryPoint;
            AnyDevCard = tradeResources.AnyDevCard;
        }
    }
}
