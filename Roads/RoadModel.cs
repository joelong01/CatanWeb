using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class RoadModel(RoadKey roadKey) : ObservableObject, IComparable<RoadModel>  
    {
        [ObservableProperty]
        private RoadKey _roadKey = roadKey;

        [ObservableProperty]
        private RoadState _roadState = RoadState.Unowned;

        [ObservableProperty]
        private string? _ownerId;

        [ObservableProperty]
        private int _buildIndex = 0;

        public int CompareTo(RoadModel? other)
        {
            if (other is null) return 1;
            return RoadKey.CompareTo(other.RoadKey);
        }
        public override string ToString()
        {
            return $"{RoadKey}-{RoadState}-{OwnerId}-{BuildIndex}";
        }
    }
}
