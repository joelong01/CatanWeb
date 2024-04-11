using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Controls
{
    public sealed partial class GameButtons : UserControl
    {
        public GameButtons()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(GameViewModel), typeof(GameButtons), new PropertyMetadata(null, ViewModelChanged));
        public GameViewModel ViewModel
        {
            get => ( GameViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as GameButtons;
            var depPropValue = (GameViewModel)e.NewValue;
            depPropClass?.SetViewModel(depPropValue);
        }
       

        private void SetViewModel(GameViewModel value)
        {
            this.DataContext = value;
          //  this.TraceMessage($"GameViewModel updated:  {value.GetHashCode()}");
        }


        private void OnUndo(object sender, RoutedEventArgs e)
        {
            this.TraceMessage("did it!");
        }
    }
}
