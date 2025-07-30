using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Catan3.GameService.Controllers;
using Catan3.Shared.Models;
using Tests.GameService.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Tests.GameService
{
    /// <summary>
    /// Comprehensive tests for the PickingBoard game state via SignalR.
    /// Tests all 4 actions available in PickingBoard state:
    /// 1. Shuffle - Randomize board layout 
    /// 2. Balance - Balance board resources
    /// 3. Undo - Revert to previous board state
    /// 4. Redo - Forward to last board state
    /// 
    /// These tests focus on SignalR functionality and real-time updates that 
    /// the companion interface relies on, rather than detailed board validation.
    /// The old hanging GET tests have been removed in favor of SignalR.
    /// </summary>
    public class PickingBoardTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public PickingBoardTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory; // Use the injected factory instead of creating a new one!
        }

        [Fact]
        public async Task PickingBoard_ShuffleAction_ShouldSucceedViaSignalR()
        {
            // Arrange - Create a Regular game (starts in PickingBoard)
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Summary);

            // Get initial hash
            var initialGameState = session.GetClient("Alice").LastGameState;
            var initialHash = initialGameState?.GameHash;

            // Act - Execute Shuffle action
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);

            // Assert - Verify shuffle completed and hash changed
            var shuffledGameState = session.GetClient("Alice").LastGameState;
            Assert.NotNull(shuffledGameState);
            Assert.Equal(session.GameId, shuffledGameState.GameId);
            Assert.Equal(GameState.PickingBoard, shuffledGameState.GameState);
            Assert.NotEqual(initialHash, shuffledGameState.GameHash); // Hash should change after shuffle
        }

        [Fact]
        public async Task PickingBoard_NextAction_ShouldAdvanceFromPickingBoardViaSignalR()
        {
            // Arrange - Create a Regular game (starts in PickingBoard)
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Summary);

            // Act - Execute Next action to advance past PickingBoard
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);

            // Assert - Should advance to WaitingForRollForOrder
            await session.VerifyAllClientsInState(GameState.WaitingForRollForOrder);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task PickingBoard_UndoAction_ShouldSucceedAfterShuffleViaSignalR()
        {
            // Arrange - Create a Regular game (starts in PickingBoard)
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Summary);

            // First, execute a shuffle to create history
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);

            // Act - Execute Undo action
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Undo);

            // Assert - Should still be in PickingBoard with Redo enabled
            var gameModelAfterUndo = session.GetClient("Alice").LastGameState;
            Assert.NotNull(gameModelAfterUndo);
            Assert.Equal(session.GameId, gameModelAfterUndo.GameId);
            Assert.Equal(GameState.PickingBoard, gameModelAfterUndo.GameState);
            Assert.True(gameModelAfterUndo.ActionFlags.RedoEnabled, "Redo should be enabled after Undo");
        }

        [Fact]
        public async Task PickingBoard_RedoAction_ShouldSucceedAfterUndoViaSignalR()
        {
            // Arrange - Create a Regular game (starts in PickingBoard)
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Summary);

            // Create history: shuffle -> undo -> redo
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Undo);

            // Act - Execute Redo action
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Redo);

            // Assert - Should still be in PickingBoard
            var gameModelAfterRedo = session.GetClient("Alice").LastGameState;
            Assert.NotNull(gameModelAfterRedo);
            Assert.Equal(session.GameId, gameModelAfterRedo.GameId);
            Assert.Equal(GameState.PickingBoard, gameModelAfterRedo.GameState);
        }

        [Fact]
        public async Task PickingBoard_ShuffleAndNext_CompleteWorkflowViaSignalR()
        {
            // Test the complete PickingBoard workflow: Shuffle -> Next -> Advance to next phase
            
            // Arrange - Create a Regular game (starts in PickingBoard)
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Summary);

            var currentPlayerId = session.GetCurrentPlayerId();

            // Act - Execute complete workflow
            // Step 1: Shuffle
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Shuffle);
            await session.VerifyAllClientsInState(GameState.PickingBoard);

            // Step 2: Next (advance to next phase)
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Next);

            // Assert - Should have progressed to WaitingForRollForOrder
            await session.VerifyAllClientsInState(GameState.WaitingForRollForOrder);
            await session.VerifyGameConsistency();
        }

        [Fact]
        public async Task PickingBoard_BalanceAction_ShouldSucceedViaSignalR()
        {
            // Arrange - Create a Regular game (starts in PickingBoard)
            await using var session = await StateProgression.AdvanceToStateWithAllPlayers(
                _factory, GameState.PickingBoard, GameType.Regular, LogLevel.Summary);

            // Act - Execute Balance action
            var currentPlayerId = session.GetCurrentPlayerId();
            await session.ExecuteActionWithVerification(currentPlayerId, GameAction.Balance);

            // Assert - Should still be in PickingBoard
            var balancedGameState = session.GetClient("Alice").LastGameState;
            Assert.NotNull(balancedGameState);
            Assert.Equal(session.GameId, balancedGameState.GameId);
            Assert.Equal(GameState.PickingBoard, balancedGameState.GameState);
        }
    }
}