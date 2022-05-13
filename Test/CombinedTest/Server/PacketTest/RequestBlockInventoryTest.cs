using System.Collections.Generic;
using System.Linq;
using Core.Block.BlockFactory;
using Core.Block.Blocks.Machine;
using Core.Item;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.StartServerSystem;
using Server.Util;
using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class RequestBlockInventoryTest
    {
        private const int MachineBlockId = 1;
        private const int InputSlotNum = 2;
        private const int OutPutSlotNum = 1;
        
        
        //通常の機械のテスト
        [Test]
        public void MachineInventoryRequest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            
            var machine = serviceProvider.GetService<BlockFactory>().Create(MachineBlockId,5) as VanillaMachine;
            machine.SetItem(0,itemStackFactory.Create(1,2));
            machine.SetItem(2,itemStackFactory.Create(4,5));
            
            serviceProvider.GetService<IWorldBlockDatastore>().AddBlock(machine,5,10,BlockDirection.North);

            //レスポンスの取得
            var response = new ByteListEnumerator(packet.GetPacketResponse(RequestBlock(5,10))[0].ToList());
            
            Assert.AreEqual(6,response.MoveNextToGetShort()); //packet id
            Assert.AreEqual(InputSlotNum + OutPutSlotNum,response.MoveNextToGetShort()); // slot num
            
            
            Assert.AreEqual(MachineBlockId,response.MoveNextToGetInt()); // block id
            
            Assert.AreEqual(1,response.MoveNextToGetInt()); // item id
            Assert.AreEqual(2,response.MoveNextToGetInt()); // item count
            
            Assert.AreEqual(0,response.MoveNextToGetInt());
            Assert.AreEqual(0,response.MoveNextToGetInt()); 
            
            Assert.AreEqual(4,response.MoveNextToGetInt());
            Assert.AreEqual(5,response.MoveNextToGetInt()); 
            
            Assert.AreEqual(1,response.MoveNextToGetShort()); //UI type id
            Assert.AreEqual(InputSlotNum,response.MoveNextToGetShort()); //input slot
            Assert.AreEqual(OutPutSlotNum,response.MoveNextToGetShort()); //output slot
            
        }

        private List<byte> RequestBlock(int x, int y)
        {
            var bytes = new List<byte>();
            bytes.AddRange(ToByteList.Convert((short) 9));
            bytes.AddRange(ToByteList.Convert(x));
            bytes.AddRange(ToByteList.Convert(y));

            return bytes;
        }
    }
}