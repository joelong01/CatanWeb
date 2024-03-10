using Catan3.Utility;

namespace Catan3.Models
{
    public static class GameExtensions
    {
        //
        //  given a key, return the building.  also looks for aliases to the key
        //  this needs to be part of Game instead of BuildingModel because it needs
        //  to join two datasets (BuildingModel and TileModel)...added as an extension
        //  method so that it isn't part of the "pure data" model that gets serialized/deserialized
        public static BuildingModel? FindBuilding(this GameModel gameModel, BuildingKey key)
        {
            var building = gameModel.Buildings.FindBuilding(key);
            if (building is null)
            {
                var aliases = key.Aliases();
                foreach ((HexPosition position, Direction direction) in aliases)
                {
                    var aliasCoords = key.TileKey.GetAdjacentTile(direction);
                    var aliasKey = new BuildingKey(aliasCoords, position);
                    building = gameModel.Buildings.FindBuilding(aliasKey);
                    if (building is not null)
                    {
                        return building;
                    }
                }
            }
            return building;
        }
        //
        //  given a key, return the road.  also looks for aliases to the key
        //  this needs to be part of Game instead of RoadModel because it needs
        //  to join two datasets (BuildingModel and TileModel)...added as an extension
        //  method so that it isn't part of the "pure data" model that gets serialized/deserialized
        public static RoadModel? FindRoad(this GameModel gameModel, RoadKey key)
        {
            var building = gameModel.Roads.FindRoad(key);
            return building;
        }
    }
}
