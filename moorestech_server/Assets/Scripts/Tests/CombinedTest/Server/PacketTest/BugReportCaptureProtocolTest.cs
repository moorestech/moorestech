using System;
using System.IO;
using System.Linq;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using System.Text.RegularExpressions;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BugReportCaptureProtocolTest
    {
        private const int RequesterPlayerId = 1;
        private const int UnrelatedPlayerId = 2;

        [Test]
        public void 即時取得を要求すると次のtick末尾で書かれ完了イベントに一覧が載る()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-capture-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            GameUpdater.RestoreCurrentTick(10);
            ring.Start(600, 1800, 16);
            var sink = EventTestUtil.RegisterCaptureSink(provider, RequesterPlayerId);
            var unrelatedSink = EventTestUtil.RegisterCaptureSink(provider, UnrelatedPlayerId);

            var request = MessagePackSerializer.Serialize(BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest());
            var context = new PacketResponseContext(null);
            context.TryBindPlayerId(RequesterPlayerId);
            var responseBytes = packet.GetPacketResponse(request, context);
            var response = MessagePackSerializer.Deserialize<BugReportCaptureProtocol.BugReportCaptureResponse>(responseBytes[0]);
            Assert.IsTrue(response.Accepted, "常時記録が有効なのに要求が受理されていない");
            Assert.Greater(response.RequestedCaptureId, 0L);

            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();
            GameUpdater.UpdateOneTick();

            var completed = sink.TakeAll().Where(e => e.Tag == BugReportCaptureCompletedEventPacket.EventTag).ToList();
            Assert.AreEqual(1, completed.Count, "完了イベントが1件届いていない");
            var payload = MessagePackSerializer.Deserialize<BugReportCaptureCompletedEventPacket.BugReportCaptureCompletedMessagePack>(completed[0].Payload);
            Assert.AreEqual(response.RequestedCaptureId, payload.CaptureId);
            Assert.AreEqual(11UL, payload.Tick);
            Assert.IsTrue(payload.Success, "書き出しに成功したのに完了イベントが失敗を伝えている");
            Assert.AreEqual(provider.GetRequiredService<WorldDataDirectory>().SnapshotDirectory, payload.SnapshotDirectory);

            // 完了イベントのサーバーデータは、このサーバーがマスタを読んだ場所そのものでなければならない
            // The completion event's server data must be the very directory this server read its masters from
            // 別の場所（../moorestech_master 等）を報告側が推測すると、再現は別マスタで走り読み解けない例外で落ちる
            // A guess on the report side (such as ../moorestech_master) makes the reproduction run different masters and die inscrutably
            Assert.AreEqual(options.ServerDataDirectory, payload.ServerDataDirectory);
            Assert.AreEqual(options.ServerDataDirectory, provider.GetRequiredService<ServerDataDirectory>().Root);
            // tick_10 は常時記録開始時の基準スナップショット、tick_11 が今回の即時取得
            // tick_10 is the baseline taken when always-on capture started; tick_11 is this immediate capture
            CollectionAssert.AreEqual(new[] { "tick_10.json", "tick_11.json" }, payload.SnapshotFileNames);

            // 取り込みtickの直後から新しい区間が始まる。切り替えが落ちると再生は取り込み以降のパケットを失う
            // A new segment starts right after the captured tick; a missed rotation loses every packet after the capture in replay
            CollectionAssert.AreEqual(new[] { "packets_11.bin", "packets_12.bin" }, payload.PacketLogFileNames);

            // 全接続へ配ると、無関係なクライアントが他人の完了とサーバー側の絶対パスを受け取る
            // Broadcasting would hand an unrelated client someone else's completion and the server-side absolute path
            Assert.IsEmpty(unrelatedSink.TakeAll().Where(e => e.Tag == BugReportCaptureCompletedEventPacket.EventTag).ToList(), "完了イベントが要求元以外へ配信されている");
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }

        // パケット記録が止まったのに取得が成功だけを名乗ると、再現側はパケットが欠けた箱を正常な資料として読み「一致しない」を非決定性と誤読する
        // If packet capture dies while the acquisition claims only success, the reproduction side reads a packet-starved bundle as sound material and misreads the mismatch as non-determinism
        [Test]
        public void パケット記録が縮退したら完了イベントに理由と停止tickが載る()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-capture-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var directory = provider.GetRequiredService<WorldDataDirectory>();
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            var packetLog = provider.GetRequiredService<ReceivedPacketLog>();
            GameUpdater.RestoreCurrentTick(10);
            ring.Start(600, 1800, 16);
            var sink = EventTestUtil.RegisterCaptureSink(provider, RequesterPlayerId);

            // 置き場ごと消してパケット記録だけをI/O失敗で止め、スナップショットは書ける状態へ戻す
            // Delete the directory so only packet capture dies on I/O, then restore it so snapshots can still be written
            Directory.Delete(directory.SnapshotDirectory, true);
            LogAssert.Expect(LogType.Error, new Regex("^パケットログの区間切り替えに失敗しました"));
            packetLog.Rotate(11);
            Assert.IsFalse(packetLog.IsActive, "パケット記録が止まっていない");
            Directory.CreateDirectory(directory.SnapshotDirectory);

            var request = MessagePackSerializer.Serialize(BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest());
            var context = new PacketResponseContext(null);
            context.TryBindPlayerId(RequesterPlayerId);
            packet.GetPacketResponse(request, context);

            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();
            GameUpdater.UpdateOneTick();

            var completed = sink.TakeAll().Where(e => e.Tag == BugReportCaptureCompletedEventPacket.EventTag).ToList();
            Assert.AreEqual(1, completed.Count, "完了イベントが1件届いていない");
            var payload = MessagePackSerializer.Deserialize<BugReportCaptureCompletedEventPacket.BugReportCaptureCompletedMessagePack>(completed[0].Payload);
            Assert.IsTrue(payload.Success, "スナップショットは書けているのに書き出しが失敗扱いになっている");
            StringAssert.Contains("パケットログの区間切り替えに失敗しました", payload.PacketLogDegradeReason, "パケット記録の縮退理由が取得結果に載っていない");
            Assert.AreEqual(11UL, payload.PacketLogDegradedAtTick, "パケット記録を止めたtickが取得結果に載っていない");
            ring.Stop();
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }

        // 要求元が分からない接続を受理すると、完了イベントの宛先が無いまま要求だけが積まれる
        // Accepting a request from a connection with no known player leaves the request queued with nowhere to send its completion
        [Test]
        public void プレイヤーが確定していない接続からの即時取得要求は拒否される()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-capture-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            GameUpdater.RestoreCurrentTick(10);
            ring.Start(600, 1800, 16);
            LogAssert.Expect(LogType.Warning, new Regex("プレイヤーが確定していない接続のため即時スナップショット要求を受け付けられません"));

            var request = MessagePackSerializer.Serialize(BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest());
            var responseBytes = packet.GetPacketResponse(request, new PacketResponseContext(null));
            var response = MessagePackSerializer.Deserialize<BugReportCaptureProtocol.BugReportCaptureResponse>(responseBytes[0]);

            Assert.IsFalse(response.Accepted, "要求元が分からない接続からの要求が受理されている");
            Assert.IsNotEmpty(response.RejectedReason, "拒否の理由が要求元へ返っていない");
            ring.Stop();
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }
    }
}
