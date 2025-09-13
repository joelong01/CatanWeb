using Catan3.Shared.Interfaces;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// GameService implementation of IFileOperations that uses local file system operations.
    /// Uses a configured temp directory for game service file operations.
    /// </summary>
    public class GameServiceFileOperationsAdapter : IFileOperations
    {
        private readonly string _basePath;

        public GameServiceFileOperationsAdapter()
        {
            // GameService uses temp directory for file operations
            _basePath = Path.Combine(Path.GetTempPath(), "Catan3Games");
        }

        public async Task<bool> WriteTextFileAsync(string relativePath, string content)
        {
            try
            {
                var fullPath = GetFullPath(relativePath);
                EnsureDirectoryExists(relativePath);
                
                await File.WriteAllTextAsync(fullPath, content);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string?> ReadTextFileAsync(string relativePath)
        {
            try
            {
                var fullPath = GetFullPath(relativePath);
                if (!File.Exists(fullPath))
                    return null;
                    
                return await File.ReadAllTextAsync(fullPath);
            }
            catch
            {
                return null;
            }
        }

        public string GetFullPath(string relativePath)
        {
            return Path.Combine(_basePath, relativePath);
        }

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