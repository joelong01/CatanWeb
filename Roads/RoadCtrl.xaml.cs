using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Catan3.Models;
using Catan3.Utility;
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
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(RoadViewModel), typeof(RoadCtrl), new PropertyMetadata(new RoadKey(HexCoordinates.Default, HexSide.None), ViewModelChanged));
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

        /// <summary>
        ///     This is not bound in XAML because you can't bind to commands on Polygon mouse envents in XAML in winui3
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
      
        private void OnPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ViewModel.MouseClickedCommand.Execute(ViewModel);
        }
    }
}
