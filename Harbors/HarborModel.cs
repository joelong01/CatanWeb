

using System;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models

{
    public partial class HarborModel : ObservableObject, IComparable<HarborModel>
    {
        [ObservableProperty]
        private HexCoordinates _hexCoordinates = new (0,0,0);

        [ObservableProperty]
        private HarborType _harborType = HarborType.ThreeForOne;

        [ObservableProperty]
        private HexSide _side = HexSide.Bottom;

        [ObservableProperty]
        private PlayerModel? _owner = null;
    
        public static  HarborModel Default => new();

        public HarborModel(HexCoordinates tilekey, HarborType harbortype, HexSide position)
        {
            this.HexCoordinates = tilekey;
            this.HarborType = harbortype;
            this.Side = position;
        }
        public HarborModel()
        { 
            
        }

        public int CompareTo(HarborModel? other)
        {
            if (other is null) return 1;

            // First, compare by HexCoordinates
            int hexCompare = HexCoordinates.CompareTo(other.HexCoordinates);
            if (hexCompare != 0)
            {
                return hexCompare;
            }

            // If HexCoordinates are the same, then compare by HexPosition
            // Since HexPosition is an enum, we can directly compare their underlying integer values
            return Side.CompareTo(other.Side);
        }
    }


}
