

using Catan3.Utility;

namespace Catan3.Models

{
    public partial class HarborModel
    {
        public static  HarborModel Default => new();

        public HarborModel(HexCoordinates tilekey, HarborType harbortype, HexSide position)
        {
            this.TileCoordinates = tilekey;
            this.HarborType = harbortype;
            this.Position = position;
        }
        public HarborModel()
        { 
            
        }
    }


}
