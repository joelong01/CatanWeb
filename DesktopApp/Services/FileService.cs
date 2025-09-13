using System;
using System.Collections.Generic;
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
    public class FileService : Catan3.Shared.Interfaces.IPersistenceService
    {
        /// <summary>
        /// Returns the name of the file that the user picked.
        /// </summary>
        public string? Location { get; private set; }

        private string _saveDirectory = string.Empty;

        /// <summary>
        /// Gets or sets the base directory where files should be saved.
        /// Used as the root for relative paths in save operations.
        /// </summary>
        public string SaveDirectory 
        { 
            get => _saveDirectory;
            set 
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("SaveDirectory cannot be null or empty", nameof(value));
                
                if (!Directory.Exists(value))
                    throw new DirectoryNotFoundException($"SaveDirectory does not exist: {value}");
                
                _saveDirectory = value;
                this.TraceMessage($"SaveDirectory set to: {value}");
            }
        }

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
                // Fail fast if SaveDirectory is not configured
                if (string.IsNullOrEmpty(_saveDirectory))
                    throw new InvalidOperationException("SaveDirectory must be set before saving files with relative paths");
                
                location = Path.Combine(_saveDirectory, location);
                this.TraceMessage($"🗂️ SaveAsync: Using configured SaveDirectory: '{_saveDirectory}'");
                this.TraceMessage($"🗂️ SaveAsync: Full save path: '{location}'");
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
        /// Generates a save file path for a new game in the standard Catan save location.
        /// </summary>
        /// <param name="gameModel">The game model to generate a path for.</param>
        /// <returns>Full path to save the new game file.</returns>
        public static string GenerateNewGameSaveFilePath(Catan3.Shared.Models.GameModel gameModel)
        {
            var documentsPath = GetCorrectDocumentsPath();
            var fileName = $"{gameModel.GameType}-{gameModel.GameId}.catan";
            return Path.Join(documentsPath, "Catan Saved Games", fileName);
        }

        /// <summary>
        /// Gets the correct full Documents folder path, working around potential truncation issues
        /// </summary>
        /// <returns>The full Documents folder path</returns>
        public static string GetCorrectDocumentsPath()
        {
            // Check for custom path override via environment variable
            var customPath = Environment.GetEnvironmentVariable("CATAN_DOCUMENTS_PATH");
            typeof(FileService).TraceMessage($"CATAN_DOCUMENTS_PATH env var: '{customPath}'");
            
            if (!string.IsNullOrEmpty(customPath))
            {
                // Trim any trailing backslash for consistency
                customPath = customPath.TrimEnd('\\');
                
                if (Directory.Exists(customPath))
                {
                    typeof(FileService).TraceMessage($"Using custom path: '{customPath}'");
                    return customPath;
                }
                else
                {
                    typeof(FileService).TraceMessage($"Custom path does not exist: '{customPath}'");
                }
            }

            // Try multiple methods to get the correct Documents path
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            typeof(FileService).TraceMessage($"Using default Documents path: '{documentsPath}'");
            return documentsPath;
        }

        /// <summary>
        /// Writes text content to a file using the configured Documents path
        /// </summary>
        /// <param name="relativePath">Path relative to Documents folder (e.g., "Catan Saved Games/test.catan_test")</param>
        /// <param name="content">Text content to write</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> WriteTextAsync(string relativePath, string content)
        {
            try
            {
                var fullPath = GetFullPath(relativePath);
                EnsureDirectoryExists(relativePath);
                
                await File.WriteAllTextAsync(fullPath, content);
                return true;
            }
            catch (Exception ex)
            {
                this.TraceMessage($"❌ WriteTextFileAsync failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads text content from a file using the configured Documents path
        /// </summary>
        /// <param name="relativePath">Path relative to Documents folder</param>
        /// <returns>File content or null if file doesn't exist or error occurs</returns>
        public async Task<string?> ReadTextFileAsync(string relativePath)
        {
            try
            {
                var fullPath = GetFullPath(relativePath);
                if (!File.Exists(fullPath))
                    return null;
                    
                return await File.ReadAllTextAsync(fullPath);
            }
            catch (Exception ex)
            {
                this.TraceMessage($"❌ ReadTextFileAsync failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the full path for a relative path using the configured Documents folder
        /// </summary>
        /// <param name="relativePath">Path relative to Documents folder</param>
        /// <returns>Full absolute path</returns>
        public string GetFullPath(string relativePath)
        {
            var documentsPath = GetCorrectDocumentsPath();
            return Path.Combine(documentsPath, relativePath);
        }

        /// <summary>
        /// Ensures the directory exists for the given relative path
        /// </summary>
        /// <param name="relativePath">Path relative to Documents folder</param>
        public void EnsureDirectoryExists(string relativePath)
        {
            var fullPath = GetFullPath(relativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}