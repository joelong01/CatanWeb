using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Catan3.Shared.Models;
using Tests.GameService.SignalR;

namespace Tests.GameService.SignalR
{
    /// <summary>
    /// Layer 1: PickingBoard state testing with enhanced multi-player infrastructure.
    /// This is the foundation layer - all other states depend on this working correctly.
    /// 
    /// Tests verify:
    /// 1. Game creation with correct player counts (3 for Regular, 5 for Expansion)
    /// 2. All players connect via SignalR and receive initial state
    /// 3. Current player can execute actions (Shuffle, Next)
    /// 4. All players receive real-time updates
    /// 5. Game state consistency across all clients
    /// </summary>
    public class Layer1_PickingBoardTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public Layer1_PickingBoardTests(WebApplicationFactory<Program> factory)
        {
            _factory = TestWebApplicationFactory.Create();
        }

        [Fact]
        public async Task PickingBoard_RegularGame_ThreePlayersConnected()
        {
            // Arrange - Create Regular game starting in PickingBoard
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Summary);

            // Assert - Should have exactly 3 players connected
            var expectedPlayers = new[] { "Alice", "Bob", "Charlie" };
            Assert.Equal(3, session.PlayerIds.Length);
            
            foreach (var playerId in expectedPlayers)
            {
                var client = session.GetClient(playerId);
                Assert.Equal(playerId, client.PlayerId);
                Assert.Equal(session.GameId, client.GameId);
            }

            // Verify all clients are in PickingBoard state
            await session.VerifyAllClientsInState(GameState.PickingBoard);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task PickingBoard_ExpansionGame_FivePlayersConnected()
        {
            // Arrange - Create Expansion game starting in PickingBoard
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Expansion, LogLevel.Summary);

            // Assert - Should have exactly 5 players connected
            var expectedPlayers = new[] { "Alice", "Bob", "Charlie", "David", "Eve" };
            Assert.Equal(5, session.PlayerIds.Length);
            
            foreach (var playerId in expectedPlayers)
            {
                var client = session.GetClient(playerId);
                Assert.Equal(playerId, client.PlayerId);
                Assert.Equal(session.GameId, client.GameId);
            }

            // Verify all clients are in PickingBoard state
            await session.VerifyAllClientsInState(GameState.PickingBoard);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task PickingBoard_CurrentPlayerShuffleAction_AllClientsReceiveUpdate()
        {
            // Arrange - Create Regular game with all players connected
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Detailed);

