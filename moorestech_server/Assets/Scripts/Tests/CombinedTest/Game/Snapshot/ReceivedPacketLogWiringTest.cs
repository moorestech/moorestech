using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Core.Update;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Game.Snapshot
{
    public class ReceivedPacketLogWiringTest
    {
        [Test]
        public void 受信キュー経由で入ったパケットを実処理tick付きで読み戻せる()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-packetwire-{Guid.NewGuid():N}", "save.json");
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, savePath),
            };
            var (packetResponseCreator, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            var ring = provider.GetRequiredService<WorldSnapshotRing>();
            var packetLog = provider.GetRequiredService<ReceivedPacketLog>();
            GameUpdater.RestoreCurrentTick(500);
            ring.Start(SnapshotRingConfig.PeriodTicks, SnapshotRingConfig.Generations);

            // 本番と同じ受信経路を組み、受信スレッド側の入口からパケットを流し込む
            // Build the production receive path and feed the packet from the receive-thread entry point
            using var listener = CreateBoundLoopbackListener();
            using var clientSocket = ConnectTo(listener);
            using var acceptedSocket = listener.Accept();
            var sendQueueProcessor = new SendQueueProcessor(acceptedSocket);

            try
            {
                var receiveQueueProcessor = new ReceiveQueueProcessor(
                    packetResponseCreator,
                    sendQueueProcessor,
                    new PacketResponseContext(sendQueueProcessor),
                    provider.GetRequiredService<TickEndPacketQueue>(),
                    packetLog);

                GrantRequiredItems(provider, ForUnitTestModBlockId.BlockId, 1);
                var payload = CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (21, 22));
                receiveQueueProcessor.EnqueuePacket(payload);
                GameUpdater.UpdateOneTick();
                packetLog.Flush();

                // 設置が起きた＝パケットが本当に処理された。その同じtickがログに載っていることを突き合わせる
                // The block exists, so the packet really ran; check the log carries that very tick
                Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(21, 22)));
                var records = ReceivedPacketLogReader.ReadAll(packetLog.SegmentFilePaths());
                Assert.AreEqual(1, records.Count, "受信パケットがログへ1件も入っていない");
                Assert.AreEqual(501UL, records[0].Tick);
                CollectionAssert.AreEqual(payload, records[0].Payload);
            }
            finally
            {
                // assert失敗時も前景の送信スレッドとFileStreamを必ず解放してから削除する
                // Even on assert failure, release the foreground send thread and the FileStream before deleting
                sendQueueProcessor.Dispose();
                packetLog.Stop();
                Directory.Delete(Path.GetDirectoryName(savePath), true);
            }
        }

        private static Socket CreateBoundLoopbackListener()
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);
            return listener;
        }

        private static Socket ConnectTo(Socket listener)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(listener.LocalEndPoint);
            return socket;
        }
    }
}
