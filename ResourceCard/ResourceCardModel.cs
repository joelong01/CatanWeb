using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml; 
namespace Catan3.Models
{
    public partial class ResourceCardModel : ObservableObject
    {
        [ObservableProperty]
        private ResourceCardType _resourceType = ResourceCardType.None;

        [ObservableProperty]
        private int _count = 0;

        [ObservableProperty]
        private CatanOrientation _orientation = CatanOrientation.FaceDown;

        [ObservableProperty]
        private Visibility _countVisibility = Visibility.Visible;
    }
}
