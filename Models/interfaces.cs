
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Catan3.Utility;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;


namespace Catan3.Models
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
    }

    public interface IGameMetadata
    {
        GameType GameType { get; }
        string Description { get; }
        List<HexCoordinates> TileKeys { get; }
        public List<ResourceType> Resources { get; }
        public List<int> Numbers { get; }
        public List<HarborModel> Harbors { get; }
        public BoardLayout Layout { get; }
        public bool HasSupplemental { get; }
        public HouseRules HouseRules { get; }
        public ResourceRules ResourceRules { get; }
        public List<EntitlementPurchaseModel> PurchaseableEntitlements { get; }

    }
    public interface IBoardLayout
    {
        double TileHeight { get; }
        double HexSize { get; set; }
        double HexStrokeThickness { get; set; }
        double TileGap { get; set; }
        double ColumnOffset { get; set; }
        int RowCount { get; set; }
        double GameMargin { get; set; }
        double ControlWidth { get; }
        double ControlHeight { get; }
        double BuildingSize { get; set; }
        double BoardWidth { get; }
        double BoardHeight { get; }
        double RoadStrokeThickness { get; }
        double Top(HexCoordinates key);
        double Left(HexCoordinates key);
        PointCollection TileHexPoints { get; }
        PointCollection BuildingHexPoints { get; }
        public Dictionary<HexPosition, Point> ListToDictionary(PointCollection points);
    }
}
