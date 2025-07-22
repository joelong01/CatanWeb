using System.Text.Json.Serialization;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Should contain all the resources used to track rolls. If you add one, add it everywhere...
    /// </summary>
    public class ResourcesModel
    {
        public int Brick { get; set; }
        public int GoldMine { get; set; }
        public int Ore { get; set; }
        public int Sheep { get; set; }
        public int Wheat { get; set; }
        public int Wood { get; set; }
        public int Paper { get; set; }
        public int Cloth { get; set; }
        public int Coin { get; set; }
        public int Politics { get; set; }
        public int Trade { get; set; }
        public int Science { get; set; }
        public int VictoryPoint { get; set; }
        public int AnyDevCard { get; set; }
        public int Robber { get; set; }
        public int Fish { get; set; }

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
            Fish = tradeResources.Fish;
        }

        [JsonIgnore]
        public int Count => Wheat + Wood + Brick + Ore + Sheep + GoldMine + Cloth + Coin + Paper + VictoryPoint + Politics + Science + Trade + AnyDevCard + Robber + Fish;

        public override string ToString()
        {
            return $"[Count={Count}][Robber={Robber}][Ore={Ore}][Brick={Brick}][Wheat={Wheat}][Wood={Wood}][Sheep={Sheep}][Gold={GoldMine}][Coin={Coin}][Cloth={Cloth}][Paper={Paper}][Fish={Fish}]";
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
                case ResourceType.Fish:
                    count = Fish;
                    break;
                default:
                    count = 0;
                    break;
            }
            return count;
        }
    }
}