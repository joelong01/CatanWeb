

using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class RobberViewModel(RobberModel? robberModel) : ObservableObject
    {
        [JsonIgnore]
        [ObservableProperty]
        private RobberModel? _robberModel = robberModel;

   
    }
}
