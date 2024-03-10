using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Catan3.Models;
using Catan3.Utility;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Security.Cryptography.Certificates;
namespace Catan3.Controls
{
    public delegate void RoadClicked(RoadViewModel viewModel);
    public delegate void RoadMouseEnter(RoadViewModel viewModel);
    public delegate void RoadMouseLeave(RoadViewModel viewModel);
    public partial class RoadCtrl : UserControl
    {
        public event RoadClicked? RoadClicked;
        public RoadCtrl()
        {
            InitializeComponent();
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(RoadViewModel), typeof(RoadCtrl), new PropertyMetadata(new RoadKey(TileKey.Default, RoadPosition.None), ViewModelChanged));
        public RoadViewModel ViewModel
        {
            get => ( RoadViewModel )GetValue(ViewModelProperty);
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


        private void Grid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            RoadClicked?.Invoke(this.ViewModel);
        }

    }
}
