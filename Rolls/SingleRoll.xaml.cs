using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static Catan3.Models.TurnRollViewModel;

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

        public static readonly DependencyProperty TurnRollProperty = DependencyProperty.Register("TurnRoll", typeof(TurnRollViewModel), typeof(SingleRoll), new PropertyMetadata(null));
        public TurnRollViewModel TurnRoll
        {
            get => ( TurnRollViewModel )GetValue(TurnRollProperty);
            set => SetValue(TurnRollProperty, value);
        }

        public static readonly DependencyProperty GameRollsProperty = DependencyProperty.Register("GameRolls", typeof(GameRollViewModel), typeof(SingleRoll), new PropertyMetadata(null));
        public GameRollViewModel GameRolls
        {
            get => ( GameRollViewModel )GetValue(GameRollsProperty);
            set => SetValue(GameRollsProperty, value);
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
