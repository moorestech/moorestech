using Core.Update;
using Game.SaveLoad.Snapshot;
using NUnit.Framework;

namespace Tests.CombinedTest.Game.Snapshot
{
    public class SnapshotRingConfigTest
    {
        [Test]
        public void 剪定直後でも直前120秒を再生できる周期と世代数になっている()
        {
            // 剪定直後の最古スナップショットは(世代数-1)周期ぶん前。ここが120秒を割ると「直前2分を確実に残す」が破れる
            // Right after pruning the oldest snapshot is (generations-1) periods old; below 120 seconds the "guaranteed last two minutes" decision breaks
            var worstCaseTicks = (uint)((SnapshotRingConfig.Generations - 1) * SnapshotRingConfig.PeriodTicks);
            Assert.GreaterOrEqual(GameUpdater.TicksToSeconds(worstCaseTicks), 120.0);
        }
    }
}
