using WinUIEx;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3
{
    public partial class MainWindow : WindowEx
    {
        public MainWindow()
        {
            this.InitializeComponent();
            var windowManger = WindowManager.Get(this);
            windowManger.IsMaximizable = true;
            windowManger.IsResizable = true;
            windowManger.PersistenceId = "Catan_WinUi";
            this.Content = new MainPage(); // Set MainPage as the content of MainWindow
           
        }
      
    }
}
