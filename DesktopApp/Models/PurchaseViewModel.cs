using System.Windows.Input;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace Catan3.Models
{
    /// <summary>
    /// Represents a view model for an entitlement purchase, including the purchase model, unspent count, orientation, description, glyph, command, and brushes.
    /// </summary>
    public partial class EntitlementPurchaseViewModel : ObservableObject
    {
        /// <summary>
        /// Gets or sets the entitlement purchase model.
        /// </summary>
        [ObservableProperty]
        public partial EntitlementPurchaseModel EntitlementPurchaseModel { get; set; }

        /// <summary>
        /// Gets or sets the count of unspent entitlements.
        /// </summary>
        [ObservableProperty]
        public partial int Unspent { get; set; } = 0;

        /// <summary>
        /// Gets or sets the orientation of the entitlement purchase.
        /// </summary>
        [ObservableProperty]
        public partial CatanOrientation Orientation { get; set; } = CatanOrientation.FaceDown;

        /// <summary>
        /// Gets or sets the description of the entitlement purchase.
        /// </summary>
        [ObservableProperty]
        public partial string Description { get; set; }

        /// <summary>
        /// Gets or sets the glyph representing the entitlement purchase.
        /// </summary>
        [ObservableProperty]
        public partial string Glyph { get; set; }

        /// <summary>
        /// Gets or sets the command associated with the entitlement purchase.
        /// </summary>
        [ObservableProperty]
        public partial ICommand Command { get; set; }

        /// <summary>
        /// Gets or sets the foreground brush for the entitlement purchase.
        /// </summary>
        [ObservableProperty]
        public partial Brush Foreground { get; set; } = StaticBrushes.BlackBrush;

        /// <summary>
        /// Gets or sets the background brush for the entitlement purchase.
        /// </summary>
        [ObservableProperty]
        public partial Brush Background { get; set; } = StaticBrushes.WhiteBrush;

        /// <summary>
        /// Merges the specified data model, unspent count, and brushes into the current view model.
        /// </summary>
        /// <param name="dataModel">The entitlement purchase model to merge.</param>
        /// <param name="unspent">The count of unspent entitlements.</param>
        /// <param name="foreground">The foreground brush.</param>
        /// <param name="background">The background brush.</param>
        public void Merge(EntitlementPurchaseModel dataModel, int unspent, Brush foreground, Brush background)
        {
            Foreground = foreground;
            Background = background;
            Orientation = dataModel.Enabled ? CatanOrientation.FaceUp : CatanOrientation.FaceDown;
            Unspent = unspent;
            EntitlementPurchaseModel = dataModel;
        }

        /// <summary>
        /// Initializes a new instance of the EntitlementPurchaseViewModel class with the specified command, data model, and brushes.
        /// </summary>
        /// <param name="command">The command associated with the entitlement purchase.</param>
        /// <param name="dataModel">The entitlement purchase model.</param>
        /// <param name="foreground">The foreground brush.</param>
        /// <param name="background">The background brush.</param>
        /// <exception cref="GameException">Thrown when the entitlement is not found in the EntitlementGlyph dictionary.</exception>
        public EntitlementPurchaseViewModel(ICommand command, EntitlementPurchaseModel dataModel, Brush foreground, Brush background)
        {
            Foreground = foreground;
            Background = background;
            CatanFont.EntitlementGlyph.TryGetValue(dataModel.Entitlement, out string? glyph);
            if (glyph is null)
            {
                throw new GameException($"{dataModel.Entitlement} is not in the EntitlementGlyph dictionary. Did you forget to add it?");
            }
            Description = dataModel.Entitlement.Description();
            Glyph = glyph;
            EntitlementPurchaseModel = dataModel;
            Command = command;
        }
    }
}
