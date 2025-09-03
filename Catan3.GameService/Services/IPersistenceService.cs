using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Catan3.GameService.Utility;
using Catan3.Shared.Interfaces;

namespace Catan3.GameService.Services
{

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
        public string SaveDirectory { get; set; } = string.Empty;

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
                // Use SaveDirectory for relative paths
                if (!Path.IsPathRooted(location))
                {
                    if (string.IsNullOrEmpty(SaveDirectory))
                        throw new InvalidOperationException("SaveDirectory must be set before saving files with relative paths");
                    
                    location = Path.Combine(SaveDirectory, location);
                }

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

        public async Task<bool> WriteTextAsync(string location, string content)
        {
            try
            {
                var contentBytes = System.Text.Encoding.UTF8.GetBytes(content);
                return await SaveAsync(location, contentBytes);
            }
            catch (Exception ex)
            {
                _logger.LogEvent("FileOperation", $"Error writing text file: {ex}", LogLevel.Error);
                return false;
            }
        }
    }
}