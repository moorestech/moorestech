#if NET6_0
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Item.Config;
using Game.Block.BlockInventory;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class RemoveBlockProtocolTest
    {
        private const int MachineBlockId = 1;
        private const int PlayerId = 0;

        [Test]
        public void RemoveTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            var playerInventoryData =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);

            var block = blockFactory.Create(MachineBlockId, 0);
            var blockInventory = (IBlockInventory)block;
            blockInventory.InsertItem(itemStackFactory.Create(10, 7));
            var blockConfigData = blockConfig.GetBlockConfig(block.BlockId);

            
            worldBlock.AddBlock(block, 0, 0, BlockDirection.North);

            Assert.AreEqual(0, worldBlock.GetBlock(0, 0).EntityId);

            


            
            packet.GetPacketResponse(RemoveBlock(0, 0, PlayerId));


            
            Assert.False(worldBlock.Exists(0, 0));


            var playerSlotIndex = PlayerInventoryConst.HotBarSlotToInventorySlot(0);
            
            Assert.AreEqual(10, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex).Id);
            Assert.AreEqual(7, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex).Count);

            
            Assert.AreEqual(blockConfigData.ItemId, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex + 1).Id);
            Assert.AreEqual(1, playerInventoryData.MainOpenableInventory.GetItem(playerSlotIndex + 1).Count);
        }


        
        [Test]
        public void InventoryFullToRemoveBlockSomeItemRemainTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var itemConfig = serviceProvider.GetService<IItemConfig>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            var mainInventory =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;

            //2
            for (var i = 2; i < mainInventory.GetSlotSize(); i++) mainInventory.SetItem(i, itemStackFactory.Create(1000, 1));

            //ID31
            var id3MaxStack = itemConfig.GetItemConfig(3).MaxStack;
            mainInventory.SetItem(0, itemStackFactory.Create(3, id3MaxStack - 1));
            //ID41
            mainInventory.SetItem(1, itemStackFactory.Create(4, 1));


            
            var block = blockFactory.Create(MachineBlockId, 0);
            var blockInventory = (IBlockInventory)block;
            //ID32ID45
            //ID31
            blockInventory.SetItem(0, itemStackFactory.Create(3, 2));
            blockInventory.SetItem(1, itemStackFactory.Create(4, 5));

            
            worldBlock.AddBlock(block, 0, 0, BlockDirection.North);


            
            packet.GetPacketResponse(RemoveBlock(0, 0, PlayerId));


            
            Assert.True(worldBlock.Exists(0, 0));

            
            Assert.AreEqual(itemStackFactory.Create(3, id3MaxStack), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(4, 6), mainInventory.GetItem(1));

            
            Assert.AreEqual(itemStackFactory.Create(3, 1), blockInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.CreatEmpty(), blockInventory.GetItem(1));
        }

        
        [Test]
        public void InventoryFullToCantRemoveBlockTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var worldBlock = serviceProvider.GetService<IWorldBlockDatastore>();
            var blockFactory = serviceProvider.GetService<IBlockFactory>();
            var itemConfig = serviceProvider.GetService<IItemConfig>();
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            var mainInventory =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;

            
            for (var i = 0; i < mainInventory.GetSlotSize(); i++) mainInventory.SetItem(i, itemStackFactory.Create(1000, 1));

            
            var block = blockFactory.Create(MachineBlockId, 0);
            worldBlock.AddBlock(block, 0, 0, BlockDirection.North);


            
            packet.GetPacketResponse(RemoveBlock(0, 0, PlayerId));


            
            Assert.True(worldBlock.Exists(0, 0));
        }


        private List<byte> RemoveBlock(int x, int y, int playerId)
        {
            return MessagePackSerializer.Serialize(new RemoveBlockProtocolMessagePack(playerId, x, y)).ToList();
        }
    }
}
#endif