using System.Text.Json.Serialization;
using Catan3.Models;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan10.Models
{
    /// <summary>
    ///     Should contain all the resources used to track rolls.  if you add one, add it everywhere...
    /// </summary>
    public partial class ResourcesModel : ObservableObject
    {
        [ObservableProperty]
        private int _brick;
        [ObservableProperty]
        private int _goldMine;
        [ObservableProperty]
        private int _ore;
        [ObservableProperty]
        private int _sheep;
        [ObservableProperty]
        private int _wheat;
        [ObservableProperty]
        private int _wood;
        [ObservableProperty]
        private int _paper;
        [ObservableProperty]
        private int _cloth;
        [ObservableProperty]
        private int _coin;
        [ObservableProperty]
        private int _politics;
        [ObservableProperty]
        private int _trade;
        [ObservableProperty]
        private int _science;
        [ObservableProperty]
        private int _victoryPoint;
        [ObservableProperty]
        private int _anyDevCard;
        [ObservableProperty]
        private int _robber;
        public ResourcesModel() { }
        public ResourcesModel(ResourcesModel tradeResources)
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
            Robber = tradeResources.Robber;
        }
        [JsonIgnore]
        public int Count => Wheat + Wood + Brick + Ore + Sheep + GoldMine + Cloth + Coin + Paper + VictoryPoint + Politics + Science + Trade + AnyDevCard + Robber;
        public override string ToString()
        {
            return $"[Count={Count}][Robber={Robber}][Ore={Ore}][Brick={Brick}][Wheat={Wheat}][Wood={Wood}][Sheep={Sheep}][Gold={GoldMine}][Coin={Coin}][Cloth={Cloth}][Paper={Paper}]";
        }
        public int CountForResource(ResourceType resourceCardType)
        {
            int count = 0;
            switch (resourceCardType)
            {
                case ResourceType.Robber:
                    count = Robber;
                    break;
                case ResourceType.Sheep:
                    count = Sheep;
                    break;
                case ResourceType.Wood:
                    count = Wood;
                    break;
                case ResourceType.Ore:
                    count = Ore;
                    break;
                case ResourceType.Wheat:
                    count = Wheat;
                    break;
                case ResourceType.Brick:
                    count = Brick;
                    break;
                case ResourceType.GoldMine:
                    count = GoldMine;
                    break;
                case ResourceType.Coin:
                    count = Coin;
                    break;
                case ResourceType.Cloth:
                    count = Cloth;
                    break;
                case ResourceType.Paper:
                    count = Paper;
                    break;
                case ResourceType.Politics:
                    count = Politics;
                    break;
                case ResourceType.Trade:
                    count = Trade;
                    break;
                case ResourceType.Science:
                    count = Science;
                    break;
                case ResourceType.AnyDevCard:
                    count = AnyDevCard;
                    break;
                case ResourceType.VictoryPoint:
                    count = VictoryPoint;
                    break;
                default:
                    count = 0;
                    break;
            }
            return count;
        }
    }
}
