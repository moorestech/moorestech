using System;
using System.IO;
using System.Linq;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game.Snapshot
{
    // 周期世代と即時確保の保持責務が混ざると、即時確保の本数上限が直前の周期世代を押し出す
    // With periodic and immediate retention mixed, the immediate count cap evicts the periodic generations just taken
    public class SnapshotRetentionSplitTest
    {
        [Test]
        public void 即時確保の上限を超えても保持区間の周期世代は残り即時確保だけが消える()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-ring-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var directory = provider.GetRequiredService<WorldDataDirectory>();
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            GameUpdater.RestoreCurrentTick(0);

            // 保持区間40tickを周期10tickで覆い、即時確保は2件までしか残さない設定にする
            // Cover a 40-tick retention window at a 10-tick period while keeping at most two immediate captures
            ring.Start(10u, 40u, 2);
            for (var i = 0; i < 50; i++) GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();

            // 上限を超える3件の即時確保を、周期の期日に当たらないtickで取る
            // Take three immediate captures, one past the cap, on ticks that never fall on a periodic due date
            for (var i = 0; i < 3; i++)
            {
                ring.RequestImmediateSnapshot();
                GameUpdater.UpdateOneTick();
                ring.WaitForPendingWrites();
            }

            var kept = WorldDataDirectory.EnumerateSnapshotFiles(directory.SnapshotDirectory).Select(Path.GetFileName).ToArray();
            var expected = new ulong[] { 10, 20, 30, 40, 50, 52, 53 }.Select(WorldDataDirectory.SnapshotFileName).ToArray();
            CollectionAssert.AreEqual(expected, kept, "即時確保の上限が周期世代を押し出している");
            ring.Stop();
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }
    }
}
