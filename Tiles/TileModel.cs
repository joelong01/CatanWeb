using System.ComponentModel;
namespace Catan3.Models
{
    public partial class TileModel
    {
        public TileModel() { }
        public override string ToString()
        {
            return $"({ResourceType}, {Number}, {TileKey})";
        }
    }
}
