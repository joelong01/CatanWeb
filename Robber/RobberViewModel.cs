

using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class RobberViewModel(RobberModel? robberModel) : ObservableObject
    {
        [ObservableProperty]
        private RobberModel? _robberModel = robberModel;
    }
}
