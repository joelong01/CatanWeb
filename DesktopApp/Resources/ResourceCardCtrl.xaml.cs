using Catan3.Models;
using Catan3.Shared.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3.Controls
{
    public sealed partial class ResourceCardCtrl : UserControl
    {
        public ResourceCardCtrl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(ResourceCounterViewModel), typeof(ResourceCardCtrl), new PropertyMetadata(ResourceCounterViewModel.Default));
        public ResourceCounterViewModel ViewModel
        {
            get => ( ResourceCounterViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        public static readonly DependencyProperty ResourceTypeProperty = DependencyProperty.Register("ResourceType", typeof(ResourceType), typeof(ResourceCardCtrl), new PropertyMetadata(ResourceType.None));
        public ResourceType ResourceType
        {
            get => ( ResourceType )GetValue(ResourceTypeProperty);
            set => SetValue(ResourceTypeProperty, value);
        }
       
    }
}
