using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace Catan3
{
    public static class PointExtensions
    {
        public static Point Offset(this Point point, double x, double y)
        {
            return new Point(point.X + x, point.Y + y);
        }
    }

    public static class StaticBrushes
    {
        public static readonly SolidColorBrush RedBrush = new(Colors.Red);
        public static readonly SolidColorBrush BlackBrush = new(Colors.Black);
        public static readonly SolidColorBrush WhiteBrush = new(Colors.White);
    }
    public static class ListExtensions
    {
        public static string ListToCsv<T>(this IEnumerable<T> list)
        {
            string s = String.Empty;
            int c = list.Count();
            for (int i = 0; i < c - 1; i++)
            {
                s += list.ElementAt(i)?.ToString();
                s += ",";
            }
            s += list.ElementAt(c - 1)?.ToString();
            return s;
        }

        public static PointCollection Clone(this PointCollection points)
        {
            var clonedPoints = new PointCollection();
            foreach (var point in points)
            {
                clonedPoints.Add(point);
            }
            return clonedPoints;
        }
    }
    public static class BrushCache
    {
        private static readonly Dictionary<Color, SolidColorBrush> solidBrushes = [];
        private static readonly Dictionary<(Color, Color), LinearGradientBrush> gradientBrushes = [];
        public static SolidColorBrush GetSolidColorBrush(Color color)
        {
            if (!solidBrushes.TryGetValue(color, out var brush))
            {
                brush = new SolidColorBrush(color);

                solidBrushes[color] = brush;
            }
            return brush;
        }
        public static LinearGradientBrush GetGradientBrush(Color startColor, Color endColor)
        {
            var key = (startColor, endColor);
            if (!gradientBrushes.TryGetValue(key, out var brush))
            {
                // Create GradientStopCollection and add two GradientStops for start and end colors
                GradientStopCollection gradientStopCollection =
                [
                    new GradientStop { Color = startColor, Offset = 0.0 },
                    new GradientStop { Color = endColor, Offset = 1.0 }
                ];

                // Create the LinearGradientBrush
                brush = new LinearGradientBrush
                {
                    GradientStops = gradientStopCollection,
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1)
                };

                gradientBrushes[key] = brush;
            }
            return brush;
        }
    }
    public static class Extensions
    {
        public static void TraceMessage(this object o, string toWrite, int indentLevel = 0, [CallerMemberName] string cmb = "", [CallerLineNumber] int cln = 0, [CallerFilePath] string cfp = "")
        {
            for (int i = 0; i < indentLevel; i++)
            {
                Debug.Indent();
            }
            Debug.WriteLine($"{cfp}({cln}):{toWrite}\t\t[Caller={cmb}]");
            for (int i = 0; i < indentLevel; i++)
            {
                Debug.Unindent();
            }
        }
    }
}
