using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Catan3.Shared.Models;
using Catan3.DesktopApp.Layout;
namespace Catan3.Models
{
    /// <summary>
    /// Represents the view model for a tile, including its properties and layout.
    /// </summary>
    public partial class TileViewModel : ObservableRecipient
    {
        /// <summary>
        /// Gets or sets the tile model.
        /// </summary>
        [ObservableProperty]
        public partial TileModel Tile { get; set; } = TileModel.Default;

        /// <summary>
        /// Gets or sets the board layout.
        /// </summary>
        [ObservableProperty]
        public partial BoardVisualLayout? Layout { get; set; }

        /// <summary>
        /// Gets or sets the left position of the tile.
        /// </summary>
        [ObservableProperty]
        public partial double Left { get; set; } = 110.0;

        /// <summary>
        /// Gets or sets the top position of the tile.
        /// </summary>
        [ObservableProperty]
        public partial double Top { get; set; } = 200.0;

        /// <summary>
        /// Gets or sets the index of the tile.
        /// </summary>
        [ObservableProperty]
        public partial int Index { get; set; } = -1;

        /// <summary>
        /// Gets or sets a value indicating whether the tile is dimmed.
        /// </summary>
        [ObservableProperty]
        public partial bool Dimmed { get; set; } = false;

        /// <summary>
        /// Gets or sets the orientation of the tile.
        /// </summary>
        [ObservableProperty]
        public partial CatanOrientation Orientation { get; set; } = CatanOrientation.FaceUp;

        /// <summary>
        /// Gets or sets a value indicating whether targeting is allowed.
        /// </summary>
        [ObservableProperty]
        public partial bool AllowTargeting { get; set; } = false;

        /// <summary>
        /// Gets the default instance of the TileViewModel class.
        /// </summary>
        public static TileViewModel Default { get; } = new(TileModel.Default, BoardVisualLayout.Default);

        /// <summary>
        /// Gets or sets the collection of target view models.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<TargetViewModel> Targets { get; set; } = [];

        /// <summary>
        /// Gets or sets the visibility of the tile index.
        /// </summary>
        [ObservableProperty]
        public partial Visibility TileIndexVisibility { get; set; } = Visibility.Collapsed;
    }

  

/// <summary>
    /// Represents the view model for a target, including its properties and equality comparison.
    /// </summary>
    public partial class TargetViewModel : ObservableObject, IEquatable<TargetViewModel?>
    {
        /// <summary>
        /// Gets or sets the name of the target.
        /// </summary>
        [ObservableProperty]
        public partial string Name { get; set; }

        /// <summary>
        /// Gets or sets the ID of the target.
        /// </summary>
        [ObservableProperty]
        public partial string? Id { get; set; }

        /// <summary>
        /// Initializes a new instance of the TargetViewModel class.
        /// </summary>
        /// <param name="name">The name of the target.</param>
        /// <param name="playerId">The ID of the player.</param>
        public TargetViewModel(string name, string? playerId)
        {
            Name = $"Target: {name}" ?? throw new ArgumentNullException(nameof(name), "Name cannot be null.");
            Id = playerId;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current TargetViewModel.
        /// </summary>
        /// <param name="obj">The object to compare with the current TargetViewModel.</param>
        /// <returns>True if the specified object is equal to the current TargetViewModel; otherwise, false.</returns>
        public override bool Equals(object? obj)
        {
            return Equals(obj as TargetViewModel);
        }

        /// <summary>
        /// Determines whether the specified TargetViewModel is equal to the current TargetViewModel.
        /// </summary>
        /// <param name="other">The TargetViewModel to compare with the current TargetViewModel.</param>
        /// <returns>True if the specified TargetViewModel is equal to the current TargetViewModel; otherwise, false.</returns>
        public bool Equals(TargetViewModel? other)
        {
            return other != null && Id == other.Id;
        }

        /// <summary>
        /// Returns a hash code for the current TargetViewModel.
        /// </summary>
        /// <returns>A hash code for the current TargetViewModel.</returns>
        public override int GetHashCode()
        {
            return Id?.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Determines whether two TargetViewModel instances are equal.
        /// </summary>
        /// <param name="left">The first TargetViewModel to compare.</param>
        /// <param name="right">The second TargetViewModel to compare.</param>
        /// <returns>True if the specified TargetViewModel instances are equal; otherwise, false.</returns>
        public static bool operator ==(TargetViewModel? left, TargetViewModel? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two TargetViewModel instances are not equal.
        /// </summary>
        /// <param name="left">The first TargetViewModel to compare.</param>
        /// <param name="right">The second TargetViewModel to compare.</param>
        /// <returns>True if the specified TargetViewModel instances are not equal; otherwise, false.</returns>
        public static bool operator !=(TargetViewModel? left, TargetViewModel? right)
        {
            return !( left == right );
        }
    }
}
