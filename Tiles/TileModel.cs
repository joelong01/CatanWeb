using System.ComponentModel;
namespace Catan3.Models
{
    public partial class TileModel
    {
        public TileModel() { }
        public override string ToString()
        {
            return $"({ResourceTileType}, {Number}, {TileKey})";
        }

        public int Stars
        {
            get
            {
                switch (Number)
                {
                    case 2:
                    case 12:
                        return 1;
                    case 3:
                    case 11:
                        return 2;
                    case 4:
                    case 10:
                        return 3;
                    case 5:
                    case 9:
                        return 4;
                    case 6:
                    case 8:
                        return 5;
                    case 7:
                        return 0;
                    default:
                        throw new System.Exception("Invaled Number");
                }
            }
        }
    }
}
