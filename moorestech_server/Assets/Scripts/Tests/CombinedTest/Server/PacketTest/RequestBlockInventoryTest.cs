using System.Collections.Generic;
using System.Linq;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RequestBlockInventoryTest
    {
        private const int MachineBlockId = 1;
        private const int InputSlotNum = 2;
        private const int OutPutSlotNum = 3;
        
        
        //通常の機械のテスト
        [Test]
        public void MachineInventoryRequest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            
            var machinePosInfo = new BlockPositionInfo(new Vector3Int(5, 10), BlockDirection.North, Vector3Int.one);
            var machine = ServerContext.BlockFactory.Create(MachineBlockId, new BlockInstanceId(5), machinePosInfo);
            var machineComponent = machine.GetComponent<VanillaMachineBlockInventoryComponent>();
            machineComponent.SetItem(0, itemStackFactory.Create(1, 2));
            machineComponent.SetItem(2, itemStackFactory.Create(4, 5));
            
            ServerContext.WorldBlockDatastore.TryAddBlock(machine);
            
            //レスポンスの取得
            var data = MessagePackSerializer.Deserialize<BlockInventoryResponseProtocolMessagePack>(
                packet.GetPacketResponse(RequestBlock(new Vector3Int(5, 10)))[0].ToArray());
            
            Assert.AreEqual(InputSlotNum + OutPutSlotNum, data.ItemIds.Length); // slot num
            
            
            Assert.AreEqual(MachineBlockId, data.BlockId); // block id
            
            Assert.AreEqual(1, data.ItemIds[0]); // item id
            Assert.AreEqual(2, data.ItemCounts[0]); // item count
            
            Assert.AreEqual(0, data.ItemIds[1]);
            Assert.AreEqual(0, data.ItemCounts[1]);
            
            Assert.AreEqual(4, data.ItemIds[2]);
            Assert.AreEqual(5, data.ItemCounts[2]);
        }
        
        private List<byte> RequestBlock(Vector3Int pos)
        {
            return MessagePackSerializer.Serialize(new RequestBlockInventoryRequestProtocolMessagePack(pos)).ToList();
        }
    }
}