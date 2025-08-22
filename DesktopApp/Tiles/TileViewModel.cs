using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.Activation;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Catan3.DesktopApp.Layout;
using BoardVisualLayout = Catan3.DesktopApp.Layout.BoardVisualLayout;
namespace Catan3.Models
{
    /// <summary>
    ///     this is the partial class to the template generated TileViewModel.  we subscribe to change events for Layout changes
    ///     and then update the layout calculations based on updates to the layoutproperties (Hex Size, Gap, Stroke, etc.)
    /// </summary>
    public partial class TileViewModel : ObservableRecipient
    {
        public TileViewModel(TileModel tile, BoardVisualLayout? layout)
        {
            Tile = tile;
            Layout = layout;
            IsActive = true;
            if (Layout is not null and BoardVisualLayout rbl)
            {
                rbl.PropertyChanged += Layout_PropertyChanged;
            }
            UpdateLayout();
            Messenger.Register<UpdateOrientation>(this, (recipient, message) =>
            {
                this.Orientation = message.Orientation;
            });
            Messenger.Register<EndGame>(this, (recipient, message) =>
            {
                Messenger.UnregisterAll(this);
            });
            Messenger.Register(this, (object recipient, GameStateChanged message) =>
            {
               switch (message.State)
                {
                    case Shared.Models.GameState.AllocateResourceForward:
                    case Shared.Models.GameState.AllocateResourceReverse:
                    case Shared.Models.GameState.MustMoveRobber:
                        TileIndexVisibility = Visibility.Visible;
                        break;
                    default:
                        TileIndexVisibility = Visibility.Collapsed;
                        break;
                }
            });

        }
       
        private void RegisterTargetMessageResponse()
        {
            if (this.Messenger.IsRegistered<TileOwnersResponse>(this))
            {
                this.TraceMessage($"{this} is already registered!");
                return;
            }
            this.Messenger.Register<TileOwnersResponse>(this, (recipient, message) =>
            {
                this.TraceMessage($"{this} response received ");
                try
                {
                    Targets.Clear();
                    if (this.Tile.ResourceTileType != ResourceType.Desert) // 1/14/2025: Desert tiles never target
                    {
                        foreach (var owner in message.Owners)
                        {
                            var target = new TargetViewModel(owner.Name, owner.Id);
                            if (!Targets.Contains(target))
                            {
                                Targets.Add(target);
                            }
                        }
                    }
                    // 1/14/2025: Always add an option to not target somebody
                    Targets.Add(new TargetViewModel("Nobody. Hatred Deferred.", null));
                }
                finally
                {
                   // this.TraceMessage($"{this} unregistering for response");
                    this.Messenger.Unregister<TileOwnersResponse>(this);
                }
            });
        }
        [RelayCommand]
        public void Target()
        {
            this.TraceMessage("sending target message");
            RegisterTargetMessageResponse();
            Messenger.Send(new RequestTileOwners(this)); ;
        }
        [RelayCommand]
        public void TargetPicked(string? id)
        {
         //   this.TraceMessage($"targeting {id}");
            this.Messenger.Send<MoveRobberMessage>(new MoveRobberMessage(this.Tile.TileKey, id));
        }
        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not null and BoardVisualLayout layout)
            {
                // this.TraceMessage($"{e.PropertyName} changed for tile {Tile.HexCoordinates}");
                Layout = layout;
                UpdateLayout();
                if (e.PropertyName == nameof(BoardVisualLayout.OuterHexSize))
                {
                    // The extension methods InnerHexPoints() and OuterHexPoints() depend on OuterHexSize
                    // so we notify about the layout properties that actually change
                    OnPropertyChanged(nameof(Layout.InnerHexSize));
                    OnPropertyChanged(nameof(Layout.ControlHeight));
                    OnPropertyChanged(nameof(Layout.ControlWidth));
                }
            }
        }
        private void UpdateLayout()
        {
            if (Layout != null)
            {
                Left = Layout.Left(Tile.TileKey);
                Top = Layout.Top(Tile.TileKey);
            }
        }
        public override string ToString()
        {
            return Tile.ToString();
        }
        public void TraceIfFirst()
        {
            if (this.Tile.TileKey.Q == 0 && this.Tile.TileKey.R == 0 && this.Tile.TileKey.S == 0)
            {
                this.TraceMessage($"[{Tile}]:[Left={Left}][top={Top}]");
            }
        }
        public CatanOrientation TempGoldOrientation(TileModel _, bool tempGold)
        {
            Debug.Assert(tempGold == this.Tile.TemporarilyGold);
            
            var orientation =  tempGold ? CatanOrientation.FaceUp : CatanOrientation.FaceDown;
            return orientation;
        }
        /// <summary>
        ///     if any of these 3 things change, we need to update the resource type image
        /// </summary>
        /// <param name="tileModel"></param>
        /// <param name="tempGold"></param>
        /// <param name="resourceTileType"></param>
        /// <returns></returns>
        public Brush GetTileResourceType(TileModel _, bool tempGold, ResourceType resourceTileType)
        {
           
        
            var resourceType = tempGold ? ResourceType.GoldMine : resourceTileType;
            string key = $"ResourceTileType.{resourceType}";
            var brush =  ( ImageBrush )Application.Current.Resources[key];
            return ( Brush )brush;
        }
        public Brush GetTileBorderBrush(TileModel _, bool highlighted)
        {
            if (!highlighted)
            {
                return ( Brush )Application.Current.Resources["bmMaple"];
            }
          
            return ( Brush )BrushCache.GetSolidColorBrush(Colors.Yellow);
        }
        public ImageBrush GetResourceImage(TileModel tileModel, ResourceType resourceType)
        {
            // this.TraceMessage($"Resource: {resourceCardType}");
            string key = $"ResourceCard.{resourceType}";
            var result =  ( ImageBrush )Application.Current.Resources[key];
            Debug.Assert(result is not null);
           
            return result;
        }
    }
}
