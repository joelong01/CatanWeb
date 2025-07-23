using Xunit;
using Catan3.GameService.Factory;
using Catan3.Shared.Models;

namespace Tests.GameService
{
    public class GameFactoryTests
    {
        [Fact]
        public void CreateGame_WithValidRegularGameSetup_ShouldCreateValidGame()
        {
            // Arrange
            var playerIds = new List<string> { "player1", "player2", "player3", "player4" };
            var gameType = GameType.Regular;

            // Act
            var game = GameFactory.CreateGame(gameType, playerIds);

            // Assert
            Assert.NotNull(game);
            Assert.Equal(gameType, game.GameType);
            Assert.Equal(4, game.Players.Count);
            Assert.Equal("player1", game.CurrentPlayerId); // First player should be current
            
            // Verify all players were created correctly
            for (int i = 0; i < playerIds.Count; i++)
            {
                Assert.Equal(playerIds[i], game.Players[i].Id);
            }

            // Verify game components are initialized
            Assert.NotNull(game.Tiles);
            Assert.NotNull(game.Buildings);
            Assert.NotNull(game.Roads);
            Assert.NotNull(game.Harbors);
            Assert.NotNull(game.ActionFlags);
            Assert.NotNull(game.GameResourcesModel);
            Assert.NotNull(game.RollModel);
            Assert.NotNull(game.Robber);

            // Regular Catan should have 19 tiles
            Assert.Equal(19, game.Tiles.Count);
            
            // Should have buildings for each tile (6 positions per tile)
            Assert.True(game.Buildings.Count > 0);
            
            // Should have roads for each tile (6 sides per tile)
            Assert.True(game.Roads.Count > 0);

            // Verify game validation passes
            Assert.True(game.ValidateGame());
        }

        [Fact]
        public void CreateGame_WithTooFewPlayers_ShouldThrowException()
        {
            // Arrange
            var playerIds = new List<string> { "player1" }; // Only 1 player, need at least 2
            var gameType = GameType.Regular;

            // Act & Assert
            var exception = Assert.Throws<Exception>(() => GameFactory.CreateGame(gameType, playerIds));
            Assert.Contains("must have players between", exception.Message);
        }

        [Fact]
        public void CreateGame_WithTooManyPlayers_ShouldThrowException()
        {
            // Arrange
            var playerIds = new List<string> { "p1", "p2", "p3", "p4", "p5", "p6", "p7", "p8" }; // Too many players
            var gameType = GameType.Regular;

            // Act & Assert
            var exception = Assert.Throws<Exception>(() => GameFactory.CreateGame(gameType, playerIds));
            Assert.Contains("must have players between", exception.Message);
        }

        [Fact]
        public void CreateGame_WithExpansionType_ShouldCreateExpansionGame()
        {
            // Arrange
            var playerIds = new List<string> { "player1", "player2", "player3", "player4" };
            var gameType = GameType.Expansion;

            // Act
            var game = GameFactory.CreateGame(gameType, playerIds);

            // Assert
            Assert.Equal(GameType.Expansion, game.GameType);
            Assert.Equal(30, game.Tiles.Count); // Expansion board should have 30 tiles (vs 19 for regular)
            Assert.True(game.HasSupplementalBuildPhase); // Expansion should have supplemental phases
            
            // Expansion should have more entitlements than regular game
            var regularGame = GameFactory.CreateGame(GameType.Regular, playerIds);
            Assert.True(game.EntitlementPurchaseModel.Count >= regularGame.EntitlementPurchaseModel.Count);
            
            // Verify the larger board still validates correctly
            Assert.True(game.ValidateGame());
        }

        [Fact]
        public void ValidateGame_WithValidBoard_ShouldReturnTrue()
        {
            // Arrange
            var playerIds = new List<string> { "player1", "player2", "player3" };
            var game = GameFactory.CreateGame(GameType.Regular, playerIds);

            // Act
            var isValid = game.ValidateGame();

            // Assert
            Assert.True(isValid, "A freshly created game should pass validation");
        }

        [Fact]
        public void Shuffle_ShouldMaintainGameIntegrity()
        {
            // Arrange
            var playerIds = new List<string> { "player1", "player2", "player3" };
            var game = GameFactory.CreateGame(GameType.Regular, playerIds);
            
            var originalTileCount = game.Tiles.Count;
            var originalBuildingCount = game.Buildings.Count;
            var originalRoadCount = game.Roads.Count;
            var originalHarborCount = game.Harbors.Count;

            // Act
            game.Shuffle();

            // Assert
            // Counts should remain the same
            Assert.Equal(originalTileCount, game.Tiles.Count);
            Assert.Equal(originalBuildingCount, game.Buildings.Count);
            Assert.Equal(originalRoadCount, game.Roads.Count);
            Assert.Equal(originalHarborCount, game.Harbors.Count);
            
            // Game should still be valid after shuffling
            Assert.True(game.ValidateGame());

            // Should have correct number of desert tiles with number 7
            var desertTiles = game.Tiles.Where(t => t.ResourceTileType == ResourceType.Desert).ToList();
            var sevenTiles = game.Tiles.Where(t => t.Number == 7).ToList();
            Assert.Equal(desertTiles.Count, sevenTiles.Count);
            
            // All desert tiles should have number 7
            foreach (var desert in desertTiles)
            {
                Assert.Equal(7, desert.Number);
            }
        }
    }
}
