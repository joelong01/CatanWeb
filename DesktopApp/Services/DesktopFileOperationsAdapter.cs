using System.Threading.Tasks;
using Catan3.Shared.Interfaces;

namespace Catan3.Services
{
    /// <summary>
    /// Desktop implementation of IFileOperations that delegates to FileService
    /// </summary>
    public class DesktopFileOperationsAdapter : IFileOperations
    {
        private readonly Catan.Services.IPersistenceService _fileService;

        public DesktopFileOperationsAdapter(Catan.Services.IPersistenceService fileService)
        {
            _fileService = fileService;
        }

        public Task<bool> WriteTextFileAsync(string relativePath, string content)
        {
            return _fileService.WriteTextFileAsync(relativePath, content);
        }

        public Task<string?> ReadTextFileAsync(string relativePath)
        {
            return _fileService.ReadTextFileAsync(relativePath);
        }

        public string GetFullPath(string relativePath)
        {
            return _fileService.GetFullPath(relativePath);
        }

        public void EnsureDirectoryExists(string relativePath)
        {
            _fileService.EnsureDirectoryExists(relativePath);
        }
    }
}