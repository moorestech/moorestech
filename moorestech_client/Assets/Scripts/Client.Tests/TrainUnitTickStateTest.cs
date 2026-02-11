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
            // Verify received hash tick works as the simulation upper bound.
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
            // スナップショット基準tick更新で受信済み/検証済みtickが巻き戻らないことを確認する。
            // Ensure baseline updates do not roll back received/verified ticks.
            var state = new TrainUnitTickState();
            state.RecordHashReceived(20);

            state.SetSnapshotBaselineTick(10);

            Assert.AreEqual(10, state.GetTick());
            Assert.AreEqual(20, state.GetHashReceivedTick());
        }
    }
}
