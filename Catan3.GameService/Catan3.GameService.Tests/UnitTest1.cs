using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.GameService.Controllers;
using FluentAssertions;

namespace Catan3.GameService.Tests;

public class GameStateMachineTests
{
    private readonly GameStateMachine _gameStateMachine;
    private readonly List<string> _testPlayerIds;

    public GameStateMachineTests()
    {
        _gameStateMachine = new GameStateMachine();
        _testPlayerIds = new List<string> { "player1", "player2", "player3", "player4" };
    }

    [Fact]
    public void HandleNewGame_ShouldCreateValidGameModel()
    {
        // Arrange
        var newGameMessage = new NewGameMessage(GameType.Regular, _testPlayerIds);

        // Act
        var result = _gameStateMachine.HandleNewGame(newGameMessage);

        // Assert
        result.Should().NotBeNull();
        result.Players.Should().HaveCount(4);
        result.Players.Select(p => p.Id).Should().BeEquivalentTo(_testPlayerIds);
        result.GameState.Should().Be(Catan3.Shared.Models.GameState.WaitingForNewGame);
        result.CurrentPlayerId.Should().NotBeEmpty();
        result.ActionFlags.Should().NotBeNull();
    }

    [Fact]
    public void HandleDoAction_Next_ShouldAdvanceGameState()
    {
        // Arrange - Start with a new game
        var newGameMessage = new NewGameMessage(GameType.Regular, _testPlayerIds);
        var initialGame = _gameStateMachine.HandleNewGame(newGameMessage);
        
        var nextAction = new DoAction(GameAction.Next);

        // Act
        var result = _gameStateMachine.HandleDoAction(nextAction);

        // Assert
        result.Should().NotBeNull();
        result.GameState.Should().NotBe(initialGame.GameState); // Should have changed
    }

    [Fact]
    public void HandleDoAction_Undo_ShouldRevertPreviousAction()
    {
        // Arrange - Start game and perform an action
        var newGameMessage = new NewGameMessage(GameType.Regular, _testPlayerIds);
        var initialGame = _gameStateMachine.HandleNewGame(newGameMessage);
        
        // Perform an action first
        var nextAction = new DoAction(GameAction.Next);
        var afterNext = _gameStateMachine.HandleDoAction(nextAction);
        
        var undoAction = new DoAction(GameAction.Undo);

        // Act
        var result = _gameStateMachine.HandleDoAction(undoAction);
