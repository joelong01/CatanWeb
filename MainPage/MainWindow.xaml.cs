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
           
            this.Content = new MainPage(); // Set MainPage as the content of MainWindow
           
        }
      
    }
}
