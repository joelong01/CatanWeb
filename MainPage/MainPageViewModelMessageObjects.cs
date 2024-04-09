namespace Catan3.Models
{



    public class RequestShuffle
    {

    }

    public class RoadMouseEntered(RoadViewModel roadViewModel)
    {
        public RoadViewModel RoadViewModel { get; } = roadViewModel;
    }

    public class RoadMouseExit(RoadViewModel roadViewModel)
    {
        public RoadViewModel RoadViewModel { get; } = roadViewModel;
    }

    public class BuildingMouseEntered(BuildingViewModel buildingViewModel)
    {
        public BuildingViewModel BuildingViewModel { get; } = buildingViewModel;
    }

    public class BuildingMouseExit(BuildingViewModel buildingViewModel)
    {
        public BuildingViewModel BuildingViewModel { get; } = buildingViewModel;
    }
    public class BuildingUpgrade(BuildingViewModel buildingViewModel)
    {
        public BuildingViewModel BuildingViewModel { get; } = buildingViewModel;
    }


    public class CurrentPlayerChanged(PlayerViewModel currentPlayer)
    {
        public PlayerViewModel CurrentPlayer { get; } = currentPlayer;
    }

}
