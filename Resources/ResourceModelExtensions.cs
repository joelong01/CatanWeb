using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Catan10.Models;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Shapes;

namespace Catan3.Models
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
                //case ResourceTileType.Cloth:
                //    model.Cloth += toAdd;
                //    break;
                //case ResourceTileType.Coin:
                //    Coin += toAdd;
                //    break;
                //case ResourceTileType.Paper:
                //    Paper += toAdd;
                //    break;
                //case ResourceTileType.Desert:
                //    break;
                //case ResourceTileType.Politics:
                //    Politics += toAdd;
                //    break;
                //case ResourceTileType.Science:
                //    Science += toAdd;
                //    break;
                //case ResourceTileType.Trade:
                //    Trade += toAdd;
                //    break;
                //case ResourceTileType.VictoryPoint:
                //    VictoryPoint += toAdd;
                //    break;
                //case ResourceTileType.AnyDevCard:
                //    AnyDevCard += toAdd;
                //    break;
                case ResourceType.Back:

                case ResourceType.None:

                case ResourceType.Sea:

                default:
                    model.TraceMessage($"{resourceType} passed to Add()");
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
