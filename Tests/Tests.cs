using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Catan3.Models;

namespace Catan3.Tests
{
    class CatanTests
    {

        List<Func<Task>> TestFunctions { get; } = [];

        public CatanTests()
        {
            TestFunctions.Add(TestGameRollModelSerialization);
            TestFunctions.Add(TestPlayerDatabaseSerialization);

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
