using System;
using System.ComponentModel;
using System.Diagnostics;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace Catan3.Models
{
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

        /// <summary>
        ///     Bound to UI control
        ///     return the proper way to represent the BuildingState
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception"></exception>

        public string GetStateGlyph(BuildingState state)
        {

            var glyph =  state switch
            {
                BuildingState.Empty => string.Empty,
                BuildingState.Settlement => CatanFont.Settlement,
                BuildingState.City => CatanFont.City,
                BuildingState.Highlighted=>Stars.ToString(),
                BuildingState.Stars => Stars.ToString() ,
                BuildingState.Knight => CatanFont.Knight,
                _ => throw new System.Exception("Did you add a state w/o setting a glyph?"),
            };
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
        public Brush GetForegroundBrush(BuildingState state, string ownerId, PlayerViewModel currentPlayer)
        {

            if (state == BuildingState.Empty)
            {
                Debug.Assert(ownerId is null);
                return BrushCache.GetSolidColorBrush(Colors.Transparent);
            }

            if (ownerId is not null)
            {
                Debug.Assert(state != BuildingState.Highlighted);
                PlayerViewModel owner = PlayerDatabase.FromId(ownerId) ?? throw new Exception($"Bad PlayerId: {ownerId}");
                return BrushCache.GetSolidColorBrush(owner.Foreground);
            }
            else
            {
                return BrushCache.GetSolidColorBrush(currentPlayer.Foreground);
            }


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
        public Brush GetBackgroundBrush(BuildingState state, string ownerId, PlayerViewModel currentPlayer)
        {

            if (state == BuildingState.Empty)
            {
                Debug.Assert(ownerId is null);
                return BrushCache.GetSolidColorBrush(Colors.Transparent);
            }
            if (ownerId is not null)
            {
                PlayerViewModel owner = PlayerDatabase.FromId(ownerId) ?? throw new Exception($"Bad PlayerId: {ownerId}");
                return BrushCache.GetGradientBrush(owner.Background, Colors.Black);
            }
            else
            {
                return BrushCache.GetGradientBrush(currentPlayer.Background, Colors.Black);
            }
        }



        public override string? ToString() => $"{Building} Stars={Stars}";

    }
}

