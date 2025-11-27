using Catan3.Models;
using Catan3.Shared.Extensions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace Catan3.Controls
{
    public delegate void RoadClicked(RoadViewModel viewModel);
    public delegate void RoadMouseEnter(RoadViewModel viewModel);
    public delegate void RoadMouseLeave(RoadViewModel viewModel);
    public partial class RoadCtrl : UserControl
    {

        public RoadCtrl()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(RoadViewModel), typeof(RoadCtrl), new PropertyMetadata(null, ViewModelChanged));
        public RoadViewModel ViewModel
        {
            get => (RoadViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as RoadCtrl;
            var depPropValue = (RoadViewModel)e.NewValue;
            depPropClass?.SetViewModel(depPropValue);
        }
        private void SetViewModel(RoadViewModel value)
        {
            DataContext = value;
        }
        /// <summary>
        ///     This is not bound in XAML because you can't bind to commands on Polygon mouse envents in XAML in winui3
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>

        private void OnPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ViewModel.MouseClickedCommand.Execute(ViewModel);
        }

        /// <summary>
        /// Gets the automation ID for the road control - consistent with TileCtrl pattern
        /// </summary>
        /// <param name="roadKey">The road key</param>
        /// <returns>Automation ID string for the road</returns>
        public string GetCtrlAutomationId(RoadViewModel viewModel)
        {
            if (viewModel is null || viewModel.Road is null || viewModel.Road.RoadKey is null)
            {
                return "RoadCtrl-Default";
            }
            return viewModel.Road.RoadKey.GetAutomationId();
        }
    }
}
