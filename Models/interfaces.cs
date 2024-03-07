
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;


namespace Catan3.Models
{
    public interface ICatanModel
    {
        void Initi() { }
    }
    public interface IHouseRules
    {
        int GoldTiles { get; }
        bool WallsProtectCities { get; }
        bool HideBaronBeforeInvasion { get; }
        bool KnightMovesBaronBeforeRoll { get; }
    }
    public interface IBoardInfo
    {
        List<TileKey> TileKeys { get; }
        public List<ResourceType> Resources { get; }
        public List<int> Numbers { get; }
        public IBoardLayout Layout { get; }
    }
    public interface IBoardLayout
    {
        double TileHeight { get; }
        double HexSize { get; set; }
        double HexStrokeThickness { get; set; }
        double TileGap { get; set; }
        double ColumnOffset { get; set; }
        int ColumnCount { get; set; }
        double GameMargin { get; set; }
        double ControlWidth { get; }
        double ControlHeight { get; }
        double BuildingSize { get; set; }
        double BoardWidth { get; }
        double RoadStrokeThickness { get; }
        double Top(TileKey key);
        double Left(TileKey key);
        PointCollection TileHexPoints { get; }
        PointCollection BuildingHexPoints { get; }
        public Dictionary<BuildingPosition, Point> ListToDictionary(PointCollection points);
    }
}
