using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using Core.Block.BlockFactory;
using Core.Block.Blocks.Machine;
using Core.Item;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;
using Server.Protocol.PacketResponse;

using Server.Util;

using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
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
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            
            var machine = serviceProvider.GetService<BlockFactory>().Create(MachineBlockId,5) as VanillaMachineBase;
            machine.SetItem(0,itemStackFactory.Create(1,2));
            machine.SetItem(2,itemStackFactory.Create(4,5));
            
            serviceProvider.GetService<IWorldBlockDatastore>().AddBlock(machine,5,10,BlockDirection.North);

            //レスポンスの取得
            var data = MessagePackSerializer.Deserialize<BlockInventoryResponseProtocolMessagePack>(packet.GetPacketResponse(RequestBlock(5,10))[0].ToArray());

            Assert.AreEqual(InputSlotNum + OutPutSlotNum,data.ItemIds.Length); // slot num
            
            
            Assert.AreEqual(MachineBlockId,data.BlockId); // block id
            
            Assert.AreEqual(1,data.ItemIds[0]); // item id
            Assert.AreEqual(2,data.ItemCounts[0]); // item count
            
            Assert.AreEqual(0,data.ItemIds[1]);
            Assert.AreEqual(0,data.ItemCounts[1]); 
            
            Assert.AreEqual(4,data.ItemIds[2]);
            Assert.AreEqual(5,data.ItemCounts[2]); 
            
        }

        private List<byte> RequestBlock(int x, int y)
        {
            return MessagePackSerializer.Serialize(new RequestBlockInventoryRequestProtocolMessagePack(x,y)).ToList();
        }
    }
}