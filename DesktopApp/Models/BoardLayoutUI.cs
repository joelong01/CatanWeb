using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Catan3.DesktopApp.Layout
{
    /// <summary>
    /// UI-specific partial extension of BoardVisualLayout to add WinUI PointCollection properties
    /// This allows the Shared BoardLayout to be used in UI binding while keeping the core class UI-agnostic
    /// Properties are defined in Layout/BoardVisualLayout.cs
    /// </summary>
    public partial class BoardVisualLayout
    {
        // Properties InnerHexPoints, OuterHexPoints, and PointyHexPoints are defined in Layout/BoardLayout.cs
    }
}
