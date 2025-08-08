using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Catan3.Shared.Extensions;
using Catan3.Shared.Utility;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Catan3.Shared.Models
{
    /// <summary>
    /// Represents flags for various game actions.
    /// Supports both plain object usage (for JSON/API) and MVVM usage (for UI data binding).
    /// </summary>
    public partial class ActionFlags : ObservableObject
    {
        [ObservableProperty]
        public partial bool UndoEnabled { get; set; } = false;

        [ObservableProperty]
        public partial bool RedoEnabled { get; set; } = false;

        [ObservableProperty]
        public partial bool NextEnabled { get; set; } = false;

        [ObservableProperty]
        public partial bool RollsEnabled { get; set; } = false;

        public override string ToString()
        {
            return $"UndoEnabled: {UndoEnabled} RedoEnabled={RedoEnabled} NextEnabled={NextEnabled}";
        }
    }

    /// <summary>
    /// Represents the main game model containing all game state.
    /// Supports both plain object usage (for JSON/API) and MVVM usage (for UI data binding).
    /// </summary>
    public partial class GameModel : ObservableObject
    {
        /// <summary>
        /// What kinds of things can be purchased in this game and if they are allowed to be purchased at this time?
        /// </summary>
        [ObservableProperty]
        public partial List<EntitlementPurchaseModel> EntitlementPurchaseModel { get; set; } = new();

        [ObservableProperty]
        public partial ActionFlags ActionFlags { get; set; } = new();

        [ObservableProperty]
        public partial GameType GameType { get; set; } = GameType.Regular;

        [ObservableProperty]
        public partial GameState GameState { get; set; } = GameState.WaitingForNewGame;

        [ObservableProperty]
        public partial bool HasSupplementalBuildPhase { get; set; } = false;

        [ObservableProperty]
        public partial List<PlayerModel> Players { get; set; } = new();

        [ObservableProperty]
        public partial List<TileModel> Tiles { get; set; } = new();

        [ObservableProperty]
        public partial List<BuildingModel> Buildings { get; set; } = new();

        [ObservableProperty]
        public partial List<RoadModel> Roads { get; set; } = new();

        [ObservableProperty]
        public partial List<HarborModel> Harbors { get; set; } = new();

        [ObservableProperty]
        public partial RobberModel Robber { get; set; } = new();

        [ObservableProperty]
        public partial HouseRules HouseRules { get; set; } = new();

        [ObservableProperty]
        public partial ResourceRules ResourceRules { get; set; } = new();

        [ObservableProperty]
        public partial string CurrentPlayerId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique identifier for this game instance.
        /// This field supports Rule 7 (Single Source of Truth) by ensuring GameModel contains all game metadata.
        /// </summary>
        [ObservableProperty]
        public partial string GameId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets when this game was created.
        /// This field supports Rule 7 (Single Source of Truth) by ensuring GameModel contains all game metadata.
        /// </summary>
        [ObservableProperty]
        public partial DateTime CreatedTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the software version number of the GameStateMachine.
        /// This field represents the software version (hardcoded to 1) and does NOT change during gameplay.
        /// Version represents GameStateMachine software compatibility, not game state changes.
        /// This is used for client/server compatibility checks, not hanging GET version comparison.
        /// </summary>
        public int GameStateMachineVersion { get; } = 1;

        /// <summary>
        /// Gets or sets a hash representing the current state of the game board and player data.
        /// This hash includes Tiles, Players, Harbors, Roads, Buildings, GameState, CurrentPlayerId, and Robber.
        /// Used for fast verification that all clients have identical game states in multi-player testing.
        /// The hash is computed by the GameStateMachine whenever the game state changes.
        /// </summary>
        [ObservableProperty]
        public partial string GameHash { get; set; } = string.Empty;

        [ObservableProperty]
        public partial RollModel RollModel { get; set; } = new();

        // keep track of the player who goes when there is nobody left to do supplemental
        [ObservableProperty]
        public partial string NextPlayerToRollAfterSupplemental { get; set; } = "";

        // keep track of the total resources ever generated in the game by everyone
        [ObservableProperty]
        public partial ResourcesModel GameResourcesModel { get; set; } = new();

        [ObservableProperty]
        public partial GameState PreviousGameState { get; set; } = GameState.Uninitialized;

        public GameModel(IGameMetadata gameInfo, List<PlayerModel> players)
        {
            Players = players;
            GameType = gameInfo.HouseRules != null ? GameType.Regular : GameType.Regular; // Simplified for now
            HasSupplementalBuildPhase = false; // Simplified for now
            ResourceRules = gameInfo.ResourceRules;
            HouseRules = gameInfo.HouseRules ?? throw new GameException("House Rules cannot be null!");
            CurrentPlayerId = players.Count > 0 ? players[0].Id : string.Empty;
            EntitlementPurchaseModel.AddRange(GetDefaultPurchaseableEntitlements());
            
            // Initialize new fields for Rule 7 compliance
            CreatedTime = DateTime.UtcNow;
            GameStateMachineVersion = 1;
            // GameHash will be computed later by GameStateMachine when state changes
            GameHash = string.Empty;
        }

        public GameModel()
        {
            Players = new();
            GameType = GameType.Regular;
            HasSupplementalBuildPhase = false;
            ResourceRules = new ResourceRules();
            
            // Initialize new fields for Rule 7 compliance
            CreatedTime = DateTime.UtcNow;
            GameStateMachineVersion = 1;
            // GameHash will be computed later by GameStateMachine when state changes
            GameHash = string.Empty;
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
            var hashDisplay = string.IsNullOrEmpty(GameHash) ? "no-hash" : GameHash[..Math.Min(8, GameHash.Length)];
            return $"State={GameState} CurrentPlayer={CurrentPlayerId} Hash={hashDisplay}...";
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

        // Rule 7 Compliance: Helper methods for computed fields that GameInfo needs
        // These ensure GameModel is the single source of truth for all game information

        /// <summary>
        /// Extracts display name from player ID following Desktop app pattern.
        /// "Joe-001" → "Joe"
        /// </summary>
        public static string ExtractNameFromId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Unknown";
            
            // Desktop app pattern: "Joe-001" -> "Joe"
            if (id.Contains('-'))
            {
                var parts = id.Split('-');
                if (parts.Length >= 2)
                {
                    return parts[0];
                }
            }
            
            return id;
        }

        /// <summary>
        /// Gets a user-friendly display name for this game.
        /// Format: "GameType - FirstPlayerName +AdditionalCount (Time)"
        /// Example: "Regular - Alice +2 (17:10)"
        /// </summary>
        public string GetDisplayName()
        {
            var timeStr = CreatedTime.ToString("HH:mm");
            var playersStr = Players.Count > 0 ? Players[0].Name : "Unknown";
            if (Players.Count > 1)
            {
                playersStr += $" +{Players.Count - 1}";
            }
            
            return $"{GameType} - {playersStr} ({timeStr})";
        }

        /// <summary>
        /// Gets a user-friendly formatted game state string.
        /// </summary>
        public string GetFormattedGameState()
        {
            return GameState switch
            {
                GameState.PickingBoard => "Setting up board",
                GameState.WaitingForRollForOrder => "Rolling for turn order",
                GameState.FinishedRollOrder => "Setting turn order",
                GameState.BeginResourceAllocation => "Starting placement",
                GameState.AllocateResourceForward => "Initial placement →",
                GameState.AllocateResourceReverse => "Final placement ←",
                GameState.DoneResourceAllocation => "Setup complete",
                GameState.WaitingForRoll => "Waiting for dice roll",
                GameState.WaitingForNext => "Player's turn",
                GameState.PickSupplementalPlayers => "Choosing supplemental",
                GameState.Supplemental => "Supplemental building",
                GameState.MustMoveRobber => "Moving robber",
                GameState.WaitingForNewGame => "Waiting to start",
                _ => GameState.ToString()
            };
        }

        /// <summary>
        /// Gets the current player's display name.
        /// </summary>
        public string GetCurrentPlayerName()
        {
            var currentPlayer = Players.FirstOrDefault(p => p.Id == CurrentPlayerId);
            return currentPlayer?.Name ?? "Unknown";
        }

        /// <summary>
        /// Gets the creation time in display format.
        /// Format: "Dec 25, 17:10"
        /// </summary>
        public string GetCreatedTimeDisplay()
        {
            return CreatedTime.ToString("MMM dd, HH:mm");
        }

        /// <summary>
        /// Determines if the game is in an active state (not finished).
        /// </summary>
        public bool GetIsActive()
        {
            return GameState switch
            {
                GameState.WaitingForNewGame => false,
                _ => true  // Consider all other states as active
            };
        }

        /// <summary>
        /// Gets a summary string for the game.
        /// Format: "GameType • PlayerCount players • FormattedGameState"
        /// Example: "Regular • 3 players • Setting up board"
        /// </summary>
        public string GetSummary()
        {
            return $"{GameType} • {Players.Count} players • {GetFormattedGameState()}";
        }

        /// <summary>
        /// Gets a list of player names for API compatibility.
        /// </summary>
        public List<string> GetPlayerNames()
        {
            return Players.Select(p => p.Name).ToList();
        }
    }

   
}