using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Controls
{

    public sealed partial class PlayerCounterCtrl : UserControl
    {
        public PlayerCounterCtrl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty CountProperty = DependencyProperty.Register("Count", typeof(int), typeof(PlayerCounterCtrl), new PropertyMetadata(0));
        public int Count
        {
            get => ( int )GetValue(CountProperty);
            set => SetValue(CountProperty, value);
        }
        public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register("Glyph", typeof(string), typeof(PlayerCounterCtrl), new PropertyMetadata(""));
        public string Glyph
        {
            get => ( string )GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public static readonly DependencyProperty CountHorizontalAlignmentProperty = DependencyProperty.Register("CountHorizontalAlignment", typeof(HorizontalAlignment), typeof(PlayerCounterCtrl), new PropertyMetadata(HorizontalAlignment.Center));
        public HorizontalAlignment CountHorizontalAlignment
        {
            get => ( HorizontalAlignment )GetValue(CountHorizontalAlignmentProperty);
            set => SetValue(CountHorizontalAlignmentProperty, value);
        }

        public static readonly DependencyProperty CountVerticalAlignmentProperty = DependencyProperty.Register("CountVerticalAlignment", typeof(VerticalAlignment), typeof(PlayerCounterCtrl), new PropertyMetadata(VerticalAlignment.Center));
        public VerticalAlignment CountVerticalAlignment
        {
            get => ( VerticalAlignment )GetValue(CountVerticalAlignmentProperty);
            set => SetValue(CountVerticalAlignmentProperty, value);
        }


    }
}
