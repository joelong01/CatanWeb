using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Catan3;
using Microsoft.UI.Xaml;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;
namespace Catan.Services
{
    /// <summary>
    /// Provides file operations to open and save files with asynchronous support.
    /// </summary>
    public interface IPersistanceService
    {
        Task<bool> SaveAsync(string location, byte[] data);
        Task<byte[]?> OpenAsync(string location);
        void CloseFile(string location);
        string? Location { get; }
        Task<string?> OpenFileAsync(WindowEx parent, IList<string> filters);
        Task<string> PickSaveFileAsync(string defaultFileName);

    }



    /// <summary>
    /// The FileHandler class provides methods to open, read, write, and close a file using a cached FileStream.
    /// </summary>
    public class FileHandler : IDisposable
    {
        public string FilePath { get; private set; }
        private FileStream? _fileStream;

        /// <summary>
        /// Initializes a new instance of the FileHandler class and opens the file for read/write operations.
        /// </summary>
        /// <param name="relativeFilePath">The path to the file, reletive to "My Documents".</param>
        /// <exception cref="ArgumentNullException">Thrown when filePath is null.</exception>
        /// <exception cref="ArgumentException">Thrown when filePath is an empty string, contains only white spaces, or contains invalid characters.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to filePath is denied.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the specified path is invalid.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs.</exception>
        public FileHandler(string relativeFilePath)
        {
            if (string.IsNullOrEmpty(relativeFilePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(relativeFilePath));
            }

            FilePath = relativeFilePath;

           
        }

        public async Task<FileStream> InitializeFileHandlerAsync(string path)
        {
            try
            {
                string fullPath;

                if (Path.IsPathRooted(path))
                {
                    // If the path is a fully qualified name (FQN)
                    fullPath = path;
                }
                else if (path.StartsWith("ms-appx:///"))
                {
                    // If the path is an ms-appx URI
                    var uri = new Uri(path);
                    StorageFile storageFile = await StorageFile.GetFileFromApplicationUriAsync(uri);
                    fullPath = storageFile.Path;
                }
                else
                {
                    // If the path is a relative path
                    StorageFolder documentsFolder = KnownFolders.DocumentsLibrary;
                    StorageFile storageFile = await documentsFolder.CreateFileAsync(path, CreationCollisionOption.OpenIfExists);
                    fullPath = storageFile.Path;
                }

                var result = new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return result;
            }
            catch (Exception e)
            {
                this.TraceMessage($"{e}");
                throw;
            }
        }



        /// <summary>
        /// Writes the specified byte array content to the file asynchronously.
        /// </summary>
        /// <param name="content">The byte array content to write to the file.</param>
        /// <exception cref="ObjectDisposedException">Thrown when the FileStream is closed.</exception>
        /// <exception cref="NotSupportedException">Thrown when the stream does not support writing.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs.</exception>
        public async Task<bool> WriteContentAsync(byte[] content)
        {
            try
            {
                _fileStream ??= await InitializeFileHandlerAsync(FilePath);
                // Clear the file content and set the file position to the beginning
                _fileStream.SetLength(0);
                _fileStream.Seek(0, SeekOrigin.Begin);

                await _fileStream.WriteAsync(content, 0, content.Length);
                await _fileStream.FlushAsync(); // Ensure all data is written to the file
              //  this.TraceMessage("Content written to file.");
                CloseFile();
                return true;
            }
            catch (Exception ex)
            {
                this.TraceMessage($"An error occurred while writing to the file: {ex.Message}");
                throw;
            }

        }

        /// <summary>
        /// Reads the content of the file into a byte array asynchronously.
        /// </summary>
        /// <returns>A byte array containing the file content.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the FileStream is closed.</exception>
        /// <exception cref="NotSupportedException">Thrown when the stream does not support reading.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs.</exception>
        public async Task<byte[]> ReadContentsAsync()
        {
            try
            {
                _fileStream ??= await InitializeFileHandlerAsync(FilePath);
                // Set the file position to the beginning
                _fileStream.Seek(0, SeekOrigin.Begin);

                byte[] content = new byte[_fileStream.Length];
                await _fileStream.ReadAsync(content, 0, content.Length);
                CloseFile();
                return content;
            }
            catch (Exception ex)
            {
                this.TraceMessage($"An error occurred while reading the file: {ex.Message}");
                return Array.Empty<byte>(); // Return an empty array in case of error
            }
        }

