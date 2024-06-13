using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Catan3.Models;
using Catan3.Utility;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
namespace Catan3
{

    public static class PlayerDatabase
    {
        /// <summary>
        ///     Fully qualified path to the location that should be used to store the cropped image.
        ///     The filename is in "My Documents\Catan Saved Games\Players\<player_id>_cropped_<salt>.png"
        ///     where <salt> is a number we increment.
        /// </summary>
        /// <param name="playerId">The PlayerId of the player to update.  Will throw if the playerId is bad.</param>
        /// <returns>The fully qualified path to the location that should be used to store the cropped image.</returns>
        /// <exception cref="GameException"></exception>
        public static string GetNextCroppedFileName(string playerId)
        {
            var playerViewModel = FromId(playerId) ?? throw new GameException($"Bad PlayerId {playerId}");
            var fqn = playerViewModel.CroppedImageUri;
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
            var newFileName = $"{parts[0]}_{parts[1]}_{newSalt}.png";
            var newFqn = Path.Combine(folderPath, newFileName);

            return newFqn;
        }
        public static List<PlayerViewModel> AvailablePlayers { get; private set; } = [];
        public static List<PlayerViewModel> DefaultPlayers { get; } =
            [
                new ("Joe-001",    "Joe",     "ms-appx:///Assets/DefaultPlayers/Joe.jpg",    "ms-appx:///Assets/DefaultPlayers/Joe.jpg",    Colors.Blue),
                new ("Dodgy-001",  "Dodgy",   "ms-appx:///Assets/DefaultPlayers/Dodgy.png",  "ms-appx:///Assets/DefaultPlayers/Dodgy.png",  Colors.Red),
                new ("Doug-001",   "Doug",    "ms-appx:///Assets/DefaultPlayers/Doug.jpg",   "ms-appx:///Assets/DefaultPlayers/Doug.jpg",   Colors.Green),
                new ("Ryan-001",   "Ryan",    "ms-appx:///Assets/DefaultPlayers/Ryan.jpg",   "ms-appx:///Assets/DefaultPlayers/Ryan.jpg",   Colors.DarkGray),
                new ("Adrian-001", "Adrian",  "ms-appx:///Assets/DefaultPlayers/Adrian.jpg", "ms-appx:///Assets/DefaultPlayers/Adrian.jpg", Colors.Purple),
                new ("Chris-001",  "Chris",   "ms-appx:///Assets/DefaultPlayers/Chris.jpg",  "ms-appx:///Assets/DefaultPlayers/Chris.jpg",  Colors.Black)
                    ];
        public static PlayerViewModel? FromId(string id)
        {
            return AvailablePlayers.FirstOrDefault(x => x.Id == id);
        }
        public static string PlayerFolder => "Catan Saved Games\\Players";
        public static string PlayersFileName => "AllPlayers.json";
        public static async Task LoadPlayerDatabase()
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
                        AvailablePlayers.Clear();
                        AvailablePlayers = [.. players];
                        Debug.Assert(AvailablePlayers.Count > 0);
                    }
                }
                if (AvailablePlayers.Count == 0)
                {
                    AvailablePlayers = await SaveDefaultPlayers(folder, PlayersFileName);
                }
                AvailablePlayers.ForEach((p) => p.InitializeAfterDeserialization());
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Access denied: {ex}");
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"I/O error: {ex}");
            }
            catch (Exception ex)
            {
                PlayersFileName.TraceMessage($"Exception: {ex}");
            }

        }
        public static JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions
        {

            WriteIndented = true,
        };
        private static async Task<List<PlayerViewModel>> SaveDefaultPlayers(StorageFolder folder, string fileName)
        {
            try
            {
                List<PlayerViewModel> result = [];
                foreach (var player in DefaultPlayers)
                {
                    await CopyResourceFile(folder, player.CroppedImageUri, $"{player.Id}_cropped.png");
                    await CopyResourceFile(folder, player.ImageUri, $"{player.Id}_image.png");
                    string playerJson = JsonSerializer.Serialize(player, PlayerDatabase.JsonSerializerOptions);
                    PlayerViewModel? playerCopy = JsonSerializer.Deserialize<PlayerViewModel>(playerJson,  PlayerDatabase.JsonSerializerOptions);
                    if (playerCopy is not null)
                    {
                        playerCopy.ImageUri = Path.Join($"{folder.Path}", $"{player.Id}_image.png");
                        playerCopy.CroppedImageUri = Path.Join($"{folder.Path}", $"{player.Id}_cropped.png");
                        result.Add(playerCopy);
                    }
                    else
                    {
                        Application.Current.TraceMessage($"Unable to Deserialize {playerJson}");
                    }

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
        public static async Task SavePlayers()
        {
            string json = JsonSerializer.Serialize(AvailablePlayers);
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

    }
}
