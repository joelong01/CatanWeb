using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
namespace Catan3.Models
{
    public partial class RoadViewModel : ObservableObject
    {
        [RelayCommand]
        private void MouseEnter()
        {
            MainPage.Messenger.Send(new RoadMouseEntered(this));
        }

        [RelayCommand]
        private void MouseExit()
        {
            MainPage.Messenger.Send(new RoadMouseExit(this));
        }

    }
}
