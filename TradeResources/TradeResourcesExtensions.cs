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
    public static class TradeResourcesModelExtensions
    {
        public static void Add(this TradeResourcesModel a, TradeResourcesModel b)
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

        public static TradeResourcesModel TradeResourcesModelForRedDie(SpecialDice roll)
        {
            TradeResourcesModel tr = new();
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

        public static TradeResourcesModel TradeResourcesModelForCity(ResourceTileType resourceType, bool pirates)
        {
            TradeResourcesModel tr = new TradeResourcesModel();

            switch (resourceType)
            {
                case ResourceTileType.Sheep:
                    tr.Sheep++;
                    tr.Cloth += pirates ? 1 : 0;
                    tr.Sheep += pirates ? 0 : 1;
                    break;
                case ResourceTileType.Wood:
                    tr.Wood++;
                    tr.Wood += pirates ? 0 : 1;
                    tr.Paper += pirates ? 1 : 0;
                    break;
                case ResourceTileType.Ore:
                    tr.Ore++;
                    tr.Ore += pirates ? 0 : 1;
                    tr.Coin += pirates ? 1 : 0;
                    break;
                case ResourceTileType.Wheat:
                    tr.Wheat += 2;
                    break;
                case ResourceTileType.Brick:
                    tr.Brick += 2;
                    break;
                case ResourceTileType.GoldMine:
                    tr.GoldMine += 2;
                    break;
                default:
                    break;
            }
            return tr;
        }

        public static void AddResource(this TradeResourcesModel model, ResourceTileType resourceType, int toAdd)
        {
            switch (resourceType)
            {
                case ResourceTileType.Sheep:
                    model.Sheep += toAdd;
                    break;

                case ResourceTileType.Wood:
                    model.Wood += toAdd;
                    break;

                case ResourceTileType.Ore:
                    model.Ore += toAdd;
                    break;

                case ResourceTileType.Wheat:
                    model.Wheat += toAdd;
                    break;

                case ResourceTileType.Brick:
                    model.Brick += toAdd;
                    break;

                case ResourceTileType.GoldMine:
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
                case ResourceTileType.Back:

                case ResourceTileType.None:

                case ResourceTileType.Sea:

                default:
                    model.TraceMessage($"{resourceType} passed to Add()");
                    break;
            }
        }

        public static TradeResourcesModel TradeResourcesModelForBuilding(BuildingState buildingState, ResourceTileType resourceType, bool pirates)
        {
            var tr = new TradeResourcesModel();
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
