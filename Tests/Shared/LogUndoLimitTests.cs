using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Xunit;

namespace Tests.Shared
{
    /// <summary>
    /// Tests for the bounded undo stack (LogConstants.MaxUndoDepth), the replay anchor
    /// capture/salvage, and ReplayableRandom restoration on undo.
    /// See .design/remove-compression.md and epic #197.
    /// </summary>
    public class LogUndoLimitTests
    {
        private const int Cap = LogConstants.MaxUndoDepth + 1; // 26

        private static Log<string> NewLog() => new(null, "undo-limit-test");

        private static GameModel Model(string name, GameState state = GameState.WaitingForNext)
            => new() { GameName = name, GameState = state };

        private static void PushStates(Log<string> log, int count, GameState state = GameState.WaitingForNext)
        {
            for (int i = 0; i < count; i++)
                log.Done(Model($"state-{i}", state));
        }

        [Fact]
        public void Done_PastCapacity_EvictsOldest_KeepingCurrent()
        {
            var log = NewLog();
            PushStates(log, 30);

            Assert.Equal(Cap, log.DoneCount);
            Assert.Equal("state-29", log.CurrentState().GameName); // last pushed survives
        }

        [Fact]
        public void EnforceUndoLimit_NeverEvictsCurrentState()
        {
            var log = NewLog();
            PushStates(log, 100);

            Assert.Equal(Cap, log.DoneCount);
            Assert.Equal("state-99", log.CurrentState().GameName);
        }

        [Fact]
        public void Undo_AvailableUpToMaxUndoDepth()
        {
            var log = NewLog();
            PushStates(log, 30); // capped to 26 entries -> 25 undo steps

            int undos = 0;
            while (log.CanUndo)
            {
                Assert.NotNull(log.Undo());
                undos++;
            }

            Assert.Equal(LogConstants.MaxUndoDepth, undos); // exactly 25
        }

        [Fact]
        public void Done_StillClearsRedo_AfterUndo()
        {
            var log = NewLog();
            PushStates(log, 5);

            log.Undo();
            log.Undo();
            Assert.True(log.CanRedo);
            Assert.Equal(2, log.RedoCount);

            log.Done(Model("new-action"));

            Assert.False(log.CanRedo);
            Assert.Equal(0, log.RedoCount);
            Assert.Equal("new-action", log.CurrentState().GameName);
        }

        [Fact]
        public void CanUndo_FalseAtStart_TrueAfterSecondState()
        {
            var log = NewLog();

            log.Done(Model("state-0"));
            Assert.False(log.CanUndo);

            log.Done(Model("state-1"));
            Assert.True(log.CanUndo);
        }

        [Fact]
        public void Done_FullWindow_UndoThenNewAction_CorrectCounts()
        {
            var log = NewLog();
            PushStates(log, Cap); // exactly at capacity (26)
            Assert.Equal(Cap, log.DoneCount);

            for (int i = 0; i < 5; i++) log.Undo();
            Assert.Equal(Cap - 5, log.DoneCount);
            Assert.Equal(5, log.RedoCount);

            for (int i = 0; i < 6; i++)
            {
                log.Done(Model($"fresh-{i}"));
                Assert.False(log.CanRedo); // each new action clears redo
            }

            Assert.Equal(Cap, log.DoneCount); // re-grew but stayed capped
            Assert.Equal("fresh-5", log.CurrentState().GameName);
        }

        [Fact]
        public void Undo_RestoresReplayableRandomState()
        {
            var log = NewLog();
            log.Done(new GameModel { GameName = "a", Random = new ReplayableRandom(123, 5) });
            log.Done(new GameModel { GameName = "b", Random = new ReplayableRandom(123, 11) });

            Assert.Equal(11, log.CurrentState().Random.Iterations);

            var prev = log.Undo();
            Assert.NotNull(prev);
            Assert.Equal(5, prev!.Random.Iterations);
            Assert.Equal(5, log.CurrentState().Random.Iterations);
        }

        // ---- Load / backward-compatibility (legacy SerializableLog has no AnchorState) ----

        private static SerializableLog LegacyLog(int doneCount, int redoCount = 0, int anchorAtOldestOffset = -1)
        {
            // SerializableLog stores most-recent-first (index 0 = newest, last = oldest).
            var done = new List<string>();
            for (int i = doneCount - 1; i >= 0; i--)
            {
                var state = (anchorAtOldestOffset >= 0 && i == anchorAtOldestOffset)
                    ? GameState.WaitingForRollForOrder
                    : GameState.WaitingForNext;
                done.Add(JsonHelper.Serialize(new GameModel { GameName = $"state-{i}", GameState = state }));
            }

            var redo = new List<string>();
            for (int i = redoCount - 1; i >= 0; i--)
                redo.Add(JsonHelper.Serialize(new GameModel { GameName = $"redo-{i}" }));

            return new SerializableLog
            {
                DoneStack = done,
                RedoStack = redo,
                DoneCount = doneCount,
                RedoCount = redoCount,
                AnchorState = null, // legacy format: field absent / null
            };
        }

        [Fact]
        public void Load_OverCapLegacy_TrimsToCap()
        {
            var log = Log<string>.FromSerializableLog(LegacyLog(100), null!, "load-test");

            Assert.Equal(Cap, log.DoneCount);
            Assert.Equal("state-99", log.CurrentState().GameName); // newest preserved
        }

        [Fact]
        public void Load_WithRedoStack_TrimDoesNotCorruptRedoBranch()
        {
            var log = Log<string>.FromSerializableLog(LegacyLog(100, redoCount: 5), null!, "load-test");

            Assert.Equal(Cap, log.DoneCount);
            Assert.Equal(5, log.RedoCount);
            Assert.True(log.CanRedo);
        }

        [Fact]
        public void Anchor_CapturedAtWaitingForRollForOrder()
        {
            var log = NewLog();
            log.Done(Model("setup", GameState.PickingBoard));
            log.Done(Model("roll-for-order", GameState.WaitingForRollForOrder));
            log.Done(Model("playing", GameState.WaitingForNext));

            Assert.NotNull(log.AnchorState);
            Assert.NotNull(log.GetSerializableLog().AnchorState);
            Assert.Contains("roll-for-order", log.AnchorState!);
        }

        [Fact]
        public void Anchor_SalvagedOnLoad_WhenNull_EvenAfterTrim()
        {
            // Legacy long save: anchor sits at the oldest end and would be trimmed away,
            // but must be salvaged on load (scan-before-trim).
            var legacy = LegacyLog(100, redoCount: 0, anchorAtOldestOffset: 0); // oldest entry is WaitingForRollForOrder
            var log = Log<string>.FromSerializableLog(legacy, null!, "load-test");

            Assert.Equal(Cap, log.DoneCount);            // trimmed
            Assert.NotNull(log.AnchorState);             // but anchor recovered
            Assert.Contains("state-0", log.AnchorState!); // the oldest (setup) snapshot
        }
    }
}
