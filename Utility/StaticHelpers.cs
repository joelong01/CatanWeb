using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;
using Windows.UI;

namespace Catan3
{
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

    //public static class DragAndDrop
    //{
    //    public interface IDragAndDropProgress
    //    {
    //        #region Methods

    //        void Report(PointerRoutedEventArgs e, Point value);

    //        #endregion Methods
    //    }
    //    public static Task<Point> DragAsync(UIElement control, PointerRoutedEventArgs origE, IDragAndDropProgress? progress = null)
    //    {
    //        TaskCompletionSource<Point> taskCompletionSource = new TaskCompletionSource<Point>();
    //        UIElement mousePositionWindow = Window.Current.Content;
    //        GeneralTransform gt = Window.Current.Content.TransformToVisual(control);
    //        UIElement root = Window.Current.Content;

    //        Point pointMouseDown = gt.TransformPoint(origE.GetCurrentPoint(mousePositionWindow).Side);


    //        PointerEventHandler pointerMovedHandler = (object s, PointerRoutedEventArgs e) =>
    //        {
    //            Point pt = e.GetCurrentPoint(mousePositionWindow).Side;
    //            pt = gt.TransformPoint(pt);
    //            Point delta = new Point
    //            {
    //                X = pt.X - pointMouseDown.X,
    //                Y = pt.Y - pointMouseDown.Y
    //            };

    //            if (!( control.RenderTransform is CompositeTransform compositeTransform ))
    //            {
    //                compositeTransform = new CompositeTransform();
    //                control.RenderTransform = compositeTransform;
    //            }
    //            compositeTransform.TranslateX += delta.X;
    //            compositeTransform.TranslateY += delta.Y;
    //            control.RenderTransform = compositeTransform;
    //            pointMouseDown = pt;
    //            if (progress != null)
    //            {
    //                progress.Report(e, pt);
    //            }
    //        };
    //    }
    //}
}