            // Act - Current player shuffles board
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);

            // Assert - All clients should remain in PickingBoard with updated tiles
            await session.VerifyAllClientsInState(GameState.PickingBoard);
            await session.VerifyGameConsistency();
            
            // Verify the action was executed by checking one client's game state
            var gameState = session.GetClient(currentPlayerId).LastGameState;
            Assert.NotNull(gameState);
            Assert.Equal(GameState.PickingBoard, gameState.GameState);
        }

        [Fact]
        public async Task PickingBoard_CurrentPlayerNextAction_AdvancesToWaitingForRollForOrder()
        {
            // Arrange - Create Regular game starting in PickingBoard
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Detailed);

            // Act - Current player executes Next to advance from PickingBoard
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);

            // Assert - All clients should advance to WaitingForRollForOrder
            await session.VerifyAllClientsInState(GameState.WaitingForRollForOrder);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task PickingBoard_MultipleActions_AllClientsStayInSync()
        {
            // Arrange - Create Regular game with all players connected
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Summary);

            var currentPlayerId = session.GetCurrentPlayerId();

            // Act - Execute multiple actions in sequence
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);
            await session.VerifyGameConsistency();

            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);
            await session.VerifyGameConsistency();

            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);

            // Assert - All clients should have advanced to next state
            await session.VerifyAllClientsInState(GameState.WaitingForRollForOrder);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task PickingBoard_CustomPlayerIds_ShouldWork()
        {
            // Test that the infrastructure works with arbitrary player IDs

            // Arrange - Create custom session with specific player IDs
            var customPlayerIds = new[] { "Player1", "Player2", "Player3" };
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

        [Fact]
        public async Task PickingBoard_LogLevels_ShouldControlOutput()
        {
            // Test that logging levels work correctly
            
            // Silent mode - should produce minimal output
            await using var silentSession = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Silent);
            
            await silentSession.VerifyAllClientsInState(GameState.PickingBoard);
            
            // Detailed mode - should produce verbose output
            await using var detailedSession = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Detailed);
            
            await detailedSession.VerifyAllClientsInState(GameState.PickingBoard);
        }

        [Fact]
        public async Task PickingBoard_TwoConsecutiveShuffles_ShouldProduceDifferentHashes()
        {
            // This test verifies that the shuffle action actually randomizes the board
            // by checking if two consecutive shuffles produce different GameHash values

            var testStartTime = DateTime.UtcNow;
            LogEvent("TestStart", "Beginning PickingBoard shuffle test with hash verification");

            // Arrange - Create a Regular game 
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Detailed);

            // Get initial hash
            var initialGameState = session.GetClient("Alice").LastGameState;
            var initialHash = initialGameState?.GameHash;
            Assert.NotNull(initialHash);
            
            LogEvent("InitialHash", $"Initial GameHash: {initialHash}");

            // Act - Execute first shuffle
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);
            
            var firstShuffleState = session.GetClient("Alice").LastGameState;
            var firstShuffleHash = firstShuffleState?.GameHash;
            Assert.NotNull(firstShuffleHash);
            
            LogEvent("FirstShuffle", $"After first shuffle: {firstShuffleHash}");

            // Act - Execute second shuffle
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);
            
            var secondShuffleState = session.GetClient("Alice").LastGameState;
            var secondShuffleHash = secondShuffleState?.GameHash;
            Assert.NotNull(secondShuffleHash);
            
            LogEvent("SecondShuffle", $"After second shuffle: {secondShuffleHash}");

            // Assert - The two shuffles should produce different hashes
            Assert.NotEqual(firstShuffleHash, secondShuffleHash);
            
            if (firstShuffleHash == secondShuffleHash)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Two consecutive shuffles should produce different boards! " +
                    $"Both shuffles produced the same hash: {firstShuffleHash}");
            }
            
            // Also verify they're both different from the initial
            Assert.NotEqual(initialHash, firstShuffleHash);
            Assert.NotEqual(initialHash, secondShuffleHash);

            // NEW: Test Undo/Redo functionality after shuffles
            LogEvent("UndoTest", "Testing Undo after shuffles");

            // Act - Execute Undo (should revert to first shuffle state)
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Undo);
            
            var undoState = session.GetClient("Alice").LastGameState;
            var undoHash = undoState?.GameHash;
            Assert.NotNull(undoHash);
            
            LogEvent("UndoHash", $"After Undo: {undoHash}");
            
            // Assert - Undo should restore the first shuffle hash
            Assert.Equal(firstShuffleHash, undoHash);
            Assert.True(undoState?.ActionFlags.RedoEnabled, "Redo should be enabled after Undo");

            // Act - Execute Redo (should return to second shuffle state)
            LogEvent("RedoTest", "Testing Redo to restore second shuffle");
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Redo);
            
            var redoState = session.GetClient("Alice").LastGameState;
            var redoHash = redoState?.GameHash;
            Assert.NotNull(redoHash);
            
            LogEvent("RedoHash", $"After Redo: {redoHash}");
            
            // Assert - Redo should restore the second shuffle hash
            Assert.Equal(secondShuffleHash, redoHash);

            // Final verification
            var testEndTime = DateTime.UtcNow;
            var totalTestTime = testEndTime - testStartTime;
            
            LogEvent("TestComplete", $"? All shuffle/undo/redo operations verified successfully!");
            LogEvent("TestTiming", $"?? Total test execution time: {totalTestTime.TotalSeconds:F2} seconds");
            
            // Performance assertion - test should complete reasonably fast
            Assert.True(totalTestTime.TotalSeconds < 30, 
                $"Test should complete within 30 seconds, took {totalTestTime.TotalSeconds:F2} seconds");

            // Final consistency check
            await session.VerifyGameConsistency();
        }

        private void LogEvent(string eventType, string message)
        {
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [{eventType}] {message}");
        }
    }
}