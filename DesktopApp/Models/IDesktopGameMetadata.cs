using Catan3.Shared.Models;

using BoardVisualLayout = Catan3.DesktopApp.Layout.BoardVisualLayout;

namespace Catan3.DesktopApp.Models
{
    /// <summary>
    /// Desktop-specific extension of IGameMetadata that adds UI layout information
    /// </summary>
    public interface IDesktopGameMetadata : IGameMetadata
    {
        BoardVisualLayout Layout { get; }
    }
}
