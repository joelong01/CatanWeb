using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using Catan3.Models;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
namespace Catan3
{
    /// <summary>
    ///     this class enables view models without references to the PlayerDatabase to get information
    ///     about players via MVVM messaging.
    /// </summary>
    public class DatabaseHelper : ObservableRecipient
    {
        /// <summary>
        ///     use:
        ///     
        ///         var dbHelper = new DatabaseHelper();
        ///         var playerViewModel = await dbHelper.GetPlayerAsync(playerId);
        ///         
        /// </summary>
        /// <param name="playerId"></param>
        /// <returns></returns>

        public async Task<PlayerViewModel?> GetPlayerAsync(string playerId)
        {
            var tcs = new TaskCompletionSource<PlayerViewModel?>();
            void MessageHandler(object recipient, GetPlayerResponse message)
            {
                tcs.SetResult(message.Player);
                WeakReferenceMessenger.Default.Unregister<OpenFileResponseMessage>(this);
            }
            WeakReferenceMessenger.Default.Register<GetPlayerResponse>(this, MessageHandler);
            // Send the request message to the PlayerDatabase
            WeakReferenceMessenger.Default.Send(new GetPlayerMessage(playerId));
            return await tcs.Task;
        }
    }

    public interface IPlayerDatabase
    {

        ObservableCollection<PlayerViewModel> AllPlayers { get; }
        PlayerViewModel? FromId(string id);
        Task LoadPlayerDatabase();
        Task SavePlayers();
        Task DeletePlayer(PlayerViewModel player);
        Task AddPlayer();
        Task<string> SaveCroppedImage(PlayerViewModel player, MemoryStream memoryStream);
    }


    public partial class PlayerDatabase : ObservableRecipient, IPlayerDatabase
    {
        public static IPlayerDatabase? Instance { get; private set; }
        public PlayerDatabase()
        {
            Instance = this;
            Messenger.Register<GetPlayerMessage>(this, (recipient, message) =>
            {
                WeakReferenceMessenger.Default.Send(new GetPlayerResponse(this.FromId(message.PlayerId)));
            });
        }

        public static string DefaultImageUri => "ms-appx:///Assets/DefaultPlayers/guest.png";

        /// <summary>
        ///     Fully qualified path to the location that should be used to store the cropped image.
        ///     The filename is in "My Documents\Catan Saved Games\Players\<player_id>_cropped.png"
        ///     where player_id is guaranteed to be unique.
        ///     
        ///     the salt is needed because it changes the file name and so when the image is updated
        ///     the xaml binding info will load the new image
        ///     
        /// </summary>
        /// <param name="playerId">The PlayerId of the player to update.  Will throw if the playerId is bad.</param>
        /// <returns>The fully qualified path to the location that should be used to store the cropped image.</returns>
        /// <exception cref="GameException"></exception>
        public string GetNextImageName(string fqn, string root)
        {
            var folderPath = Path.GetDirectoryName(fqn) ?? throw new GameException($"Invalid Directory Name in PlayerDatabase {fqn}");
            if (folderPath == string.Empty)
            {
                folderPath = KnownFolders.DocumentsLibrary.Path;
            }
            var fileName = Path.GetFileNameWithoutExtension(fqn);
            var parts = fileName.Split('_');
            if (parts.Length < 3 || !int.TryParse(parts[2], out int currentSalt))
            {
                currentSalt = 0; // If there's no salt or it's invalid, start from 0
            }
            var newSalt = currentSalt + 1;
            var newFileName = $"{parts[0]}_{root}_{newSalt}.png";
            var newFqn = Path.Combine(folderPath, newFileName);

            return newFqn;
        }
        /// <summary>
        ///     this just wants the file name, not the folder
        /// </summary>
        /// <param name="player"></param>
        /// <param name="fqn"></param>
        /// <returns></returns>
        private string GetNexCroppedImageName(PlayerViewModel player, string fqn)
        {
            if (fqn.Contains("ms-appx:"))
            {


                return $"{player.Id}_cropped_001.png";

            }

            return GetNextImageName(fqn, "cropped");
        }

        private string GetNextImageName(PlayerViewModel player, string fqn)
        {
            if (fqn.Contains("ms-appx:"))
            {
                return $"{player.Id}_image_001.png";
            }

            return GetNextImageName(fqn, "image");
        }

