#if NET6_0
using System.Linq;
using Core.Item;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    public class CraftProtocolFailedTest
    {
        private const int PlayerId = 1;

        private const int TestCraftItemId = 6;


        ///     

        [Test]
        public void CanNotNormalCraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).GrabInventory;

            //craftingInventory
            var item = itemStackFactory.Create(TestCraftItemId, 2);
            craftInventory.SetItem(0, item);

            
            grabInventory.SetItem(0, itemStackFactory.Create(2, 1));


            
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId, 0)).ToList());


            
            Assert.AreEqual(item, craftInventory.GetItem(0));
        }


        ///     

        [Test]
        public void AllCraftReminderTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;


            //craftingInventory
            craftInventory.SetItem(0, itemStackFactory.Create(TestCraftItemId, 100));

            //2
            for (var i = 2; i < mainInventory.GetSlotSize(); i++) mainInventory.SetItem(i, itemStackFactory.Create(2, 1));


            
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId, 1)).ToList());


            
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 100), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 60), mainInventory.GetItem(1));
            
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 98), craftInventory.GetItem(0));
        }


        ///     

        [Test]
        public void AllCanNotCraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;


            //craftingInventory
            craftInventory.SetItem(0, itemStackFactory.Create(TestCraftItemId, 100));

            
            for (var i = 0; i < mainInventory.GetSlotSize(); i++) mainInventory.SetItem(i, itemStackFactory.Create(2, 1));


            
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId, 1)).ToList());


            
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 100), craftInventory.GetItem(0));
        }



        ///     1

        [Test]
        public void OneStackCraftReminderTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;


            //craftingInventory
            craftInventory.SetItem(0, itemStackFactory.Create(TestCraftItemId, 100));

            //1
            for (var i = 1; i < mainInventory.GetSlotSize(); i++) mainInventory.SetItem(i, itemStackFactory.Create(2, 1));
            //1
            mainInventory.SetItem(0, itemStackFactory.Create(TestCraftItemId, 20));


            //1
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId, 2)).ToList());


            
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 100), mainInventory.GetItem(0));
            
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 99), craftInventory.GetItem(0));
        }



        ///     1

        [Test]
        public void OneCanNotStackCraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingOpenableInventory;
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;


            //craftingInventory
            craftInventory.SetItem(0, itemStackFactory.Create(TestCraftItemId, 100));

            
            for (var i = 0; i < mainInventory.GetSlotSize(); i++) mainInventory.SetItem(i, itemStackFactory.Create(2, 1));


            
            packet.GetPacketResponse(
                MessagePackSerializer.Serialize(new CraftProtocolMessagePack(PlayerId, 2)).ToList());


            
            Assert.AreEqual(itemStackFactory.Create(TestCraftItemId, 100), craftInventory.GetItem(0));
        }
    }
}
#endif