
using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
namespace Catan3.Models
{
    /// <summary>
    /// Represents the view model for the robber, including its model and orientation.
    /// </summary>
    public partial class RobberViewModel : ObservableRecipient
    {
        /// <summary>
        /// Gets or sets the robber model.
        /// </summary>
        [ObservableProperty]
        [JsonIgnore]
        public partial RobberModel RobberModel { get; set; }

        /// <summary>
        /// Gets or sets the orientation of the robber.
        /// </summary>
        [ObservableProperty]
        public partial CatanOrientation Orientation { get; set; } = CatanOrientation.FaceUp;

        /// <summary>
        /// Initializes a new instance of the RobberViewModel class with the specified robber model.
        /// </summary>
        /// <param name="robberModel">The robber model.</param>
        public RobberViewModel(RobberModel robberModel)
        {
            RobberModel = robberModel;
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
                if (message.PlayerColors.PlayerId == RobberModel.MovedBy)
                {
                    OnPropertyChanged(nameof(Foreground));
                    OnPropertyChanged(nameof(Background));
                }
            });
        }

        /// <summary>
        /// Gets the default instance of the RobberViewModel class.
        /// </summary>
        public static RobberViewModel Default { get; } = new RobberViewModel(new());

        /// <summary>
        /// Gets the background brush for the robber based on the player ID.
        /// </summary>
        /// <param name="robberModel">The robber model.</param>
        /// <param name="playerId">The player ID.</param>
        /// <returns>A Brush representing the background color of the shield.</returns>
        /// <exception cref="Exception">Thrown when the player ID is invalid.</exception>
        public Brush Background(RobberModel robberModel, string playerId)
        {
            if (playerId is not null && PlayerDatabase.Instance is not null)
            {
                PlayerViewModel owner = PlayerDatabase.Instance.FromId(playerId) ?? throw new Exception($"Bad PlayerId: {playerId}");
                return owner.PlayerColors.BackgroundBrush;
            }
            else
            {
                return BrushCache.GetGradientBrush(Colors.White, Colors.Black);
            }
        }

        /// <summary>
        /// Gets the foreground brush for the robber based on the player ID.
        /// </summary>
        /// <param name="robberModel">The robber model.</param>
        /// <param name="playerId">The player ID.</param>
        /// <returns>A Brush representing the foreground color of the shield.</returns>
        /// <exception cref="Exception">Thrown when the player ID is invalid.</exception>
        public Brush Foreground(RobberModel robberModel, string playerId)
        {
            if (playerId is not null && PlayerDatabase.Instance is not null)
            {
                PlayerViewModel owner = PlayerDatabase.Instance.FromId(playerId) ?? throw new Exception($"Bad PlayerId: {playerId}");
                return owner.PlayerColors.ForegroundBrush;
            }
            else
            {
                return BrushCache.GetSolidColorBrush(Colors.Red);
            }
        }

        /// <summary>
        /// Binds the resources stolen by the robber to a string representation.
        /// </summary>
        /// <param name="robberModel">The robber model.</param>
        /// <param name="stolen">The number of resources stolen.</param>
        /// <returns>A string representing the number of resources stolen.</returns>
        public string BIND_ResourcesStolen(RobberModel robberModel, int stolen)
        {
            Debug.Assert(robberModel.ResourcesStolen == stolen);
            return stolen.ToString();
        }

        /// <summary>
        /// Returns a string representation of the RobberViewModel.
        /// </summary>
        /// <returns>A string representing the RobberViewModel.</returns>
        public override string ToString()
        {
            return RobberModel.ToString();
        }
    }
}
