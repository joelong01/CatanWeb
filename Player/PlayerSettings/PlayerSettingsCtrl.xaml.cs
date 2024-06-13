using System;
using System.IO;
using System.Threading.Tasks;
using Catan3.Models;
using Catan3.Player;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3.Controls
{
    public sealed partial class PlayerSettingsCtrl : UserControl
    {
        public PlayerSettingsCtrl()
        {
            this.InitializeComponent();
        }
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(PlayerSettingsViewModel), typeof(PlayerSettingsCtrl), new PropertyMetadata(null, ViewModelChanged));
        public PlayerSettingsViewModel ViewModel
        {
            get => ( PlayerSettingsViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as PlayerSettingsCtrl;
            var depPropValue = (PlayerSettingsViewModel)e.NewValue;
            depPropClass?.SetViewModel(depPropValue);
        }
        private void SetViewModel(PlayerSettingsViewModel value)
        {
        }
        private void ImageCropper_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        private async void ImageCropper_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var storageFile = items[0] as StorageFile;
                    if (storageFile != null)
                    {
                        await LoadImage(storageFile);
                    }
                }
            }
        }
        private async Task LoadImage(StorageFile file)
        {
            if (this.CTRL_ImageCropper is null) return;
            using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);
            await this.CTRL_ImageCropper.LoadImageFromFile(file);
            this.CTRL_ImageCropper.AspectRatio = this.CTRL_ImageCropper.ActualWidth / this.CTRL_ImageCropper.ActualHeight;
        }
        private async Task PickImage()
        {
            var filePicker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                FileTypeFilter =
                {
                    ".png", ".jpg", ".jpeg"
                }
            };
            IntPtr hwnd = WindowNative.GetWindowHandle(PlayerEditorWindow.EditorWindow);
            InitializeWithWindow.Initialize(filePicker, hwnd);
            var file = await filePicker.PickSingleFileAsync();
            if (file != null && CTRL_ImageCropper != null)
            {
                await this.CTRL_ImageCropper.LoadImageFromFile(file);
            }
        }
        private async Task SaveCroppedImage()
        {
            // Ensure the filename is correctly assigned to the property
            var filePath = PlayerDatabase.GetNextCroppedFileName(ViewModel.SelectedPlayer.Id);
            string fileName = Path.GetFileName(filePath);

            var folder = await KnownFolders.DocumentsLibrary.CreateFolderAsync(Path.Join("Catan Saved Games", "Players"), CreationCollisionOption.OpenIfExists);
            var imageFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            using (IRandomAccessStream fileStream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                try
                {
                    await this.CTRL_ImageCropper.SaveAsync(fileStream, BitmapFileFormat.Png);
                    StorageFile file = await StorageFile.GetFileFromPathAsync(ViewModel.SelectedPlayer.CroppedImageUri);
                    await file.DeleteAsync();
                    ViewModel.SelectedPlayer.CroppedImageUri = filePath;
                    await PlayerDatabase.SavePlayers();
                }
                catch (Exception ex)
                {
                    // Handle the exception as needed
                    System.Diagnostics.Debug.WriteLine($"Exception saving cropped image: {ex}");
                }
            }
        }
       
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            await SaveCroppedImage();
        }
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            this.CTRL_ImageCropper.Reset();
        }

    }
}
