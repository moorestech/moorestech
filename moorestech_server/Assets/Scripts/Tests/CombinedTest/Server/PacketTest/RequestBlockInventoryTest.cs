using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.BlockInventoryRequestProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RequestBlockInventoryTest
    {
        private const int InputSlotNum = 2;
        private const int OutPutSlotNum = 3;
        
        //通常の機械のテスト
        [Test]
        public void MachineInventoryRequest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(5, 10), BlockDirection.North, out var machineBlock, System.Array.Empty<BlockCreateParam>());
            var machineComponent = machineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            machineComponent.SetItem(0, itemStackFactory.Create(new ItemId(1), 2));
            machineComponent.SetItem(2, itemStackFactory.Create(new ItemId(4), 5));
            
            //レスポンスの取得
            var data = MessagePackSerializer.Deserialize<BlockInventoryResponseProtocolMessagePack>(packet.GetPacketResponse(RequestBlock(new Vector3Int(5, 10)))[0].ToArray());
            
            Assert.AreEqual(InputSlotNum + OutPutSlotNum, data.Items.Length); // slot num
            
            
            Assert.AreEqual(ForUnitTestModBlockId.MachineId, data.BlockId); // block id
            
            Assert.AreEqual(1, data.Items[0].Id.AsPrimitive()); // item id
            Assert.AreEqual(2, data.Items[0].Count); // item count
            
            Assert.AreEqual(0, data.Items[1].Id.AsPrimitive());
            Assert.AreEqual(0, data.Items[1].Count);
            
            Assert.AreEqual(4, data.Items[2].Id.AsPrimitive());
            Assert.AreEqual(5, data.Items[2].Count);
        }
        
        private List<byte> RequestBlock(Vector3Int pos)
        {
            return MessagePackSerializer.Serialize(new RequestBlockInventoryRequestProtocolMessagePack(pos)).ToList();
        }
    }
}