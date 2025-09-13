using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3.Controls
{
    public sealed partial class CatanNumber : UserControl
    {
        public CatanNumber()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty StarsProperty = DependencyProperty.Register("Stars", typeof(string), typeof(CatanNumber), new PropertyMetadata(""));
        public string Stars
        {
            get => ( string )GetValue(StarsProperty);
            set => SetValue(StarsProperty, value);
        }
        public static readonly DependencyProperty NumberProperty = DependencyProperty.Register("Number", typeof(int), typeof(CatanNumber), new PropertyMetadata(7, NumberChanged));
        public int Number
        {
            get => ( int )GetValue(NumberProperty);
            set => SetValue(NumberProperty, value);
        }
        private static void NumberChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as CatanNumber;
            var depPropValue = (int)e.NewValue;
            depPropClass?.SetNumber(depPropValue);
        }
        private void SetNumber(int value)
        {
            
            int count = 0;
           
            switch (value)
            {
                case 2:
                case 12:
                    count = 1;
                    break;
                case 3:
                case 11:
                    count = 2;
                    break;
                case 4:
                case 10:
                    count = 3;
                    break;
                case 5:
                case 9:
                    count = 4;
                    break;
                case 6:
                case 8:
                    count = 5; break;
                case 7:
                    count = 0;
                    break;
            }
            char star = '\uE00A';
            Stars = new string(star, count);
        }
        public SolidColorBrush BIND_StarForeground(int number)
        {
            if (number == 8 || number == 6) return BrushCache.GetSolidColorBrush(Colors.Red);
            return BrushCache.GetSolidColorBrush(Colors.White);
        }
    }
}
