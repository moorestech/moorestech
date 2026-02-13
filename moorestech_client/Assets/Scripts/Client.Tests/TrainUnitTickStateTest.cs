using Client.Game.InGame.Train.Unit;
using NUnit.Framework;

namespace Client.Tests
{
    public class TrainUnitTickStateTest
    {
        [Test]
        public void HashReceivedTick_GatesSimulationProgress()
        {
            // 受信済みhashのtickが進行可能な上限tickになることを確認する。
            // Verify the received hash tick acts as simulation upper bound.
            var state = new TrainUnitTickState();
            Assert.IsFalse(state.IsAllowSimulationNowTick());

            state.RecordHashReceived(3);
            Assert.IsTrue(state.IsAllowSimulationNowTick());

            state.AdvanceTick();
            state.AdvanceTick();
            state.AdvanceTick();

            Assert.AreEqual(3, state.GetTick());
            Assert.IsFalse(state.IsAllowSimulationNowTick());
        }

        [Test]
        public void SnapshotBaselineTick_DoesNotRollbackVerifiedOrReceivedTick()
        {
            // スナップショット基準tick更新で受信済みtickが巻き戻らないことを確認する。
            // Ensure baseline update does not roll back received hash tick.
            var state = new TrainUnitTickState();
            state.RecordHashReceived(20);

            state.SetSnapshotBaseline(10, 500);

            Assert.AreEqual(10, state.GetTick());
            Assert.AreEqual(20, state.GetHashReceivedTick());
            Assert.AreEqual(10, state.GetLastHashVerifiedTick());
            Assert.AreEqual((uint)500, state.GetAppliedTickSequenceId());
        }

        [Test]
        public void LatestHashTickWindow_ReturnsPreviousAndLatestHashTicks()
        {
            // hashを2回以上受信したら直近の窓情報が取得できることを確認する。
            // Ensure the latest hash window is available after two hash receives.
            var state = new TrainUnitTickState();

            state.RecordHashReceived(126);
            Assert.IsFalse(state.TryGetLatestHashTickWindow(out _, out _));

            state.RecordHashReceived(130);
            Assert.IsTrue(state.TryGetLatestHashTickWindow(out var previousHashTick, out var latestHashTick));
            Assert.AreEqual(126, previousHashTick);
            Assert.AreEqual(130, latestHashTick);
        }

        [Test]
        public void IsAllowSimulationNowTick_StopsWhenCurrentTickIsNotHashVerified()
        {
            // hash受信済みでも直前tickが未検証なら進行不可になることを確認する。
            // Ensure simulation stops when current tick is not hash-verified yet.
            var state = new TrainUnitTickState();
            state.SetSnapshotBaseline(100, 1000);
            state.RecordHashReceived(103);

            Assert.IsTrue(state.IsAllowSimulationNowTick());
            state.AdvanceTick();
            Assert.IsFalse(state.IsAllowSimulationNowTick());

            state.RecordHashVerified(101);
            Assert.IsTrue(state.IsAllowSimulationNowTick());
        }
    }
}
