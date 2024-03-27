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

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(HarborViewModel), typeof(HarborCtrl), new PropertyMetadata(null, ViewModelChanged));
        public HarborViewModel ViewModel
        {
            get => ( HarborViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as HarborCtrl;
            depPropClass?.SetViewModel(( HarborViewModel )e.NewValue, (HarborViewModel)e.OldValue);
        }
        private void SetViewModel(HarborViewModel newValue, HarborViewModel oldValue)
        {

            if (oldValue is not null)
            {
                oldValue.PropertyChanged -= HarborViewModel_PropertyChanged;
            }

            if (newValue is not null)
            {
                newValue.PropertyChanged += HarborViewModel_PropertyChanged;
            }
            SetOrientation();
        }

        private void HarborViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HarborViewModel.Orientation))
            {
                SetOrientation();
            }
        }

        private void SetOrientation()
        {
            if (ViewModel.Orientation == CatanOrientation.FaceUp)
            {
                AnimationHelpers.FlipToFaceUp(C_Front, C_Back);
            }
            else // Assuming the only other state is FaceDown
            {
                AnimationHelpers.FlipToFaceDown(C_Front, C_Back);
            }
        }

        public ImageBrush Bind_HarborImage(HarborType harborType)
        {
            string assetName = harborType.ToString();
            string key = $"HarborType.{assetName}";
            return ( ImageBrush )Application.Current.Resources[key];
        }

    }
}
