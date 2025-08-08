using System.Collections.Generic;
using System.Text.Json.Serialization;
using Catan3.Shared.Utility;

namespace Catan3.Shared.Models
{



   

    /// <summary>
    ///     Note: System.Text.Json cares that the ctor parameters have the same names as the fields, so they need to be spelled this way.
    /// </summary>
    /// <param name="maxCities"></param>
    /// <param name="maxSettlements"></param>
    /// <param name="maxRoads"></param>
    /// <param name="minPlayers"></param>
    /// <param name="maxPlayers"></param>
    public class ResourceRules(int maxCities, int maxSettlements, int maxRoads, int minPlayers, int maxPlayers)
    {
        public ResourceRules() : this(0, 0, 0, 0, 0) { }
        public int MaxCities { get; set; } = maxCities;
        public int MaxSettlements { get; set; } = maxSettlements;
        public int MaxRoads { get; set; } = maxRoads;
        public int MinPlayers { get; set; } = minPlayers;
        public int MaxPlayers { get; set; } = maxPlayers;
        [JsonIgnore]
        public static ResourceRules Default { get; set; } = new();
        public int MaxEntitlementCount(Entitlement entitlement)
        {
            int result = 0;
            switch (entitlement)
            {
                case Entitlement.Undefined:
                    break;
                case Entitlement.DevCard:
                    break;
                case Entitlement.Settlement:
                    result = MaxSettlements;
                    break;
                case Entitlement.City:
                    result = MaxCities;
                    break;
                case Entitlement.Road:
                    result = MaxRoads;
                    break;
                case Entitlement.Ship:
                    break;
                case Entitlement.BuyKnight:
                    break;
                case Entitlement.UpgradeKnight:
                    break;
                case Entitlement.ActivateKnight:
                    break;
                case Entitlement.Soldier:
                    break;
                case Entitlement.PoliticsUpgrade:
                    break;
                case Entitlement.ScienceUpgrade:
                    break;
                case Entitlement.TradeUpgrade:
                    break;
                case Entitlement.Wall:
                    break;
                case Entitlement.DestroyCity:
                    break;
                case Entitlement.Bishop:
                    break;
                case Entitlement.Deserter:
                    break;
                case Entitlement.Inventor:
                    break;
                case Entitlement.Intrigue:
                    break;
                case Entitlement.Diplomat:
                    break;
                case Entitlement.Merchant:
                    break;
                case Entitlement.KnightDisplacement:
                    break;
                case Entitlement.UpgradeToMetro:
                    break;
                case Entitlement.KnightDisplacementMoveKnightOutOfTheWay:
                    break;
                case Entitlement.RolledSeven:
                    break;
            }
            return result;
        }
    }



    public interface IGameMetadata
    {
        GameType GameType { get; }
        string Description { get; }
        List<HexCoordinates> TileKeys { get; }
        public List<ResourceType> Resources { get; }
        public List<int> Numbers { get; }
        public List<HarborModel> Harbors { get; }
        public bool HasSupplemental { get; }
        public HouseRules HouseRules { get; }
        public ResourceRules ResourceRules { get; }
        public List<EntitlementPurchaseModel> PurchaseableEntitlements { get; }
    }

    
}