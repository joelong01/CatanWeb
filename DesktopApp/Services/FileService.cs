using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Catan3;
using Microsoft.UI.Xaml;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;

namespace Catan.Services
{
    /// <summary>
    /// Provides file operations to open and save files with asynchronous support.
    /// </summary>
    public interface IPersistenceService
    {
        Task<bool> SaveAsync(string location, byte[] data);
        Task<byte[]?> OpenAsync(string location);
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
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the FileHandler class and opens the file for read/write operations.
        /// </summary>
        /// <param name="relativeFilePath">The path to the file, relative to "My Documents".</param>
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

        /// <summary>
        /// Initializes the FileHandler asynchronously with the specified path.
        /// </summary>
        /// <param name="path">The path to the file.</param>
        /// <returns>A FileStream for the specified path.</returns>
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
                    // If the path is a relative path - use corrected Documents path
                    var documentsPath = FileService.GetCorrectDocumentsPath();
                    var documentsFolder = await StorageFolder.GetFolderFromPathAsync(documentsPath);
                    StorageFile storageFile = await documentsFolder.CreateFileAsync(path, CreationCollisionOption.OpenIfExists);
                    fullPath = storageFile.Path;
                    
                    //this.TraceMessage($"🗂️ FileHandler: Using corrected Documents path: '{documentsPath}'");
                    //this.TraceMessage($"🗂️ FileHandler: Full file path: '{fullPath}'");
                }

                var result = new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return result;
            }
            catch (Exception e)
            {
                this.TraceMessage($"❌ FileHandler error: {e}");
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

                await _fileStream.WriteAsync(content.AsMemory(0, content.Length));
                await _fileStream.FlushAsync(); // Ensure all data is written to the file
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
                int bytesRead = 0;
                while (bytesRead < content.Length)
                {
                    int read = await _fileStream.ReadAsync(content.AsMemory(bytesRead, content.Length - bytesRead));
                    if (read == 0)
                    {
                        break;
                    }
                    bytesRead += read;
                }
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
                _fileStream = null;
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _fileStream?.Dispose();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Implements file operations for opening and saving files on disk, utilizing the Windows Storage API.
    /// </summary>
    public class FileService : IPersistenceService
    {
        /// <summary>
        /// Returns the name of the file that the user picked.
        /// </summary>
        public string? Location { get; private set; }

        /// <summary>
        /// Opens a file selected by the user and reads its bytes asynchronously.
        /// </summary>
        /// <param name="location">The location of the file to open.</param>
        /// <returns>The byte array of the file's contents if successful, null otherwise.</returns>
        public async Task<byte[]?> OpenAsync(string location)
        {
            try
            {
                Location = location;
                using var fileStream = new FileStream(location, FileMode.Open, FileAccess.Read, FileShare.Read);
                byte[] content = new byte[fileStream.Length];
                await fileStream.ReadExactlyAsync(content.AsMemory(0, (int)fileStream.Length));
                return content;
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error in opening file: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Opens a file picker for the user to select a file and returns the file path.
        /// </summary>
        /// <param name="parent">The parent window for the file picker.</param>
        /// <param name="filters">The file type filters for the picker.</param>
        /// <returns>The file path of the selected file, or null if no file was selected.</returns>
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
                var file = await openPicker.PickSingleFileAsync();
                return file?.Path;
            }
            catch (Exception ex)
            {
                this.TraceMessage($"Error in opening file: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Saves the provided byte array to the specified location.
        /// </summary>
        /// <param name="location">The location to save the file.</param>
        /// <param name="data">The data to write to the file.</param>
        /// <returns>True if the file was successfully saved, false otherwise.</returns>
        public async Task<bool> SaveAsync(string location, byte[] data)
        {
            const int maxRetries = 3;
            const int delayMilliseconds = 1000;

            // Verify the path
            if (location.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                this.TraceMessage($"Error: Invalid file path '{location}'");
                return false;
            }

            if (!Path.IsPathRooted(location))
            {
                // Use corrected Documents path to avoid truncation issues
                var documentsFolder = GetCorrectDocumentsPath();
                location = Path.Combine(documentsFolder, location);
                //this.TraceMessage($"🗂️ SaveAsync: Using corrected Documents path: '{documentsFolder}'");
                //this.TraceMessage($"🗂️ SaveAsync: Full save path: '{location}'");
            }

            var directory = Path.GetDirectoryName(location) ?? throw new Exception("this really shouldn't be null!");
            if (!Directory.Exists(directory))
            {
                this.TraceMessage($"🗂️ SaveAsync: Creating directory: '{directory}'");
                Directory.CreateDirectory(directory);
            }

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    using var fileStream = new FileStream(location, FileMode.Create, FileAccess.Write, FileShare.None);
                    await fileStream.WriteAsync(data.AsMemory(0, data.Length));
                    await fileStream.FlushAsync(); // Ensure all data is written to the file
                  //  this.TraceMessage($"✅ SaveAsync: Successfully saved file '{location}' on attempt {attempt + 1}");
                    return true;
                }
                catch (IOException ex) when (attempt < maxRetries - 1)
                {
                    this.TraceMessage($"Error saving file (attempt {attempt + 1}): {ex.Message}. Retrying in {delayMilliseconds}ms...");
                    await Task.Delay(delayMilliseconds);
                }
                catch (Exception ex)
                {
                    this.TraceMessage($"❌ SaveAsync: Error saving file '{location}': {ex}");
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Prompts the user to pick a file for saving. This function initializes a FileSavePicker and returns the selected file path.
        /// </summary>
        /// <param name="defaultFileName">The default filename to suggest in the picker.</param>
        /// <returns>The picked file path, or an empty string if no file was selected.</returns>
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
            var file = await savePicker.PickSaveFileAsync();
            return file?.Path ?? string.Empty;
        }

        /// <summary>
        /// Gets the correct full Documents folder path, working around potential truncation issues
        /// </summary>
        /// <returns>The full Documents folder path</returns>
        public static string GetCorrectDocumentsPath()
        {
            // Try multiple methods to get the correct Documents path
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            
            // Check if the path is truncated by looking for a username that's too short
            if (documentsPath.Contains(@"C:\Users\joelo\") && !documentsPath.Contains(@"C:\Users\joelong\"))
            {
                // Fix truncated username
                documentsPath = documentsPath.Replace(@"C:\Users\joelo\", @"C:\Users\joelong\");
            }
            
            return documentsPath;
        }
    }
}