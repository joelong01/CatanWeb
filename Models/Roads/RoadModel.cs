using System.ComponentModel;
namespace Catan3.Models
{
    public partial class RoadModel(RoadKey key) : INotifyPropertyChanged
    {
        public RoadState RoadState { get; set; } = RoadState.Unowned;
        public RoadKey RoadKey { get; set; } = key;
        public PlayerModel? Owner { get; set; } = null;
    }
}
