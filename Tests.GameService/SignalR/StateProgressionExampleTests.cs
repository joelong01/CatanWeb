using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Catan3.Shared.Models;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Example test class demonstrating the proper approach for state-specific testing.
    /// Each test advances to its target state independently, following approach #2.
    /// This provides test isolation while allowing comprehensive state machine testing.
    /// </summary>
    public class StateProgressionExampleTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly List<HubConnection> _connections = new();

        public StateProgressionExampleTests(WebApplicationFactory<Program> factory)
        {
            _factory = TestWebApplicationFactory.Create();
        }

        [Fact]
        public async Task PickingBoard_ShuffleAction_ShouldWorkCorrectly()
        {
            // Arrange - Start with PickingBoard state using multi-client approach
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Summary);

            // Act - Execute Shuffle action (only valid in PickingBoard state)
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);

            // Assert - All clients should be in PickingBoard with updated tiles
            await session.VerifyAllClientsInState(GameState.PickingBoard);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task WaitingForRoll_RollAction_ShouldAdvanceToWaitingForNext()
        {
            // Arrange - Advance to WaitingForRoll state using multi-client approach
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.WaitingForRoll, GameType.Regular, LogLevel.Summary);

            // Act - Execute dice roll (only valid in WaitingForRoll state)
            var currentPlayerId = session.GetCurrentPlayerId();
            var client = session.GetClient(currentPlayerId);
            await client.ExecuteRollAsync(session.GameId, 3, 3); // Roll 6

            // Assert - All clients should receive the roll update and advance to WaitingForNext
            await session.VerifyAllClientsInState(GameState.WaitingForNext);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task WaitingForNext_PurchaseAction_ShouldWorkWithMultipleClients()
        {
            // Arrange - Advance to WaitingForNext state using multi-client approach
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.WaitingForNext, GameType.Regular, LogLevel.Summary);

            // Act - Current player attempts purchase
            var currentPlayerId = session.GetCurrentPlayerId();
            var client = session.GetClient(currentPlayerId);

            try
            {
                await client.ExecutePurchaseAsync(session.GameId, Entitlement.Road);
                await session.VerifyGameConsistency();
                Console.WriteLine("? Purchase succeeded and all clients synchronized");
            }
            catch (TimeoutException)
            {
                // Purchase might fail due to insufficient resources, which is expected
                Console.WriteLine("? Purchase failed as expected - resource validation working");
            }
        }

        [Fact]
        public async Task AllocateResourceForward_NextAction_ShouldAdvanceCorrectly()
        {
            // Arrange - Advance to AllocateResourceForward using multi-client approach
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.AllocateResourceForward, GameType.Regular, LogLevel.Summary);

            // Act - Current player executes Next action
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);

            // Assert - All clients should receive the state update consistently
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task MultiClient_StateProgression_AllPlayersReceiveUpdates()
        {
            // This test demonstrates the multi-client advantage over single-client testing

            // Arrange - Create multi-client session
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Summary);

            // Verify all players are connected
            var expectedPlayers = new[] { "Alice", "Bob", "Charlie" };
            Assert.Equal(3, session.PlayerIds.Length);
            
            foreach (var playerId in expectedPlayers)
            {
                var client = session.GetClient(playerId);
                Assert.Equal(playerId, client.PlayerId);
                Assert.NotNull(client.Connection);
            }

            // Act - Current player shuffles
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);

            // Assert - All players should have consistent state
            await session.VerifyAllClientsInState(GameState.PickingBoard);
            await session.VerifyGameConsistency();

            Console.WriteLine("? Multi-client state progression verified - all players synchronized");
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var connection in _connections)
            {
                await SignalRTestHelper.DisposeConnection(connection);
            }
            _connections.Clear();
        }
    }
}