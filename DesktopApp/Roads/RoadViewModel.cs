using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Catan3.Shared.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinPoint = Windows.Foundation.Point;
using HexCoordinates = Catan3.Shared.Utility.HexCoordinates;
using Direction = Catan3.Shared.Models.Direction;
using HexGeometry = Catan3.DesktopApp.Layout.HexGeometry;
using Catan3.DesktopApp.Layout;
namespace Catan3.Models

{
    /// <summary>
    /// Represents the view model for a road, including its layout, position, and related properties.
    /// </summary>
    public partial class RoadViewModel : ObservableRecipient
    {
        /// <summary>
        /// Gets or sets the road model.
        /// </summary>
        [JsonIgnore]
        [ObservableProperty]
        public partial RoadModel Road { get; set; }

        /// <summary>
        /// Gets or sets the board layout.
        /// </summary>
        [ObservableProperty]
        public partial BoardVisualLayout Layout { get; set; } = BoardVisualLayout.Default;

        /// <summary>
        /// Gets or sets the center point of the road.
        /// </summary>
        [ObservableProperty]
        public partial WinPoint RoadCenter { get; set; } = new WinPoint(0, 0);

        /// <summary>
        /// Gets or sets the left position of the road.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RoadPolygon))]
        public partial double Left { get; set; }

        /// <summary>
        /// Gets or sets the top position of the road.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RoadPolygon))]
        public partial double Top { get; set; }

        /// <summary>
        /// Gets or sets the index of the road.
        /// </summary>
        [ObservableProperty]
        public partial double Index { get; set; }

        /// <summary>
        /// Gets or sets the current player.
        /// </summary>
        [ObservableProperty]
        public partial PlayerViewModel CurrentPlayer { get; set; } = PlayerViewModel.Default;

        public RoadViewModel() { throw new NotImplementedException("This constructor should never be called."); }

        /// <summary>
        /// Initializes a new instance of the RoadViewModel class.
        /// </summary>
        /// <param name="road">The road model.</param>
        /// <param name="layout">The board layout.</param>
        [SetsRequiredMembers]
        public RoadViewModel(RoadModel road, BoardVisualLayout layout)
        {
            Road = road ?? throw new ArgumentNullException(nameof(road), "road cannot be null"); ;
      
            IsActive = true;
            Messenger.Register<CurrentPlayerChanged>(this, (recipient, message) =>
            {
                HandleCurrentPlayerChanged(message.CurrentPlayer);
            });
            Messenger.Register<EndGame>(this, (recipient, message) =>
            {
                Messenger.UnregisterAll(this);
            });
            Messenger.Register<PlayerColorChanged>(this, (recipient, message) =>
            {
                if (message.PlayerColors.PlayerId == Road.OwnerId)
                {
                    OnPropertyChanged(nameof(GetForegroundBrush));
                    OnPropertyChanged(nameof(GetBackgroundBrush));
                }
            });
            if (layout is not null && layout is BoardVisualLayout rbl)
            {
                rbl.PropertyChanged += Layout_PropertyChanged;
            }
            UpdateLayout();
        }

