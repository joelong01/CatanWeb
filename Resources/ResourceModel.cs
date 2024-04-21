using System.Text.Json.Serialization;
using Catan3.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan10.Models
{
    public partial class ResourcesModel : ObservableObject
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
        }

        [JsonIgnore]
        public int Count => Wheat + Wood + Brick + Ore + Sheep + GoldMine + Cloth + Coin + Paper + VictoryPoint + Politics + Science + Trade + AnyDevCard;


        public override string ToString()
        {
            return $"[Count={Count}][Ore={Ore}][Brick={Brick}][Wheat={Wheat}][Wood={Wood}][Sheep={Sheep}][Gold={GoldMine}][Coin={Coin}][Cloth={Cloth}][Paper={Paper}]";
        }

        public int CountForResource(ResourceType resourceCardType)
        {
            int count = 0;
            switch (resourceCardType)
            {
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
