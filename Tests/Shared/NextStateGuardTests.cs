using System;
using System.Linq;
using System.Threading.Tasks;
using Catan3.Shared.GameLogic;
using Catan3.Shared.Interfaces;
using Catan3.Shared.Models;
using Catan3.Shared.Utility;
using Xunit;

namespace Tests.Shared
{
    /// <summary>
    /// Regression tests for issue #182 — server-side gate on Next-state
    /// transitions. The bug was that <c>CanTransitionToNext</c> was a
    /// stub that always returned <c>true</c>, so any
    /// <c>NextMessage</c> reaching the server (UI race, SignalR replay,
    /// stale client, direct API call) would advance the turn even when
    /// the current player had unspent entitlements — corrupting the
    /// game log permanently.
    ///
    /// Invariant restored by these tests: "0 unspent entitlements at
    /// turn start AND end."
    /// </summary>
    public class NextStateGuardTests
    {
        /// <summary>
        /// Minimal logger that swallows trace output. The real logger
        /// is over-built for these tests; we only care about behavior
        /// of the state machine.
        /// </summary>
        private sealed class SilentLogger : ICatanDebugTrace
        {
            public void Trace(GameTraceLevel _, string __,
                int ___ = 0, string ____ = "", int _____ = 0, string ______ = "")
            { }
        }

        /// <summary>
        /// Build a minimal game in WaitingForNext with one player who
        /// optionally has <paramref name="unspent"/> entitlements
        /// pending in their queue.
        /// </summary>
        private static GameStateMachine BuildMachine(
            GameState state,
            params Entitlement[] unspent)
        {
            // PlayerModel.Name is derived from Id; passing "Test-NNN"
            // mirrors the existing test-data convention (e.g. "Joe-001").
            // Two players so NextState's ChangePlayer rotation works for
            // the "successful Next" path; only player 1 owns the
            // unspent entitlements.
            var player1 = new PlayerModel("Test-001");
            var player2 = new PlayerModel("Test-002");
            foreach (var e in unspent)
            {
                player1.UnspentEntitlements.Add(e);
            }

            var gameModel = new GameModel
            {
                GameId = Guid.NewGuid().ToString(),
                GameState = state,
                CurrentPlayerId = player1.Id,
            };
            gameModel.Players.Add(player1);
            gameModel.Players.Add(player2);

            var log = new Log<string>(
                PersistenceService: null,
                gameModel: gameModel,
                isTest: true,
                logger: null);

            return new GameStateMachine(log, new SilentLogger(), persistenceService: null!);
        }

        // ──────────────────────────────────────────────────────────────
        // Positive case — Next is allowed when no unspent entitlements
        // ──────────────────────────────────────────────────────────────

        [Fact]
        public async Task Next_in_WaitingForNext_with_zero_unspent_passes_the_guard()
        {
            // With zero unspent, the guard must NOT throw. Downstream
            // state-transition logic (gold tile selection, etc.) may
            // throw because this minimal GameModel doesn't have tiles
            // populated — those are separate concerns. We verify only
            // that the failure (if any) is NOT the guard.
            var sm = BuildMachine(GameState.WaitingForNext /* no unspent */);

            try
            {
                var result = await sm.HandleNextAsync(new NextMessage());
                Assert.NotNull(result);
            }
            catch (GameException ex)
            {
                Assert.DoesNotContain("Cannot transition to Next state",
                    ex.Message, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                // Downstream Debug.Assert (e.g. gold-tile validation)
                // surfaces as a non-GameException — also fine; the
                // guard wasn't the blocker.
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Regression cases — the bug we're fixing
        // ──────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(Entitlement.Road)]
        [InlineData(Entitlement.Settlement)]
        [InlineData(Entitlement.City)]
        public async Task Next_in_WaitingForNext_throws_when_unspent_remains(
            Entitlement leftover)
        {
            var sm = BuildMachine(GameState.WaitingForNext, leftover);

            var ex = await Assert.ThrowsAsync<GameException>(
                () => sm.HandleNextAsync(new NextMessage()));
            Assert.Contains("Cannot transition to Next state",
                ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Next_in_Supplemental_throws_when_unspent_remains()
        {
            // Supplemental also accepts purchases — same gating applies.
            var sm = BuildMachine(GameState.Supplemental, Entitlement.Road);

            await Assert.ThrowsAsync<GameException>(
                () => sm.HandleNextAsync(new NextMessage()));
        }

        [Fact]
        public async Task Next_reproduces_the_c28ebc24_corruption_scenario()
        {
            // The exact entitlement bag observed in the wedged game from
            // issue #182. The fix must reject this combination —
            // otherwise the bug class persists.
            var sm = BuildMachine(GameState.WaitingForNext,
                Entitlement.Settlement, Entitlement.City,
                Entitlement.Road, Entitlement.Road, Entitlement.Road,
                Entitlement.Road, Entitlement.Road,
                Entitlement.Settlement, Entitlement.City,
                Entitlement.Road, Entitlement.Road, Entitlement.Road);

            await Assert.ThrowsAsync<GameException>(
                () => sm.HandleNextAsync(new NextMessage()));
        }

        // ──────────────────────────────────────────────────────────────
        // States outside the gating set — guard does NOT block them
        // (so the fix doesn't accidentally break unrelated transitions)
        // ──────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(GameState.WaitingForPlayers)]
        [InlineData(GameState.PickingBoard)]
        public async Task Next_in_non_purchase_states_is_not_gated_by_unspent(
            GameState state)
        {
            // Even if a player somehow has unspent entitlements (which
            // shouldn't happen pre-allocation, but the guard is purely
            // about Next-eligibility, not data validity), states that
            // don't accept purchases aren't blocked.
            var sm = BuildMachine(state, Entitlement.Road);

            // Must not throw on the guard. (May throw downstream for
            // other reasons, e.g. board not picked yet — we only
            // assert that the CanTransitionToNext gate isn't the
            // blocker, by checking the exception message if any.)
            try
            {
                await sm.HandleNextAsync(new NextMessage());
            }
            catch (GameException ex)
            {
                Assert.DoesNotContain("Cannot transition to Next state",
                    ex.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
