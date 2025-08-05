
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Catan10.Models;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
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
    public partial class GameModel : ObservableObject
    {
        /// <summary>
        ///     What kinds of things can be purchased in this game and if they are allowed to be purchased
        ///     at this time?
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<EntitlementPurchaseModel> EntitlementPurchaseModel { get; set; } = new();

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
        public partial ObservableCollection<TileModel> Tiles { get; set; } = new();

        [ObservableProperty]
        public partial ObservableCollection<BuildingModel> Buildings { get; set; } = new();

        [ObservableProperty]
        public partial ObservableCollection<RoadModel> Roads { get; set; } = new();

        [ObservableProperty]
        public partial ObservableCollection<HarborModel> Harbors { get; set; } = new();

        [ObservableProperty]
        public partial RobberModel Robber { get; set; } = new();

        [ObservableProperty]
        public partial HouseRules HouseRules { get; set; } = new();

        [ObservableProperty]
        public partial ResourceRules ResourceRules { get; set; }

        [ObservableProperty]
        public partial string CurrentPlayerId { get; set; } = string.Empty;

        [ObservableProperty]
        public partial RollModel RollModel { get; set; } = new();

        // keep track of the player who goes when there is nobody left to do supplemental
        [ObservableProperty]
        public partial string NextPlayerToRollAfterSupplemental { get; set; } = "";

        //
        //  keep track of the total resources ever generated in the game by everyone
        [ObservableProperty]
        public partial ResourcesModel GameResourcesModel { get; set; } = new();

        [ObservableProperty]
        public partial GameState PreviousGameState { get; set; } = GameState.Uninitialized;

        partial void OnGameStateChanged(GameState oldValue, GameState newValue)
        {
            PreviousGameState = oldValue;
        }

        public override string ToString()
        {
            return $"State={GameState} CurrentPlayer={CurrentPlayerId}";
        }

        /// <summary>
        ///     called by the GameFactory when a new game is created.  All data that the game needs
        ///     should be created here.
        /// </summary>
        /// <param name="gameInfo"></param>
        /// <param name="players"></param>
        public GameModel(IGameMetadata gameInfo, List<PlayerModel> players)
        {
            Debug.Assert(players.Count > 0); // enforced by caller
            GameType = gameInfo.GameType;
            HasSupplementalBuildPhase = gameInfo.HasSupplemental;
            Players = players;
            ResourceRules = gameInfo.ResourceRules;
            HouseRules = gameInfo.HouseRules;
            CurrentPlayerId = players[0].Id;
            EntitlementPurchaseModel.AddRange(gameInfo.PurchaseableEntitlements);
        }

        [JsonConstructor]
        public GameModel()
        {
            Players = new();
            GameType = GameType.Regular;
            HasSupplementalBuildPhase = false;
            ResourceRules = ResourceRules.Default;
        }

        /// <summary>
        ///     Add up all the stars for the given resource type
        /// </summary>
        /// <param name="tileType"></param>
        /// <returns></returns>
        public int StarCount(ResourceType tileType)
        {
            var total = this.Tiles.Where(tile => tile.ResourceTileType == tileType)
                    .Sum(tile => tile.Stars);
            return total;
        }

        public string Serialize()
        {
            string gameModelJson = String.Empty;
            FunctionTimer.CallTimedFunction("GameModel.Serialize", () =>
            {
                gameModelJson = JsonSerializer.Serialize(this);
            });
            return gameModelJson;
        }
    }
}
