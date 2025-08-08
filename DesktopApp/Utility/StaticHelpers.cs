using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Catan3.Shared.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.UI;
namespace Catan3
{
    public static class EnumExtensions
    {
        #region Methods
        public static string Description(this Enum instance)
        {
            string output = "";
            Type type = instance.GetType();
            if (type is null) return String.Empty;
            FieldInfo? fi = type.GetField(instance.ToString());
            if (fi is null) return String.Empty;
            DescriptionAttribute[]? attrs = fi.GetCustomAttributes(attributeType: typeof(DescriptionAttribute), false) as DescriptionAttribute[];
            if (attrs is not null && attrs.Length > 0)
            {
                output = attrs[0].Description;
            }
            return output;
        }
        #endregion Methods
    }
    public static class AnimationHelpers
    {
        public static void FlipToFaceUp(FrameworkElement faceDown, FrameworkElement faceUp)
        {
            // Animate CANVAS_FaceDown to 90 degrees
            AnimateRotation(faceDown, 0, 90, () =>
            {
                //  CANVAS_FaceDown.Visibility = Visibility.Collapsed;
                // Once CANVAS_FaceDown is flipped, start animating CANVAS_FaceUp from -90 to 0 degrees
                //  CANVAS_FaceUp.Visibility = Visibility.Visible;
                AnimateRotation(faceUp, -90, 0, null); // No further action on completion
            });
        }
        public static void FlipToFaceDown(FrameworkElement faceDown, FrameworkElement faceUp)
        {
            // Animate CANVAS_FaceUp to 90 degrees
            AnimateRotation(faceUp, 0, 90, () =>
            {
                // Once CANVAS_FaceUp is flipped, start animating CANVAS_FaceDown from -90 to 0 degrees
                AnimateRotation(faceDown, -90, 0, null); // No further action on completion
            });
        }
        private static void AnimateRotation(FrameworkElement element, double from, double to, Action? onAnimationCompleted)
        {
            if (element.Projection == null)
            {
                element.Projection = new PlaneProjection();
            }
            var da = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(0.5)
            };
            var storyboard = new Storyboard();
            storyboard.Children.Add(da);
            Storyboard.SetTarget(da, element.Projection);
            Storyboard.SetTargetProperty(da, "RotationY");
            if (onAnimationCompleted != null)
            {
                storyboard.Completed += (s, e) => onAnimationCompleted();
            }
            storyboard.Begin();
        }
    }
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
            if (list.Count() == 0) return "Empty";
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
        public static void InsertSorted<T>(this IList<T> collection, T item) where T : IComparable<T>
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            int index = 0;
            while (index < collection.Count && collection[index].CompareTo(item) < 0)
            {
                index++;
            }
            collection.Insert(index, item);
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
        public static ImageBrush ResourceCardImage(ResourceType resourceType)
        {
            try
            {
                string key = $"ResourceCard.{resourceType}";
                var result =  ( ImageBrush )Application.Current.Resources[key];
                Debug.Assert(result is not null);
                return result;
            }
            catch
            ( Exception ex )
            {
                resourceType.TraceMessage($"{ex.Message}");
                return ( ImageBrush )Application.Current.Resources["ResourceCard.None"];
            }
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
    public delegate void SimulatedButtonClick();
    public class ButtonLookAndFeel
    {
        public event SimulatedButtonClick? SimulatedClick;
        private bool isPointerCaptured = false;
        public ButtonLookAndFeel(Grid grid)
        {
          
            grid.PointerEntered += OnPointerEntered;
            grid.PointerExited += OnPointerExited;
            grid.PointerPressed += OnPointerPressed;
        }
        private void OnPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
           
            if (sender is Grid grid)
            {
                grid.BorderThickness = new Thickness(1);
            }
        }
        private void OnPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            
            if (sender is Grid grid)
            {
                if (!isPointerCaptured)
                {
                    grid.BorderThickness = new Thickness(0);
                }
            }
        }
   
        private void OnPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
          
            if (sender is Grid grid)
            {
                void pointerReleasedHandler(object s, PointerRoutedEventArgs origE)
                {
                    if (s is Grid releasedGrid)
                    {
                        // Check if pointer is still within the grid bounds when released
                        var point = origE.GetCurrentPoint(releasedGrid).Position;
                        bool isInside = point.X >= 0 && point.X <= releasedGrid.ActualWidth &&
                                point.Y >= 0 && point.Y <= releasedGrid.ActualHeight;
                        if (( PointerEventHandler? )pointerReleasedHandler is not null)
                        {
                            releasedGrid.PointerReleased -= pointerReleasedHandler;
                        }
                        releasedGrid.ReleasePointerCapture(origE.Pointer);
                        isPointerCaptured = false;
                        SwapColors(grid);
                        if (isInside)
                        {
                            SimulatedClick?.Invoke();
                        }
                        else
                        {
                            releasedGrid.TraceMessage("Pointer released outside the grid.");
                            grid.BorderThickness = new Thickness(0);
                        }
                    }
                }
                grid.CapturePointer(e.Pointer);
                isPointerCaptured = true;
                grid.PointerReleased += pointerReleasedHandler;
                SwapColors(grid);
            }
        }
        private void SwapColors(Grid grid)
        {
            Brush? temp = null;
            grid.BorderBrush = grid.Background;
            foreach (FrameworkElement child in grid.Children)
            {
                if (child is TextBlock tb)
                {
                    temp = tb.Foreground;
                    tb.Foreground = grid.Background;
                }
            }
            grid.Background = temp;
        }
    }
}
