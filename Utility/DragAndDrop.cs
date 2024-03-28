using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace Catan3.Utility
{
    public delegate void MouseEnterHandler(UIElement control);
    public delegate void MouseLeaveHandler(UIElement control);
    public class DragHelper
    {
        public event MouseEnterHandler? DragEnter;
        public event MouseLeaveHandler?  DragLeave;
        private UIElement? currentHoverTarget;

        public Task<Point> DragAsync<T>(UIElement root, FrameworkElement toDrag, FrameworkElement knight, PointerRoutedEventArgs origE, List<T> targets) where T : UIElement
        {
            TaskCompletionSource<Point> taskCompletionSource = new TaskCompletionSource<Point>();
            UIElement mousePositionWindow = Window.Current.Content;
            GeneralTransform gt = Window.Current.Content.TransformToVisual(toDrag);

            Point pointMouseDown = gt.TransformPoint(origE.GetCurrentPoint(mousePositionWindow).Position);

            PointerEventHandler? pointerMovedHandler = null;
            PointerEventHandler ? pointerReleasedHandler = null;
            PointerEventHandler ? pointerEnterHandler = null;
            PointerEventHandler ? pointerExitedHandler = null;

            if (toDrag.RenderTransform is not CompositeTransform compositeTransform)
            {
                compositeTransform = new CompositeTransform();
                toDrag.RenderTransform = compositeTransform;
            }

            pointerEnterHandler = (object s, PointerRoutedEventArgs e) =>
            {
                DragEnter?.Invoke(( UIElement )s);

            };

            pointerExitedHandler = (object s, PointerRoutedEventArgs e) =>
            {
                DragLeave?.Invoke(( UIElement )s);
            };

            pointerMovedHandler = (object s, PointerRoutedEventArgs e) =>
            {

                Point pt = e.GetCurrentPoint(mousePositionWindow).Position;
                pt = gt.TransformPoint(pt);
                Point delta = new Point
                {
                    X = pt.X - pointMouseDown.X,
                    Y = pt.Y - pointMouseDown.Y
                };


                compositeTransform.TranslateX += delta.X;
                compositeTransform.TranslateY += delta.Y;
                pointMouseDown = pt;


                var newHoverTarget = GetControlUnderMouse<T>(e,root, targets, knight);


                // Raise events if necessary
                if (newHoverTarget != currentHoverTarget)
                {
                    if (currentHoverTarget != null)
                    {
                        DragLeave?.Invoke(currentHoverTarget);
                    }
                    if (newHoverTarget != null)
                    {
                        DragEnter?.Invoke(newHoverTarget);
                    }
                    currentHoverTarget = newHoverTarget;
                }

            };


            pointerReleasedHandler = (object s, PointerRoutedEventArgs e) =>
            {
                UIElement localControl = (UIElement)s;
                localControl.PointerMoved -= pointerMovedHandler;
                localControl.PointerReleased -= pointerReleasedHandler;
                foreach (var t in targets)
                {
                    t.PointerEntered -= pointerEnterHandler;
                    t.PointerExited -= pointerExitedHandler;
                }
                localControl.ReleasePointerCapture(origE.Pointer);
                Point exitPoint = e.GetCurrentPoint(mousePositionWindow).Position;

                taskCompletionSource.SetResult(exitPoint);
            };

            toDrag.CapturePointer(origE.Pointer);
            toDrag.PointerMoved += pointerMovedHandler;
            toDrag.PointerReleased += pointerReleasedHandler;
            foreach (var t in targets)
            {
                t.PointerEntered += pointerEnterHandler;
                t.PointerExited += pointerExitedHandler;
            }
            return taskCompletionSource.Task;
        }

        private UIElement? GetControlUnderMouse<T>(PointerRoutedEventArgs e, UIElement start, List<T> targets, FrameworkElement skip) where T : UIElement
        {
            Point mousePositionRelativeToMainPage = e.GetCurrentPoint(start).Position;

            var elementsUnderMouse = VisualTreeHelper.FindElementsInHostCoordinates(mousePositionRelativeToMainPage, start);

            foreach (var element in elementsUnderMouse)
            {
                if (element == skip) continue;
                if (element == skip.Parent) continue;

                //if (element.GetType() == typeof(BuildingCtrl))
                //{
                //    this.TraceMessage($"BUILDING: {element as BuildingCtrl}");
                //}
                bool contains =  targets.Contains(element);
                if (contains)
                {
                    return element;

                }
            }
            return null;
        }



        public static T? FindChildControl<T>(DependencyObject control) where T : DependencyObject
        {
            int childCount = VisualTreeHelper.GetChildrenCount(control);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(control, i);
                if ( child is null)
                {
                    return null;
                }
                if (child is T)
                {
                    return ( T )child;
                }
                else
                {
                    T? childOfChild = FindChildControl<T>(child);
                    if (childOfChild is not null)
                    {
                        return childOfChild;
                    }
                }
            }
            return null;
        }

        public static FrameworkElement? GetFirstParent(FrameworkElement start, Type stopElementType)
        {
            if (start == null || stopElementType == null)
            {
                throw new ArgumentNullException();
            }

            FrameworkElement parent = start;

            while (parent != null)
            {
                parent = ( FrameworkElement )VisualTreeHelper.GetParent(parent);

                if (parent != null && stopElementType.IsInstanceOfType(parent))
                {
                    return parent;
                }
            }

            return null; // Return null if no parent of the specified type is found
        }
        public static UIElement? GetNextControlFromVisualStack(UIElement root, UIElement topWindow, Type lookFor, PointerRoutedEventArgs e, Point mousePosition)
        {
            //    this.TraceMessage($"Point {mousePosition}");

            Point mousePositionRelativeToMainPage = e.GetCurrentPoint(root).Position;

            var elementsUnderMouse = VisualTreeHelper.FindElementsInHostCoordinates(mousePositionRelativeToMainPage, topWindow);

            foreach (var element in elementsUnderMouse)
            {

                if (element.GetType() == lookFor)
                {

                    return element;

                }
            }
            return null;
        }
    }
}
