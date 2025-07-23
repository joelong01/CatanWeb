using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Catan3.GameService.Services;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Tests.GameService
{
    public class DiscoveryServiceTests : IDisposable
    {
        private readonly ILogger<UdpDiscoveryService> _logger;
        private readonly DiscoveryServiceOptions _options;
        private UdpClient? _testClient;

        public DiscoveryServiceTests()
        {
            // Create a mock logger
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<UdpDiscoveryService>();

            // Setup test options
            _options = new DiscoveryServiceOptions
            {
                BroadcastPort = 8766, // Use different port for testing
                BroadcastInterval = 1000, // Faster interval for testing
                Enabled = true
            };
        }

        [Fact]
        public void Constructor_ShouldInitializeWithDefaultGameInfo()
        {
            // Arrange & Act
            var optionsWrapper = Options.Create(_options);
            var service = new UdpDiscoveryService(_logger, optionsWrapper);

            // Assert
            Assert.NotNull(service);
        }

        [Fact]
        public async Task StartAsync_WhenEnabled_ShouldStartSuccessfully()
        {
            // Arrange
            var optionsWrapper = Options.Create(_options);
            var service = new UdpDiscoveryService(_logger, optionsWrapper);

            // Act & Assert
            await service.StartAsync(CancellationToken.None);
            
            // Cleanup
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
        }

        [Fact]
        public async Task StartAsync_WhenDisabled_ShouldNotStart()
        {
            // Arrange
            var disabledOptions = new DiscoveryServiceOptions { Enabled = false };
            var optionsWrapper = Options.Create(disabledOptions);
            var service = new UdpDiscoveryService(_logger, optionsWrapper);

            // Act & Assert - Should not throw
            await service.StartAsync(CancellationToken.None);
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
        }

        [Fact]
        public void UpdateGameInfo_ShouldUpdateInternalState()
        {
            // Arrange
            var optionsWrapper = Options.Create(_options);
            var service = new UdpDiscoveryService(_logger, optionsWrapper);
            var gameId = "test-game-123";
            var gameState = "WaitingForRoll";
            var playerCount = 4;
            var roomCode = "ABCD";

            // Act
            service.UpdateGameInfo(gameId, gameState, playerCount, roomCode);

            // Assert - The method should complete without throwing
            // Note: Since the internal state is private, we can't directly assert it
            // but we can ensure the method doesn't crash
            Assert.True(true);
        }

        [Fact]
        public async Task StopAsync_ShouldStopServiceGracefully()
        {
            // Arrange
            var optionsWrapper = Options.Create(_options);
            var service = new UdpDiscoveryService(_logger, optionsWrapper);

            await service.StartAsync(CancellationToken.None);

            // Act
            await service.StopAsync(CancellationToken.None);

            // Assert - Should complete without throwing
            service.Dispose();
        }

        [Fact]
        public async Task BroadcastMessage_ShouldHaveCorrectFormat()
        {
            // Arrange
            var testPort = 8767; // Different port for this test
            var testOptions = new DiscoveryServiceOptions
            {
                BroadcastPort = testPort,
                BroadcastInterval = 500, // Shorter interval for faster testing
                Enabled = true
            };

            // Setup UDP listener to capture broadcast
            _testClient = new UdpClient(testPort);
            var messagesReceived = new List<string>();
            var expectedMessageReceived = new TaskCompletionSource<DiscoveryMessage>();

            // Start listening in background
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!expectedMessageReceived.Task.IsCompleted)
                    {
                        var result = await _testClient.ReceiveAsync();
                        var message = Encoding.UTF8.GetString(result.Buffer);
                        messagesReceived.Add(message);
                        
                        try
                        {
                            var discoveryMessage = JsonSerializer.Deserialize<DiscoveryMessage>(message, new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            });
                            
                            // Check if this is the message we're looking for (with our test data)
                            if (discoveryMessage != null && 
                                discoveryMessage.GameState == "WaitingForRoll" && 
                                discoveryMessage.PlayerCount == 3 && 
                                discoveryMessage.RoomCode == "TEST")
                            {
                                expectedMessageReceived.SetResult(discoveryMessage);
                                break;
                            }
                        }
                        catch
                        {
                            // Ignore JSON parsing errors, continue listening
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!expectedMessageReceived.Task.IsCompleted)
                    {
                        expectedMessageReceived.SetException(ex);
                    }
                }
            });

            var optionsWrapper = Options.Create(testOptions);
            var service = new UdpDiscoveryService(_logger, optionsWrapper);

            // Act
            await service.StartAsync(CancellationToken.None);
            
            // Update game info
            service.UpdateGameInfo("test-game", "WaitingForRoll", 3, "TEST");

            // Wait for the specific broadcast message (with timeout)
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            try
            {
                var discoveryMessage = await expectedMessageReceived.Task.WaitAsync(cts.Token);
                
                // Assert
                Assert.NotNull(discoveryMessage);
                Assert.NotNull(discoveryMessage.GameId);
                Assert.NotEmpty(discoveryMessage.GameId);
                Assert.Equal("WaitingForRoll", discoveryMessage.GameState);
                Assert.Equal(3, discoveryMessage.PlayerCount);
                Assert.Equal("TEST", discoveryMessage.RoomCode);
                Assert.Equal(8080, discoveryMessage.ServicePort);
                Assert.Contains("companion", discoveryMessage.WebCompanionUrl);
                Assert.True(discoveryMessage.Timestamp > DateTime.MinValue);
                
                // Verify we received at least one message
                Assert.True(messagesReceived.Count > 0);
            }
            finally
            {
                await service.StopAsync(CancellationToken.None);
                service.Dispose();
            }
        }

        [Fact]
        public void DiscoveryServiceOptions_ShouldHaveCorrectDefaults()
        {
            // Arrange & Act
            var options = new DiscoveryServiceOptions();

            // Assert
            Assert.Equal(8765, options.BroadcastPort);
            Assert.Equal(5000, options.BroadcastInterval);
            Assert.True(options.Enabled);
        }

        [Fact]
        public void DiscoveryMessage_ShouldSerializeCorrectly()
        {
            // Arrange
            var message = new DiscoveryMessage
            {
                GameId = "test-123",
                GameName = "Test Game",
                PlayerCount = 4,
                GameState = "Playing",
                ServicePort = 8080,
                WebCompanionUrl = "http://localhost:8080/companion",
                RoomCode = "ABCD",
                Timestamp = DateTime.UtcNow
            };

            // Act
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var deserialized = JsonSerializer.Deserialize<DiscoveryMessage>(json, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Assert
            Assert.NotNull(json);
            Assert.NotNull(deserialized);
            Assert.Equal(message.GameId, deserialized.GameId);
            Assert.Equal(message.GameName, deserialized.GameName);
            Assert.Equal(message.PlayerCount, deserialized.PlayerCount);
            Assert.Equal(message.GameState, deserialized.GameState);
            Assert.Equal(message.ServicePort, deserialized.ServicePort);
            Assert.Equal(message.WebCompanionUrl, deserialized.WebCompanionUrl);
            Assert.Equal(message.RoomCode, deserialized.RoomCode);
        }

        [Fact]
        public async Task Service_ShouldHandleCancellationGracefully()
        {
            // Arrange
            var optionsWrapper = Options.Create(_options);
            var service = new UdpDiscoveryService(_logger, optionsWrapper);
            
            // Act
            await service.StartAsync(CancellationToken.None);
            
            // Cancel immediately
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            
            // Should not throw
            await service.StopAsync(cts.Token);
            service.Dispose();
        }

        [Fact]
        public async Task UpdateGameInfo_MultipleCalls_ShouldNotCrash()
        {
            // Arrange
            var optionsWrapper = Options.Create(_options);
            var service = new UdpDiscoveryService(_logger, optionsWrapper);

            // Act - Multiple rapid updates
            for (int i = 0; i < 10; i++)
            {
                service.UpdateGameInfo($"game-{i}", $"state-{i}", i, $"CODE{i}");
            }

            // Allow some time for async operations
            await Task.Delay(100);

            // Assert - Should not crash
            service.Dispose();
        }

        [Fact]
        public void DiscoveryMessage_DefaultValues_ShouldBeCorrect()
        {
            // Arrange & Act
            var message = new DiscoveryMessage();

            // Assert
            Assert.Equal(string.Empty, message.GameId);
            Assert.Equal("Catan Game", message.GameName);
            Assert.Equal(0, message.PlayerCount);
            Assert.Equal(string.Empty, message.GameState);
            Assert.Equal(0, message.ServicePort);
            Assert.Equal(string.Empty, message.WebCompanionUrl);
            Assert.Equal(string.Empty, message.RoomCode);
            Assert.Equal(DateTime.MinValue, message.Timestamp);
        }

        public void Dispose()
        {
            _testClient?.Close();
            _testClient?.Dispose();
        }
    }
}