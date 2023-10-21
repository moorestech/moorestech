#if NET6_0
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Item;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest.Event
{
    /// <summary>
    ///     
    /// </summary>
    public class BlockInventoryUpdateEventPacketTest
    {
        private const int MachineBlockId = 1;
        private const int PlayerId = 3;
        private const short PacketId = 16;

        
        [Test]
        public void BlockInventoryUpdatePacketTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var worldBlockDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            
            var block = blockFactory.Create(MachineBlockId, 1);
            var blockInventory = (IOpenableInventory)block;
            worldBlockDataStore.AddBlock(block, 5, 7, BlockDirection.North);


            
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(5, 7, true));
            
            blockInventory.SetItem(1, itemStackFactory.Create(4, 8));


            
            
            var eventPacket = packetResponse.GetPacketResponse(GetEventPacket());

            
            Assert.AreEqual(1, eventPacket.Count);

            var data =
                MessagePackSerializer.Deserialize<OpenableBlockInventoryUpdateEventMessagePack>(
                    eventPacket[0].ToArray());

            Assert.AreEqual(1, data.Slot); // slot id
            Assert.AreEqual(4, data.Item.Id); // item id
            Assert.AreEqual(8, data.Item.Count); // item count
            Assert.AreEqual(5, data.X); // x
            Assert.AreEqual(7, data.Y); // y


            
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(5, 7, false));

            
            blockInventory.SetItem(2, itemStackFactory.Create(4, 8));


            
            
            eventPacket = packetResponse.GetPacketResponse(GetEventPacket());
            
            Assert.AreEqual(0, eventPacket.Count);
        }


        //１
        [Test]
        public void OnlyOneInventoryCanBeOpenedTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var worldBlockDataStore = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            //1
            var block1 = blockFactory.Create(MachineBlockId, 1);
            var block1Inventory = (IOpenableInventory)block1;
            worldBlockDataStore.AddBlock(block1, 5, 7, BlockDirection.North);
            //2
            var block2 = blockFactory.Create(MachineBlockId, 2);
            worldBlockDataStore.AddBlock(block2, 10, 20, BlockDirection.North);


            
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(5, 7, true));
            
            packetResponse.GetPacketResponse(OpenCloseBlockInventoryPacket(10, 20, true));


            
            block1Inventory.SetItem(2, itemStackFactory.Create(4, 8));


            
            
            var eventPacket = packetResponse.GetPacketResponse(GetEventPacket());
            
            Assert.AreEqual(0, eventPacket.Count);
        }


        private List<byte> OpenCloseBlockInventoryPacket(int x, int y, bool isOpen)
        {
            return MessagePackSerializer.Serialize(new BlockInventoryOpenCloseProtocolMessagePack(PlayerId, x, y, isOpen)).ToList();
        }

        private List<byte> GetEventPacket()
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(PlayerId)).ToList();
            ;
        }
    }
}
#endif