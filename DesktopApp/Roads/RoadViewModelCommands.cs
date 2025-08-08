using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Catan3.Shared.Models;
namespace Catan3.Models
{
    public partial class RoadViewModel : ObservableRecipient
    {
       
        [RelayCommand]
        private void MouseClicked()
        {
            if (Road.RoadState == RoadState.Buildable)
            {
                Messenger.Send(new RoadPurchaseMessage(Road.RoadKey));
            }
        }
    }
}
