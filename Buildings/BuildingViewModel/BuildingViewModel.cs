using System;
using System.ComponentModel;
using System.Diagnostics;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
namespace Catan3.Models
{
    /// <summary>
    ///     The building visual state goes through the following
    ///     
    ///     1. Buildable gets set by the Controller.
    ///         => if a building is not buildable and it is Empty, then is is always transparent to the user
    ///         => if a building is not buildable and is owned, it is Opacity 1.0, but it should not be "selectable"
    ///     3. GetStateGlyph() will 
    /// </summary>
    public partial class BuildingViewModel : ObservableRecipient
    {
        public BuildingViewModel(BuildingModel building, BoardLayout layout) : this()
        {
            Building = building;
            Layout = layout;
            IsActive = true;
            if (Layout is not null && Layout is BoardLayout rbl)
            {
                rbl.PropertyChanged += Layout_PropertyChanged;
                Layout = rbl;
            }
            else
            {
                Layout = BoardLayout.Default;
            }
           
            UpdateLayout();
        }
        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is BoardLayout layout)
            {
                this.Layout = layout;
                UpdateLayout();
            }
        }
        private void UpdateLayout()
        {
            Top = GetTop(Building.BuildingKey);
            Left = GetLeft(Building.BuildingKey);
        }
        /// <summary>
        ///     top (and Left) are centered in the OuterKexPoints positions
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private double GetTop(BuildingKey key)
        {
            var top =  Layout.Top(key.HexCoordinates);
            var center = Layout.OuterHexPoints.FlatTopListToDictionary()[key.Position];
            top += center.Y;
            top -= ( Layout.BuildingSize ) * 0.5;
            return top;
        }
        /// <summary>
        ///     top (and Left) are centered in the OuterKexPoints positions
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private double GetLeft(BuildingKey key)
        {
            var left =  Layout.Left(key.HexCoordinates) ;
            var center =  Layout.OuterHexPoints.FlatTopListToDictionary()[key.Position];
            left -= Layout.BuildingSize / 2.0;
            left += center.X;
            return left;
        }
        public void Update()
        {
            OnBuildingChanged(this.Building);
        }
        BuildingKey TestKey = new BuildingKey(new HexCoordinates(-1, 1, 0), HexPosition.BottomRight);
        /// <summary>
        ///     Bound to UI control
        ///     return the proper way to represent the BuildingState.  This is a function instead of a property
        ///     because we need to update this when Building.BuildingState changed and it is difficult to arrange
        ///     for the underlying property notification to happen using the MVVM toolkit because we are coordinating
        ///     the connection between state owned by the ViewModel (StateGlyph) and state owned by the Model (BuildingState).
        ///     
        ///     we do not need to pass in the ShownStars to this function because when ShownStars changes, it updates VisualState
        /// 
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>
        public string BIND_StateGlyph(BuildingState state, BuildingVisualState visualState)
        {
            string glyph;
            switch (state)
            {
                case BuildingState.PossibleSettlement:
                    //
                    // if the building is marked as a possible settlement, that means they have an entitlement
                    // to build and the location is eligible for a building.
                    if (visualState == BuildingVisualState.Stars)
                    {
                        // - during Board Picking or Resource Allocation
                        glyph = Stars.ToString();
                    }
                    else if (visualState == BuildingVisualState.Highlighted)
                    {
                        glyph = CatanFont.Settlement;
                    }
                    else
                    {
                        glyph = String.Empty;
                    }
                    break;
                case BuildingState.Settlement:
                    glyph = CatanFont.Settlement;
                    break;
                case BuildingState.City:
                    glyph = CatanFont.City;
                    break;
                case BuildingState.NotBuildable:
                    glyph = String.Empty;
                    break;
                case BuildingState.Knight:
                    glyph = CatanFont.Knight;
                    break;
                case BuildingState.Metropolis:
                    glyph = CatanFont.Metro;
                    break;
                default:
                    throw new GameException("Did you add a state w/o setting a glyph?", ErrorLevel.Critical);
            }
            return glyph;
        }
        /// <summary>
        ///     if the state is empty, be transparent
        ///     if their is an owner, use their color
        ///     otherwise, use the color of the current player
        /// 
        ///     all brushes are cached.
        /// 
        /// </summary>
        /// <param name="state"></param>
        /// <param name="ownerId"></param>
        /// <param name="currentPlayer"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Brush BIND_Foreground(BuildingVisualState visualState, string? ownerId, PlayerViewModel currentPlayer)
        {
            return GetBrush(visualState, ownerId, currentPlayer, visualState != BuildingVisualState.Highlighted);
        }
        public Brush BIND_Background(BuildingVisualState visualState, string? ownerId, PlayerViewModel currentPlayer)
        {
            return GetBrush(visualState, ownerId, currentPlayer, visualState == BuildingVisualState.Highlighted);
        }
        private static Brush GetBrush(BuildingVisualState visualState, string? ownerId, PlayerViewModel currentPlayer, bool foreground)
        {
            //
            // not buildable or in a state we shoudln't show
            if (visualState == BuildingVisualState.Hidden)
            {
                Debug.Assert(ownerId is null);
                return BrushCache.GetSolidColorBrush(Colors.Transparent);
            }
            if (ownerId is not null)
            {
                //
                //  if there is an owner, always use the owner color
                Debug.Assert(ownerId is not null);
                PlayerViewModel owner = PlayerDatabase.FromId(ownerId) ?? throw new Exception($"Bad PlayerId: {ownerId}");
                if (foreground)
                {
                    return owner.PlayerColors.ForegroundBrush;
                }
                else
                {
                    return owner.PlayerColors.BackgroundBrush;
                }
            }
            else
            {
                if (foreground)
                {
                    return currentPlayer.PlayerColors.ForegroundBrush;
                }
                else
                {
                    return currentPlayer.PlayerColors.BackgroundBrush;
                }
            }
        }
        public Visibility BIND_BuildIndexVisibility(BuildingVisualState state)
        {
            if (state == BuildingVisualState.Highlighted) return Visibility.Visible;
            return Visibility.Collapsed;
        }
        private void BreakOnKey(BuildingKey key)
        {
            Debug.Assert(key != this.Building.BuildingKey);
        }
        public override string? ToString() => $"B={Building} VS={VisualState} S={Stars} I={BuildIndex}";
    }
}