        /// <summary>
        ///     the list of players.  the game binds to this list in NewGame and PlayerEditorPage
        ///     it probably shouldn't - instead it should have an interface for doing CRUD against
        ///     players. But that can be done later.
        /// </summary>
        [ObservableProperty]
        public partial ObservableCollection<PlayerViewModel> AllPlayers { get; set; } =  [];

        public static List<PlayerViewModel> DefaultPlayers()
        {
            return
            [
                new("Joe-001", "Joe", "ms-appx:///Assets/DefaultPlayers/Joe.jpg", "ms-appx:///Assets/DefaultPlayers/Joe.jpg", Colors.Blue),
                new("Dodgy-001", "Dodgy", "ms-appx:///Assets/DefaultPlayers/Dodgy.png", "ms-appx:///Assets/DefaultPlayers/Dodgy.png", Colors.Red),
                new("Doug-001", "Doug", "ms-appx:///Assets/DefaultPlayers/Doug.jpg", "ms-appx:///Assets/DefaultPlayers/Doug.jpg", Colors.Green),
                new("Ryan-001", "Ryan", "ms-appx:///Assets/DefaultPlayers/Ryan.jpg", "ms-appx:///Assets/DefaultPlayers/Ryan.jpg", Colors.DarkGray),
                new("Adrian-001", "Adrian", "ms-appx:///Assets/DefaultPlayers/Adrian.jpg", "ms-appx:///Assets/DefaultPlayers/Adrian.jpg", Colors.Purple),
                new("Chris-001", "Chris", "ms-appx:///Assets/DefaultPlayers/Chris.jpg", "ms-appx:///Assets/DefaultPlayers/Chris.jpg", Colors.Black)
                    ];
        }