        /// <summary>
        /// Closes the file and releases the resources.
        /// </summary>
        /// <exception cref="IOException">Thrown when an I/O error occurs.</exception>
        public void CloseFile()
        {
            try
            {
                if (_fileStream is null) return;
                _fileStream.Close();
                FilePath = string.Empty;
               // this.TraceMessage("File closed.");
            }
            catch (Exception ex)
            {
                this.TraceMessage($"An error occurred while closing the file: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Disposes the resources used by the FileHandler.
        /// </summary>
        public void Dispose()
        {
            if (_fileStream is not null)
            {
                _fileStream?.Dispose();
            }
        }


    }



    /// <summary>
    /// Implements file operations for opening and saving files on disk, utilizing the Windows Storage API.
    /// </summary>
    public class FileService : IPersistanceService
    {
        private FileHandler? FileHandler { get; set; }
        /// <summary>
        ///     returns the name of the file that the user picked
        /// </summary>
        public string? Location
        {
            get
            {
                if (FileHandler is null) return null;
                return FileHandler.FilePath;


            }
        }
        /// <summary>
        /// Opens a file selected by the user and reads its bytes asynchronously.
        /// </summary>
        /// <returns>The byte array of the file's contents if successful, null otherwise.</returns>
        public async Task<byte[]?> OpenAsync(string location)
        {
            try
            {
                if (FileHandler is not null)
                {
                    FileHandler.CloseFile();
                    FileHandler = null;
                }

                FileHandler = new FileHandler(location);
                return await FileHandler.ReadContentsAsync();


            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error in opening file: {ex}");
                return null;
            }
        }

        public void CloseFile(string location)
        {
            if (Location == location && FileHandler is not null)
            {
                FileHandler.CloseFile();
                FileHandler = null;
            }
        }

        public async Task<string?> OpenFileAsync(WindowEx parent, IList<string> filters)
        {
            try
            {
                var openPicker = new FileOpenPicker
                {
                    ViewMode = PickerViewMode.Thumbnail,
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,

                };


                IntPtr hwnd = WindowNative.GetWindowHandle(parent);
                InitializeWithWindow.Initialize(openPicker, hwnd);
                foreach (var f in filters)
                {
                    openPicker.FileTypeFilter.Add(f);
                }
                var folder= await openPicker.PickSingleFileAsync();
                if (folder is null) return null;

                return folder.Path;
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error in opening file: {ex}");
                return null;
            }
        }



        /// <summary>
        /// Saves the provided byte array to the previously used file, or prompts the user to select a file if none is set.
        /// </summary>
        /// <param name="data">The data to write to the file.</param>
        /// <returns>True if the file was successfully saved, false otherwise.</returns>
        public async Task<bool> SaveAsync(string location, byte[] data)
        {
            try
            {
                if (FileHandler is not null)
                {
                    if (FileHandler.FilePath == location)
                    {
                        await FileHandler.WriteContentAsync(data);
                        return true;
                    }

                    FileHandler.CloseFile();
                    FileHandler = null;
                }
                Debug.Assert(FileHandler is null);
                FileHandler = new FileHandler(location);
                await FileHandler.WriteContentAsync(data);

                return true;
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error saving file: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Prompts the user to pick a file for saving. This function initializes a FileSavePicker and returns the selected file.
        /// </summary>
        /// <param name="defaultFileName">The default filename to suggest in the picker.</param>
        /// <returns>The picked StorageFile, or null if no file was selected.</returns>
        public async Task<string> PickSaveFileAsync(string defaultFileName)
        {
            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            savePicker.FileTypeChoices.Add("Catan File", [".catan"]);
            savePicker.SuggestedFileName = defaultFileName;
            var window = (Application.Current as App)?.MainWindow as MainWindow;
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            InitializeWithWindow.Initialize(savePicker, hwnd);
            var file =  await savePicker.PickSaveFileAsync();
            if (file is not null)
            {
                return file.Path;
            }
            return "";
        }
    }
}