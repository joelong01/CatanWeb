namespace Catan3.Models

{
    public enum GameAction { Shuffle, Undo, Redo,
        NextPlayer
    }

    public class DoAction(GameAction action)
    {
        public GameAction Action { get; } = action;
    }


    public class BuyRoad(RoadKey key)
    {
        public RoadKey RoadKey { get; set; } = key;
    }

    public class BuildingMouseEntered(BuildingViewModel buildingViewModel)
    {
        public BuildingViewModel BuildingViewModel { get; } = buildingViewModel;
    }

    public class BuildingMouseExit(BuildingViewModel buildingViewModel)
    {
        public BuildingViewModel BuildingViewModel { get; } = buildingViewModel;
    }
    public class BuildingUpgrade(BuildingKey key)
    {
        public BuildingKey BuildingKey { get; } = key;
    }


    public class CurrentPlayerChanged(PlayerViewModel currentPlayer)
    {
        public PlayerViewModel CurrentPlayer { get; } = currentPlayer;
    }

}
