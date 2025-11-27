using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3.Controls
{
    public sealed partial class TrackedResourcesCtrl : UserControl
    {
        public TrackedResourcesCtrl()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty ResourcesViewModelProperty = DependencyProperty.Register("ResourcesViewModel", typeof(ResourcesViewModel), typeof(TrackedResourcesCtrl), new PropertyMetadata(null, ResourcesViewModelChanged));
        public ResourcesViewModel ResourcesViewModel
        {
            get => (ResourcesViewModel)GetValue(ResourcesViewModelProperty);
            set => SetValue(ResourcesViewModelProperty, value);
        }
        private static void ResourcesViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as TrackedResourcesCtrl;
            var depPropValue = (ResourcesViewModel)e.NewValue;
            depPropClass?.SetResourcesViewModel(depPropValue);
        }
        private void SetResourcesViewModel(ResourcesViewModel value)
        {
            this.DataContext = value;
        }

    }
}
