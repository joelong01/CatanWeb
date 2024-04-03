using System;
using System.Diagnostics;
using Catan3.Utility;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
namespace Catan3.Converters
{
    public class DoubleToThickness : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string d)
            {
                var n = Double.Parse(d);
                return new Thickness(n);
               
            }
            if (value is double val)
            {
                return new Thickness(val);
            }

            throw new ArgumentException($"{value} is not a double");
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Thickness thickness)
            {
                return thickness.Left;
            }
            throw new ArgumentException($"{value} is not a Thickness");
        }
    }

    public class NullToDefaultValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null)
            {
                // Return a default value or use the parameter to specify one
                return parameter ?? 0; // Example default value
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    public class BuildingPositionToFillConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            Color color= Colors.Red;
            if (value is HexPosition position)
            {

                switch (position)
                {
                    case HexPosition.TopLeft:
                        color = Colors.Red;
                        break;
                    case HexPosition.TopRight:
                        color = Colors.Blue;
                        break;
                    case HexPosition.Right:
                        color = Colors.Green;
                        break;
                    case HexPosition.BottomRight:
                        color = Colors.Yellow;
                        break;
                    case HexPosition.BottomLeft:
                        color = Colors.Purple;
                        break;
                    case HexPosition.Left:
                        color = Colors.Black;
                        break;
                    case HexPosition.None:
                        color = Colors.White;
                        break;
                    default:
                        break;
                }
            }
            return BrushCache.GetSolidColorBrush(color);
        }



        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    public class ResourceTypeToImageBrush : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string key = $"ResourceTileType.{value}";
            return ( ImageBrush )Application.Current.Resources[key];
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    ///     used to hide the Number on the desert
    /// </summary>
    public class NumberToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (( int )value == 7) return Visibility.Collapsed;
            return Visibility.Visible;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    ///    used to set the pips on 6 and 8 red, otherwise black.
    /// </summary>
    public class NumberToPipsForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int intValue && ( intValue == 6 || intValue == 8 ))
            {
                return StaticBrushes.RedBrush;
            }
            return StaticBrushes.WhiteBrush;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    ///     value is the number
    ///     parameter is the pip index (0-5)
    /// </summary>
    public class NumberToPipsVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int n = (int)value;
            if (n == 7) return Visibility.Collapsed;
            int pipIndex = Int32.Parse((string)parameter);
            switch (n)
            {
                case 2:
                case 12:
                    if (pipIndex < 1) return Visibility.Visible;
                    break;
                case 3:
                case 11:
                    if (pipIndex < 2) return Visibility.Visible;
                    break;
                case 4:
                case 10:
                    if (pipIndex < 3) return Visibility.Visible;
                    break;
                case 5:
                case 9:
                    if (pipIndex < 4) return Visibility.Visible;
                    break;
                case 6:
                case 8:
                    return Visibility.Visible;
                default:
                    throw new Exception("bad parameter in binding");
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool vis)
            {
                bool toCheck = true; // Default to true, adjust based on your needs
                if (parameter is string param)
                {
                    // Convert the string parameter to bool
                    toCheck = bool.Parse(param);
                }
                return ( vis == toCheck ) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    public class MetroToScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isMetropolis)
            {
                return isMetropolis ? 0.6 : 1.0;
            }
            return 1.0; // Default scale when value is null or not a boolean
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    public class ScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double original && parameter is string param)
            {
                double scale = Double.Parse(param);
                return original * scale;
            }
            if (parameter is null) return 1.0;
            Debug.Assert(false, "Value should be a double");
            return 1.0;
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
