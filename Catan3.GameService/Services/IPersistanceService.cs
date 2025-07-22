using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// Provides file operations to open and save files with asynchronous support.
    /// </summary>
    public interface IPersistanceService
    {
        Task<bool> SaveAsync(string location, byte[] data);
        Task<byte[]?> OpenAsync(string location);
        string? Location { get; }
        Task<string?> OpenFileAsync(IList<string> filters);
        Task<string> PickSaveFileAsync(string defaultFileName);
    }

    /// <summary>
    /// Simple implementation of IPersistanceService for the game service
    /// </summary>
    public class GameServicePersistanceService : IPersistanceService
    {
        public string? Location { get; private set; }

        public async Task<byte[]?> OpenAsync(string location)
        {
            try
            {
                Location = location;
                return await File.ReadAllBytesAsync(location);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening file: {ex}");
                return null;
            }
        }

        public async Task<bool> SaveAsync(string location, byte[] data)
        {
            try
            {
                // Ensure directory exists
                var directory = Path.GetDirectoryName(location);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(location, data);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving file: {ex}");
                return false;
            }
        }

        public Task<string?> OpenFileAsync(IList<string> filters)
        {
            // Not needed for game service - would be used by UI
            throw new NotImplementedException("File picker not available in game service");
        }

        public Task<string> PickSaveFileAsync(string defaultFileName)
        {
            // Not needed for game service - would be used by UI
            throw new NotImplementedException("File picker not available in game service");
        }
    }
}