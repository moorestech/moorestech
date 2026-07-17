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

            // 設置→保存要求の順で処理し、保存はこの時点では実行されない（要求のみ）
            // Process placement then the save request; the save itself is only requested at this point
            var context = new PacketResponseContext();
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (60, 0)), context);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new SaveProtocol.SaveProtocolMessagePack()), context);
            Assert.IsFalse(File.Exists(savePath));

            // tick末尾の安定点で要求済み保存が実行される
            // The requested save executes at the tick-end stable boundary
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(File.Exists(savePath));

            // 新しい世界へ読み込み、ブロックと課金後在庫が同じ時点で保存されたことを確認する
            // Load a fresh world and verify the block and charged inventory came from one boundary
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
    }
}
