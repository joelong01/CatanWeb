using Catan3.Models;
using Catan3.Utility;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Controls
{
    public sealed partial class HarborCtrl : UserControl
    {

        public HarborCtrl()
        {
            this.InitializeComponent();
        }

        private static HarborViewModel DefaultViewModel()
        {
            HarborModel hm = new();
            HarborViewModel hvm = new HarborViewModel(hm, new BoardLayout());
            return hvm;

        }

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(HarborViewModel), typeof(HarborCtrl), new PropertyMetadata(DefaultViewModel()));
        public HarborViewModel ViewModel
        {
            get => ( HarborViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        public ImageBrush Bind_HarborImage(HarborType harborType)
        {
            string assetName = harborType.ToString();
            string key = $"HarborType.{assetName}";
            return ( ImageBrush )Application.Current.Resources[key];
        }

    }
}
