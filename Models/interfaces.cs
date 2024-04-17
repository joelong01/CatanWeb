
using System.Collections.Generic;
using System.ComponentModel;
using Catan3.Utility;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;


namespace Catan3.Models
{

    public interface IHouseRules
    {
        int GoldTiles { get; }
        bool WallsProtectCities { get; }
        bool HideRobberBeforeInvasion { get; }
        bool KnightMovesRobberBeforeRoll { get; }
    }
    public interface IBoardInfo
    {
        List<HexCoordinates> TileKeys { get; }
        public List<ResourceTileType> Resources { get; }
        public List<int> Numbers { get; }
        public List<HarborModel> Harbors { get; }
        public BoardLayout Layout { get; }
        public bool HasSupplemental { get; }
        public HouseRules HouseRules { get; }

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
