using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Catan3;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using WinRT.Interop;
using WinUIEx;
namespace Catan.Services
{
    /// <summary>
    /// Provides file operations to open and save files with asynchronous support.
    /// </summary>
    public interface IFileService
    {
        Task<bool> SaveFileAsync(byte[] data);
        Task<bool> SaveFileAsAsync(string defaultFileName, byte[] data);
        Task<byte[]?> OpenFileAsync();
        string? FileName { get; }

        Task<StorageFile?> GetFileAsync(WindowEx parent, IList<string> filters);
       
    }
    /// <summary>
    /// Implements file operations for opening and saving files on disk, utilizing the Windows Storage API.
    /// </summary>
    public class FileService : IFileService
    {
        private StorageFile? _file = null;  // Holds a reference to the currently selected file
        /// <summary>
        ///     returns the name of the file that the user picked
        /// </summary>
        public string? FileName
        {
            get
            {
                if (_file is null) return null;
                return _file.Name;
            }
        }
        /// <summary>
        /// Opens a file selected by the user and reads its bytes asynchronously.
        /// </summary>
        /// <returns>The byte array of the file's contents if successful, null otherwise.</returns>
        public async Task<byte[]?> OpenFileAsync()
        {
            try
            {
                var openPicker = new FileOpenPicker
                {
                    ViewMode = PickerViewMode.Thumbnail,
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };
                openPicker.FileTypeFilter.Add(".catan");
                var window = (Application.Current as App)?.MainWindow as MainWindow;
                IntPtr hwnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(openPicker, hwnd);
                StorageFile file = await openPicker.PickSingleFileAsync();
                if (file == null) return null;
                var compressedData = await FileIO.ReadBufferAsync(file);
                return compressedData.ToArray();
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error in opening file: {ex}");
                return null;
            }
        }

        public async Task<StorageFile?> GetFileAsync(WindowEx parent, IList<string> filters)
        {
            try
            {
                var openPicker = new FileOpenPicker
                {
                    ViewMode = PickerViewMode.Thumbnail,
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary
                };
                foreach (var f in filters)
                {
                    openPicker.FileTypeFilter.Add(f);
                }
             
                IntPtr hwnd = WindowNative.GetWindowHandle(parent);
                InitializeWithWindow.Initialize(openPicker, hwnd);
                return await openPicker.PickSingleFileAsync();
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error in opening file: {ex}");
                return null;
            }
        }
   
        

            /// <summary>
            /// Saves the provided byte array to a file chosen by the user with a suggested filename.
            /// </summary>
            /// <param name="defaultFileName">The default name to suggest when saving the file.</param>
            /// <param name="data">The data to write to the file.</param>
            /// <returns>True if the file was successfully saved, false otherwise.</returns>
            public async Task<bool> SaveFileAsAsync(string defaultFileName, byte[] data)
        {
            _file = await PickFile(defaultFileName);
            if (_file == null) return false;
            return await WriteToDisk(data);
        }
        /// <summary>
        /// Saves the provided byte array to the previously used file, or prompts the user to select a file if none is set.
        /// </summary>
        /// <param name="data">The data to write to the file.</param>
        /// <returns>True if the file was successfully saved, false otherwise.</returns>
        public async Task<bool> SaveFileAsync(byte[] data)
        {
            try
            {
                _file ??= await PickFile("");
                if (_file == null) return false;
                return await WriteToDisk(data);
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error saving file: {ex}");
                return false;
            }
        }
        /// <summary>
        /// Writes the given byte array data to the disk using the currently set StorageFile.
        /// </summary>
        /// <param name="data">The byte data to write.</param>
        /// <returns>True if the write operation was successful, otherwise false.</returns>
        private async Task<bool> WriteToDisk(byte[] data)
        {
            if (_file == null) return false;
            CachedFileManager.DeferUpdates(_file);
            await FileIO.WriteBytesAsync(_file, data);
            FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(_file);
            return status == FileUpdateStatus.Complete;
        }
        /// <summary>
        /// Prompts the user to pick a file for saving. This function initializes a FileSavePicker and returns the selected file.
        /// </summary>
        /// <param name="defaultFileName">The default filename to suggest in the picker.</param>
        /// <returns>The picked StorageFile, or null if no file was selected.</returns>
        private async Task<StorageFile> PickFile(string defaultFileName)
        {
            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            savePicker.FileTypeChoices.Add("Catan File", new List<string> { ".catan" });
            savePicker.SuggestedFileName = defaultFileName;
            var window = (Application.Current as App)?.MainWindow as MainWindow;
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(savePicker, hwnd);
            return await savePicker.PickSaveFileAsync();
        }
    }
}