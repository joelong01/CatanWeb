using System;
using System.Collections.Generic;
using System.ComponentModel;
namespace Catan3.Models
{
    public partial class TileKey(int q, int r, int s) : INotifyPropertyChanged
    {
        public static
                Dictionary<Direction, TileKey> Directions
        { get; } = new()
                    {
                        { Direction.North, new TileKey(0, -1, 1) },
                        { Direction.NorthEast, new TileKey(1, -1, 0) },
                        { Direction.SouthEast, new TileKey(1, 0, -1) },
                        { Direction.South, new TileKey(0, 1, -1) },
                        { Direction.SouthWest, new TileKey(-1, 1, 0) },
                        { Direction.NorthWest, new TileKey(-1, 0, 1) }
                    };
     
        public override string ToString()
        {
            return $"({Q},{R},{S})";
        }
        public static TileKey? FromString(string str)
        {
            string[] tokens = str.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (tokens is null || tokens.Length != 3) return null;
            var q = Int32.Parse(tokens[0]);
            var r = Int32.Parse(tokens[1]);
            var s = Int32.Parse(tokens[2]);
            return new TileKey(q, r, s);
        }
        public static TileKey operator +(TileKey x, TileKey y)
        {
            return new TileKey(x.Q + y.Q, x.R + y.R, x.S + y.S);
        }
        //
        //  this will be the TileKeys of the North tile.  depending on the 
        //  collection, the tile might not exist
        public TileKey North => this + Directions[Direction.North];
        public TileKey NorthEast => this + Directions[Direction.NorthEast];
        public TileKey SouthEast => this + Directions[Direction.SouthEast];
        public TileKey South => this + Directions[Direction.South];
        public TileKey SouthWest => this + Directions[Direction.SouthWest];
        public TileKey NorthWest => this + Directions[Direction.NorthWest];
        public TileKey GetAdjacentTile(Direction dir) => this + Directions[dir];
        public override bool Equals(object? obj)
        {
            if (obj == null) return false;
            return obj is TileKey key &&
                   Q == key.Q &&
                   R == key.R &&
                   S == key.S;
        }
        public override int GetHashCode() => HashCode.Combine(Q, R, S);
        public static TileKey Default => new(-10, -10, -10);
        public static bool operator ==(TileKey left, TileKey right)
        {
            if (left is null || right is null)
            {
                return false;
            }
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            return left.Equals(right);
        }
        public static bool operator !=(TileKey left, TileKey right) => !( left == right );
        public double Top(BoardLayout layout)
        {
            var top =  ( .5 * Q +  R)*layout.OuterHexSize * Math.Sqrt(3) ;
            top += 2 * layout.OuterHexSize * Math.Sqrt(3);
            top = Math.Round(top + layout.BuildingSize * .5, 1); // the buildings will go on top of the highest tile, give them room
            top += layout.InnerHexStrokeThickness + layout.GameMargin;
            return top;
        }
        public double Left(BoardLayout layout)
        {
            var left = 2 * layout.OuterHexSize * .75 * Q ;
            left += layout.ColumnOffset * 2 * layout.OuterHexSize;
            left += ( layout.BuildingSize * 0.5 );
            left += layout.InnerHexStrokeThickness * 0.5;
            left += layout.GameMargin;

            return left;
        }
    }
}
