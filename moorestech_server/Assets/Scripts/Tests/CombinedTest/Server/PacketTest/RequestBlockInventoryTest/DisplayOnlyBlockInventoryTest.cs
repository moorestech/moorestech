using System;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class DisplayOnlyBlockInventoryTest : RequestBlockInventoryTestBase
    {
        [TestCase("9CE688A4-8985-40B6-AD52-0F98F3BAF55E")]
        [TestCase("00000000-0000-0000-0000-000000000029")]
        [TestCase("00000000-0000-0000-0000-00000000000c")]
        public void OpenableWithoutItemInventoryReturnsSuccess(string blockGuid)
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var position = new Vector3Int(40, 20, 0);
            var id = MasterHolder.BlockMaster.GetBlockId(new Guid(blockGuid));
            TrainTestHelper.PlaceBlock(environment, id, position, BlockDirection.North);

            // 液体容器と歯車の表示はアイテム在庫を必要としない
            // Fluid containers and gear detail views do not require item inventories
            var bytes = environment.PacketResponseCreator.GetPacketResponse(RequestBlock(position), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1))[0];
            var response = MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(bytes);
            Assert.AreEqual(InventoryRequestResult.Success, response.Result);
            Assert.IsEmpty(response.Items);
            Assert.IsEmpty(response.Fluids);

            // 撤去された表示専用ブロックは成功へ補完しない
            // A removed display-only block must still report a missing block
            var missingBytes = environment.PacketResponseCreator.GetPacketResponse(RequestBlock(position + Vector3Int.right * 100), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1))[0];
            var missing = MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(missingBytes);
            Assert.AreEqual(InventoryRequestResult.BlockNotFound, missing.Result);
        }
    }
}
