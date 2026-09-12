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
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class BugReportCaptureProtocolTest
    {
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
            var sink = EventTestUtil.RegisterCaptureSink(provider, 1);

            var request = MessagePackSerializer.Serialize(BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest());
            var responseBytes = packet.GetPacketResponse(request, new PacketResponseContext(null));
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
            // tick_10 は常時記録開始時の基準スナップショット、tick_11 が今回の即時取得
            // tick_10 is the baseline taken when always-on capture started; tick_11 is this immediate capture
            CollectionAssert.AreEqual(new[] { "tick_10.json", "tick_11.json" }, payload.SnapshotFileNames);

            // 取り込みtickの直後から新しい区間が始まる。切り替えが落ちると再生は取り込み以降のパケットを失う
            // A new segment starts right after the captured tick; a missed rotation loses every packet after the capture in replay
            CollectionAssert.AreEqual(new[] { "packets_11.bin", "packets_12.bin" }, payload.PacketLogFileNames);
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }
    }
}
