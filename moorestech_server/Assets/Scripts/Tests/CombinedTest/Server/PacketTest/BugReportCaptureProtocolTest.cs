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
            ring.Start(600, 4);
            var sink = EventTestUtil.RegisterCaptureSink(provider, 1);

            var request = MessagePackSerializer.Serialize(BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest());
            var responseBytes = packet.GetPacketResponse(request, new PacketResponseContext(null));
            var response = MessagePackSerializer.Deserialize<BugReportCaptureProtocol.BugReportCaptureResponse>(responseBytes[0]);
            Assert.Greater(response.RequestedCaptureId, 0L);

            GameUpdater.UpdateOneTick();
            ring.WaitForPendingWrites();
            GameUpdater.UpdateOneTick();

            var completed = sink.TakeAll().Where(e => e.Tag == BugReportCaptureCompletedEventPacket.EventTag).ToList();
            Assert.AreEqual(1, completed.Count, "完了イベントが1件届いていない");
            var payload = MessagePackSerializer.Deserialize<BugReportCaptureCompletedEventPacket.BugReportCaptureCompletedMessagePack>(completed[0].Payload);
            Assert.AreEqual(response.RequestedCaptureId, payload.CaptureId);
            Assert.AreEqual(11UL, payload.Tick);
            Assert.AreEqual(provider.GetRequiredService<WorldDataDirectory>().SnapshotDirectory, payload.SnapshotDirectory);
            CollectionAssert.Contains(payload.SnapshotFileNames, "tick_11.json");
            CollectionAssert.Contains(payload.PacketLogFileNames, "packets_11.bin");
            Directory.Delete(Path.GetDirectoryName(savePath), true);
        }
    }
}
