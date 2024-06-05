

using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace Catan3.Models
{
    public partial class RobberViewModel : ObservableRecipient
    {
        [JsonIgnore]
        [ObservableProperty]
        private RobberModel _robberModel;

        [ObservableProperty]
        private CatanOrientation _orientation = CatanOrientation.FaceUp;


        public RobberViewModel(RobberModel robberModel)
        {
            _robberModel = robberModel;
            Messenger.Register<UpdateOrientation>(this, (recipient, message) =>
            {
                this.Orientation = message.Orientation;
            });
            Messenger.Register<EndGame>(this, (recipient, message) =>
            {
                Messenger.UnregisterAll(this);
            });
            Messenger.Register<PlayerColorChanged>(this, (recipient, message) =>
            {
                if (message.Player.Id == RobberModel.MovedBy)
                {
                    OnPropertyChanged(nameof(Foreground));
                    OnPropertyChanged(nameof(Background));
                }
            });
        }

        public static RobberViewModel Default { get; } = new RobberViewModel(new());
        /// <summary>
        ///     Binding function for calculated lproperty Background (the color of the shield)
        ///     we must pass robberModel in here because when you Undo/Redo, the RobberModel
        ///     is set on the RobberViewModel which means that OnPropertyChanged(namedof(RobberModel))
        ///     is called, and the bindings depend on the RobberModel, so they are updated and 
        ///     this function is called.
        /// </summary>
        /// <param name="robberModel"></param>
        /// <param name="playerId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Brush Background(RobberModel robberModel, string playerId)
        {

            if (playerId is not null)
            {
                PlayerViewModel owner = PlayerDatabase.FromId(playerId) ?? throw new Exception($"Bad PlayerId: {playerId}");

                return owner.PlayerColors.BackgroundBrush;
            }
            else
            {
                return BrushCache.GetGradientBrush(Colors.White, Colors.Black);
            }
        }

        public Brush Foreground(RobberModel robberModel, string playerId)
        {
            if (playerId is not null)
            {
                PlayerViewModel owner = PlayerDatabase.FromId(playerId) ?? throw new Exception($"Bad PlayerId: {playerId}");
                return owner.PlayerColors.ForegroundBrush;
              
            }
            else
            {
                return BrushCache.GetSolidColorBrush(Colors.Red);
            }
        }

        public string BIND_ResourcesStolen(RobberModel robberModel, int stolen)
        {
            Debug.Assert(robberModel.ResourcesStolen == stolen);
            return stolen.ToString();
        }

        public override string ToString()
        {
            return RobberModel.ToString();
        }
    }
}
