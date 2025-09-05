using System.ComponentModel;
using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        public static readonly DependencyProperty GameViewModelProperty = DependencyProperty.Register("GameViewModel", typeof(GameViewModel), typeof(BoardMeasurementCtrl), new PropertyMetadata(null, GameViewModelChanged));
        public GameViewModel GameViewModel
        {
            get => ( GameViewModel )GetValue(GameViewModelProperty);
            set => SetValue(GameViewModelProperty, value);
        }
        private static void GameViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as BoardMeasurementCtrl;
            var depPropValue = (GameViewModel)e.NewValue;
            depPropClass?.SetGameViewModel(( GameViewModel )e.OldValue, ( GameViewModel )e.NewValue);
        }
        private void SetGameViewModel(GameViewModel old, GameViewModel newGameViewModel) 
        {
            if (old is not null)
            {
                old.PropertyChanged -= GameViewModel_PropertyChanged;
            }
            if ( newGameViewModel is not null)
            {
                newGameViewModel.PropertyChanged += GameViewModel_PropertyChanged;
            }
            this.DataContext = newGameViewModel;
        }

        private void GameViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GameViewModel.ShownStars))
            {
                GridView_Resources.SelectedItems.Clear();
            }
        }

       
    }
}
