using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Controls
{
    public sealed partial class SingleRoll : UserControl
    {
        public SingleRoll()
        {
            this.InitializeComponent();
        }

        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(RollViewModel), typeof(SingleRoll), new PropertyMetadata(null));
        public RollViewModel ViewModel
        {
            get => ( RollViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        public static readonly DependencyProperty NumberProperty = DependencyProperty.Register("Number", typeof(int), typeof(SingleRoll), new PropertyMetadata(0, NumberChanged));
        public int Number
        {
            get => ( int )GetValue(NumberProperty);
            set => SetValue(NumberProperty, value);
        }
        private static void NumberChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as SingleRoll;
            var depPropValue = (int)e.NewValue;
            depPropClass?.SetNumber(depPropValue);
        }
        private void SetNumber(int value)
        {
            ValidCatanRoll = (ValidCatanRoll)value;
        }

        public static readonly DependencyProperty ValidCatanRollProperty = DependencyProperty.Register("ValidCatanRoll", typeof(ValidCatanRoll), typeof(SingleRoll), new PropertyMetadata(ValidCatanRoll.None));
        public ValidCatanRoll ValidCatanRoll
        {
            get => ( ValidCatanRoll )GetValue(ValidCatanRollProperty);
            set => SetValue(ValidCatanRollProperty, value);
        }

    }
}
