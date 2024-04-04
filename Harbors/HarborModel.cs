

using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models

{
    public partial class HarborModel : ObservableObject
    {
        [ObservableProperty]
        private HexCoordinates _tileCoordinates = new (0,0,0);

        [ObservableProperty]
        private HarborType _harborType = HarborType.ThreeForOne;

        [ObservableProperty]
        private HexSide _side = HexSide.Bottom;

        [ObservableProperty]
        private PlayerModel? _owner = null;
    
        public static  HarborModel Default => new();

        public HarborModel(HexCoordinates tilekey, HarborType harbortype, HexSide position)
        {
            this.TileCoordinates = tilekey;
            this.HarborType = harbortype;
            this.Side = position;
        }
        public HarborModel()
        { 
            
        }
    }


}
