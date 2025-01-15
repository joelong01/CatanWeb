using System;
using System.Diagnostics;
using Catan3.Models;
using Catan3.Utility;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
namespace Catan3.Converters
{
    /// <summary>
    /// Converts GameState to the appropriate DataTemplate.
    /// </summary>
    public class GameStateToTemplateConverter : IValueConverter
    {
        public DataTemplate? RollOrderTemplate { get; set; }
        public DataTemplate? PlayerStatsTemplate { get; set; }

        public object? Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is GameState gameState)
            {
                return gameState switch
                {
                    GameState.FinishedRollOrder => RollOrderTemplate,
                    _ => PlayerStatsTemplate,
                };
            }
            return PlayerStatsTemplate;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// Converts a SolidColorBrush to a Color and vice versa.
    /// </summary>
    public class BrushToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is SolidColorBrush brush)
            {
                return brush.Color;
            }
            return Colors.Transparent; // Default to transparent if not applicable
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is Color color)
            {
                return new SolidColorBrush(color);
            }
            return Colors.Transparent; // Default to transparent if not applicable
        }
    }
    /// <summary>
    /// Converts a double or string to a Thickness and vice versa.
    /// </summary>
    public class DoubleToThickness : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string d && double.TryParse(d, out double n))
            {
                return new Thickness(n);
            }
            if (value is double val)
            {
                return new Thickness(val);
            }
            throw new ArgumentException($"{value} is not a double or a valid string representation of a double");
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
    /// <summary>
    /// Converts null values to a default value.
    /// </summary>
    public class NullToDefaultValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value ?? parameter ?? 0; // Example default value
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// Converts a HexPosition to a SolidColorBrush.
    /// </summary>
    public class BuildingPositionToFillConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            Color color = Colors.Red;
            if (value is HexPosition position)
            {
                color = position switch
                {
                    HexPosition.TopLeft => Colors.Red,
                    HexPosition.TopRight => Colors.Blue,
                    HexPosition.Right => Colors.Green,
                    HexPosition.BottomRight => Colors.Yellow,
                    HexPosition.BottomLeft => Colors.Purple,
                    HexPosition.Left => Colors.Black,
                    HexPosition.None => Colors.White,
                    _ => Colors.Red,
                };
            }
            return BrushCache.GetSolidColorBrush(color);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// Converts a ResourceType to an ImageBrush.
    /// </summary>
    public class ResourceTypeToImageBrush : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string key = $"ResourceTileType.{value}";
            return (ImageBrush)Application.Current.Resources[key];
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// Converts a number to Visibility, hiding the number 7.
    /// </summary>
    public class NumberToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((int)value == 7) return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// Converts a number to a Brush, setting the pips on 6 and 8 to red, otherwise white.
    /// </summary>
    public class NumberToPipsForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int intValue && (intValue == 6 || intValue == 8))
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
    /// Converts a number to Visibility for pips, based on the pip index.
    /// </summary>
    public class NumberToPipsVisibility : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int n = (int)value;
            if (n == 7) return Visibility.Collapsed;
            int pipIndex = Int32.Parse((string)parameter);
            return n switch
            {
                2 or 12 => pipIndex < 1 ? Visibility.Visible : Visibility.Collapsed,
                3 or 11 => pipIndex < 2 ? Visibility.Visible : Visibility.Collapsed,
                4 or 10 => pipIndex < 3 ? Visibility.Visible : Visibility.Collapsed,
                5 or 9 => pipIndex < 4 ? Visibility.Visible : Visibility.Collapsed,
                6 or 8 => Visibility.Visible,
                _ => throw new Exception("bad parameter in binding"),
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    /// <summary>
    /// Converts a boolean to Visibility.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool vis)
            {
                bool toCheck = true; // Default to true, adjust based on your needs
                if (parameter is string param && bool.TryParse(param, out bool parsedParam))
                {
                    toCheck = parsedParam;
                }
                return (vis == toCheck) ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts a GameState to Visibility.
    /// </summary>
    public class GameStateToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is GameState currentState)
            {
                if (parameter is string param && Enum.TryParse(param, out GameState desiredState))
                {
                    return currentState == desiredState ? Visibility.Visible : Visibility.Collapsed;
                }
                throw new ArgumentException("Parameter must be a string that holds a valid GameState.", nameof(parameter));
            }
            throw new ArgumentException("Value must be of type GameState.", nameof(value));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }



    /// <summary>
    /// Scales a double value by a specified factor.
    /// </summary>
    public class ScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double original && parameter is string param && double.TryParse(param, out double scale))
            {
                return original * scale;
            }
            if (parameter is null) return 1.0;
            Debug.Assert(false, "Value should be a double and parameter should be a valid double string");
            return 1.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
