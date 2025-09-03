using System.Collections.Generic;
using System.Threading.Tasks;

namespace Catan3.Shared.Interfaces
{
    /// <summary>
    /// Provides file operations to open and save files with asynchronous support.
    /// Platform-neutral interface for Desktop and Service implementations.
    /// </summary>
    public interface IPersistenceService
    {
        /// <summary>
        /// Saves data to the specified location asynchronously.
        /// </summary>
        /// <param name="location">The file path to save to.</param>
        /// <param name="data">The data to save.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> SaveAsync(string location, byte[] data);

        /// <summary>
        /// Opens and reads data from the specified location asynchronously.
        /// </summary>
        /// <param name="location">The file path to read from.</param>
        /// <returns>The file data, or null if unsuccessful.</returns>
        Task<byte[]?> OpenAsync(string location);

        /// <summary>
        /// Gets the current location being worked with.
        /// </summary>
        string? Location { get; }

        /// <summary>
        /// Gets or sets the base directory where files should be saved.
        /// Implementations should use this as the root for relative paths.
        /// </summary>
        string SaveDirectory { get; set; }

        /// <summary>
        /// Writes text content to the specified location asynchronously.
        /// </summary>
        /// <param name="location">The file path to write to.</param>
        /// <param name="content">The text content to write.</param>
        /// <returns>True if successful, false otherwise.</returns>
        Task<bool> WriteTextAsync(string location, string content);
    }
}