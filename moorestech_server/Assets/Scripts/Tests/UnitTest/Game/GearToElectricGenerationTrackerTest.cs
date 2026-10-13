using Game.Block.Blocks.GearToElectric;
using Game.Gear.Common;
using NUnit.Framework;
using UniRx;

namespace Tests.UnitTest.Game
{
    public class GearToElectricGenerationTrackerTest
    {
        // 最低RPM未満は停止し、ちょうどなら発電する
        // Below minimum RPM stops; exactly the minimum generates
        [Test]
        public void BelowMinimumRpmIsStoppedEvenWithSuppliedPower()
        {
            var tracker = new GearToElectricGenerationTracker(40f);
            tracker.RecordElectricTick(new RPM(39f), 50f);
            Assert.AreEqual(GearToElectricGenerationState.Stopped, tracker.State);
            tracker.RecordElectricTick(new RPM(40f), 50f);
            Assert.AreEqual(GearToElectricGenerationState.Generating, tracker.State);
        }

        // 最低RPMが0でも回転0は停止する
        // Zero rotation stops even if the minimum is zero
        [Test]
        public void ZeroRpmIsStoppedWhenMinimumRpmIsZero()
        {
            var tracker = new GearToElectricGenerationTracker(0f);
            tracker.RecordElectricTick(new RPM(0f), 50f);
            Assert.AreEqual(GearToElectricGenerationState.Stopped, tracker.State);
        }

        // 回転中に供給しなければ待機中
        // Spinning without supply is idle
        [Test]
        public void SpinningWithoutSuppliedPowerIsIdle()
        {
            var tracker = new GearToElectricGenerationTracker(40f);
            tracker.RecordElectricTick(new RPM(40f), 0f);
            Assert.AreEqual(GearToElectricGenerationState.Idle, tracker.State);
        }

        // 初回と可視値の変更時だけ通知する
        // Notify on the first tick and changes to visible values only
        [Test]
        public void NotifiesOnlyWhenSuppliedPowerOrStateChanges()
        {
            var tracker = new GearToElectricGenerationTracker(40f);
            var count = 0;
            tracker.OnGenerationChanged.Subscribe(_ => count++);
            tracker.RecordElectricTick(new RPM(40f), 50f);
            Assert.AreEqual(1, count);
            tracker.RecordElectricTick(new RPM(40f), 50f);
            Assert.AreEqual(1, count);
            tracker.RecordElectricTick(new RPM(40f), 30f);
            Assert.AreEqual(2, count);
            tracker.RecordElectricTick(new RPM(10f), 30f);
            Assert.AreEqual(3, count);
            Assert.AreEqual(GearToElectricGenerationState.Stopped, tracker.State);
        }

        // 表示丸め境界の微小差も確定・通知し、厳密同値だけ抑制する
        // Settle and notify a tiny change across display rounding; suppress only exact equality
        [Test]
        public void TinyPowerChangeAcrossDisplayRoundingBoundaryIsNotLost()
        {
            var tracker = new GearToElectricGenerationTracker(40f);
            var count = 0;
            tracker.OnGenerationChanged.Subscribe(_ => count++);

            tracker.RecordElectricTick(new RPM(40f), 29.49999f);
            Assert.AreEqual(29.49999f, tracker.SuppliedPower);
            Assert.AreEqual(GearToElectricGenerationState.Generating, tracker.State);
            Assert.AreEqual(1, count);

            tracker.RecordElectricTick(new RPM(40f), 29.5f);
            Assert.AreEqual(29.5f, tracker.SuppliedPower);
            Assert.AreEqual(GearToElectricGenerationState.Generating, tracker.State);
            Assert.AreEqual(2, count);

            tracker.RecordElectricTick(new RPM(40f), 29.5f);
            Assert.AreEqual(2, count);
        }
    }
}
