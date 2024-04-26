using System.Collections.Generic;
using Catan3.Utility;

namespace Catan3.Models
{
    public static class GameExtensions
    {
        public static bool AllocationPhase(this GameModel gameModel)
        {
            return (gameModel.GameState == GameState.AllocateResourceForward || gameModel.GameState == GameState.AllocateResourceReverse);
        } 
        public static List<RoadModel> AdjacentRoads(this GameModel gameModel, BuildingKey buildingKey)
        {
            List<RoadModel> result = [];
            List<RoadKey> roadKeys  =[];
            switch (buildingKey.Position)
            {
                case HexPosition.Right:
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates, HexSide.TopRight));
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates, HexSide.BottomRight));
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates.NorthEast, HexSide.Bottom));
                    break;
                case HexPosition.BottomRight:
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates, HexSide.Bottom));
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates, HexSide.BottomRight));
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates.South, HexSide.TopRight));
                    break;
                case HexPosition.BottomLeft:
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates, HexSide.Bottom));
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates, HexSide.BottomLeft));
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates.South, HexSide.TopLeft));
                    break;
                case HexPosition.Left:
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates, HexSide.TopLeft));
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates, HexSide.BottomLeft));
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates.NorthWest, HexSide.Bottom));
                    break;
                case HexPosition.TopLeft:
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates, HexSide.TopLeft));
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates, HexSide.Top));
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates.NorthWest, HexSide.TopRight));
                    break;
                case HexPosition.TopRight:
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates, HexSide.TopRight));
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates, HexSide.Top));
                    roadKeys.Add(new RoadKey(buildingKey.HexCoordinates.North, HexSide.BottomRight));
                    break;
                case HexPosition.None:
                    break;
            }

            foreach (var key in roadKeys)
            {
                var road = gameModel.Roads.FindRoad(key);
                if (road is not null)
                {
                    result.Add(road);
                }
            }

            return result;
        }

    }
}
