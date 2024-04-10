using Catan3.Models;
using Catan3.Utility;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Controls
{
    public sealed partial class BuildingCtrl : UserControl
    {
        public BuildingCtrl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty BuildingViewModelProperty = DependencyProperty.Register("BuildingViewModel", typeof(BuildingViewModel), typeof(BuildingCtrl), new PropertyMetadata(null, BuildingViewModelChanged));
        public BuildingViewModel BuildingViewModel
        {
            get => ( BuildingViewModel )GetValue(BuildingViewModelProperty);
            set => SetValue(BuildingViewModelProperty, value);
        }
        private static void BuildingViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as BuildingCtrl;
            var depPropValue = (BuildingViewModel)e.NewValue;
            depPropClass?.SetBuildingViewModel(depPropValue);
        }
        private void SetBuildingViewModel(BuildingViewModel value)
        {
           // this.TraceMessage($"{value} Hashcode: {value.GetHashCode()}");
            //var bindingExpression = TXT_StateGlyph.GetBindingExpression(TextBlock.TextProperty);
            //if (bindingExpression is not null) bindingExpression.UpdateSource();
        }
        private static double MetroBuildingZoom(bool isMetropolis)
        {
            return isMetropolis ? 0.6 : 1.0;
        }

        private string LaurelGlyph => CatanFont.Laurel;

        

        private void OnPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            BuildingViewModel.UpgradeCommand.Execute(BuildingViewModel);
        }

        private void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            BuildingViewModel.MouseExitCommand.Execute(BuildingViewModel);
        }

        private void OnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            BuildingViewModel.MouseEnterCommand.Execute(BuildingViewModel);
        }
    }
}
