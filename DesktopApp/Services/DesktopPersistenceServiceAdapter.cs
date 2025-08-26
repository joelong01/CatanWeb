using Catan3.Shared.Interfaces;
using System.Threading.Tasks;

namespace Catan3.Services
{
    /// <summary>
    /// Adapter that converts Desktop IPersistenceService to Shared IPersistenceService interface.
    /// </summary>
    public class DesktopPersistenceServiceAdapter : IPersistenceService
    {
        private readonly Catan.Services.IPersistenceService _desktopService;

        public DesktopPersistenceServiceAdapter(Catan.Services.IPersistenceService desktopService)
        {
            _desktopService = desktopService;
        }

        public Task<bool> SaveAsync(string location, byte[] data)
        {
            return _desktopService.SaveAsync(location, data);
        }

        public Task<byte[]?> OpenAsync(string location)
        {
            return _desktopService.OpenAsync(location);
        }

        public string? Location => _desktopService.Location;
    }
}