using Catan10.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Models
{
    public partial class TradeResourceViewModel : ObservableObject
    {
        [ObservableProperty]
        private TradeResourcesModel resourceModel = new();

        public override string ToString()
        {
            return ResourceModel.ToString();
        }
    }
}
