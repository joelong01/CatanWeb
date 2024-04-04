using System.Text.Json.Serialization;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    public partial class TileModel
    {
      
        public TileModel() { }
        public override string ToString()
        {
            return $"({ResourceTileType}, {Number}, {TileKey})";
        }
        [JsonIgnore]
        public int Stars
        {
            get
            {
                return Number switch
                {
                    2 or 12 => 1,
                    3 or 11 => 2,
                    4 or 10 => 3,
                    5 or 9 => 4,
                    6 or 8 => 5,
                    7 => 0,
                    _ => throw new System.Exception("Invaled Number"),
                };
            }
        }
    }
}
