using System.Diagnostics;
using Catan3.Models;
using Catan3.Player;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;


namespace Catan3
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class NewGamePage : Page
    {
        public NewGamePage()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(NewGameViewModel), typeof(NewGamePage), new PropertyMetadata(null));
        public NewGameViewModel ViewModel
        {
            get => ( NewGameViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private void OnStart(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage), ViewModel);
            Frame.BackStack.Clear();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is NewGameViewModel viewModel)
            {
                ViewModel = viewModel;
            }
            else
            {
                Debug.Assert(false, "the paramater should be a GameViewModel");
            }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {

            Frame.GoBack();
        }

        private void OnManagePlayers(object sender, RoutedEventArgs e)
        {
            PlayerEditorWindow window = new();
            PlayerSettingsViewModel viewModel = new(window,  MainWindow.PlayerDatabase);
            window.ViewModel = viewModel;
            window.Activate();
        }
    }
}
