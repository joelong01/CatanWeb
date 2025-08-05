using System;
using Catan3.Shared.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents a road model in the game, including its key, state, owner, and build index.
    /// Implements IComparable for sorting and comparison purposes.
    /// Supports both plain object usage (for JSON/API) and MVVM usage (for UI data binding).
    /// </summary>
    public partial class RoadModel : ObservableObject, IComparable<RoadModel>
    {
        /// <summary>
        /// Gets or sets the key of the road.
        /// </summary>
        [ObservableProperty]
        private RoadKey _roadKey = new();

        /// <summary>
        /// Gets or sets the state of the road.
        /// </summary>
        [ObservableProperty]
        private RoadState _roadState = RoadState.Unowned;

        /// <summary>
        /// Gets or sets the owner ID of the road.
        /// </summary>
        [ObservableProperty]
        private string? _ownerId;

        /// <summary>
        /// Gets or sets the build index of the road.
        /// </summary>
        [ObservableProperty]
        private int _buildIndex = 0;

        public RoadModel() { }

        public RoadModel(RoadKey roadKey)
        {
            RoadKey = roadKey;
        }

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