using System;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class BuildingModel  : ObservableObject, IComparable<BuildingModel>
    {
        public BuildingModel() : this(new BuildingKey(HexCoordinates.Default, Utility.HexPosition.None), BuildingState.NotBuildable)
        {
        }
        public BuildingModel(BuildingKey buildingKey, BuildingState buildingState)
        {
            BuildingKey = buildingKey;
            BuildingState = buildingState;
        }

        

        [ObservableProperty]
        private BuildingKey _buildingKey;

        [ObservableProperty]
        private BuildingState _buildingState;

        [ObservableProperty]
        private bool _wall = false;

        [ObservableProperty]
        private bool _metropolis = false;

        [ObservableProperty]
        private string? _ownerId = null;


        public static BuildingModel Default { get; } = new();

        public int CompareTo(BuildingModel? other)
        {
            if (other is null) return 1;
            
            return BuildingKey.CompareTo(other.BuildingKey);
        }
        public override string? ToString() => $"{BuildingKey}-{BuildingState}-{OwnerId}";

       
    }
}
