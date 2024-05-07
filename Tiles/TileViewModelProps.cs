using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class TileViewModel : ObservableRecipient
    {

        [ObservableProperty]
        private TileModel _tile = TileModel.Default;

        [ObservableProperty]
        private BoardLayout? _layout;

        [ObservableProperty]
        private double _left = 110.0;

        [ObservableProperty]
        private double _top = 200.0;

        [ObservableProperty]
        private int _index = -1;

        [ObservableProperty]
        private CatanOrientation _orientation = CatanOrientation.FaceUp;

        [ObservableProperty]
        private bool _allowTargetting = false;

        public static TileViewModel Default { get; } = new(TileModel.Default, BoardLayout.Default);

        [ObservableProperty]
        public ObservableCollection<TargetViewModel> _targets = [];

    }


    public partial class TargetViewModel : ObservableObject, IEquatable<TargetViewModel?>
    {
        // Auto-generated properties by CommunityToolkit MVVM source generators.
        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string id;

        public TargetViewModel(string name, string playerid)
        {
            Name = $"Target: {name}" ?? throw new ArgumentNullException(nameof(name), "Name cannot be null.");
            Id = playerid ?? throw new ArgumentNullException(nameof(playerid), "Player ID cannot be null.");
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as TargetViewModel);
        }

        public bool Equals(TargetViewModel? other)
        {
            // Use the property here to adhere to MVVMTK0034
            return other != null && Id == other.Id;
        }

        public override int GetHashCode()
        {
            // Use the property; ensures we're using the correct getter
            return Id.GetHashCode();
        }

        public static bool operator ==(TargetViewModel? left, TargetViewModel? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(TargetViewModel? left, TargetViewModel? right)
        {
            return !( left == right );
        }
    }

}
