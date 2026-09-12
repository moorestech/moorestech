using System;
using System.IO;
using System.Linq;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Snapshot;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;

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
            var directory = provider.GetRequiredService<WorldDataDirectory>();
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            GameUpdater.RestoreCurrentTick(100);
            ring.Start(600, 1800, 16);

            SnapshotWritten written = null;
            ring.OnSnapshotWritten.Subscribe(w => written = w);
            var result = ring.RequestImmediateSnapshot();
            Assert.IsTrue(result.Accepted, "常時記録が有効なのに要求が受理されていない");
            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();
            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();

            Assert.IsNotNull(written, "完了通知が来ていない");
            Assert.IsTrue(written.HasRequester, "要求付きの完了が要求元なしとして流れている");
            Assert.IsTrue(written.Success, "書き出しに成功したのに成否が失敗になっている");
            Assert.AreEqual(result.RequestId, written.RequestId);
            Assert.AreEqual(101UL, written.Tick);
            Assert.IsTrue(File.Exists(directory.SnapshotFilePath(written.Tick)));
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

            var kept = WorldDataDirectory.EnumerateSnapshotFiles(directory.SnapshotDirectory).Select(Path.GetFileName).ToArray();
            var expected = new ulong[] { 10, 20, 30, 40, 50, 51 }.Select(WorldDataDirectory.SnapshotFileName).ToArray();
            CollectionAssert.AreEqual(expected, kept, "即時取得が保持区間内の周期世代を消している");
            Assert.IsTrue(File.Exists(directory.SnapshotFilePath(10)), "保持区間の開始を覆う最古スナップショットが消えている");
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }

        // 拒否を成功と同じ形で返すと、要求元は来ない完了イベントを永久に待つ
        // Returning a rejection in the same shape as a success makes the requester wait forever for a completion that never arrives
        [Test]
        public void 未開始のリングは即時要求を拒否として返す()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            Assert.IsFalse(ring.IsActive);
            LogAssert.Expect(LogType.Warning, new Regex("常時記録が無効のため即時スナップショット要求を受け付けられません"));

            var result = ring.RequestImmediateSnapshot();
            Assert.IsFalse(result.Accepted, "常時記録が無効なのに要求が受理されている");
            Assert.IsNotEmpty(result.RejectedReason, "拒否の理由が要求元へ返っていない");
        }

        // 一覧を辞書順で並べると、桁が増えた瞬間に最古が最新として載り、バンドルが古いスナップショットを掴む
        // Lexicographic ordering makes the oldest look newest once the digits grow, so a bundle would grab a stale snapshot
        [Test]
        public void 完了通知のファイル一覧は桁を跨いでもtick昇順になる()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-ring-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            GameUpdater.RestoreCurrentTick(8);
            ring.Start(600, 1800, 16);

            SnapshotWritten written = null;
            ring.OnSnapshotWritten.Subscribe(w => written = w);
            CaptureAt(9);
            CaptureAt(100);

            // 先頭の tick_8 は開始時の基準スナップショット
            // The leading tick_8 is the baseline snapshot taken at start
            CollectionAssert.AreEqual(new[] { "tick_8.json", "tick_9.json", "tick_100.json" }, written.SnapshotFileNames, "スナップショット一覧が辞書順になっている");
            CollectionAssert.AreEqual(new[] { "packets_9.bin", "packets_10.bin", "packets_101.bin" }, written.PacketLogFileNames, "区間ファイル一覧が辞書順になっている");
            Directory.Delete(Path.GetDirectoryName(savePath), true);

            #region Internal

            void CaptureAt(ulong tick)
            {
                GameUpdater.RestoreCurrentTick(tick - 1);
                ring.RequestImmediateSnapshot();
                GameUpdater.UpdateOneTick();
                ring.WaitForPendingWrites();
            }

            #endregion
        }

        // 基準スナップショットが無いと、開始から最初の周期までに起きたバグは再生の出発点を持たない
        // Without a baseline snapshot, a bug that happens between start and the first period has no point to replay from
        [Test]
        public void 開始時に基準スナップショットを書き区間はその直後から始まる()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-ring-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var directory = provider.GetRequiredService<WorldDataDirectory>();
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            GameUpdater.RestoreCurrentTick(5);

            // 周期600tickなので、基準を書かなければ最初の周期までスナップショットは1本も存在しない
            // At a 600-tick period, no snapshot would exist until the first period unless the baseline is written
            ring.Start(600, 1800, 16);
            ring.WaitForPendingWrites();

            var snapshots = WorldDataDirectory.EnumerateSnapshotFiles(directory.SnapshotDirectory).Select(Path.GetFileName).ToArray();
            CollectionAssert.AreEqual(new[] { "tick_5.json" }, snapshots, "開始tickの基準スナップショットが書かれていない");

            // 区間が基準tickの直後から始まっていないと、再生は基準に含まれ済みのパケットを二重適用する
            // Unless the segment begins right after the baseline tick, replay reapplies packets the baseline already contains
            var segments = WorldDataDirectory.EnumeratePacketLogFiles(directory.SnapshotDirectory).Select(Path.GetFileName).ToArray();
            CollectionAssert.AreEqual(new[] { "packets_6.bin" }, segments, "区間が基準スナップショットの直後から始まっていない");
            ring.Stop();
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }

        // 前セッションの残骸が残ると、剪定対象にならない古いtickが残り続け、再生へ異セッションのパケットが混ざる
        // Leftovers from the previous session keep ticks that this session never prunes and mix foreign packets into replay
        [Test]
        public void 開始時に前セッションの常時記録を消す()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-ring-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var directory = provider.GetRequiredService<WorldDataDirectory>();
            var ring = provider.GetRequiredService<WorldSnapshotRing>();

            // 前セッションが残したスナップショットと区間ファイルを置く
            // Put the snapshot and segment files a previous session would have left behind
            Directory.CreateDirectory(directory.SnapshotDirectory);
            File.WriteAllText(directory.SnapshotFilePath(9999), "{}");
            File.WriteAllBytes(Path.Combine(directory.SnapshotDirectory, WorldDataDirectory.ReceivedPacketLogFileName(9999)), new byte[] { 1 });

            GameUpdater.RestoreCurrentTick(0);
            ring.Start(600, 1800, 16);

            Assert.IsFalse(File.Exists(directory.SnapshotFilePath(9999)), "前セッションのスナップショットが残っている");
            var segments = WorldDataDirectory.EnumeratePacketLogFiles(directory.SnapshotDirectory).Select(Path.GetFileName).ToArray();
            CollectionAssert.AreEqual(new[] { "packets_1.bin" }, segments, "前セッションの区間ファイルが残っている");
            ring.Stop();
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }
    }
}
