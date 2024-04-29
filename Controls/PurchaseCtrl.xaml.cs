using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Controls
{
    public sealed partial class PurchaseCtrl : UserControl
    {
        public PurchaseCtrl()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(EntitlementPurchaseViewModel), typeof(PurchaseCtrl), new PropertyMetadata(null));
        public EntitlementPurchaseViewModel ViewModel
        {
            get => ( EntitlementPurchaseViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private void Viewbox_Holding(object sender, Microsoft.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {

        }

        private void OnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!ViewModel.EntitlementPurchaseModel.Enabled) return;
            if (sender is Grid grid)
            {
                grid.BorderThickness = new Thickness(1);
            }
        }

        private void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!ViewModel.EntitlementPurchaseModel.Enabled) return;
            if (sender is Grid grid)
            {
                if (!isPointerCaptured)
                {
                    grid.BorderThickness = new Thickness(0);
                }
            }
        }
        private bool isPointerCaptured = false;
        private void OnPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!ViewModel.EntitlementPurchaseModel.Enabled) return;
            if (sender is Grid grid)
            {
                void pointerReleasedHandler(object s, PointerRoutedEventArgs origE)
                {
                    if (s is Grid releasedGrid)
                    {
                        // Check if pointer is still within the grid bounds when released
                        var point = origE.GetCurrentPoint(releasedGrid).Position;
                        bool isInside = point.X >= 0 && point.X <= releasedGrid.ActualWidth &&
                                point.Y >= 0 && point.Y <= releasedGrid.ActualHeight;

                        if (( PointerEventHandler? )pointerReleasedHandler is not null)
                        {
                            releasedGrid.PointerReleased -= pointerReleasedHandler;
                        }
                        releasedGrid.ReleasePointerCapture(origE.Pointer);
                        isPointerCaptured = false;

                        SwapColors(grid);
                        if (isInside)
                        {
                            ViewModel.Command.Execute(ViewModel.EntitlementPurchaseModel.Entitlement);

                        }
                        else
                        {
                            releasedGrid.TraceMessage("Pointer released outside the grid.");
                            grid.BorderThickness = new Thickness(0);
                        }
                    }
                }

                grid.CapturePointer(e.Pointer);
                isPointerCaptured = true;
                grid.PointerReleased += pointerReleasedHandler;

                SwapColors(grid);
            }
        }

        private void SwapColors(Grid grid)
        {
            Brush? temp = null;
            grid.BorderBrush = grid.Background;
            foreach (FrameworkElement child in grid.Children)
            {
                if (child is TextBlock tb)
                {
                    temp = tb.Foreground;
                    tb.Foreground = grid.Background;
                   
                }
            }
            grid.Background = temp;

        }
    }
}
