using System;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    /// Represents a building model in the game, including its key, state, and ownership information.
    /// Implements IComparable for sorting and comparison purposes.
    /// </summary>
    public partial class BuildingModel : ObservableObject, IComparable<BuildingModel>
    {
        /// <summary>
        /// Initializes a new instance of the BuildingModel class with default values.
        /// </summary>
        public BuildingModel() : this(new BuildingKey(HexCoordinates.Default, Utility.HexPosition.None), BuildingState.NotBuildable)
        {
        }

        /// <summary>
        /// Initializes a new instance of the BuildingModel class with the specified building key and state.
        /// </summary>
        /// <param name="buildingKey">The key identifying the building's location and position.</param>
        /// <param name="buildingState">The state of the building.</param>
        public BuildingModel(BuildingKey buildingKey, BuildingState buildingState)
        {
            BuildingKey = buildingKey;
            BuildingState = buildingState;
        }

        /// <summary>
        /// Gets or sets the key identifying the building's location and position.
        /// </summary>
        [ObservableProperty]
        public partial BuildingKey BuildingKey { get; set; }

        /// <summary>
        /// Gets or sets the state of the building.
        /// </summary>
        [ObservableProperty]
        public partial BuildingState BuildingState { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the building has a wall.
        /// </summary>
        [ObservableProperty]
        public partial bool Wall { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether the building is a metropolis.
        /// </summary>
        [ObservableProperty]
        public partial bool Metropolis { get; set; } = false;

        /// <summary>
        /// Gets or sets the ID of the owner of the building.
        /// </summary>
        [ObservableProperty]
        public partial string? OwnerId { get; set; } = null;

        /// <summary>
        /// Gets the default instance of the BuildingModel class.
        /// </summary>
        public static BuildingModel Default { get; } = new();

        /// <summary>
        /// Compares the current BuildingModel with another BuildingModel.
        /// </summary>
        /// <param name="other">The BuildingModel to compare with the current BuildingModel.</param>
        /// <returns>A value that indicates the relative order of the BuildingModels being compared.</returns>
        public int CompareTo(BuildingModel? other)
        {
            if (other is null) return 1;
            return BuildingKey.CompareTo(other.BuildingKey);
        }

        /// <summary>
        /// Returns a string representation of the BuildingModel.
        /// </summary>
        /// <returns>A string in the format "BuildingKey-BuildingState-OwnerId".</returns>
        public override string? ToString() => $"{BuildingKey}-{BuildingState}-{OwnerId}";
    }
}
