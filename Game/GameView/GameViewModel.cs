using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;


namespace Catan3.Models
{

    public partial class GameViewModel : ObservableRecipient
    {
        private GameType GameType { get; set; } = GameType.Unset;

        public static GameViewModel Default { get; } = new();

        public GameViewModel(IBoardInfo? boardinfo) : this()
        {
            BoardInfo = boardinfo;

        }
        /// <summary>
        ///     var game = new GameViewModel(GameType.Regular, PlayingPlayers)
        ///     var gameModel = GameFactory.CreateGame(SelectedGame, playerIds);
        ///     game.UpdateGameModel(gameModel);
        /// </summary>
        /// <param name="gameType"></param>
        public GameViewModel(GameType gameType, IList<PlayerViewModel> playingPlayers) : this()
        {


            GameType = gameType;
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
            this.BoardInfo.Layout.PropertyChanged += Layout_PropertyChanged;



            Tiles = [];
            Players = [];
            Roads = [];
            Buildings = [];
            Harbors = [];
            Robber = new RobberViewModel(null);
            Players = new(playingPlayers);

        }

        public GameViewModel(GameModel gameModel) : this()
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

            MergeGameModel(gameModel);


        }
        private void CallTimedFunction(string description, Action action)
        {
            using (new FunctionTimer(description))
            {
                action();
            }
        }


        public void MergeGameModel(GameModel gameModel)
        {
            if (gameModel.GameType != this.GameType) throw new Exception("Create new one instead of updating this one");
            if (BoardInfo is null) throw new Exception("Board Info can't be null");
            CallTimedFunction("Merging Tiles", () => CreateOrUpdateTiles(gameModel));
            CallTimedFunction("Merging Buildings", () => CreateOrUpdateBuildings(gameModel));
            CallTimedFunction("Merging Harbors", () => CreateOrUpdateHarbors(gameModel));
            CallTimedFunction("Merging Roads", () => CreateOrUpdateRoads(gameModel));

            Robber.RobberModel = gameModel.Robber;
            GameModel = gameModel;
            CallTimedFunction("Updating Players", () => UpdatePlayers(GameModel));
            //OnPropertyChanged(nameof(Players));
            CallTimedFunction("SetCurrentPlayer", () => SetCurrentPlayer(gameModel.CurrentPlayerId));
           // CallTimedFunction("Updating Layout", () => UpdateLayout());

        }
        /// <summary>
        /// 
        ///     Create the this.Players collection of PlayerViewModels based on the passed in list of playerIds
        ///     stored in gameModel.Players
        /// </summary>
        /// <param name="gameModel"></param>

        private void UpdatePlayers(GameModel gameModel)
        {

            for (int i = 0; i < gameModel.Players.Count; i++)
            {
                PlayerViewModel player = PlayerDatabase.FromId(gameModel.Players[i].Id) ?? throw new Exception($"Bad PlayerId: {gameModel.Players[i].Id}");
                player.Player = gameModel.Players[i];
                this.Players.Add(player);
            }

        }

        public void SetCurrentPlayer(string playerId)
        {
            PlayerViewModel player = Players.FirstOrDefault(p=> p.Id == playerId) ?? throw new Exception($"Player with Id {playerId} not found in Playing Players collection");
            CurrentPlayer = player;
        }

        private void CreateOrUpdateTiles(GameModel gameModel)
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
                }
            }
            // OnPropertyChanged(nameof(Tiles));
        }
        private void CreateOrUpdateRoads(GameModel gameModel)
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
            //   OnPropertyChanged(nameof(Roads));
        }

        private void CreateOrUpdateBuildings(GameModel gameModel)
        {
            Contract.Assert(BoardInfo is not null, "BoardInfo cannot be null.");
            if (Buildings.Count == 0) // Check if buildings need to be created for the first time
            {
                Buildings = new ObservableCollection<BuildingViewModel>(
                    gameModel.Buildings.Select(building => new BuildingViewModel(building, BoardInfo.Layout))
                );
            }
            else // Update existing buildings
            {
                Debug.Assert(Buildings.Count == gameModel.Buildings.Count, "Building count mismatch.");
                for (int i = 0; i < gameModel.Buildings.Count; i++)
                {
                    Contract.Assert(Buildings[i].Building.BuildingKey == gameModel.Buildings[i].BuildingKey, "Building key mismatch.");
                    Buildings[i].Building = gameModel.Buildings[i];
                }
            }
            //   OnPropertyChanged(nameof(Buildings));
        }


        private void CreateOrUpdateHarbors(GameModel gameModel)
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
            //    OnPropertyChanged(nameof(Harbors));
        }




        public GameViewModel(GameModel gameModel, IEnumerable<PlayerViewModel> playingPlayers) : this()
        {
            Debug.Assert(BoardInfo is not null);
            Debug.Assert(gameModel.GameType != GameType.Unset);
            if (GameType != gameModel.GameType)
            {
                Tiles = [];
                Players = [];
                Roads = [];
                Buildings = [];
                Harbors = [];
                Robber = new RobberViewModel(gameModel.Robber);
                GameType = gameModel.GameType;
                if (gameModel.GameType == GameType.Regular)
                {
                    BoardInfo = RegularBoardInfo.Default;
                }
                else if (gameModel.GameType == GameType.Expansion)
                {
                    BoardInfo = ExpansionBoardInfo.Default;
                }
                else
                {
                    throw new ArgumentException($"invalid boardsize");
                }
            }

            GameModel = gameModel;


            Tiles = CreateAndSortTileViewModelList(gameModel.Tiles);

            foreach (var building in gameModel.Buildings)
            {
                Buildings.Add(new BuildingViewModel(building, BoardInfo.Layout));
            }
            //foreach (var player in gameModel.Players)
            //{
            //    var playerViewModel = allPlayers.FirstOrDefault( p => p.Id == player.Id);
            //    if (playerViewModel is null)
            //    {
            //        throw new Exception($"Player {player.Id} not found");
            //    }
            //    playerViewModel.Player = player;
            //    Players.Add(playerViewModel);
            //}

            foreach (var road in gameModel.Roads)
            {
                var roadView = new RoadViewModel(road, BoardInfo.Layout)
                {
                    Index = Roads.Count
                };
                Roads.Add(roadView);

            }
            foreach (var harbor in gameModel.Harbors)
            {
                Harbors.Add(new HarborViewModel(harbor, BoardInfo.Layout));
            }
            Robber = new RobberViewModel(gameModel.Robber);
            this.BoardInfo.Layout.PropertyChanged += Layout_PropertyChanged;
            UpdateLayout();
            SetStars();

        }
        /// <summary>
        ///     the Star for each building is dependend on the Tiles and thus changes everytime we Shuffle...but Shuffle is driven off of
        ///     GameModel, not GameViewModel...so we can't do it there.  
        /// </summary>
        public void SetStars()
        {
            foreach (var building in Buildings)
            {
                building.Stars = TilesForBuildings(building.Building.BuildingKey).Stars();
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

        partial void OnGameModelChanged(GameModel? oldValue, GameModel newValue)
        {
            //  this.TraceMessage("GameModel changed");
        }

        partial void OnPlayersChanged(ObservableCollection<PlayerViewModel>? oldValue, ObservableCollection<PlayerViewModel> newValue)
        {
            //  this.TraceMessage("Players Changed");
        }



    }

}
