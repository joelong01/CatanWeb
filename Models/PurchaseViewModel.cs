using System.Windows.Input;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
namespace Catan3.Models
{
    public partial class EntitlementPurchaseModel : ObservableObject
    {
        /// <summary>
        ///  the Entitlement that will be purchased
        /// </summary>
        [ObservableProperty]
        private Entitlement _entitlement;
        /// <summary>
        ///  is the user allowed to buy this entitlement at this time
        /// </summary>
        [ObservableProperty]
        private bool _enabled = false;
        public EntitlementPurchaseModel(Entitlement entitlement)
        {
            Entitlement = entitlement;
        }
    }
    public partial class EntitlementPurchaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private EntitlementPurchaseModel _entitlementPurchaseModel;
        [ObservableProperty]
        private int _unspent = 0;
        [ObservableProperty]
        private CatanOrientation _orientation= CatanOrientation.FaceDown;
        [ObservableProperty]
        private string _description;
        [ObservableProperty]
        private string _glyph;
      
        [ObservableProperty]
        private ICommand _command;
        [ObservableProperty]
        private Brush _foreground = StaticBrushes.BlackBrush;
        [ObservableProperty]
        private Brush _background = StaticBrushes.WhiteBrush;
        public void Merge(EntitlementPurchaseModel dataModel, int unspent, Brush foreground, Brush background)
        {
            Foreground = foreground;
            Background = background;
            Orientation = dataModel.Enabled ? CatanOrientation.FaceUp : CatanOrientation.FaceDown;
            Unspent = unspent;
            EntitlementPurchaseModel = dataModel;
        }
        public  EntitlementPurchaseViewModel(ICommand command, EntitlementPurchaseModel dataModel, Brush foreground, Brush background)
        {
            Foreground = foreground;
            Background = background;
            CatanFont.EntitlementGlyph.TryGetValue(dataModel.Entitlement, out string? glyph);
            if (glyph is null)
            {
                throw new GameException($"{dataModel.Entitlement} is not in the EntitlementGlyph dictionary.  did you forget to add it?");
            }
            Description = dataModel.Entitlement.Description();
            Glyph = glyph;
            EntitlementPurchaseModel = dataModel;
            Command = command;
        }
    }
}