        /// <summary>
        /// Handles the PropertyChanged event for the layout.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event arguments.</param>
        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is BoardVisualLayout layout)
            {
                Layout = layout;
                UpdateLayout();
            }
        }

        /// <summary>
        /// Handles the current player change event.
        /// </summary>
        /// <param name="newCurrentPlayer">The new current player.</param>
        private void HandleCurrentPlayerChanged(PlayerViewModel newCurrentPlayer)
        {
            CurrentPlayer = newCurrentPlayer;
        }

        /// <summary>
        /// Updates the layout of the road.
        /// </summary>
        private void UpdateLayout()
        {
            const double BUILD_INDEX_TEXT_SIZE = 20;
            Top = Layout.Top(Road.RoadKey.TileKey);
            Left = Layout.Left(Road.RoadKey.TileKey);
            GetRoadPoints(Road.RoadKey.HexSide, Road.RoadKey.TileKey, Layout);

            double offset = Layout.OuterHexSize - Layout.InnerHexSize;
            var pointyTopHexPoints = HexGeometry.PointyTopHexPoints(Layout.InnerHexSize - offset / 2.0, Layout.ControlWidth / 2.0, Layout.ControlHeight / 2.0).PointyTopListToDictionary();
            RoadCenter = new WinPoint(pointyTopHexPoints[this.Road.RoadKey.HexSide].X - BUILD_INDEX_TEXT_SIZE / 2.0, pointyTopHexPoints[this.Road.RoadKey.HexSide].Y - BUILD_INDEX_TEXT_SIZE / 2.0);
            OnPropertyChanged(nameof(RoadPolygon));
        }

        /// <summary>
        /// Gets a short name for the road position, useful for debugging.
        /// </summary>
        public string PositionShortName
        {
            get
            {
                string s = $"{Index}:";
                return this.Road.RoadKey.HexSide switch
                {
                    HexSide.BottomRight => s + "BR",
                    HexSide.BottomLeft => s + "BL",
                    HexSide.TopLeft => s + "TL",
                    HexSide.TopRight => s + "TR",
                    HexSide.None => s + "None",
                    HexSide.Top => s + "T",
                    HexSide.Bottom => s + "B",
                    _ => s + "?",
                };
            }
        }

        /// <summary>
        /// Gets the polygon points for the road.
        /// </summary>
        public PointCollection RoadPolygon
        {
            get
            {
                if (Layout is null) return [];
                var points = GetRoadPoints(Road.RoadKey.HexSide, Road.RoadKey.TileKey, Layout);
                return points;
            }
        }

        private static int cacheHit;
        private static int cacheMiss;
        private static readonly Dictionary<(double size, double tilegap, double stroke), Dictionary<HexSide, PointCollection>> RoadCache = [];

        /// <summary>
        /// Gets the points for the road based on the side, tile key, and layout.
        /// </summary>
        /// <param name="side">The side of the hexagon.</param>
        /// <param name="tileKey">The tile key.</param>
        /// <param name="layout">The board layout.</param>
        /// <returns>The points for the road.</returns>
        private static PointCollection GetRoadPoints(HexSide side, HexCoordinates tileKey, BoardVisualLayout layout)
        {
            Dictionary<HexSide, PointCollection>? sideDictionary;
            if (RoadCache.TryGetValue((layout.OuterHexSize, layout.TileGap, layout.InnerHexStrokeThickness), out sideDictionary))
            {
                if (sideDictionary is not null)
                {
                    if (sideDictionary.TryGetValue(side, out var cachedPoints))
                        if (cachedPoints is not null)
                        {
                            Debug.Assert(cachedPoints.Count > 0);
                            cacheHit++;
                            return cachedPoints.Clone();
                        }
                }
            }
            else
            {
                sideDictionary = [];
                RoadCache.Clear();
                RoadCache[(layout.OuterHexSize, layout.TileGap, layout.InnerHexStrokeThickness)] = sideDictionary;
            }
            Debug.Assert(sideDictionary is not null);
            var points = PointsForSide(side, tileKey, layout);
            cacheMiss++;
            sideDictionary[side] = points;
            return points;
        }

        /// <summary>
        /// Gets the points for a specific side of the hexagon.
        /// </summary>
        /// <param name="side">The side of the hexagon.</param>
        /// <param name="tileKey">The tile key.</param>
        /// <param name="layout">The board layout.</param>
        /// <returns>The points for the specified side.</returns>
        private static PointCollection PointsForSide(HexSide side, HexCoordinates tileKey, BoardVisualLayout layout)
        {
            PointCollection points = [];
            if (layout is null) return points;
            var outerHexPoints = layout.OuterHexPoints.FlatTopListToDictionary();
            var innerHexPoints = layout.InnerHexPoints.FlatTopListToDictionary();
            WinPoint delta;
            switch (side)
            {
                case HexSide.None:
                    break;
                case HexSide.Top:
                    PointCollection bottom = PointsForSide(HexSide.Bottom, tileKey, layout);
                    foreach (var point in bottom)
                    {
                        points.Add(new WinPoint(point.X, point.Y - layout.ControlHeight));
                    }
                    break;
                case HexSide.TopRight:
                    delta = GapBetweenTiles(tileKey, Direction.NorthEast, layout);
                    points.Add(outerHexPoints[HexPosition.TopRight]);
                    points.Add(innerHexPoints[HexPosition.TopRight]);
                    points.Add(innerHexPoints[HexPosition.Right]);
                    points.Add(outerHexPoints[HexPosition.Right]);
                    points.Add(new WinPoint(innerHexPoints[HexPosition.BottomLeft].X + delta.X, innerHexPoints[HexPosition.BottomRight].Y + delta.Y));
                    points.Add(new WinPoint(innerHexPoints[HexPosition.Left].X + delta.X, innerHexPoints[HexPosition.Left].Y + delta.Y));
                    break;
                case HexSide.BottomRight:
                    delta = GapBetweenTiles(tileKey, Direction.SouthEast, layout);
                    points.Add(innerHexPoints[HexPosition.BottomRight]);
                    points.Add(outerHexPoints[HexPosition.BottomRight]);
                    points.Add(new WinPoint(innerHexPoints[HexPosition.Left].X + delta.X, innerHexPoints[HexPosition.Left].Y + delta.Y));
                    points.Add(new WinPoint(innerHexPoints[HexPosition.TopLeft].X + delta.X, innerHexPoints[HexPosition.TopLeft].Y + delta.Y));
                    points.Add(outerHexPoints[HexPosition.Right]);
                    points.Add(innerHexPoints[HexPosition.Right]);
                    break;
                case HexSide.Bottom:
                    points.Add(outerHexPoints[HexPosition.BottomLeft]);
                    points.Add(innerHexPoints[HexPosition.BottomLeft]);
                    points.Add(innerHexPoints[HexPosition.BottomRight]);
                    points.Add(outerHexPoints[HexPosition.BottomRight]);
                    points.Add(new WinPoint(innerHexPoints[HexPosition.BottomRight].X, innerHexPoints[HexPosition.TopRight].Y + layout.ControlHeight));
                    points.Add(new WinPoint(innerHexPoints[HexPosition.BottomLeft].X, innerHexPoints[HexPosition.TopLeft].Y + layout.ControlHeight));
                    break;
                case HexSide.BottomLeft:
                    delta = GapBetweenTiles(tileKey, Direction.SouthWest, layout);
                    points.Add(innerHexPoints[HexPosition.BottomLeft]);
                    points.Add(outerHexPoints[HexPosition.BottomLeft]);
                    points.Add(new WinPoint(innerHexPoints[HexPosition.Right].X + delta.X, innerHexPoints[HexPosition.Right].Y + delta.Y));
                    points.Add(new WinPoint(innerHexPoints[HexPosition.TopRight].X + delta.X, innerHexPoints[HexPosition.TopRight].Y + delta.Y));
                    points.Add(outerHexPoints[HexPosition.Left]);
                    points.Add(innerHexPoints[HexPosition.Left]);
                    break;
                case HexSide.TopLeft:
                    delta = GapBetweenTiles(tileKey, Direction.NorthWest, layout);
                    points.Add(innerHexPoints[HexPosition.TopLeft]);
                    points.Add(outerHexPoints[HexPosition.TopLeft]);
                    points.Add(new WinPoint(innerHexPoints[HexPosition.Right].X + delta.X, innerHexPoints[HexPosition.Right].Y + delta.Y));
                    points.Add(new WinPoint(innerHexPoints[HexPosition.BottomRight].X + delta.X, innerHexPoints[HexPosition.BottomRight].Y + delta.Y));
                    points.Add(outerHexPoints[HexPosition.Left]);
                    points.Add(innerHexPoints[HexPosition.Left]);
                    break;
                default:
                    break;
            }
            return points;
        }

        /// <summary>
        /// Calculates the gap between tiles based on the direction and layout.
        /// </summary>
        /// <param name="key">The hex coordinates of the tile.</param>
        /// <param name="direction">The direction to the adjacent tile.</param>
        /// <param name="layout">The board layout.</param>
        /// <returns>The gap between the tiles as a Point.</returns>
        private static WinPoint GapBetweenTiles(HexCoordinates key, Direction direction, BoardVisualLayout layout)
        {
            var adjacentKey = key.GetAdjacentTile(direction);
            double xGap = layout.Left(adjacentKey) - layout.Left(key);
            double yGap = layout.Top(adjacentKey) - layout.Top(key);
            return new WinPoint(xGap, yGap);
        }

        /// <summary>
        /// Gets the foreground brush for the road based on its state, owner, and current player.
        /// </summary>
        /// <param name="state">The state of the road.</param>
        /// <param name="ownerId">The owner ID of the road.</param>
        /// <param name="currentPlayer">The current player.</param>
        /// <returns>The foreground brush for the road.</returns>
        public Brush GetForegroundBrush(RoadState state, string ownerId, PlayerViewModel currentPlayer)
        {
            if (state == RoadState.Unowned && ownerId is null)
            {
                return BrushCache.GetSolidColorBrush(Colors.Transparent);
            }
            if (ownerId is not null && PlayerDatabase.Instance is not null)
            {
                PlayerViewModel owner = PlayerDatabase.Instance.FromId(ownerId) ?? throw new Exception($"Bad PlayerId: {ownerId}");
                return owner.PlayerColors.ForegroundBrush;
            }
            else
            {
                return currentPlayer.PlayerColors.ForegroundBrush;
            }
        }

        /// <summary>
        /// Gets the background brush for the road based on its state, owner, and current player.
        /// </summary>
        /// <param name="state">The state of the road.</param>
        /// <param name="ownerId">The owner ID of the road.</param>
        /// <param name="currentPlayer">The current player.</param>
        /// <returns>The background brush for the road.</returns>
        /// <exception cref="Exception">Thrown when the player ID is invalid.</exception>
        public Brush GetBackgroundBrush(RoadState state, string ownerId, PlayerViewModel currentPlayer)
        {
            if (state == RoadState.Unowned && ownerId is null && state != RoadState.Buildable)
            {
                return BrushCache.GetSolidColorBrush(Colors.Transparent);
            }
            if (ownerId is not null && PlayerDatabase.Instance is not null)
            {
                PlayerViewModel owner = PlayerDatabase.Instance.FromId(ownerId) ?? throw new Exception($"Bad PlayerId: {ownerId}");
                return owner.PlayerColors.BackgroundBrush;
            }
            else
            {
                return currentPlayer.PlayerColors.BackgroundBrush;
            }
        }

        /// <summary>
        /// Gets the opacity for the road based on its model and state.
        /// </summary>
        /// <param name="model">The road model.</param>
        /// <param name="state">The state of the road.</param>
        /// <returns>The opacity for the road.</returns>
        public double Opacity(RoadModel model, RoadState state)
        {
            if (state == RoadState.Buildable) return 0.5;
            if (model.OwnerId is not null) { return 1.0; }
            return 0.0;
        }

        /// <summary>
        /// Gets the build index as a string.
        /// </summary>
        /// <param name="index">The build index.</param>
        /// <returns>The build index as a string.</returns>
        public string BuildIndex(int index)
        {
            if (index > 0) return index.ToString();
            return "";
        }

        /// <summary>
        /// Gets the visibility for the build index.
        /// </summary>
        /// <param name="index">The build index.</param>
        /// <returns>The visibility for the build index.</returns>
        public Visibility ShowBuildIndex(int index)
        {
            return index > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Returns a string representation of the RoadViewModel.
        /// </summary>
        /// <returns>A string representation of the RoadViewModel.</returns>
        public override string ToString()
        {
            return $"{Road}";
        }

        /// <summary>
        /// Stable AutomationId for UI automation of a road control.
        /// </summary>
        public string AutomationId => $"Road-{Road.RoadKey.TileKey.Q}_{Road.RoadKey.TileKey.R}_{Road.RoadKey.TileKey.S}-{Road.RoadKey.HexSide}";
    }
}
