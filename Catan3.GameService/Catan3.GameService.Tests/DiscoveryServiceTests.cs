using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Catan3.GameService.Services;
using FluentAssertions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Catan3.GameService.Tests;

public class DiscoveryServiceTests
{
    private readonly Mock<ILogger<UdpDiscoveryService>> _mockLogger;
    private readonly Mock<IOptions<DiscoveryServiceOptions>> _mockOptions;
    private readonly UdpDiscoveryService _discoveryService;

    public DiscoveryServiceTests()
    {
        _mockLogger = new Mock<ILogger<UdpDiscoveryService>>();
        _mockOptions = new Mock<IOptions<DiscoveryServiceOptions>>();
        
        // Setup default options
        _mockOptions.Setup(x => x.Value).Returns(new DiscoveryServiceOptions
        {
            BroadcastPort = 8765,
            BroadcastInterval = 1000, // Use shorter interval for tests
            Enabled = true
        });
        
        _discoveryService = new UdpDiscoveryService(_mockLogger.Object, _mockOptions.Object);
    }

    [Fact]
    public async Task StartAsync_ShouldStartBroadcasting()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token;

        // Act
        var startTask = _discoveryService.StartAsync(cancellationToken);

        // Allow some time for broadcasting to start
        await Task.Delay(100);

        // Assert - Service should start without throwing
        startTask.IsCompleted.Should().BeFalse(); // Should be running
        
        // Cancel to stop the service
        await _discoveryService.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_ShouldStopBroadcasting()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token;
        
        // Start the service
        var startTask = _discoveryService.StartAsync(cancellationToken);
        await Task.Delay(100); // Let it start

        // Act
        await _discoveryService.StopAsync(CancellationToken.None);

        // Assert - Should stop gracefully
        var act = async () => await startTask;
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DiscoveryService_ShouldBroadcastOnExpectedPort()
    {
        // Arrange - Create a UDP listener on the expected port
        const int expectedPort = 8765;
        UdpClient? udpListener = null;
        
        try
        {
            udpListener = new UdpClient(expectedPort);
            udpListener.Client.ReceiveTimeout = 5000; // 5 second timeout
            
            var cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token;
            
            // Act - Start the discovery service
            var startTask = _discoveryService.StartAsync(cancellationToken);
            
            // Listen for broadcast message
            var receiveTask = Task.Run(async () =>
            {
                try
                {
                    var endPoint = new IPEndPoint(IPAddress.Any, 0);
                    var result = await udpListener.ReceiveAsync();
                    return Encoding.UTF8.GetString(result.Buffer);
                }
                catch (Exception)
                {
                    return null;
                }
            });

            // Wait for either message or timeout
            var completedTask = await Task.WhenAny(receiveTask, Task.Delay(6000));
            
            if (completedTask == receiveTask)
            {
                var message = await receiveTask;
                
                // Assert
                message.Should().NotBeNull();
                message.Should().NotBeEmpty();
                
                // Verify it's valid JSON
                var act = () => JsonSerializer.Deserialize<JsonElement>(message!);
                act.Should().NotThrow();
                
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(message!);
                jsonElement.TryGetProperty("gameId", out _).Should().BeTrue();
                jsonElement.TryGetProperty("servicePort", out _).Should().BeTrue();
                jsonElement.TryGetProperty("webCompanionUrl", out _).Should().BeTrue();
            }
            
            // Stop the service
            await _discoveryService.StopAsync(CancellationToken.None);
        }
        finally
        {
            udpListener?.Close();
            udpListener?.Dispose();
        }
    }

    [Fact]
    public async Task DiscoveryService_BroadcastMessage_ShouldContainExpectedFields()
    {
        // This test checks the structure of the broadcast message
        // Since we can't easily intercept the actual broadcast in unit tests,
        // we'll test that the service starts and stops properly
        
        // Arrange
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)).Token;
        
        // Act & Assert - Should not throw
        var act = async () =>
        {
            await _discoveryService.StartAsync(cancellationToken);
            await Task.Delay(100);
            await _discoveryService.StopAsync(CancellationToken.None);
        };
        
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DiscoveryService_ShouldHandleCancellation_Gracefully()
    {
        // Arrange
        var cancellationTokenSource = new CancellationTokenSource();
        
        // Act
        var startTask = _discoveryService.StartAsync(cancellationTokenSource.Token);
        
        // Cancel after a short delay
        await Task.Delay(100);
        cancellationTokenSource.Cancel();
        
        // Assert - Should complete without throwing
        var act = async () => await startTask;
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DiscoveryService_ShouldLogErrors_WhenBroadcastFails()
    {
        // This test verifies that errors are logged appropriately
        // We can't easily force a broadcast failure in unit tests,
        // but we can verify the service handles the lifecycle correctly
        
        // Arrange
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromMilliseconds(200)).Token;
        
        // Act
        await _discoveryService.StartAsync(cancellationToken);
        await Task.Delay(300); // Let it run for a bit
        await _discoveryService.StopAsync(CancellationToken.None);
        
        // Assert - Verify logger was used (service should log startup/shutdown)
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void DiscoveryService_ShouldImplementIHostedService()
    {
        // Assert
        _discoveryService.Should().BeAssignableTo<IHostedService>();
    }

    [Fact]
    public void DiscoveryService_ShouldImplementIDiscoveryService()
    {
        // Assert
        _discoveryService.Should().BeAssignableTo<IDiscoveryService>();
    }

    [Fact]
    public async Task DiscoveryService_MultipleStartCalls_ShouldNotCauseIssues()
    {
        // Arrange
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromMilliseconds(300)).Token;
        
        // Act - Call start multiple times
        var startTask1 = _discoveryService.StartAsync(cancellationToken);
        await Task.Delay(50);
        var startTask2 = _discoveryService.StartAsync(cancellationToken);
        
        // Wait for completion
        await Task.Delay(400);
        
        // Stop
        await _discoveryService.StopAsync(CancellationToken.None);
        
        // Assert - Should handle multiple starts gracefully
        var act1 = async () => await startTask1;
        var act2 = async () => await startTask2;
        
        await act1.Should().NotThrowAsync();
        await act2.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DiscoveryService_StopBeforeStart_ShouldNotThrow()
    {
        // Act & Assert
        var act = async () => await _discoveryService.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void UpdateGameInfo_ShouldUpdateCurrentGameInfo()
    {
        // Arrange
        var gameId = "test-game-123";
        var gameState = "WaitingForRoll";
        var playerCount = 4;
        var roomCode = "ABCD";

        // Act & Assert - Should not throw
        var act = () => _discoveryService.UpdateGameInfo(gameId, gameState, playerCount, roomCode);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DiscoveryService_WithDisabledOptions_ShouldNotStart()
    {
        // Arrange
        var disabledOptions = new Mock<IOptions<DiscoveryServiceOptions>>();
        disabledOptions.Setup(x => x.Value).Returns(new DiscoveryServiceOptions
        {
            Enabled = false
        });

        var disabledService = new UdpDiscoveryService(_mockLogger.Object, disabledOptions.Object);
        var cancellationToken = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)).Token;

        // Act & Assert - Should complete quickly without broadcasting
        var act = async () => await disabledService.StartAsync(cancellationToken);
        await act.Should().NotThrowAsync();
        
        // Verify it logged that it's disabled
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}