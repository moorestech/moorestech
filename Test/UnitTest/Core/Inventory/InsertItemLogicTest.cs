#if NET6_0
using Core.Inventory;
using Core.Item;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.UnitTest.Core.Inventory
{
    /// <summary>
    ///     <see cref="InventoryInsertItem" /> 
    /// </summary>
    public class InsertItemLogicTest
    {
        [Test]
        public void InsertItemWithPrioritySlotTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);

            var itemStackFactory = serviceProvider.GetRequiredService<ItemStackFactory>();

            var toInventory = new OpenableInventoryItemDataStoreService((_, _) => { }, itemStackFactory, 10);


            // 8,9
            var insertItem = itemStackFactory.Create(1, 10);
            toInventory.InsertItemWithPrioritySlot(insertItem, new[] { 8, 9 });
            //8
            Assert.AreEqual(insertItem, toInventory.GetItem(8));

            //ID29
            insertItem = itemStackFactory.Create(2, 10);
            toInventory.InsertItemWithPrioritySlot(insertItem, new[] { 8, 9 });
            //9
            Assert.AreEqual(insertItem, toInventory.GetItem(9));

            //ID38,90
            insertItem = itemStackFactory.Create(3, 10);
            toInventory.InsertItemWithPrioritySlot(insertItem, new[] { 8, 9 });
            //0
            Assert.AreEqual(insertItem, toInventory.GetItem(0));
        }
    }
}
#endif