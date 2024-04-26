using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
namespace Catan3.Models
{
    public partial class RoadViewModel : ObservableRecipient
    {
        [RelayCommand]
        private void MouseEnter()
        {
            if (Road.OwnerId is null && Road.Buildable)
            {
                Road.RoadState = RoadState.Highlighted;
            }
        }

        [RelayCommand]
        private void MouseExit()
        {
            if (Road.RoadState == RoadState.Highlighted)
            {
                Road.RoadState = RoadState.Unowned;
            }
        }

        [RelayCommand]
        private void MouseClicked()
        {
            if (Road.Buildable)
            {
                Messenger.Send(new RoadPurchaseMessage(Road.RoadKey));
            }
        }

    }
}
