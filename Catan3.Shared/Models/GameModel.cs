using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Catan3.Shared.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Shared.Models
{
    public class ActionFlags
    {
        public bool UndoEnabled { get; set; } = false;
        public bool RedoEnabled { get; set; } = false;
        public bool NextEnabled { get; set; } = false;
        public bool RollsEnabled { get; set; } = false;

        public override string ToString()
        {
            return $"UndoEnabled: {UndoEnabled} RedoEnabled={RedoEnabled} NextEnabled={NextEnabled}";
        }
    }

    public class GameModel
    {
        /// <summary>
        /// What kinds of things can be purchased in this game and if they are allowed to be purchased at this time?
        /// </summary>
        public List<EntitlementPurchaseModel> EntitlementPurchaseModel { get; set; } = new();

        public ActionFlags ActionFlags { get; set; } = new();

        public GameType GameType { get; set; } = GameType.Regular;

        public GameState GameState { get; set; } = GameState.WaitingForNewGame;

        public bool HasSupplementalBuildPhase { get; set; } = false;

        public List<PlayerModel> Players { get; set; } = new();

        public List<TileModel> Tiles { get; set; } = new();

        public List<BuildingModel> Buildings { get; set; } = new();

        public List<RoadModel> Roads { get; set; } = new();

        public List<HarborModel> Harbors { get; set; } = new();

        public RobberModel Robber { get; set; } = new();

        public HouseRules HouseRules { get; set; } = new();

        public ResourceRules ResourceRules { get; set; } = new();

        public string CurrentPlayerId { get; set; } = string.Empty;

        public RollModel RollModel { get; set; } = new();

        // keep track of the player who goes when there is nobody left to do supplemental
        public string NextPlayerToRollAfterSupplemental { get; set; } = "";

        // keep track of the total resources ever generated in the game by everyone
        public ResourcesModel GameResourcesModel { get; set; } = new();

        public GameState PreviousGameState { get; set; } = GameState.Uninitialized;

        public GameModel(IGameMetadata gameInfo, List<PlayerModel> players)
        {
            Players = players;
            GameType = gameInfo.HouseRules != null ? GameType.Regular : GameType.Regular; // Simplified for now
            HasSupplementalBuildPhase = false; // Simplified for now
            ResourceRules = gameInfo.ResourceRules;
            HouseRules = gameInfo.HouseRules ?? throw new GameException("House Rules cannot be null!");
            CurrentPlayerId = players.Count > 0 ? players[0].Id : string.Empty;
            EntitlementPurchaseModel.AddRange(GetDefaultPurchaseableEntitlements());
        }

        public GameModel()
        {
            Players = new();
            GameType = GameType.Regular;
            HasSupplementalBuildPhase = false;
            ResourceRules = new ResourceRules();
        }

        /// <summary>
        /// Add up all the stars for the given resource type
        /// </summary>
        /// <param name="tileType"></param>
        /// <returns></returns>
        public int StarCount(ResourceType tileType)
        {
            var total = this.Tiles.Where(tile => tile.ResourceTileType == tileType)
                    .Sum(tile => tile.Stars);
            return total;
        }

        public override string ToString()
        {
            return $"State={GameState} CurrentPlayer={CurrentPlayerId}";
        }

        // Helper methods
        public PlayerModel CurrentPlayer()
        {
            return Players.FirstOrDefault(p => p.Id == CurrentPlayerId) ?? Players.FirstOrDefault() ?? new PlayerModel("default");
        }

        public string NextPlayerId(string currentPlayerId, int direction)
        {
            var currentIndex = Players.FindIndex(p => p.Id == currentPlayerId);
            if (currentIndex == -1) return Players.FirstOrDefault()?.Id ?? string.Empty;
            
            var nextIndex = (currentIndex + direction) % Players.Count;
            if (nextIndex < 0) nextIndex = Players.Count - 1;
            
            return Players[nextIndex].Id;
        }

        public void ChangePlayer(int direction)
        {
            CurrentPlayerId = NextPlayerId(CurrentPlayerId, direction);
        }

        public void ChangePlayerTo(string playerId)
        {
            if (Players.Any(p => p.Id == playerId))
            {
                CurrentPlayerId = playerId;
            }
        }

        public EntitlementPurchaseModel PurchaseModel(Entitlement entitlement)
        {
            return EntitlementPurchaseModel.FirstOrDefault(e => e.Entitlement == entitlement) 
                ?? new EntitlementPurchaseModel { Entitlement = entitlement, Enabled = false };
        }

        public GamePhase Phase()
        {
            return GameState switch
            {
                GameState.AllocateResourceForward or GameState.AllocateResourceReverse => GamePhase.PickingResources,
                GameState.PickingBoard => GamePhase.PickingBoard,
                GameState.WaitingForRoll => GamePhase.Rolling,
                GameState.WaitingForNext or GameState.Supplemental => GamePhase.Purchase,
                _ => GamePhase.Unspecified
            };
        }

        public bool ValidateGame()
        {
            // Simplified validation for now
            return Players.Count > 0 && !string.IsNullOrEmpty(CurrentPlayerId);
        }

        public void Shuffle()
        {
            // Simplified shuffle implementation
            var random = new Random();
            for (int i = Tiles.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (Tiles[i], Tiles[j]) = (Tiles[j], Tiles[i]);
            }
        }

        public HarborModel? FindAdjacentHarbor(BuildingKey buildingKey)
        {
            // Simplified implementation
            return Harbors.FirstOrDefault();
        }

        public List<TileModel> TilesForBuildings(BuildingKey buildingKey)
        {
            // Simplified implementation - return empty list for now
            return new List<TileModel>();
        }

        private List<EntitlementPurchaseModel> GetDefaultPurchaseableEntitlements()
        {
            return new List<EntitlementPurchaseModel>
            {
                new() { Entitlement = Entitlement.Settlement, Enabled = true },
                new() { Entitlement = Entitlement.Road, Enabled = true },
                new() { Entitlement = Entitlement.City, Enabled = true },
                new() { Entitlement = Entitlement.Soldier, Enabled = true }
            };
        }
    }

   
}