using System.Threading.Tasks;

namespace Catan3.Shared.Interfaces
{
    /// <summary>
    /// Interface for file operations that can be implemented differently by Desktop and GameService
    /// </summary>
    public interface IFileOperations
    {
        /// <summary>
        /// Writes text content to a file
        /// </summary>
        /// <param name="relativePath">Path relative to the configured documents/data folder</param>
        /// <param name="content">Text content to write</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> WriteTextFileAsync(string relativePath, string content);

        /// <summary>
        /// Reads text content from a file
        /// </summary>
        /// <param name="relativePath">Path relative to the configured documents/data folder</param>
        /// <returns>File content or null if file doesn't exist or error occurs</returns>
        Task<string?> ReadTextFileAsync(string relativePath);

        /// <summary>
        /// Gets the full path for a relative path
        /// </summary>
        /// <param name="relativePath">Path relative to the configured documents/data folder</param>
        /// <returns>Full absolute path</returns>
        string GetFullPath(string relativePath);

        /// <summary>
        /// Ensures the directory exists for the given relative path
        /// </summary>
        /// <param name="relativePath">Path relative to the configured documents/data folder</param>
        void EnsureDirectoryExists(string relativePath);
    }
}