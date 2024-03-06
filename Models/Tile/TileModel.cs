using System.ComponentModel;
namespace Catan3.Models
{
    public partial class TileModel : INotifyPropertyChanged
    {
        public TileKey TileKey { get; set; } = TileKey.Default;
        public ResourceType ResourceType { get; set; } = ResourceType.None;
        public int Number { get; set; } = 0;
        public override string ToString()
        {
            return $"({ResourceType}, {Number}, {TileKey})";
        }
    }
}