        public static PlayerViewModel GuestPlayer => new PlayerViewModel("Guest-001", "Guest", DefaultImageUri, DefaultImageUri, Colors.HotPink);
        public PlayerViewModel? FromId(string id)
        {
            return AllPlayers.FirstOrDefault(x => x.Id == id);
        }
        public static string PlayerFolder => "Catan Saved Games\\Players";
        public static string PlayersFileName => "AllPlayers.json";
        public async Task LoadPlayerDatabase()
        {

            try
            {
                var folder = await KnownFolders.DocumentsLibrary.CreateFolderAsync(PlayerFolder, CreationCollisionOption.OpenIfExists);
                var item = await folder.TryGetItemAsync(PlayersFileName);
                if (item is not null && item is StorageFile file)
                {

                    string json = await FileIO.ReadTextAsync(file);
                    var players = JsonSerializer.Deserialize<List<PlayerViewModel>>(json);
                    if (players is not null && players.Count > 0)
                    {
                        AllPlayers.Clear();
                        AllPlayers = [.. players];
                        Debug.Assert(AllPlayers.Count > 0);
                    }
                }
                if (AllPlayers.Count == 0)
                {
                    AllPlayers = await SaveDefaultPlayers(folder, PlayersFileName);
                }
                foreach (var p in AllPlayers)
                {
                    p.InitializeAfterDeserialization();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                App.Current.TraceMessage($"Access denied: {ex}");
            }
            catch (IOException ex)
            {
                App.Current.TraceMessage($"I/O error: {ex}");
            }
            catch (Exception ex)
            {
                // Log detailed information about the exception
                App.Current.TraceMessage($"Exception: {ex.Message}");
                App.Current.TraceMessage($"Stack Trace: {ex.StackTrace}");

                // If the exception is a Win32Exception, you can log specific details
                if (ex.InnerException is System.ComponentModel.Win32Exception win32Exception)
                {
                    App.Current.TraceMessage($"Win32 Error Code: {win32Exception.ErrorCode}");
                    App.Current.TraceMessage($"Win32 Message: {win32Exception.Message}");
                }

            }

        }
        public static JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions
        {
            WriteIndented = true,
        };
        private async Task<ObservableCollection<PlayerViewModel>> SaveDefaultPlayers(StorageFolder folder, string fileName)
        {
            try
            {
                ObservableCollection<PlayerViewModel> result = [];

                foreach (var player in DefaultPlayers())
                {

                    PlayerViewModel? playerCopy = await SavePlayerLocally(folder, player);
                    if (playerCopy is not null)
                    {
                        result.Add(playerCopy);
                    }


                }

                //
                // we don't add Guest to the json of AvailablePlayers, but we save the images in the same place
                // so that we can drive the UI generic across all players, including Guest.
                var guestCopy = await SavePlayerLocally(folder, GuestPlayer);

                if (guestCopy is not null)
                {
                    result.Add(guestCopy);
                }

                string json = JsonSerializer.Serialize(result);
                var databaseFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
                await FileIO.WriteTextAsync(databaseFile, json);
                return result;
            }
            catch (Exception ex)
            {
                Application.Current.TraceMessage($"{ex}");
                throw;
            }
        }

        private async Task<PlayerViewModel?> SavePlayerLocally(StorageFolder folder, PlayerViewModel player)
        {
            var saltedCroppedUri = GetNexCroppedImageName(player, player.CroppedImageUri);
            var saltedImageUri = GetNextImageName(player, player.ImageUri);
            await CopyResourceFile(folder, player.CroppedImageUri, saltedCroppedUri);
            await CopyResourceFile(folder, player.ImageUri, saltedImageUri);
            player.CroppedImageUri = Path.Combine(folder.Path, saltedCroppedUri);
            player.ImageUri = Path.Combine(folder.Path, saltedImageUri);
            string playerJson = JsonSerializer.Serialize(player, PlayerDatabase.JsonSerializerOptions);
            PlayerViewModel? playerCopy = JsonSerializer.Deserialize<PlayerViewModel>(playerJson,  PlayerDatabase.JsonSerializerOptions);
            if (playerCopy is not null)
            {

                return playerCopy;
            }

            Application.Current.TraceMessage($"Unable to Deserialize {playerJson}");
            return null;

        }


        [RelayCommand]
        public async Task SavePlayers()
        {
            string json = JsonSerializer.Serialize(AllPlayers);
            var folder = await KnownFolders.DocumentsLibrary.CreateFolderAsync(PlayerFolder, CreationCollisionOption.OpenIfExists);
            var databaseFile = await folder.CreateFileAsync(PlayersFileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(databaseFile, json);
        }
        private static async Task CopyResourceFile(StorageFolder folder, string resourceUri, string destination)
        {
            StorageFile resourceFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(resourceUri));
            StorageFile destinationFile = await folder.CreateFileAsync(destination, CreationCollisionOption.ReplaceExisting);
            await resourceFile.CopyAndReplaceAsync(destinationFile);
        }
        /// <summary>
        ///     Removes the players from the AllPlayers collection.
        ///    
        /// </summary>
        /// <param name="playerId"></param>
        [RelayCommand]
        public async Task DeletePlayer(PlayerViewModel playerViewModel)
        {

            AllPlayers.Remove(playerViewModel);
            await SavePlayers();

        }
        [RelayCommand]
        public async Task AddPlayer()
        {
            string name = "Nameless";
            string id;
            while (true)
            {
                id = UniqueIdGenerator.GenerateUniqueId();

                if (FromId(id) is null)
                {
                    break;
                }
            }
            var player = new PlayerViewModel(id, name, DefaultImageUri, DefaultImageUri, Colors.HotPink);
            var folder = await KnownFolders.DocumentsLibrary.CreateFolderAsync(PlayerFolder, CreationCollisionOption.OpenIfExists);
            await SavePlayerLocally(folder, player);
            AllPlayers.Add(player);
            await SavePlayers();
        }

       

        public async Task<string> SaveCroppedImage(PlayerViewModel player, MemoryStream memoryStream)
        {


            // Ensure the filename is correctly assigned to the property
            var filePath = GetNexCroppedImageName(player, player.CroppedImageUri);
            string fileName = System.IO.Path.GetFileName(filePath);

            var folder = await KnownFolders.DocumentsLibrary.CreateFolderAsync(System.IO.Path.Join("Catan Saved Games", "Players"), CreationCollisionOption.OpenIfExists);
            var imageFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            try
            {


                await FileIO.WriteBytesAsync(imageFile, memoryStream.ToArray());
                await DeleteFile(player.CroppedImageUri);
                player.CroppedImageUri = filePath;
                await SavePlayers();
                return filePath;
            }
            catch (Exception ex)
            {
                // Handle the exception as needed
                System.Diagnostics.Debug.WriteLine($"Exception saving cropped image: {ex}");
            }

            return "";
        }

        private async Task DeleteFile(string fileUri)
        {
            try
            {
                Uri uri = new Uri(fileUri);

                StorageFile fileToDelete = await StorageFile.GetFileFromPathAsync(fileUri);

                await fileToDelete.DeleteAsync();
            }
            catch (Exception e)
            {
                this.TraceMessage($"Failed to delete file {fileUri}. Exception: {e}");
            }

        }
    }
}
