using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using Catan10.Models;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
namespace Catan3.Models
{
    public partial class GameViewModel : ObservableRecipient
    {
        private GameType GameType { get; set; } = GameType.Unset;
        //
        // 
        /// <summary>
        ///     Send messages to *viewmodels* after creating them that has information they need
        ///     that you don't want to pass as parameters. this should be static config per gametype
        ///     
        ///     the default ctor is in ./GameViewMessages.cs
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public GameViewModel(GameModel gameModel, IPlayerDatabase database) : this() // in GameViewModel() we RegisterMessages
        {
            GameType = gameModel.GameType;
            if (GameType == GameType.Regular)
            {
                BoardInfo = RegularBoardInfo.Default;
            }
            else if (GameType == GameType.Expansion)
            {
                BoardInfo = ExpansionBoardInfo.Default;
            }
            else
            {
                throw new ArgumentException($"invalid boardsize");
            }
            Debug.Assert(gameModel.Players.Count > 0);
            foreach (var player in gameModel.Players)
            {
                var playerViewModel = database.FromId(player.Id) ?? throw new Exception($"Bad PlayerId: {player.Id}");
                Players.Add(playerViewModel);
            }
            this.GameModel = gameModel; // triggers OnGameModelChanged
        }

        partial void OnGameModelChanged(GameModel? oldValue, GameModel newValue)
        {
            if (newValue.GameType != this.GameType) throw new GameException("Create new one instead of updating this one");
            if (BoardInfo is null) throw new GameException("Board Info can't be null");
            MergeTiles(newValue);
            MergeBuildings(newValue);
            MergeHarbors(newValue);
            MergeRoads(newValue);
            MergeRobber(newValue);
            MergePlayers(newValue);
            MergeRolls(newValue);
            MergeResources(newValue);
            MergePurchaseModel(newValue);
            GameModel = newValue;
            SetGameStars();
            FixupState(newValue);
            SetPlayerStars();
        }
        /// <summary>
        ///     This is calculated based on state in GameModel, but stored int the ViewModel
        ///     call *after* MergeBuildings, as that calculates buildings.Stars
        /// </summary>
        private void SetPlayerStars()
        {
            var stars = new Dictionary<string, int>();
            var ownedBuildings = Buildings.Where(b => b.Building.OwnerId is not null).ToList();
            int factor = 1;
            foreach (var building in ownedBuildings)
            {
                // this.TraceMessage($"{tile} Stars={tile.Stars}");
                switch (building.Building.BuildingState)
                {
                    case BuildingState.Settlement:
                        factor = 1;
                        break;
                    case BuildingState.City:
                        factor = 2;
                        break;
                    default:
                        factor = 0;
                        break;
                }
                int starValue = building.Stars * factor;
                Debug.Assert(building.Building.OwnerId is not null); //filtered out above
                stars[building.Building.OwnerId] = stars.GetValueOrDefault(building.Building.OwnerId, 0) + starValue;
            }
            foreach (var player in Players)
            {
                player.StatDictionary[StatName.Stars].Count = stars.GetValueOrDefault(player.Id, 0);
                player.StatDictionary[StatName.LongestRoad].Highlighted = player.Player.HasLongestRoad;
                player.StatDictionary[StatName.SoldierPlayed].Highlighted = player.Player.LargestArmy;
            }
        }
        /// <summary>
        ///     If we haven't done it yet, create the right view models
        ///     if the view model count is wrong for some reason -- maybe the board is the same, but an option 
        ///     changed what can be bought -- create a new ViewModel list
        ///     then merge the data model into the viewmodel
        /// </summary>
        /// <param name="gameModel"></param>
        private void MergePurchaseModel(GameModel gameModel)
        {
            var currentPlayer = gameModel.CurrentPlayer();
            if (gameModel.EntitlementPurchaseModel.Count != this.PurchasableEntitlements.Count)
            {
                this.PurchasableEntitlements = [];
                foreach (var dataModel in gameModel.EntitlementPurchaseModel)
                {
                    EntitlementPurchaseViewModel viewModel = new(PurchaseCommand, dataModel, this.CurrentPlayer.PlayerColors.ForegroundBrush, this.CurrentPlayer.PlayerColors.BackgroundBrush);
                    this.PurchasableEntitlements.Add(viewModel);
                }
                return;
            }
            for (int i = 0; i < gameModel.EntitlementPurchaseModel.Count; i++)
            {
                var unspent = currentPlayer.UnspentEntitlements.Count( e => e == gameModel.EntitlementPurchaseModel[i].Entitlement );
                this.PurchasableEntitlements[i].Merge(gameModel.EntitlementPurchaseModel[i], unspent, this.CurrentPlayer.PlayerColors.ForegroundBrush, this.CurrentPlayer.PlayerColors.BackgroundBrush);
            }
        }
        private void FixupState(GameModel gameModel)
        {
            // do things here that the game state requires
            if (gameModel.GameState == GameState.WaitingForRoll)
            {
                this.TurnRollViewModel.TurnRollModel = new();
            }
            if (gameModel.Phase() == GamePhase.PickingBoard || gameModel.Phase() == GamePhase.PickingResources)
            {
                // OnPropertyChanged(nameof(ShownStars));
                var currentStars = ShownStars;
                ShownStars = 14;
                ShownStars = currentStars;
                Debug.Assert(CurrentPlayer != null);
                this.Id = GameModel.GetHashCode().ToString();
            }
        }
        /// <summary>
        ///     We are keeping track of 3 kinds of Resources
        ///     1. Total reources generated by all players: gameModel.GameResourcesModel
        ///     2. Total resources generated by each playe: gameModel.Player[i].ResourcesThisGame
        ///     3. Resources generated by the player for this turn: gameModel.Player[i].ResourcesThisTurn
        ///     
        ///     the first one is merged here and the last 
        /// 
        /// </summary>
        /// <param name="gameModel"></param>
        private void MergeResources(GameModel gameModel)
        {
            this.GameResources.ResourceModel = gameModel.GameResourcesModel;
        }
        private void MergeRolls(GameModel gameModel)
        {
            this.GameRollViewModel.GameRollModel = gameModel.GameRollModel;
        }
        private void MergeRobber(GameModel gameModel)
        {
            RobberViewModel.RobberModel = gameModel.Robber;
        }
        /// <summary>
        /// 
        ///     Create the this.Players collection of PlayerViewModels based on the passed in list of playerIds
        ///     stored in gameModel.Players
        ///     
        ///     all per player state should be merged here -- including the player resources
        ///     
        ///     Note that this also updates the per-player data like total Resources
        ///     Note that it is possible for the GameModel.Player order to be different than the GameViewModel.Players
        ///     when the GameState is in GameState.WaitingForPlayerOrder
        /// </summary>
        /// <param name="gameModel"></param>
        private void MergePlayers(GameModel gameModel)
        {
            Debug.Assert(this.Players.Count == gameModel.Players.Count);
            Debug.Assert(this.Players.Count > 0);
            SetPlayerOrder(gameModel);
            for (int i = 0; i < Players.Count; i++)
            {
                Players[i].Player = gameModel.Players[i]; // triggers PlayerViewModel.PlayerChanged
                Debug.Assert(Players[i].Id == gameModel.Players[i].Id);
                if (gameModel.Players[i].Id == gameModel.CurrentPlayerId)
                {
                    this.CurrentPlayer = Players[i]; // triggers OnCurrentPlayerChanged
                }
            }
        }
        public void SetPlayerOrder(GameModel gameModel)
        {
            List<PlayerViewModel> orderedPlayerList =[];
            //
            // make the GameViewModel.Players collection match the order of the GameModel.Players
            for (int order = 0; order < gameModel.Players.Count; order++)
            {
                var playerViewModel = Players.First((p)=> p.Id == gameModel.Players[order].Id) ?? throw new GameException($"Cannot find PlayerId {gameModel.Players[order].Id} in the playing players.  bad. very bad.");
                orderedPlayerList.Add(playerViewModel);
            }
            for (int i = 0; i < Players.Count; ++i)
            {
                if (Players[i].Id != gameModel.Players[i].Id)
                {
                    Players[i] = orderedPlayerList[i];
                }
            }

        }
        private void MergeTiles(GameModel gameModel)
        {
            Contract.Assert(BoardInfo is not null);
            if (Tiles.Count == 0) // need to create them for the first time
            {
                Tiles = new ObservableCollection<TileViewModel>(
                gameModel.Tiles.Select((tile, index) => new TileViewModel(tile, BoardInfo.Layout) { Index = index }));
            }
            else
            {
                Debug.Assert(Tiles.Count == gameModel.Tiles.Count);
                for (int i = 0; i < gameModel.Tiles.Count; i++)
                {
                    Contract.Assert(Tiles[i].Tile.TileKey == gameModel.Tiles[i].TileKey);
                    Tiles[i].Tile = gameModel.Tiles[i];
                    Tiles[i].AllowTargetting = gameModel.GameState == GameState.MustMoveRobber;
                    Debug.Assert(Tiles[i].Tile == gameModel.Tiles[i]);
                }
            }
        }
        private void MergeRoads(GameModel gameModel)
        {
            Contract.Assert(BoardInfo is not null);
            if (Roads.Count == 0)
            {
                Roads = new ObservableCollection<RoadViewModel>(
                    gameModel.Roads.Select(road => new RoadViewModel(road, BoardInfo.Layout))
                );
            }
            else
            {
                Debug.Assert(Roads.Count == gameModel.Roads.Count, "Road count mismatch.");
                for (int i = 0; i < gameModel.Roads.Count; i++)
                {
                    Contract.Assert(Roads[i].Road.RoadKey == gameModel.Roads[i].RoadKey);
                    Roads[i].Road = gameModel.Roads[i];
                }
            }
        }
        private void MergeBuildings(GameModel gameModel)
        {
            if (Buildings.Count == 0)
            {
                Contract.Assert(BoardInfo is not null, "BoardInfo cannot be null.");
                Buildings = new ObservableCollection<BuildingViewModel>(
                    gameModel.Buildings.Select(building => new BuildingViewModel(building, BoardInfo.Layout))
                );
                Debug.Assert(gameModel.Tiles.Count > 0);
                //
                //  we only need to set stars once per game
                foreach (var viewModel in Buildings)
                {
                    viewModel.Stars = gameModel.TilesForBuildings(viewModel.Building.BuildingKey).Stars();
                }
            }
            Debug.Assert(Buildings.Count == gameModel.Buildings.Count);
            int buildingIndex = 1;
            var currentPlayer =  gameModel.CurrentPlayer();
            //
            //  if they have a City entitlement, highlight and mark each Settlement
            bool hasCityEntitlement = currentPlayer.UnspentEntitlements.Contains(Entitlement.City);
            bool hasSettlementEntitlement = currentPlayer.UnspentEntitlements.Contains(Entitlement.Settlement);
            for (int i = 0; i < gameModel.Buildings.Count; i++)
            {
                Contract.Assert(Buildings[i].Building.BuildingKey == gameModel.Buildings[i].BuildingKey);
                Buildings[i].Building = gameModel.Buildings[i];
                Debug.Assert(Buildings[i].Stars >= 0);
                switch (gameModel.Buildings[i].BuildingState)
                {
                    case BuildingState.PossibleSettlement:
                        if (hasSettlementEntitlement && gameModel.Phase() != GamePhase.PickingResources)
                        {
                            Buildings[i].VisualState = BuildingVisualState.Highlighted;
                            Buildings[i].BuildIndex = buildingIndex++;
                        }
                        else if (Buildings[i].Stars >= this.ShownStars)
                        {
                            Buildings[i].VisualState = BuildingVisualState.Stars;
                        }
                        break;
                    case BuildingState.NotBuildable:
                        Buildings[i].VisualState = BuildingVisualState.Hidden;
                        break;
                    case BuildingState.Settlement:
                        if (hasCityEntitlement && gameModel.Buildings[i].OwnerId == currentPlayer.Id)
                        {
                            Buildings[i].VisualState = BuildingVisualState.Highlighted;
                            Buildings[i].BuildIndex = buildingIndex++;
                        }
                        else
                        {
                            Buildings[i].VisualState = BuildingVisualState.Normal;
                        }
                        break;
                    case BuildingState.City:
                    case BuildingState.Metropolis:
                    case BuildingState.Knight:
                        Buildings[i].VisualState = BuildingVisualState.Normal;
                        break;
                }
            }
        }
        private void MergeHarbors(GameModel gameModel)
        {
            Contract.Assert(BoardInfo is not null, "BoardInfo cannot be null.");
            if (Harbors.Count == 0) // Check if harbors need to be created for the first time
            {
                Harbors = new ObservableCollection<HarborViewModel>(
                    gameModel.Harbors.Select(harbor => new HarborViewModel(harbor, BoardInfo.Layout))
                );
            }
            else // Update existing harbors
            {
                Debug.Assert(Harbors.Count == gameModel.Harbors.Count, "Harbor count mismatch.");
                for (int i = 0; i < gameModel.Harbors.Count; i++)
                {
                    Contract.Assert(Harbors[i].Harbor.HexCoordinates == gameModel.Harbors[i].HexCoordinates, "Harbor key mismatch.");
                    Contract.Assert(Harbors[i].Harbor.Side == gameModel.Harbors[i].Side, "Harbor key mismatch.");
                    Harbors[i].Harbor = gameModel.Harbors[i];
                }
            }
        }
        /// <summary>
        ///     the Star for each building is dependend on the Tiles and thus changes everytime we Shuffle...but Shuffle is driven off of
        ///     GameModel, not GameViewModel...so we can't do it there.  
        /// </summary>
        public void SetGameStars()
        {
            var resourceModel = new ResourcesModel();
            foreach (var resource in GameViewModelStatics.StarsTrackResourceList)
            {
                resourceModel.AddResource(resource, GameModel.StarCount(resource));
            }
            this.StarsResourceViewModel.ResourceModel = resourceModel;
            foreach (var viewModel in Buildings)
            {
                viewModel.Stars = GameModel.TilesForBuildings(viewModel.Building.BuildingKey).Stars();
            }
        }
        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BoardInfo.Layout.TileXOffset) ||
                e.PropertyName == nameof(BoardInfo.Layout.TileYOffset) ||
                e.PropertyName == nameof(BoardInfo.Layout.BoardWidth) ||
                e.PropertyName == nameof(BoardInfo.Layout.BoardHeight))
            {
                // those are the 4 properties updated in UpdateLayout
                return;
            }
            if (e.PropertyName == nameof(BoardInfo.Layout.OuterHexSize) || e.PropertyName == nameof(BoardInfo.Layout.BuildingSize))
            {
                // these are the properties that the UpdateLayout depends on
                UpdateLayout();
                return;
            }
        }
        /// <summary>
        ///     Data that joins 2 or more collections is implemented here instead of as extension methods to the collection
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public List<TileModel> TilesForBuildings(BuildingKey key)
        {
            Debug.Assert(GameModel is not null);
            return GameModel.TilesForBuildings(key);
        }
        private ObservableCollection<TileViewModel> CreateAndSortTileViewModelList(ObservableCollection<TileModel> tiles)
        {
            Debug.Assert(BoardInfo is not null);
            var sortedTiles = tiles.OrderBy(tvm => tvm.TileKey).ToList();
            ObservableCollection<TileViewModel> result = [];
            for (int i = 0; i < sortedTiles.Count; i++)
            {
                var tvm = new TileViewModel(sortedTiles[i], BoardInfo.Layout)
                {
                    Index = i
                };
                result.Add(tvm);
            }
            return result;
        }
        public ObservableCollection<TileViewModel> UpdateTiles(ObservableCollection<TileModel> tiles)
        {
            Debug.Assert(BoardInfo is not null);
            var sortedTiles = tiles.OrderBy(tvm => tvm.TileKey).ToList();
            ObservableCollection<TileViewModel> result = [];
            for (int i = 0; i < sortedTiles.Count; i++)
            {
                var tvm = new TileViewModel(sortedTiles[i], BoardInfo.Layout)
                {
                    Index = i
                };
                result.Add(tvm);
            }
            return result;
        }
        /// <summary>
        ///     We want to calculate the offset of the tile positions from the harborLeft of the board so that we leave the right space for the buildings 
        ///     and harbors around the board.  when we start calling .Left and .Top, the space to add to harborLeft and top is 0.  this will calculate what
        ///     should be added. 
        ///     
        ///     Left is calculated by looking for harbors off the first column
        ///     Top is calculated by looking at the top of the middle row for Harbors or Buildings
        ///     Height is calculated by looking at the bottom of the middle row for Harbors or Buildings
        ///     Width is calculated by looking at the last column for harbors 
        ///     
        ///     the space will either be a harbor or be a building.
        /// </summary>
        double cached_BuildingSize = -1;
        double cached_OuterHexSize = -1;
        public void UpdateLayout()
        {
            Contract.Assert(BoardInfo is not null && BoardInfo.Layout is not null, "Cannot do layout with no BoardInfo");
            Contract.Assert(Harbors is not null, "Must have Harbors to layout Harbors");
            if (cached_BuildingSize == BoardInfo.Layout.BuildingSize && cached_OuterHexSize == BoardInfo.Layout.OuterHexSize)
            {
                return;
            }
            cached_BuildingSize = BoardInfo.Layout.BuildingSize;
            cached_OuterHexSize = BoardInfo.Layout.OuterHexSize;
            BoardInfo.Layout.TileXOffset = 0;
            BoardInfo.Layout.TileYOffset = 0;
            var harborSize = BoardInfo.Layout.BuildingSize;
            var hexSize = BoardInfo.Layout.OuterHexSize;
            //
            //  get Y offset
            var topTile = Tiles.TopTile();
            var key = topTile.Tile.TileKey;
            var tileTop = BoardInfo.Layout.Top(key);
            var pointyDictionary = BoardInfo.Layout.PointyHexPoints.PointyTopListToDictionary();
            var top = pointyDictionary[HexSide.Top].Y;
            BoardInfo.Layout.TileYOffset = Math.Round(Math.Abs(tileTop) + Math.Abs(top) + harborSize, 2);
            // get X offset
            var firstTile = Tiles.FirstColumn().First();
            // all of the Harbors will have the same X on the first column, so make one up assuming that one will be there.
            var harborTopLeft = HarborViewModel.GetLeftTop(BoardInfo.Layout, firstTile.Tile.TileKey, HexSide.BottomLeft);
            BoardInfo.Layout.TileXOffset = Math.Abs(Math.Round(harborTopLeft.X));
            // this.TraceMessage($"({BoardInfo.Layout.TileXOffset},{BoardInfo.Layout.TileYOffset})");
            // calculate the height
            var bottomTile = Tiles.BottomTile();
            HarborViewModel? bottomHarbor = Harbors.FindHarbor(bottomTile.Tile.TileKey, HexSide.Bottom);
            if (bottomHarbor is not null)
            {
                BoardInfo.Layout.BoardHeight = bottomHarbor.Top + BoardInfo.Layout.BuildingSize; // BuildingSize is also HarborSize
            }
            else
            {
                var b = Buildings.FindBuildingViewModel(new BuildingKey(bottomTile.Tile.TileKey, HexPosition.BottomLeft));
                Debug.Assert(b != null);
                BoardInfo.Layout.BoardHeight = b.Top + BoardInfo.Layout.BuildingSize;
            }
            //
            //  calulate the Width
            var rightTile = Tiles.LastColumn().First();
            double left = BoardInfo.Layout.Left(rightTile.Tile.TileKey) ;
            BoardInfo.Layout.BoardWidth = left + 2 * hexSize + harborSize / 2.0;
            foreach (var tile in Tiles.LastColumn())
            {
                if (tile is null) continue;
                var harbors = Harbors.FindAnyHarbor(tile.Tile.TileKey);
                if (harbors is null) continue;
                foreach (HarborViewModel h in harbors)
                {
                    if (h.Harbor.Side == HexSide.Top || h.Harbor.Side == HexSide.Bottom) continue;
                    var right = h.Left + harborSize;
                    BoardInfo.Layout.BoardWidth = right;
                    return;
                }
            }
        }
    }
}
