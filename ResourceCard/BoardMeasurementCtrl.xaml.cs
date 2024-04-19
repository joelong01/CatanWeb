using System.Collections.ObjectModel;
using Catan3.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Catan3.Controls
{
    public sealed partial class BoardMeasurementCtrl : UserControl
    {

     

        public BoardMeasurementCtrl()
        {
           
            this.InitializeComponent();
        }
        public static readonly DependencyProperty GameViewModelProperty = DependencyProperty.Register("GameViewModel", typeof(GameViewModel), typeof(BoardMeasurementCtrl), new PropertyMetadata(null));
        public GameViewModel GameViewModel
        {
            get => ( GameViewModel )GetValue(GameViewModelProperty);
            set => SetValue(GameViewModelProperty, value);
        }
 
    }
}
