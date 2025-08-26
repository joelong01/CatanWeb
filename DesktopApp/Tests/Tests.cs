using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Catan3.Services;
using Catan3.Models;
using Catan3.Shared.Models;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Windows.Storage;


namespace Catan3.Tests
{



    public class TestProxy : ObservableRecipient, IDisposable
    {
        public Catan3.Shared.GameLogic.GameStateMachine GameController { get; internal set; }
        private TaskCompletionSource<GameModel>? _tcs;

        public TestProxy(string filename)
        {
            GameController = DesktopGameStateMachineFactory.Create(MainWindow.FileService, GenerateSavedFileName(filename));
            Messenger.Register<UpdateGameModel>(this, UpdateGameModel);
        }

        void UpdateGameModel(object recipient, UpdateGameModel updateMessage)
        {
            Debug.Assert(_tcs is not null);
            Debug.Assert(_tcs.Task.IsCompleted == false);
            _tcs?.TrySetResult(updateMessage.GameModel);
        }

        public Task<GameModel> NewGame(GameType gameType, IList<string> playerIds, string savedFileName)
        {
            if (_tcs is not null)
            {
                Debug.Assert(_tcs.Task.IsCompleted == true);
            }
            _tcs = new TaskCompletionSource<GameModel>(TaskCreationOptions.RunContinuationsAsynchronously);
            Messenger.Send(new Catan3.Shared.Models.NewGameMessage(gameType, playerIds));
            return _tcs.Task;
        }

        public Task<GameModel> LoadGame(string filePath)
        {

            if (_tcs is not null)
            {
                Debug.Assert(_tcs.Task.IsCompleted == true);
            }
            _tcs = new TaskCompletionSource<GameModel>(TaskCreationOptions.RunContinuationsAsynchronously);
            Messenger.Send(new LoadGameMessage(filePath)); ;
            return _tcs.Task;
        }
        public Task<GameModel> SaveAs(string filePath)
        {

            if (_tcs is not null)
            {
                Debug.Assert(_tcs.Task.IsCompleted == true);
            }
            _tcs = new TaskCompletionSource<GameModel>(TaskCreationOptions.RunContinuationsAsynchronously);
            Messenger.Send(new PersistGameMessage(LocalPersistActions.SaveAs, filePath));
            return _tcs.Task;
        }

        public void Dispose()
        {
            Messenger.UnregisterAll(this);
            Messenger.Send(new EndGame());
        }


        public static string GenerateSavedFileName(string testName)
        {
            // Use corrected Documents path to avoid truncation issues
            var documentsPath = Catan.Services.FileService.GetCorrectDocumentsPath();
            return Path.Join(documentsPath, "Catan Saved Games", "Tests", testName);
        }

    }


    class CatanTests
    {

        List<Func<Task>> TestFunctions { get; } = [];

        public CatanTests()
        {
            TestFunctions.Add(TestGameRollModelSerialization);
            TestFunctions.Add(TestPlayerDatabaseSerialization);

        }

        public async Task TestScore()
        {
            using var proxy = new TestProxy("Score Test");
            //List<string> playerIds = [];

            //for (int i = 0; i < 3; i++)
            //{
            //    playerIds.Add(MainWindow.PlayerDatabase.AllPlayers.ElementAt(i).Id);
            //}

            //var gameModel = await proxy.NewGame(GameType.Regular, playerIds, TestProxy.GenerateSavedFileName("Score Test2"));
            string appxPath = "ms-appx:///Assets/Test Files/Score Test.catan";
            var gameModel = await proxy.LoadGame(appxPath);
            gameModel = await proxy.SaveAs(TestProxy.GenerateSavedFileName("Score Test.catan"));
            this.TraceMessage($"Game Started. {gameModel.Players.Count} players");
            Debug.Assert(gameModel.Players.Count == 3);
        }

        public async Task RunAll()
        {
            List<Task> tasks = [];
            foreach (var func in TestFunctions)
            {
                tasks.Add(func());
            }

            await Task.WhenAll(tasks);
        }

        public async Task TestGameRollModelSerialization()
        {
            this.TraceMessage("TestGameRollModelSerialization...");
            try
            {

                var gameRollModel = new GameRollModel();
                gameRollModel.TotalRolls = gameRollModel.RollCounts.Length;
                for (int i = 0; i < gameRollModel.RollCounts.Length; i++)
                {
                    gameRollModel.RollCounts[i] = i;
                }


                var json = JsonSerializer.Serialize(gameRollModel);

                var grm = JsonSerializer.Deserialize<GameRollModel>(json);
                Debug.Assert(grm is not null);
                for (int i = 0; i < gameRollModel.RollCounts.Count(); i++)
                {
                    Debug.Assert(gameRollModel.RollCounts[i] == i);
                }

                var t = JsonSerializer.Deserialize<GameRollModel>(json);

            }
            catch (Exception ex)
            {
                this.TraceMessage("failed");
                this.TraceMessage($"{ex}");
                return;
            }
            this.TraceMessage("passed");
            await Task.CompletedTask;

        }


        public async Task TestPlayerDatabaseSerialization()
        {
            try
            {
                var json = JsonSerializer.Serialize(MainWindow.PlayerDatabase.AllPlayers[0]);
                var cpy = JsonSerializer.Deserialize<PlayerViewModel>(json);
                if (cpy is null)
                {
                    this.TraceMessage("FAILED to deserialize PlayerViewModel");
                    return;
                }
                this.TraceMessage($"{cpy.Id}");
            }
            catch (Exception ex)
            {
                this.TraceMessage($"FAILED to deserialize PlayerViewModel: {ex}");
                return;
            }
            try
            {
                await MainWindow.PlayerDatabase.LoadPlayerDatabase();
            }
            catch (Exception ex)
            {
                this.TraceMessage($"FAILED ayerDatabase.LoadPlayerDatabase(): {ex}");
                return;
            }


            this.TraceMessage("passed");
        }
    }
}
