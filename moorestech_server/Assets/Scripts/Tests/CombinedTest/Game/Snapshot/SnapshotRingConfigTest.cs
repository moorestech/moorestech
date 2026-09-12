using Core.Update;
using Game.SaveLoad.Snapshot;
using NUnit.Framework;

namespace Tests.CombinedTest.Game.Snapshot
{
    public class SnapshotRingConfigTest
    {
        [Test]
        public void 保持区間が直前120秒を下回らない()
        {
            // 剪定は時間基準なので、保持秒数がそのまま「直前2分を確実に残す」の保証になる
            // Pruning is time-based, so the retention seconds are directly the "guaranteed last two minutes"
            Assert.GreaterOrEqual(GameUpdater.TicksToSeconds(SnapshotRingConfig.RetentionTicks), 120.0);
        }

        [Test]
        public void 上限世代数が保持区間ぶんの周期スナップショットを収められる()
        {
            // 上限が周期スナップショットの本数を割ると、ディスク保護の剪定が保持時間の保証を先に壊す
            // If the cap is below the periodic snapshot count, disk-protection pruning breaks the retention guarantee first
            var periodicCount = (int)(SnapshotRingConfig.RetentionTicks / SnapshotRingConfig.PeriodTicks) + 1;
            Assert.GreaterOrEqual(SnapshotRingConfig.MaxGenerations, periodicCount);
        }
    }
}
