using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Catan3.Shared.Models;
using Tests.GameService.SignalR;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Layer 1: RollForOrder state testing with enhanced multi-player infrastructure.
    /// This tests the player order determination functionality that follows PickingBoard.
    /// 
    /// Tests verify:
    /// 1. Game progression from PickingBoard ? WaitingForRollForOrder ? FinishedRollOrder
    /// 2. All players connect via SignalR and receive order updates
    /// 3. SetPlayerOrder functionality with Charlie going first
    /// 4. Player order is preserved: Charlie, Alice, Bob
    /// 5. Current player changes to Charlie after order set
    /// 6. Game state consistency across all clients
    /// 7. Progression to next state (BeginResourceAllocation)
    /// </summary>
    public class Layer1_RollForOrderTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public Layer1_RollForOrderTests(WebApplicationFactory<Program> factory)
        {
            _factory = TestWebApplicationFactory.Create();
        }

        [Fact]
        public async Task RollForOrder_SetCharlieFirst_CompleteWorkflowWithTiming()
        {
            // This test follows the complete workflow: progress to FinishedRollOrder,
            // set Charlie as first player, verify order is Charlie ? Alice ? Bob,
            // advance to next state, and report total timing

            var testStartTime = DateTime.UtcNow;
            LogEvent("TestStart", "Beginning RollForOrder workflow test - Charlie goes first");

            // Arrange - Create a Regular game and advance to FinishedRollOrder
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.FinishedRollOrder, GameType.Regular, LogLevel.Detailed);

            // Verify we have exactly 3 players and are in correct state
            var expectedPlayers = new[] { "Alice", "Bob", "Charlie" };
            Assert.Equal(3, session.PlayerIds.Length);
            
            foreach (var playerId in expectedPlayers)
            {
                var client = session.GetClient(playerId);
                Assert.Equal(playerId, client.PlayerId);
                Assert.Equal(session.GameId, client.GameId);
            }

            // Verify all clients are in FinishedRollOrder state
            await session.VerifyAllClientsInState(GameState.FinishedRollOrder);
            await session.VerifyGameConsistency();

            // Get initial state and verify original order
            var initialGameState = session.GetClient("Alice").LastGameState;
            Assert.NotNull(initialGameState);
            Assert.Equal(GameState.FinishedRollOrder, initialGameState.GameState);
            
            // Original order should be Alice, Bob, Charlie (creation order)
            var initialPlayerOrder = initialGameState.Players.Select(p => p.Id).ToList();
            var initialCurrentPlayer = initialGameState.CurrentPlayerId;
            
            LogEvent("InitialOrder", $"Initial player order: {string.Join(", ", initialPlayerOrder)}");
            LogEvent("InitialCurrent", $"Initial current player: {initialCurrentPlayer}");
            
            Assert.Equal(new List<string> { "Alice", "Bob", "Charlie" }, initialPlayerOrder);
            Assert.Equal("Alice", initialCurrentPlayer);

            // Act - Set Charlie as the first player
            // According to Catan rules: if Charlie goes first, order becomes Charlie, Alice, Bob
            LogEvent("SetPlayerOrder", "Setting Charlie as first player - new order: Charlie, Alice, Bob");
            
            var newPlayerOrder = new List<string> { "Charlie", "Alice", "Bob" };
            var setPlayerOrderMessage = new SetPlayerOrderMessage(newPlayerOrder);
            
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.GetClient(currentPlayerId).Connection.InvokeAsync(
                "ExecuteSetPlayerOrder", session.GameId, currentPlayerId, setPlayerOrderMessage);

            // Verify all clients received the order change
            await session.VerifyAllClientsReceivedUpdate();
            
            var orderSetGameState = session.GetClient("Alice").LastGameState;
            Assert.NotNull(orderSetGameState);
            
            var updatedPlayerOrder = orderSetGameState.Players.Select(p => p.Id).ToList();
            var updatedCurrentPlayer = orderSetGameState.CurrentPlayerId;
            
            LogEvent("UpdatedOrder", $"Updated player order: {string.Join(", ", updatedPlayerOrder)}");
            LogEvent("UpdatedCurrent", $"Updated current player: {updatedCurrentPlayer}");
            
            // Assert - Verify Charlie is now first and current player
            Assert.Equal(newPlayerOrder, updatedPlayerOrder);
            Assert.Equal("Charlie", updatedCurrentPlayer);
            Assert.Equal(GameState.FinishedRollOrder, orderSetGameState.GameState); // Still in same state
            
            // Verify game consistency after order change
            await session.VerifyGameConsistency();

            // Act - Advance to next state (BeginResourceAllocation)
            LogEvent("NextState", "Advancing to next state to verify order preservation");
            
            var newCurrentPlayerId = session.GetCurrentPlayerId(); // Should now be Charlie
            Assert.Equal("Charlie", newCurrentPlayerId);
            
            await session.ExecuteActionWithVerification(newCurrentPlayerId, GameAction.Next);

            // Assert - Verify progression to BeginResourceAllocation with preserved order
            await session.VerifyAllClientsInState(GameState.BeginResourceAllocation);
            await session.VerifyGameConsistency();
            
            var finalGameState = session.GetClient("Alice").LastGameState;
            Assert.NotNull(finalGameState);
            
            var finalPlayerOrder = finalGameState.Players.Select(p => p.Id).ToList();
            var finalCurrentPlayer = finalGameState.CurrentPlayerId;
            
            LogEvent("FinalOrder", $"Final player order: {string.Join(", ", finalPlayerOrder)}");
            LogEvent("FinalCurrent", $"Final current player: {finalCurrentPlayer}");
            
            // Assert - Order should be preserved and Charlie should still be current
            Assert.Equal(GameState.BeginResourceAllocation, finalGameState.GameState);
            Assert.Equal(newPlayerOrder, finalPlayerOrder); // Order preserved: Charlie, Alice, Bob
            Assert.Equal("Charlie", finalCurrentPlayer); // Charlie remains current player

            // Final verification
            var testEndTime = DateTime.UtcNow;
            var totalTestTime = testEndTime - testStartTime;
            
            LogEvent("TestComplete", $"? RollForOrder workflow completed successfully!");
            LogEvent("TestTiming", $"?? Total test execution time: {totalTestTime.TotalSeconds:F2} seconds");
            LogEvent("OrderResult", $"? Player order correctly set to: {string.Join(" ? ", finalPlayerOrder)}");
            LogEvent("CurrentPlayerResult", $"? Charlie is now the current player and ready to begin allocation");
            
            // Performance assertion - test should complete reasonably fast
            Assert.True(totalTestTime.TotalSeconds < 45, 
                $"Test should complete within 45 seconds, took {totalTestTime.TotalSeconds:F2} seconds");

            // Final consistency check
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task RollForOrder_RegularGame_ThreePlayersInFinishedRollOrder()
        {
            // Arrange - Create Regular game and advance to FinishedRollOrder
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.FinishedRollOrder, GameType.Regular, LogLevel.Summary);

            // Assert - Should have exactly 3 players connected
            var expectedPlayers = new[] { "Alice", "Bob", "Charlie" };
            Assert.Equal(3, session.PlayerIds.Length);
            
            foreach (var playerId in expectedPlayers)
            {
                var client = session.GetClient(playerId);
                Assert.Equal(playerId, client.PlayerId);
                Assert.Equal(session.GameId, client.GameId);
            }

            // Verify all clients are in FinishedRollOrder state
            await session.VerifyAllClientsInState(GameState.FinishedRollOrder);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task RollForOrder_ExpansionGame_FivePlayersInFinishedRollOrder()
        {
            // Arrange - Create Expansion game and advance to FinishedRollOrder
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.FinishedRollOrder, GameType.Expansion, LogLevel.Summary);

            // Assert - Should have exactly 5 players connected
            var expectedPlayers = new[] { "Alice", "Bob", "Charlie", "David", "Eve" };
            Assert.Equal(5, session.PlayerIds.Length);
            
            foreach (var playerId in expectedPlayers)
            {
                var client = session.GetClient(playerId);
                Assert.Equal(playerId, client.PlayerId);
                Assert.Equal(session.GameId, client.GameId);
            }

            // Verify all clients are in FinishedRollOrder state
            await session.VerifyAllClientsInState(GameState.FinishedRollOrder);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task RollForOrder_SetMiddlePlayerFirst_ShouldUpdateOrderCorrectly()
        {
            // Test setting Bob (middle player) as first
            // Original: Alice, Bob, Charlie
            // Expected: Bob, Charlie, Alice

            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.FinishedRollOrder, GameType.Regular, LogLevel.Summary);

            // Set Bob as first player
            var newPlayerOrder = new List<string> { "Bob", "Charlie", "Alice" };
            var setPlayerOrderMessage = new SetPlayerOrderMessage(newPlayerOrder);
            
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.GetClient(currentPlayerId).Connection.InvokeAsync(
                "ExecuteSetPlayerOrder", session.GameId, currentPlayerId, setPlayerOrderMessage);

            // Verify all clients received the order change
            await session.VerifyAllClientsReceivedUpdate();
            await session.VerifyGameConsistency();
            
            var gameState = session.GetClient("Alice").LastGameState;
            Assert.NotNull(gameState);
            
            var actualPlayerOrder = gameState.Players.Select(p => p.Id).ToList();
            var currentPlayer = gameState.CurrentPlayerId;
            
            Assert.Equal(newPlayerOrder, actualPlayerOrder);
            Assert.Equal("Bob", currentPlayer);
        }

        [Fact]
        public async Task RollForOrder_NextAction_ShouldAdvanceToBeginResourceAllocation()
        {
            // Arrange - Create Regular game in FinishedRollOrder
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.FinishedRollOrder, GameType.Regular, LogLevel.Detailed);

            // Act - Current player executes Next to advance from FinishedRollOrder
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);

            // Assert - All clients should advance to BeginResourceAllocation
            await session.VerifyAllClientsInState(GameState.BeginResourceAllocation);
            await session.VerifyGameConsistency();
        }

        private void LogEvent(string eventType, string message)
        {
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [{eventType}] {message}");
        }
    }
}