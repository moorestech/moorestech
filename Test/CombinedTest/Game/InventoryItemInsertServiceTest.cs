#if NET6_0
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Test.Module.TestMod;

namespace Test.CombinedTest.Game
{
    public class InventoryItemInsertServiceTest
    {

        ///     insert

        [Test]
        public void InsertTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).MainOpenableInventory;
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).CraftingOpenableInventory;

            
            mainInventory.SetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(0), 1, 10);
            craftInventory.SetItem(0, 1, 10);
            craftInventory.SetItem(2, 2, 10);

            //id 1
            InventoryItemInsertService.Insert(craftInventory, 0, mainInventory, 5);

            Assert.AreEqual(15, mainInventory.GetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(0)).Count);
            Assert.AreEqual(5, craftInventory.GetItem(0).Count);


            //id 2
            InventoryItemInsertService.Insert(craftInventory, 2, mainInventory, 10);

            Assert.AreEqual(10, mainInventory.GetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(1)).Count);
            Assert.AreEqual(0, craftInventory.GetItem(2).Count);
        }



        ///     insert

        [Test]
        public void FullItemInsert()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).MainOpenableInventory;
            var craftInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).CraftingOpenableInventory;
            var id1MaxStack = serviceProvider.GetService<IItemConfig>().GetItemConfig(1).MaxStack;

            
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++) mainInventory.SetItem(i, 1, id1MaxStack);
            
            craftInventory.SetItem(0, 1, 10);
            craftInventory.SetItem(1, 2, 10);

            //id 1
            InventoryItemInsertService.Insert(craftInventory, 0, mainInventory, 5);
            
            Assert.AreEqual(10, craftInventory.GetItem(0).Count);

            //id 2
            InventoryItemInsertService.Insert(craftInventory, 1, mainInventory, 10);
            
            Assert.AreEqual(10, craftInventory.GetItem(1).Count);


            
            //5
            mainInventory.SetItem(0, 1, id1MaxStack - 5);
            //id 1
            InventoryItemInsertService.Insert(craftInventory, 0, mainInventory, 10);
            
            Assert.AreEqual(5, craftInventory.GetItem(0).Count);
        }
    }
}
#endif