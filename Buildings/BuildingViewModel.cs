using System.ComponentModel;
using Catan3.Utility;

namespace Catan3.Models
{
    public partial class BuildingViewModel
    {
        /// <summary>
        ///     This partial class gets called by the template generated BuildingViewModel during its ctor in order
        ///     to subscribe to Layoutchanges to update the calculated fiels.
        /// </summary>
        void Init()
        {
            if (Layout is not null && Layout is BoardLayout rbl)
            {
                rbl.PropertyChanged += Layout_PropertyChanged;
                Layout = Layout;
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
        public override string? ToString()
        {

            return Building.ToString();
        }
    }
}

