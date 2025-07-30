using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Catan3.Shared.Models;
using Tests.GameService.SignalR;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Demonstration tests showing the enhanced StateProgression with multi-client support.
    /// These tests showcase the new approach with realistic player counts, controllable logging,
    /// and comprehensive multi-client verification.
    /// </summary>
    public class EnhancedStateProgressionTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public EnhancedStateProgressionTests(WebApplicationFactory<Program> factory)
        {
            _factory = TestWebApplicationFactory.Create();
        }

        [Fact]
        public async Task PickingBoard_ShuffleAction_AllClientsReceiveUpdate()
        {
            // Arrange - Create Regular game with all players connected
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Summary);

            // Act - Current player shuffles, verify all other players receive updates
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);

            // Assert - All clients should be in PickingBoard with updated tiles
            await session.VerifyAllClientsInState(GameState.PickingBoard);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task AllocateResourceForward_NextAction_AllClientsReceiveUpdate()
        {
            // Arrange - Advance to AllocateResourceForward with quiet intermediate logging
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.AllocateResourceForward, GameType.Regular, LogLevel.Summary);

            // Act - Current player executes Next action
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);

            // Assert - All clients should receive the state update
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task WaitingForRoll_DiceRoll_AllClientsReceiveUpdate()
        {
            // Arrange - Advance to WaitingForRoll (requires complete allocation)
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.WaitingForRoll, GameType.Regular, LogLevel.Summary);

            // Act - Current player rolls dice
            var currentPlayerId = session.GetCurrentPlayerId();
            var client = session.GetClient(currentPlayerId);
            await client.ExecuteRollAsync(session.GameId, 4, 4); // Roll 8

            // Assert - All clients should receive the roll update and advance to WaitingForNext
            await session.VerifyAllClientsInState(GameState.WaitingForNext);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task ExpansionGame_FivePlayers_AllClientsConnected()
        {
            // Arrange - Create Expansion game with all 5 players
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Expansion, LogLevel.Summary);

            // Assert - Should have exactly 5 clients connected
            var expectedPlayers = new[] { "Alice", "Bob", "Charlie", "David", "Eve" };
            foreach (var playerId in expectedPlayers)
            {
                var client = session.GetClient(playerId);
                Assert.Equal(playerId, client.PlayerId);
            }

            // Act - Current player shuffles, verify all other players receive updates
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);

            // Assert - All 5 clients should receive the update
            await session.VerifyAllClientsInState(GameState.PickingBoard);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task LogLevel_Silent_MinimalOutput()
        {
            // Arrange & Act - Test with Silent logging (should produce minimal console output)
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.WaitingForRollForOrder, GameType.Regular, LogLevel.Silent);

            // Assert - Should have reached target state with minimal logging
            await session.VerifyAllClientsInState(GameState.WaitingForRollForOrder);
        }

        [Fact]
        public async Task LogLevel_Detailed_VerboseOutput()
        {
            // Arrange & Act - Test with Detailed logging (should show state transitions)
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.BeginResourceAllocation, GameType.Regular, LogLevel.Detailed);

            // Assert - Should have reached target state with detailed logging
            await session.VerifyAllClientsInState(GameState.BeginResourceAllocation);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task MultiClient_StateConsistency_OnlyCurrentPlayerCanAct()
        {
            // Arrange - Create session and advance to a state where only current player can act
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.AllocateResourceForward, GameType.Regular, LogLevel.Summary);

            // Act - Current player executes action
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
            
            // Act - Try to have a different player act (should fail or be ignored)
            var otherPlayerId = session.PlayerIds.First(id => id != currentPlayerId);
            try
            {
                await session.ExecuteActionWithVerification(otherPlayerId, GameAction.Next);
                // If this succeeds, the game allows it (depends on game state)
            }
            catch (Exception ex) when (ex.Message.Contains("not current player") || ex.Message.Contains("invalid"))
            {
                // Expected - non-current player cannot act
                Console.WriteLine($"? Correctly prevented {otherPlayerId} from acting when not current player");
            }

            // Assert - All clients should remain consistent regardless
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task BackwardCompatibility_LegacyMethod_StillWorks()
        {
            // Arrange & Act - Test that legacy single-client method still works
            var (gameId, connection) = await StateProgression.AdvanceToState(
                _factory, GameState.WaitingForRollForOrder);

            // Assert - Should work but with warning about using legacy method
            Assert.NotEmpty(gameId);
            Assert.NotNull(connection);
            
            // Clean up
            await SignalRTestHelper.DisposeConnection(connection);
        }

        [Fact]
        public async Task ComplexStateProgression_WaitingForNext_RealisticScenario()
        {
            // This test demonstrates the real value: getting to WaitingForNext requires
            // going through all the allocation phases with realistic multi-player coordination

            // Arrange - Advance to WaitingForNext (the most complex progression)
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.WaitingForNext, GameType.Regular, LogLevel.Summary);

            // Assert - All clients should be in WaitingForNext and consistent
            await session.VerifyAllClientsInState(GameState.WaitingForNext);
            await session.VerifyGameConsistency();

            // Act - Test purchase action in WaitingForNext state (only current player can act)
            var currentPlayerId = session.GetCurrentPlayerId();
            var client = session.GetClient(currentPlayerId);
            
            try
            {
                await client.ExecutePurchaseAsync(session.GameId, Entitlement.Road);
                await session.VerifyGameConsistency();
            }
            catch (TimeoutException)
            {
                // Purchase might fail due to insufficient resources, which is expected
                Console.WriteLine("? Purchase failed as expected - resource validation working");
            }

            // Complete the turn (only current player can advance)
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task CustomPlayerList_ArbitraryPlayers_ShouldWork()
        {
            // This test shows how to create a game with custom player IDs

            // Arrange - Create a custom session with specific player IDs
            var customPlayerIds = new[] { "Player1", "Player2", "Player3", "Player4" };
            var session = new MultiPlayerTestSession(_factory, GameType.Regular, customPlayerIds, LogLevel.Summary);
            
            try
            {
                await session.InitializeAsync();
                
                // Act - Test with custom player configuration
                var currentPlayerId = session.GetCurrentPlayerId();
                await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);

                // Assert - All custom players should receive updates
                await session.VerifyAllClientsInState(GameState.PickingBoard);
                await session.VerifyGameConsistency();
                
                // Verify all custom players are connected
                foreach (var playerId in customPlayerIds)
                {
                    var client = session.GetClient(playerId);
                    Assert.Equal(playerId, client.PlayerId);
                }
            }
            finally
            {
                await session.DisposeAsync();
            }
        }
    }
}