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
using Server.Boot.Loop.PacketProcessing;
using Server.Boot.Replay;
using MessagePack;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.Replay
{
    // スナップショットkからパケットを流し直すとk+1と一致する。これが再生の忠実性の唯一の検査
    // Replaying packets from snapshot k must reproduce snapshot k+1; this is the only fidelity check for replay
    public class SnapshotReplayExcludedPacketsTest : SnapshotReplayDeterminismTestBase
    {

        // 記録済みのセーブ／即時取得要求を再生でそのまま実行すると、再生用の一時セーブを上書きし常時記録まで走り出す
        // Running recorded save / immediate-capture requests during replay overwrites the temporary save and even starts always-on capture
        [Test]
        public void 記録されたセーブと即時取得の要求は再生対象から外れる()
        {
            var saveRoot = Path.Combine(Path.GetTempPath(), $"moorestech-replay-{Guid.NewGuid():N}");
            var savePath = Path.Combine(saveRoot, "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (packet, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var directory = provider.GetRequiredService<WorldDataDirectory>();
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            var packetLog = provider.GetRequiredService<ReceivedPacketLog>();

            try
            {
                provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();

                GameRandom.Reseed(2026UL);
                GameUpdater.RestoreCurrentTick(0);
                ring.Start(10u, 30u, 16);

                var context = Tests.Util.BoundPacketContext.Bind(1);
                var queue = provider.GetRequiredService<TickEndPacketQueue>();
                var savePayload = MessagePackSerializer.Serialize(new SaveProtocol.SaveProtocolMessagePack());
                var capturePayload = MessagePackSerializer.Serialize(BugReportCaptureProtocol.BugReportCaptureRequest.CreateCaptureNowRequest());

                for (var tick = 1; tick <= 25; tick++)
                {
                    if (tick == 13) queue.Enqueue(new RecordedLivePacketEntry(packet, context, savePayload, packetLog));
                    if (tick == 14) queue.Enqueue(new RecordedLivePacketEntry(packet, context, capturePayload, packetLog));
                    GameUpdater.UpdateOneTick();
                }
                ring.WaitForPendingWrites();

                var segments = packetLog.SegmentFilePaths().ToList();
                var result = SnapshotReplayer.Replay(new ReplayRequest(TestModDirectory.ForUnitTestModDirectory, directory, directory.SnapshotFilePath(10), segments, 20));

                Assert.AreEqual(2, result.ExcludedPacketCount, "セーブと即時取得の要求が再生対象から外れていない");
                Assert.AreEqual(0, result.ReplayedPacketCount, "再生対象から外した要求が流し直されている");

                // 区間に記録が1件も無いと不一致を非決定性と誤読する。覆っていたことを結果からも確かめる
                // A zero-record interval would make a mismatch look like non-determinism, so the coverage is checked from the result too
                Assert.AreEqual(2, result.InRangePacketCount, "区間に含まれる記録件数が結果に載っていない");
            }
            finally
            {
                packetLog.Stop();
                Directory.Delete(saveRoot, true);
            }
        }
    }
}
