using Catan.Services;
using Catan3.Models;
using WinUIEx;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3
{
    public partial class MainWindow : WindowEx
    {
        public static PlayerDatabase PlayerDatabase { get; private set; } = new();
        public static FileService FileService { get; private set; } = new();
        public static MainPageViewModel? CurrentGame { get; set; } = null;
        public static MainWindow? Instance { get; private set; } = null;
        public MainWindow()
        {
            Instance = this;
            this.InitializeComponent();
           

        }

        private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            await PlayerDatabase.LoadPlayerDatabase();
            NewGameViewModel viewModel = new(PlayerDatabase.AllPlayers);
            MainFrame.Navigate(typeof(NewGamePage), viewModel);

        }
    }
}
