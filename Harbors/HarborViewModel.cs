


using Catan3.Utility;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System.ComponentModel;

namespace Catan3.Models
{
    public partial class HarborViewModel
    {
      
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
            Top = GetTop(Harbor.TileKey);
            Left = GetLeft(Harbor.TileKey);
        }
        /// <summary>
        ///     Top (and Left) are centered in the OuterKexPoints positions
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private double GetTop(HexCoordinates key)
        {
            var top =  Layout.Top(key);
            top -= 25;
            return top;
        }
        /// <summary>
        ///     Top (and Left) are centered in the OuterKexPoints positions
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private double GetLeft(HexCoordinates key)
        {
            var left =  Layout.Left(key) ;
             
            return left;
        }
        public override string? ToString()
        {

            return Harbor.ToString();
        }
    }
}
