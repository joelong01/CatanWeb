using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Should contain all the resources used to track rolls. If you add one, add it everywhere...
    /// Supports both plain object usage (for JSON/API) and MVVM usage (for UI data binding).
    /// </summary>
    public partial class ResourcesModel : ObservableObject
    {
        [ObservableProperty]
        public partial int Brick { get; set; }

        [ObservableProperty]
        public partial int GoldMine { get; set; }
        [ObservableProperty]
        public partial int Ore { get; set; }

        [ObservableProperty]
        public partial int Sheep { get; set; }
        [ObservableProperty]
        public partial int Wheat { get; set; }

        [ObservableProperty]
        public partial int Wood { get; set; }
        [ObservableProperty]
        public partial int Paper { get; set; }

        [ObservableProperty]
        public partial int Cloth { get; set; }
        [ObservableProperty]
        public partial int Coin { get; set; }

        [ObservableProperty]
        public partial int Politics { get; set; }
        [ObservableProperty]
        public partial int Trade { get; set; }

        [ObservableProperty]
        public partial int Science { get; set; }
        [ObservableProperty]
        public partial int VictoryPoint { get; set; }

        [ObservableProperty]
        public partial int AnyDevCard { get; set; }
        [ObservableProperty]
        public partial int Robber { get; set; }

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