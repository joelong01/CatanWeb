using Catan3.Shared.Interfaces;

namespace Catan3.GameService.Services
{
    /// <summary>
    /// Adapter to convert GameService IPersistenceService to Shared IPersistenceService.
    /// </summary>
    public class GameServicePersistenceAdapter : Catan3.Shared.Interfaces.IPersistenceService
    {
        private readonly Catan3.GameService.Services.IPersistenceService _gameServicePersistence;

        public GameServicePersistenceAdapter(Catan3.GameService.Services.IPersistenceService gameServicePersistence)
        {
            _gameServicePersistence = gameServicePersistence ?? throw new ArgumentNullException(nameof(gameServicePersistence));
        }

        public Task<bool> SaveAsync(string location, byte[] data) =>
            _gameServicePersistence.SaveAsync(location, data);

        public Task<byte[]?> OpenAsync(string location) =>
            _gameServicePersistence.OpenAsync(location);

        public string? Location => _gameServicePersistence.Location;
    }
}