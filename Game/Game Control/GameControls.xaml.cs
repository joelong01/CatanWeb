using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Controls
{
    public sealed partial class GameControls : UserControl
    {
        public GameControls()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(MainPageViewModel), typeof(GameControls), new PropertyMetadata(null, ViewModelChanged));
        public MainPageViewModel ViewModel
        {
            get => ( MainPageViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as GameControls;
            var depPropValue = (MainPageViewModel)e.NewValue;
            depPropClass?.SetViewModel(depPropValue);
        }
        private void SetViewModel(MainPageViewModel value)
        {
          // this.TraceMessage($"{value.GetHashCode()}");
        }

    }
}
