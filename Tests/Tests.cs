using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Catan3.Controller;
using Catan3.Models;
using Catan3.Utility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Windows.Storage;


namespace Catan3.Tests
{



    public class TestProxy : ObservableRecipient, IDisposable
    {
        public GameController GameController { get; internal set; }
        private TaskCompletionSource<GameModel>? _tcs;

        public TestProxy(string filename)
        {
            GameController = new GameController(MainWindow.FileService, GenerateSavedFileName(filename));
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
            Messenger.Send(new NewGameMessage(gameType, playerIds, savedFileName)); ;
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

            return Path.Join(KnownFolders.DocumentsLibrary.Path, "Catan Saved Games", "Tests", testName);

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

    [TestClass]
    public class HexGeometryTests
    {
        [TestMethod]
        public void FlatTopHexPoints_ReturnsSixPoints()
        {
            double size = 10.0;
            var points = HexGeometry.FlatTopHexPoints(size, 0, 0);
            Assert.AreEqual(6, points.Count, "FlatTopHexPoints should return 6 points.");
        }

        [TestMethod]
        public void FlatTopHexPoints_KnownPositions()
        {
            double size = 10.0;
            var points = HexGeometry.FlatTopHexPoints(size, 0, 0);
            double expectedX = 2 * size;
            double expectedY = Math.Round(Math.Sqrt(3) * size / 2, 2);
            Assert.AreEqual(expectedX, points[0].X, 0.01, "First point X should be at rightmost.");
            Assert.AreEqual(expectedY, points[0].Y, 0.01, "First point Y should be at vertical center.");
        }

        [TestMethod]
        public void FlatTopHexPoints_CacheWorks()
        {
            double size = 12.0;
            int initialHit = HexGeometry.CacheHit;
            int initialMiss = HexGeometry.CacheMiss;
            var points1 = HexGeometry.FlatTopHexPoints(size, 0, 0);
            var points2 = HexGeometry.FlatTopHexPoints(size, 0, 0);
            Assert.IsTrue(HexGeometry.CacheHit > initialHit, "CacheHit should increase on repeated call.");
            Assert.IsTrue(HexGeometry.CacheMiss > initialMiss, "CacheMiss should increase on first call.");
        }

        [TestMethod]
        public void PointyTopHexPoints_ReturnsSixPoints()
        {
            double size = 10.0;
            var points = HexGeometry.PointyTopHexPoints(size, 0, 0);
            Assert.AreEqual(6, points.Count, "PointyTopHexPoints should return 6 points.");
        }

        [TestMethod]
        public void PointyTopHexPoints_KnownPositions()
        {
            double size = 10.0;
            var points = HexGeometry.PointyTopHexPoints(size, 0, 0);
            double expectedX = Math.Round(size * Math.Cos(-Math.PI / 6), 2);
            double expectedY = Math.Round(size * Math.Sin(-Math.PI / 6), 2);
            Assert.AreEqual(expectedX, points[0].X, 0.01, "First point X should match expected.");
            Assert.AreEqual(expectedY, points[0].Y, 0.01, "First point Y should match expected.");
        }

        [TestMethod]
        public void PointyTopHexPoints_CacheWorks()
        {
            double size = 15.0;
            int initialHit = HexGeometry.CacheHit;
            int initialMiss = HexGeometry.CacheMiss;
            var points1 = HexGeometry.PointyTopHexPoints(size, 0, 0);
            var points2 = HexGeometry.PointyTopHexPoints(size, 0, 0);
            Assert.IsTrue(HexGeometry.CacheHit > initialHit, "CacheHit should increase on repeated call.");
            Assert.IsTrue(HexGeometry.CacheMiss > initialMiss, "CacheMiss should increase on first call.");
        }

        [TestMethod]
        public void Height_ReturnsCorrectValue()
        {
            double size = 7.0;
            double expected = Math.Round(size * Math.Sqrt(3), 2);
            Assert.AreEqual(expected, HexGeometry.Height(size), 0.001, "Height should be size * sqrt(3).");
        }

        [TestMethod]
        public void Width_ReturnsCorrectValue()
        {
            double size = 7.0;
            double expected = Math.Round(2 * size, 2);
            Assert.AreEqual(expected, HexGeometry.Width(size), 0.001, "Width should be 2 * size.");
        }

        [TestMethod]
        public void BisectingPoint_ReturnsCorrectMidpoint()
        {
            double size = 8.0;
            var point = HexGeometry.BisectingPoint(size);
            double expectedX = Math.Round(Math.Sqrt(3) / 2.0 * size, 2);
            double expectedY = Math.Round(size / 2.0, 2);
            Assert.AreEqual(expectedX, point.X, 0.001, "BisectingPoint X should match.");
            Assert.AreEqual(expectedY, point.Y, 0.001, "BisectingPoint Y should match.");
        }

        [TestMethod]
        public void SizeFromHeight_ReturnsCorrectSize()
        {
            double height = 17.32; // 10 * sqrt(3)
            double expected = Math.Round(height / Math.Sqrt(3), 2);
            Assert.AreEqual(expected, HexGeometry.SizeFromHeight(height), 0.001, "SizeFromHeight should be height / sqrt(3).");
        }

        [TestMethod]
        public void HexSubtract_SubtractsCoordinatesCorrectly()
        {
            var a = new HexCoordinates(3, 2, -5);
            var b = new HexCoordinates(1, 1, -2);
            var result = HexGeometry.HexSubtract(a, b);
            Assert.AreEqual(2, result.Q);
            Assert.AreEqual(1, result.R);
            Assert.AreEqual(-3, result.S);
        }

        [TestMethod]
        public void Distance_ReturnsCorrectHexDistance()
        {
            var a = new HexCoordinates(0, 0, 0);
            var b = new HexCoordinates(2, -1, -1);
            double expected = (Math.Abs(2) + Math.Abs(-1) + Math.Abs(-1)) / 2.0;
            Assert.AreEqual(expected, HexGeometry.Distance(a, b), 0.001, "Distance should be correct for cube coordinates.");
        }
    }
}
