using System.Diagnostics;
using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

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
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(ResourcesViewModel), typeof(ResourceCardCtrl), new PropertyMetadata(null));
        public ResourcesViewModel ViewModel
        {
            get => ( ResourcesViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        public static readonly DependencyProperty ResourceTypeProperty = DependencyProperty.Register("ResourceType", typeof(ResourceType), typeof(ResourceCardCtrl), new PropertyMetadata(ResourceType.None));
        public ResourceType ResourceType
        {
            get => ( ResourceType )GetValue(ResourceTypeProperty);
            set => SetValue(ResourceTypeProperty, value);
        }

        private ImageBrush BIND_FrontImage(ResourceType resourceCardType)
        {
           // this.TraceMessage($"Resource: {resourceCardType}");
            string key = $"ResourceCard.{resourceCardType}";
            var result =  ( ImageBrush )Application.Current.Resources[key];
            Debug.Assert(result is not null);
            return result;
        }

      
    }
}
