using System.ComponentModel;
namespace Catan3.Models
{
    public partial class BuildingModel : INotifyPropertyChanged
    {
        public BuildingKey BuildingKey { get; set; } = BuildingKey.Default;
        public BuildingState BuildingState { get; set; } = BuildingState.Empty;
        public bool Wall { get; set; } = false;
        public bool Metropolis { get; set; } = false;
        public PlayerModel? Owner { get; set; } = null;
        public override string ToString()
        {
            return $"{BuildingKey}:{BuildingState} [Metro={Metropolis}][Wall={Wall}][Owner={Owner}]";
        }
    }
}