using System;
using Catan3.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
namespace Catan3.Utility
{
    public sealed partial class FlipperCtrl : Control
    {
        public FlipperCtrl()
        {
            this.DefaultStyleKey = typeof(FlipperCtrl);
        }
        public static readonly DependencyProperty FlipsProperty = DependencyProperty.Register("Flips", typeof(bool), typeof(FlipperCtrl), new PropertyMetadata(true));
           public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register("Orientation", typeof(CatanOrientation), typeof(FlipperCtrl), new PropertyMetadata(CatanOrientation.FaceDown, OrientationChanged));
        public CatanOrientation Orientation
        {
            get => ( CatanOrientation )GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }
        private static void OrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as FlipperCtrl;
            depPropClass?.SetOrientation(( CatanOrientation )e.OldValue, ( CatanOrientation )e.NewValue);
        }
        private void SetOrientation(CatanOrientation oldValue, CatanOrientation newValue)
        {
            if (oldValue == newValue) return;
         
          //  this.TraceMessage($"old orientation: {oldValue} New:{newValue}");
            if (Orientation == CatanOrientation.FaceUp)
            {
                FlipToFaceUp(Back, Front);
            }
            else
            {
                FlipToFaceDown(Back, Front);
            }
        }
        public void FlipToFaceUp(FrameworkElement back, FrameworkElement front)
        {
            // Animate CANVAS_FaceDown to 90 degrees
            AnimateRotation(back, 0, 90, () =>
            {
                AnimateRotation(front, -90, 0, null); // No further action on completion
            });
        }
        public void FlipToFaceDown(FrameworkElement back, FrameworkElement front)
        {
            // Animate CANVAS_FaceUp to 90 degrees
            AnimateRotation(front, 0, 90, () =>
            {
                // Once CANVAS_FaceUp is flipped, start animating CANVAS_FaceDown from -90 to 0 degrees
                AnimateRotation(back, -90, 0, null); // No further action on completion
            });
        }
        private static void AnimateRotation(FrameworkElement element, double from, double to, Action? onAnimationCompleted)
        {
//            element.TraceMessage($"From:{from} To:{to} for:{element.Name}");
            if (element.Projection == null)
            {
                element.Projection = new PlaneProjection();
            }
            var da = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(0.250)
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
        public static readonly DependencyProperty FrontProperty = DependencyProperty.Register("Front", typeof(FrameworkElement), typeof(FlipperCtrl), new PropertyMetadata(null, FrontChanged));
        public FrameworkElement Front
        {
            get => ( FrameworkElement )GetValue(FrontProperty);
            set => SetValue(FrontProperty, value);
        }
        private static void FrontChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as FlipperCtrl;
            var depPropValue = (FrameworkElement)e.NewValue;
            depPropClass?.SetFront(depPropValue);
        }
        private void SetFront(FrameworkElement front)
        {
            if (front is not null)
            {
                if (front.Projection is not PlaneProjection projection)
                {
                    projection = new PlaneProjection();
                    front.Projection = projection;
                }
                projection.RotationY = ( this.Orientation == CatanOrientation.FaceDown ) ? -90 : 0;
            }
        }
        public static readonly DependencyProperty BackProperty = DependencyProperty.Register("Back", typeof(FrameworkElement), typeof(FlipperCtrl), new PropertyMetadata(null, BackChanged));
        public FrameworkElement Back
        {
            get => ( FrameworkElement )GetValue(BackProperty);
            set => SetValue(BackProperty, value);
        }
        private static void BackChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var depPropClass = d as FlipperCtrl;
            var depPropValue = (FrameworkElement)e.NewValue;
            depPropClass?.SetBack(depPropValue);
        }
        private void SetBack(FrameworkElement back)
        {
            if (back is not null)
            {
                if (back.Projection is not PlaneProjection projection)
                {
                    projection = new PlaneProjection();
                    back.Projection = projection;
                }
                projection.RotationY = ( this.Orientation == CatanOrientation.FaceDown ) ? 0 : 90;
            }
        }
    }
}
