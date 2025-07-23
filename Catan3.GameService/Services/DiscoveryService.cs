using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Catan3.GameService.Services
{
    public class DiscoveryServiceOptions
    {
        public int BroadcastPort { get; set; } = 8765;
        public int BroadcastInterval { get; set; } = 5000; // 5 seconds
        public bool Enabled { get; set; } = true;
    }

    public class DiscoveryMessage
    {
        public string GameId { get; set; } = string.Empty;
        public string GameName { get; set; } = "Catan Game";
        public int PlayerCount { get; set; }
        public string GameState { get; set; } = string.Empty;
        public int ServicePort { get; set; }
        public string WebCompanionUrl { get; set; } = string.Empty;
        public string RoomCode { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public interface IDiscoveryService
    {
        Task StartAsync(CancellationToken cancellationToken);
        Task StopAsync(CancellationToken cancellationToken);
        void UpdateGameInfo(string gameId, string gameState, int playerCount, string roomCode = "");
    }

    public class UdpDiscoveryService : BackgroundService, IDiscoveryService
    {
        private readonly ILogger<UdpDiscoveryService> _logger;
        private readonly DiscoveryServiceOptions _options;
        private UdpClient? _udpClient;
        private IPEndPoint? _broadcastEndpoint;
        private DiscoveryMessage _currentGameInfo;
        private readonly SemaphoreSlim _updateSemaphore = new(1, 1);

        public UdpDiscoveryService(ILogger<UdpDiscoveryService> logger, IOptions<DiscoveryServiceOptions> options)
        {
            _logger = logger;
            _options = options.Value;
            _currentGameInfo = new DiscoveryMessage
            {
                GameId = Guid.NewGuid().ToString(),
                ServicePort = 8080, // Default port, can be updated
                WebCompanionUrl = $"http://{GetLocalIPAddress()}:8080/companion"
            };
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("Discovery service is disabled");
                return;
            }

            try
            {
                _udpClient = new UdpClient();
                _udpClient.EnableBroadcast = true;
                _broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, _options.BroadcastPort);

                _logger.LogInformation("UDP Discovery Service starting on port {Port}", _options.BroadcastPort);
                _logger.LogInformation("Web companion URL: {Url}", _currentGameInfo.WebCompanionUrl);

                await base.StartAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start UDP Discovery Service");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("UDP Discovery Service stopping");
            
            _udpClient?.Close();
            _udpClient?.Dispose();
            
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled || _udpClient == null || _broadcastEndpoint == null)
            {
                return;
            }

            _logger.LogInformation("UDP Discovery Service broadcasting every {Interval}ms", _options.BroadcastInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await BroadcastGameInfo(stoppingToken);
                    await Task.Delay(_options.BroadcastInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during discovery broadcast");
                    await Task.Delay(1000, stoppingToken); // Wait before retrying
                }
            }
        }

        private async Task BroadcastGameInfo(CancellationToken cancellationToken)
        {
            if (_udpClient == null || _broadcastEndpoint == null)
                return;

            await _updateSemaphore.WaitAsync(cancellationToken);
            try
            {
                _currentGameInfo.Timestamp = DateTime.UtcNow;
                
                var json = JsonSerializer.Serialize(_currentGameInfo, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                var data = Encoding.UTF8.GetBytes(json);
                
                await _udpClient.SendAsync(data, data.Length, _broadcastEndpoint);
                
                _logger.LogDebug("Broadcast game info: {GameId}, State: {State}, Players: {Players}", 
                    _currentGameInfo.GameId, _currentGameInfo.GameState, _currentGameInfo.PlayerCount);
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }

        public void UpdateGameInfo(string gameId, string gameState, int playerCount, string roomCode = "")
        {
            _ = Task.Run(async () =>
            {
                await _updateSemaphore.WaitAsync();
                try
                {
                    _currentGameInfo.GameId = gameId;
                    _currentGameInfo.GameState = gameState;
                    _currentGameInfo.PlayerCount = playerCount;
                    _currentGameInfo.RoomCode = roomCode;
                    
                    _logger.LogDebug("Updated game info: {GameId}, State: {State}, Players: {Players}", 
                        gameId, gameState, playerCount);
                }
                finally
                {
                    _updateSemaphore.Release();
                }
            });
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint)
                {
                    return endPoint.Address.ToString();
                }
            }
            catch
            {
                // Fallback to localhost if unable to determine local IP
            }
            
            return "localhost";
        }

        public override void Dispose()
        {
            _udpClient?.Dispose();
            _updateSemaphore?.Dispose();
            base.Dispose();
        }
    }
}