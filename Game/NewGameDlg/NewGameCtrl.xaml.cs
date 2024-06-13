using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3.Controls
{
    public sealed partial class NewGameCtrl : UserControl
    {
        private NewGameCtrl()
        {
            this.InitializeComponent();
            
        }
        public NewGameCtrl(NewGameViewModel viewModel) : this()
        {
            this.DataContext = viewModel;
            ViewModel = viewModel;
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(NewGameViewModel), typeof(NewGameCtrl), new PropertyMetadata(null));
        public NewGameViewModel ViewModel
        {
            get => ( NewGameViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
    }
}
