using System;
using System.IO;
using Core.Update;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Loop.PacketProcessing;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class TickEndSaveConsistencyTest
    {
        [Test]
        public void 設置と保存要求が同じtickなら設置後の世界と在庫を保存する()
        {
            var savePath = Path.Combine(Path.GetTempPath(), $"moorestech-tick-end-{Guid.NewGuid():N}.json");
            var saveOptions = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                saveJsonFilePath = new SaveJsonFilePath(savePath),
            };
            var (packet, saveProvider) = new MoorestechServerDIContainerGenerator().Create(saveOptions);
            GrantRequiredItems(saveProvider, ForUnitTestModBlockId.BlockId, 1);

            // 同じ固定入力へ設置を先、保存要求を後として積む
            // Enqueue placement before the save request in the same frozen input batch
            var queue = saveProvider.GetRequiredService<TickEndPacketQueue>();
            queue.Enqueue(new ProtocolEntry(packet,
                CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (60, 0))));
            queue.Enqueue(new ProtocolEntry(packet,
                MessagePackSerializer.Serialize(new SaveProtocol.SaveProtocolMessagePack())));
            GameUpdater.UpdateOneTick();

            // 新しい世界へ読み込み、ブロックと課金後在庫が同じ時点で保存されたことを確認する
            // Load a fresh world and verify the block and charged inventory came from one boundary
            Assert.IsTrue(File.Exists(savePath));
            var loadOptions = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                saveJsonFilePath = new SaveJsonFilePath(savePath),
            };
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(loadOptions);
            loadProvider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(60, 0)));
            AssertInventoryEmptyOfRequiredItems(loadProvider, ForUnitTestModBlockId.BlockId);
            File.Delete(savePath);
        }

        private sealed class ProtocolEntry : ITickEndPacketEntry
        {
            private readonly PacketResponseCreator _packet;
            private readonly byte[] _payload;
            public bool IsActive => true;

            public ProtocolEntry(PacketResponseCreator packet, byte[] payload)
            {
                _packet = packet;
                _payload = payload;
            }

            public TickEndPacketProcessResult Process()
            {
                return _packet.GetTickEndPacketResponse(
                    _payload, new PacketResponseContext(null), out _);
            }
        }
    }
}
