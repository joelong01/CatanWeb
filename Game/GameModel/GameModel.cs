
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
        private bool _undoEnabled = false;
        [ObservableProperty]
        private bool _redoEnabled = false;
        [ObservableProperty]
        private bool _nextEnabled = false;
        [ObservableProperty]
        private bool _rollsEnabled = false;
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
        private ObservableCollection<EntitlementPurchaseModel> _entitlementPurchaseModel = [];
        [ObservableProperty]
        private ActionFlags _actionFlags = new();
       [ObservableProperty]
        private GameType _gameType = GameType.Regular;
        [ObservableProperty]
        private GameState _gameState = GameState.WaitingForNewGame;
        [ObservableProperty]
        private bool _hasSupplementalBuildPhase = false;
        [ObservableProperty]
        private List<PlayerModel> _players = [];
      
        [ObservableProperty]
        private ObservableCollection<TileModel> _tiles = [];
        [ObservableProperty]
        private ObservableCollection<BuildingModel> _buildings = [];
        [ObservableProperty]
        private ObservableCollection<RoadModel> _roads = [];
        [ObservableProperty]
        private ObservableCollection<HarborModel> _harbors = [];
        [ObservableProperty]
        private RobberModel _robber = new();
        [ObservableProperty]
        private HouseRules _houseRules = new();
        [ObservableProperty]
        private ResourceRules _resourceRules;
        [ObservableProperty]
        private string _currentPlayerId = string.Empty;
        [ObservableProperty]
        private RollModel _rollModel = new();

        // keep track of the player who goes when there is nobody left to do supplemental
        [ObservableProperty]
        private string _nextPlayerToRollAfterSupplemental = "";
        //
        //  keep track of the total resources ever generated in the game by everyone
        [ObservableProperty]
        private ResourcesModel _gameResourcesModel = new();
        [ObservableProperty]
        private GameState _previousGameState = GameState.Uninitialized;
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
            Players = [];
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
