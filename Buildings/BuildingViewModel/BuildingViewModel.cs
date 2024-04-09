using System;
using System.ComponentModel;
using System.Diagnostics;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;

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

        partial void OnBuildingChanged(BuildingModel? oldValue, BuildingModel newValue)
        {
            if (oldValue is not null)
            {
                oldValue.PropertyChanged -= OnBuildingModelPropertyChanged;
            }

            if (newValue is not null)
            {
                newValue.PropertyChanged += OnBuildingModelPropertyChanged;
               // UpdateStateGlyph();  // Update glyph when the model changes
            }
        }

        private void OnBuildingModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(BuildingModel.BuildingState):
                    UpdateStateGlyph();
                    UpdateBrushes();
                    break;
                case nameof(BuildingModel.Owner):
                    UpdateBrushes();
                    break;
                default:
                    this.TraceMessage($"ignoring change: {e.PropertyName}");
                    break;
            }

        }

        private void UpdateBrushes()
        {
            if (Building.Owner is not null)
            {

                PlayerViewModel owner = PlayerDatabase.FromId(Building.Owner.Id) ?? throw new Exception($"Bad PlayerId: {Building.Owner.Id}");
                Background = BrushCache.GetGradientBrush(owner.Background, Colors.Black);
                Foreground = BrushCache.GetSolidColorBrush(owner.Foreground);
            }
            
        }

        public void UpdateStateGlyph()
        {

            var glyph =  Building.BuildingState switch
            {
                BuildingState.Empty => string.Empty,
                BuildingState.Settlement => CatanFont.Gate,
                BuildingState.City => CatanFont.City,
                BuildingState.Highlighted=>Stars.ToString(),
                BuildingState.Stars => Stars.ToString() ,
                BuildingState.Knight => CatanFont.Knight,
                _ => throw new System.Exception("Did you add a state w/o setting a glyph?"),
            }; ;

            StateGlyph = glyph;

        }
        partial void OnStateGlyphChanged(string? oldValue, string newValue)
        {
            // this.TraceMessage($"ObjectId={this.GetHashCode()} Glyph={newValue}");
        }
        partial void OnStarsChanged(int oldValue, int newValue)
        {
          // this.TraceMessage($"{Building.BuildingKey} Stars={newValue}");
        }


        public override string? ToString() => $"{Building} Stars={Stars}";

    }
}

