using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class BuildingModel(BuildingKey buildingkey, BuildingState buildingstate) : ObservableObject, IComparable<BuildingModel>
    {
        [ObservableProperty]
        private BuildingKey _buildingKey = buildingkey;

        [ObservableProperty]
        private BuildingState _buildingState = buildingstate;

        [ObservableProperty]
        private bool _wall = false;

        [ObservableProperty]
        private bool _metropolis = false;

        [ObservableProperty]
        private PlayerModel? _owner = null;

        public int CompareTo(BuildingModel? other)
        {
            if (other is null) return 1;
            
            return BuildingKey.CompareTo(other.BuildingKey);
        }
    }
}
