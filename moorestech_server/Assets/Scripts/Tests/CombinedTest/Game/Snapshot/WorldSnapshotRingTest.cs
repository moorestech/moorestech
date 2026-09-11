using System;
using System.IO;
using System.Linq;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.CombinedTest.Game.Snapshot
{
    public class WorldSnapshotRingTest
    {
        [Test]
        public void 周期ごとに書き世代数を超えた古い世代を消す()
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
            ring.Start(10, 20, 16);

            for (var i = 0; i < 40; i++) GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();
            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();

            var names = Directory.GetFiles(directory.SnapshotDirectory, "tick_*.json").Select(Path.GetFileName).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "tick_20.json", "tick_30.json", "tick_40.json" }, names);
            CollectionAssert.AreEqual(new ulong[] { 20, 30, 40 }, ring.CopyWrittenTicks());

            // 最古スナップショット20より前の区間は消え、21以降の区間が残る
            // Segments before the oldest snapshot (20) are gone; segments from 21 remain
            var segments = Directory.GetFiles(directory.SnapshotDirectory, "packets_*.bin").Select(Path.GetFileName).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "packets_21.bin", "packets_31.bin", "packets_41.bin" }, segments);
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }

        [Test]
        public void 即時要求は同じtickの末尾で書かれ要求IDが通知される()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-ring-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            GameUpdater.RestoreCurrentTick(100);
            ring.Start(600, 1800, 16);

            SnapshotWritten written = null;
            ring.OnSnapshotWritten.Subscribe(w => written = w);
            var requestId = ring.RequestImmediateSnapshot();
            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();
            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();

            Assert.IsNotNull(written, "完了通知が来ていない");
            Assert.AreEqual(requestId, written.RequestId);
            Assert.AreEqual(101UL, written.Tick);
            Assert.IsTrue(File.Exists(written.FilePath));
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }

        // バグ報告の即時取得が周期世代の枠を食うと、報告に必要な過去記録が報告操作そのもので消える
        // If an immediate capture ate a periodic generation, the very act of reporting would delete the history the report needs
        [Test]
        public void 即時要求は周期世代の枠を食わず保持区間の世代が残る()
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

            // 保持区間40tickを周期10tickで覆うと、最古の10を含む5世代が必要になる
            // Covering a 40-tick retention window at a 10-tick period needs five generations including the oldest at 10
            ring.Start(10, 40, 16);
            for (var i = 0; i < 50; i++) GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();

            ring.RequestImmediateSnapshot();
            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();

            CollectionAssert.AreEqual(new ulong[] { 10, 20, 30, 40, 50, 51 }, ring.CopyWrittenTicks(), "即時取得が保持区間内の周期世代を消している");
            Assert.IsTrue(File.Exists(directory.SnapshotFilePath(10)), "保持区間の開始を覆う最古スナップショットが消えている");
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }

        [Test]
        public void 未開始のリングは即時要求を無視して0を返す()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            Assert.IsFalse(ring.IsActive);
            Assert.AreEqual(0L, ring.RequestImmediateSnapshot());
        }
    }
}
