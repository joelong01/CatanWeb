using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Catan3.Models;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using System.IO;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;
// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Catan3.Player
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlayerEditorPage : Page
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(EditPlayerViewModel), typeof(PlayerEditorPage), new PropertyMetadata(null, ViewModelChanged));
        private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PlayerEditorPage page && e.NewValue is EditPlayerViewModel viewModel)
            {
                page.DataContext = viewModel;

            }
        }


        public EditPlayerViewModel ViewModel
        {
            get => ( EditPlayerViewModel )GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }
        public PlayerEditorPage()
        {
            this.InitializeComponent();
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            PlayerEditorWindow.EditorWindow?.Close();
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
            if (ImageCropper is null) return;
            using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);
            await ImageCropper.LoadImageFromFile(file);
            ImageCropper.AspectRatio = ImageCropper.ActualWidth / ImageCropper.ActualHeight;
        }
        private async Task Load()
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Owl.jpg"));
            await ImageCropper.LoadImageFromFile(file);
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
            if (file != null && ImageCropper != null)
            {
                await ImageCropper.LoadImageFromFile(file);
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
                    await ImageCropper.SaveAsync(fileStream, BitmapFileFormat.Png);
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

        private async void PickButton_Click(object sender, RoutedEventArgs e)
        {
            await PickImage();
        }
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            await SaveCroppedImage();
        }
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            ImageCropper.Reset();
        }

        private async void OnSelectedPlayerChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                await ImageCropper.LoadImageFromFile(ViewModel.SelectedPlayer.ImageUri);
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error loading {ViewModel.SelectedPlayer.ImageUri}.  Exception: {ex}");
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedPlayer.ReloadCroppedImage();
        }
    }
}
