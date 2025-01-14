using System;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    /// <summary>
    /// Represents a road model in the game, including its key, state, owner, and build index.
    /// Implements IComparable for sorting and comparison purposes.
    /// </summary>
    public partial class RoadModel(RoadKey roadKey) : ObservableObject, IComparable<RoadModel>
    {
        /// <summary>
        /// Gets or sets the key of the road.
        /// </summary>
        [ObservableProperty]
        public partial RoadKey RoadKey { get; set; } = roadKey;

        /// <summary>
        /// Gets or sets the state of the road.
        /// </summary>
        [ObservableProperty]
        public partial RoadState RoadState { get; set; } = RoadState.Unowned;

        /// <summary>
        /// Gets or sets the owner ID of the road.
        /// </summary>
        [ObservableProperty]
        public partial string? OwnerId { get; set; }

        /// <summary>
        /// Gets or sets the build index of the road.
        /// </summary>
        [ObservableProperty]
        public partial int BuildIndex { get; set; } = 0;

        /// <summary>
        /// Compares the current RoadModel with another RoadModel.
        /// </summary>
        /// <param name="other">The RoadModel to compare with the current RoadModel.</param>
        /// <returns>A value that indicates the relative order of the RoadModels being compared.</returns>
        public int CompareTo(RoadModel? other)
        {
            if (other is null) return 1;
            return RoadKey.CompareTo(other.RoadKey);
        }

        /// <summary>
        /// Returns a string representation of the RoadModel.
        /// </summary>
        /// <returns>A string in the format "RoadKey-RoadState-OwnerId-BuildIndex".</returns>
        public override string ToString()
        {
            return $"{RoadKey}-{RoadState}-{OwnerId}-{BuildIndex}";
        }
    }
}
