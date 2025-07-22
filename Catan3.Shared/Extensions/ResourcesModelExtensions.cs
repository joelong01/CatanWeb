using System.Diagnostics;
using Catan3.Shared.Models;

namespace Catan3.Shared.Extensions
{
    public static class ResourcesModelExtensions
    {
        public static void Add(this ResourcesModel a, ResourcesModel b)
        {
            a.Wheat += b.Wheat;
            a.Wood += b.Wood;
            a.Ore += b.Ore;
            a.Sheep += b.Sheep;
            a.Brick += b.Brick;
            a.GoldMine += b.GoldMine;
            a.Coin += b.Coin;
            a.Paper += b.Paper;
            a.Cloth += b.Cloth;
            a.Politics += b.Politics;
            a.Trade += b.Trade;
            a.Science += b.Science;
            a.VictoryPoint += b.VictoryPoint;
            a.AnyDevCard += b.AnyDevCard;
            a.Robber += b.Robber;
            a.Fish += b.Fish;
        }

        public static ResourcesModel TradeResourcesModelForRedDie(SpecialDice roll)
        {
            ResourcesModel tr = new();
            switch (roll)
            {
                case SpecialDice.Trade:
                    tr.Trade++;
                    break;
                case SpecialDice.Politics:
                    tr.Science++;
                    break;
                case SpecialDice.Science:
                    tr.Politics++;
                    break;
                case SpecialDice.Pirate:
                    break;
                case SpecialDice.None:
                    break;
            }
            return tr;
        }

        public static ResourcesModel TradeResourcesModelForCity(ResourceType resourceType, bool pirates)
        {
            ResourcesModel tr = new ResourcesModel();
            switch (resourceType)
            {
                case ResourceType.Sheep:
                    tr.Sheep++;
                    tr.Cloth += pirates ? 1 : 0;
                    tr.Sheep += pirates ? 0 : 1;
                    break;
                case ResourceType.Wood:
                    tr.Wood++;
                    tr.Wood += pirates ? 0 : 1;
                    tr.Paper += pirates ? 1 : 0;
                    break;
                case ResourceType.Ore:
                    tr.Ore++;
                    tr.Ore += pirates ? 0 : 1;
                    tr.Coin += pirates ? 1 : 0;
                    break;
                case ResourceType.Wheat:
                    tr.Wheat += 2;
                    break;
                case ResourceType.Brick:
                    tr.Brick += 2;
                    break;
                case ResourceType.GoldMine:
                    tr.GoldMine += 2;
                    break;
                case ResourceType.Fish:
                    tr.Fish += 2;
                    break;
                case ResourceType.Robber:
                    tr.Robber += 2;
                    break;
                default:
                    break;
            }
            return tr;
        }

        public static void AddResource(this ResourcesModel model, ResourceType resourceType, int toAdd)
        {
            switch (resourceType)
            {
                case ResourceType.Sheep:
                    model.Sheep += toAdd;
                    break;
                case ResourceType.Wood:
                    model.Wood += toAdd;
                    break;
                case ResourceType.Ore:
                    model.Ore += toAdd;
                    break;
                case ResourceType.Wheat:
                    model.Wheat += toAdd;
                    break;
                case ResourceType.Brick:
                    model.Brick += toAdd;
                    break;
                case ResourceType.GoldMine:
                    model.GoldMine += toAdd;
                    break;
                case ResourceType.Robber:
                    model.Robber += toAdd;
                    break;
                case ResourceType.Fish:
                    model.Fish += toAdd;
                    break;
                case ResourceType.Cloth:
                    model.Cloth += toAdd;
                    break;
                case ResourceType.Coin:
                    model.Coin += toAdd;
                    break;
                case ResourceType.Paper:
                    model.Paper += toAdd;
                    break;
                case ResourceType.Politics:
                    model.Politics += toAdd;
                    break;
                case ResourceType.Science:
                    model.Science += toAdd;
                    break;
                case ResourceType.Trade:
                    model.Trade += toAdd;
                    break;
                case ResourceType.VictoryPoint:
                    model.VictoryPoint += toAdd;
                    break;
                case ResourceType.AnyDevCard:
                    model.AnyDevCard += toAdd;
                    break;
                case ResourceType.Desert:
                    break;
                case ResourceType.Back:
                case ResourceType.None:
                case ResourceType.Sea:
                default:
                    Debug.WriteLine($"{resourceType} passed to AddResource()");
                    break;
            }
        }

        public static ResourcesModel TradeResourcesModelForBuilding(BuildingState buildingState, ResourceType resourceType, bool pirates)
        {
            var tr = new ResourcesModel();
            switch (buildingState)
            {
                case BuildingState.Settlement:
                    tr.AddResource(resourceType, 1);
                    break;
                case BuildingState.City:
                    return TradeResourcesModelForCity(resourceType, pirates);
            }
            return tr;
        }
    }
}