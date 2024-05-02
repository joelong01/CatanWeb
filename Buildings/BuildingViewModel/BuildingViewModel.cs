using System;
using System.ComponentModel;
using System.Diagnostics;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Text;
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
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>

        public string BIND_StateGlyph(BuildingState state)
        {

            string glyph;

            switch (state)
            {
                case BuildingState.Empty:
                    if (Building.Buildable)
                    {
                        //this.TraceMessage($"{Building}");
                        glyph = CatanFont.Settlement; // the first thing you can build
                    }
                    else
                    {
                        glyph = string.Empty;
                    }
                    break;
                case BuildingState.Settlement:
                    glyph = CatanFont.Settlement;
                    break;
                case BuildingState.City:
                    glyph = CatanFont.City;
                    break;
                case BuildingState.Highlighted:
                    if (Building.Buildable)
                    {
                        glyph = CatanFont.Settlement; // Building.Stars.ToString();
                    }
                    else
                    {
                        glyph = String.Empty;
                    }
                    break;
                case BuildingState.Stars:

                    if (Building.Buildable && Building.Stars > 0)
                    {
                        glyph = Building.Stars.ToString();
                    }
                    else
                    {
                        glyph = String.Empty;
                    }
                    break;

                case BuildingState.Knight:
                    glyph = CatanFont.Knight;
                    break;
                default:
                    throw new System.Exception("Did you add a state w/o setting a glyph?");
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
        public Brush BIND_Foreground(BuildingState state, string? ownerId, PlayerViewModel currentPlayer, bool buildable)
        {
            if (buildable && state != BuildingState.Stars)
            {
                return GetBrush(state, ownerId, currentPlayer, false);
            }

            return GetBrush(state, ownerId, currentPlayer, true);
        }

        public Brush BIND_Background(BuildingState state, string? ownerId, PlayerViewModel currentPlayer, bool buildable)
        {
            if (buildable && state != BuildingState.Stars)
            {
                return GetBrush(state, ownerId, currentPlayer, true);
            }

            return GetBrush(state, ownerId, currentPlayer, false);
        }

        private static Brush GetBrush(BuildingState state, string? ownerId, PlayerViewModel currentPlayer, bool foreground)
        {
            //
            // not buildable or in a state we shoudln't show
            if (state == BuildingState.Empty)
            {
                Debug.Assert(ownerId is null);
                return BrushCache.GetSolidColorBrush(Colors.Transparent);
            }

            if (ownerId is not null)
            {
                //
                //  if there is an owner, always use the owner color
                Debug.Assert(ownerId is not null);
                Debug.Assert(state != BuildingState.Highlighted);
                PlayerViewModel owner = PlayerDatabase.FromId(ownerId) ?? throw new Exception($"Bad PlayerId: {ownerId}");
                if (foreground)
                {
                    return BrushCache.GetSolidColorBrush(owner.Foreground);
                }
                else
                {
                    return BrushCache.GetGradientBrush(owner.Background, Colors.Black);
                }
            }
            else
            {
                if (foreground)
                {
                    return BrushCache.GetSolidColorBrush(currentPlayer.Foreground);
                }
                else
                {
                    return BrushCache.GetGradientBrush(currentPlayer.Background, Colors.Black);
                }
            }
        }
       
       

        public   Visibility BIND_BuildIndexVisibility(BuildingState state, bool buildable)
        {
            if (state == BuildingState.Stars) return Visibility.Collapsed;
            if (buildable) return Visibility.Visible;

            return Visibility.Collapsed;
        }

        private void BreakOnKey(BuildingKey key)
        {
            Debug.Assert(key != this.Building.BuildingKey);
        }

        public override string? ToString() => $"{Building}";

    }
}

