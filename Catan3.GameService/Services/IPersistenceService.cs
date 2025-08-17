using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Catan3.GameService.Utility;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// Provides file operations to open and save files with asynchronous support.
    /// </summary>
    public interface IPersistenceService
    {
        Task<bool> SaveAsync(string location, byte[] data);
        Task<byte[]?> OpenAsync(string location);
        string? Location { get; }
        Task<string?> OpenFileAsync(IList<string> filters);
        Task<string> PickSaveFileAsync(string defaultFileName);
    }

    /// <summary>
    /// Simple implementation of IPersistenceService for the game service
    /// </summary>
    public class GameServicePersistenceService : IPersistenceService
    {
        private readonly ILogger<GameServicePersistenceService> _logger;

        public GameServicePersistenceService(ILogger<GameServicePersistenceService> logger)
        {
            _logger = logger;
        }

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
                _logger.LogEvent("FileOperation", $"Error opening file: {ex}", LogLevel.Error);
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
                _logger.LogEvent("FileOperation", $"Error saving file: {ex}", LogLevel.Error);
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