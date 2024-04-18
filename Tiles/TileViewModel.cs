using System.ComponentModel;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
namespace Catan3.Models
{
    /// <summary>
    ///     this is the partial class to the template generated TileViewModel.  we subscribe to change events for Layout changes
    ///     and then update the layout calculations based on updates to the layoutproperties (Hex Size, Gap, Stroke, etc.)
    /// </summary>
    public partial class TileViewModel : ObservableRecipient
    {
        public TileViewModel(TileModel tile, BoardLayout? layout)
        {
            Tile = tile;
            Layout = layout;

            IsActive = true;
            if (Layout is not null && Layout is BoardLayout rbl)
            {
                rbl.PropertyChanged += Layout_PropertyChanged;

            }

            UpdateLayout();

            Messenger.Register<UpdateOrientation>(this, (recipient, message) =>
            {
                this.Orientation = message.Orientation;
            });

            TempGoldResourceCardModel = new ResourceCardModel()
            {
                ResourceType = tile.ResourceTileType.ToResourceCardType(),
                Orientation = CatanOrientation.FaceDown,
                CountVisibility = Microsoft.UI.Xaml.Visibility.Collapsed
            };

            Tile.PropertyChanged += Tile_PropertyChanged;
        }
        /// <summary>
        ///     Listen for the change to the TemporarilyGold flag and set the orientation of the TempGoldOrientation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Tile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TileModel.TemporarilyGold))
            {
                TempGoldResourceCardModel.Orientation = Tile.TemporarilyGold ? CatanOrientation.FaceUp : CatanOrientation.FaceDown;
            }
        }

        private void RegisterTargetMessageResponse()
        {
            if (this.Messenger.IsRegistered<TileOwnersResponse>(this))
            {
                this.TraceMessage($"{this} is already registerd!");
                return;
            }
            this.Messenger.Register<TileOwnersResponse>(this, (recipient, message) =>
            {
                this.TraceMessage($"{this} response recieved ");
                try
                {
                    Targets.Clear();
                    if (message.Owners.Count == 0)
                    {
                        Targets.Add(new TargetViewModel("Nobody. How Nice!", "Nameless-001"));
                        return;
                    }
                    foreach (var owner in message.Owners)
                    {
                        var target = new TargetViewModel(owner.Name, owner.Id);
                        if (!Targets.Contains(target))
                        {
                            Targets.Add(target);
                        }
                    }
                }
                finally
                {
                    this.TraceMessage($"{this} unregistering for response");
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
        public void TargetPicked(string id)
        {
            this.TraceMessage($"targetting {id}");
            this.Messenger.Send<MoveRobber>(new MoveRobber(this.Tile.TileKey, id));
        }

        private void Layout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not null && sender is BoardLayout layout)
            {

                // this.TraceMessage($"{e.PropertyName} changed for tile {Tile.HexCoordinates}");
                Layout = layout;
                UpdateLayout();
                if (e.PropertyName == nameof(BoardLayout.OuterHexSize))
                {
                    OnPropertyChanged(nameof(Layout.InnerHexPoints)); // Notify the UI to reevaluate this path
                    OnPropertyChanged(nameof(Layout.OuterHexPoints)); // Notify the UI to reevaluate this path
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
            var orientation =  tempGold ? CatanOrientation.FaceUp : CatanOrientation.FaceDown;
            TempGoldResourceCardModel.Orientation = orientation;
            return orientation;
        }
        /// <summary>
        ///     if any of these 3 things change, we need to update the resource type image
        /// </summary>
        /// <param name="tileModel"></param>
        /// <param name="tempGold"></param>
        /// <param name="resourceTileType"></param>
        /// <returns></returns>
        public Brush GetTileResourceType(TileModel _, bool tempGold, ResourceTileType resourceTileType)
        {
        
            var resourceType = tempGold ? ResourceTileType.GoldMine : resourceTileType;
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
            this.TraceMessage($"Highlighting {this}");
            return ( Brush )BrushCache.GetSolidColorBrush(Colors.Yellow);
        }

    }
}
