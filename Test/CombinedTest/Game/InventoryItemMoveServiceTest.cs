#if NET6_0
using Core.Item;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Test.Module.TestMod;

namespace Test.CombinedTest.Game
{
    public class InventoryItemMoveServiceTest
    {
        [Test]
        public void MoveTest()
        {
            var playerId = 1;

            //----------------------------------------------------------

            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            
            var playerInventoryData =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);


            
            var inventory = playerInventoryData.MainOpenableInventory;
            inventory.SetItem(0, itemStackFactory.Create(1, 5));
            inventory.SetItem(1, itemStackFactory.Create(1, 1));
            inventory.SetItem(2, itemStackFactory.Create(2, 1));


            
            
            InventoryItemMoveService.Move(itemStackFactory, inventory,
                0, inventory, 3, 5);
            Assert.AreEqual(inventory.GetItem(0), itemStackFactory.CreatEmpty());
            Assert.AreEqual(inventory.GetItem(3), itemStackFactory.Create(1, 5));

            
            InventoryItemMoveService.Move(itemStackFactory, inventory,
                3, inventory, 0, 3);
            Assert.AreEqual(inventory.GetItem(0), itemStackFactory.Create(1, 3));
            Assert.AreEqual(inventory.GetItem(3), itemStackFactory.Create(1, 2));

            
            InventoryItemMoveService.Move(itemStackFactory, inventory,
                0, inventory, 2, 1);
            Assert.AreEqual(inventory.GetItem(0), itemStackFactory.Create(1, 3));
            Assert.AreEqual(inventory.GetItem(2), itemStackFactory.Create(2, 1));

            
            InventoryItemMoveService.Move(itemStackFactory, inventory,
                0, inventory, 2, 3);
            Assert.AreEqual(inventory.GetItem(0), itemStackFactory.Create(2, 1));
            Assert.AreEqual(inventory.GetItem(2), itemStackFactory.Create(1, 3));

            
            InventoryItemMoveService.Move(itemStackFactory, inventory,
                2, inventory, 1, 3);
            Assert.AreEqual(inventory.GetItem(2), itemStackFactory.CreatEmpty());
            Assert.AreEqual(inventory.GetItem(1), itemStackFactory.Create(1, 4));


            
            InventoryItemMoveService.Move(itemStackFactory, inventory,
                1, inventory, 1, 4);
            Assert.AreEqual(inventory.GetItem(1), itemStackFactory.Create(1, 4));
        }
    }
}
#endif